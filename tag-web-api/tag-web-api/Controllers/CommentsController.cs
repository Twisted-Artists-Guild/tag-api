using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TAGWEBAPI.Data;
using TAGWEBAPI.Hubs;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly TAGDBContext _context;
    private readonly ILogger<CommentsController> _logger;
    private readonly IHubContext<MessagingHub> _hubContext;

    public CommentsController(TAGDBContext context, ILogger<CommentsController> logger, IHubContext<MessagingHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Get comments for a specific target (Artist, Listing, Blog, News)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<CommentsResponse>> GetComments(
        [FromQuery] CommentTargetType targetType,
        [FromQuery] int targetId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeReplies = true)
    {
        try
        {
            // Get only top-level comments (no parent)
            var query = _context.Comments
                .Where(c => c.TargetType == targetType 
                    && c.TargetId == targetId 
                    && c.ParentCommentId == null
                    && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var comments = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var commentDtos = await BuildCommentDtosAsync(comments, includeReplies);

            return Ok(new CommentsResponse
            {
                Comments = commentDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comments for {TargetType} with ID {TargetId}", targetType, targetId);
            return StatusCode(500, "An error occurred while fetching comments");
        }
    }

    /// <summary>
    /// Get replies for a specific comment
    /// </summary>
    [HttpGet("{commentId}/replies")]
    [AllowAnonymous]
    public async Task<ActionResult<CommentsResponse>> GetReplies(
        long commentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = _context.Comments
                .Where(c => c.ParentCommentId == commentId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var replies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var replyDtos = await BuildCommentDtosAsync(replies, false);

            return Ok(new CommentsResponse
            {
                Comments = replyDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching replies for comment ID {CommentId}", commentId);
            return StatusCode(500, "An error occurred while fetching replies");
        }
    }

    /// <summary>
    /// Create a new comment or reply
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CommentDto>> CreateComment([FromBody] CreateCommentRequest request)
    {
        try
        {
            // Validate content
            if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
            {
                return BadRequest("Comment content must be between 1 and 2000 characters");
            }

            // The authenticated JWT identity is the source of truth for authorship, not the request body.
            var authenticatedUserId = GetAuthenticatedUserId();
            if (authenticatedUserId == null)
            {
                return Unauthorized("Unable to resolve authenticated user from token");
            }

            // Validate user exists
            var userExists = await _context.Set<NextAuthUser>()
                .AnyAsync(u => u.Id == authenticatedUserId.Value);

            if (!userExists)
            {
                return BadRequest("User not found");
            }

            // If posting as a profile context (e.g. an artist), verify the authenticated user owns/is linked to it.
            if (string.Equals(request.AuthorEntityType, "artist", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.AuthorEntityId.HasValue)
                {
                    return BadRequest("authorEntityId is required when authorEntityType is 'artist'");
                }

                var ownsArtist = await _context.Set<Linker_UserToArtist>()
                    .AnyAsync(link => link.UserID == authenticatedUserId.Value && link.ArtistID == request.AuthorEntityId.Value);

                if (!ownsArtist)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You are not authorized to comment as this artist profile");
                }
            }

            // If it's a reply, validate parent comment exists
            if (request.ParentCommentId.HasValue)
            {
                var parentExists = await _context.Comments
                    .AnyAsync(c => c.Id == request.ParentCommentId.Value && !c.IsDeleted);

                if (!parentExists)
                {
                    return BadRequest("Parent comment not found");
                }
            }

            var comment = new Comment
            {
                TargetType = request.TargetType,
                TargetId = request.TargetId,
                UserId = authenticatedUserId.Value,
                Content = request.Content.Trim(),
                ParentCommentId = request.ParentCommentId,
                AuthorContextId = request.AuthorContextId,
                AuthorEntityType = request.AuthorEntityType,
                AuthorEntityId = request.AuthorEntityId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            await BroadcastCommentSummaryToOwners(comment);

            var commentDto = await MapSingleCommentToDtoAsync(comment, false);

            return CreatedAtAction(nameof(GetCommentById), new { id = comment.Id }, commentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment");
            return StatusCode(500, "An error occurred while creating the comment");
        }
    }

    /// <summary>
    /// Update an existing comment
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<CommentDto>> UpdateComment(long id, [FromBody] UpdateCommentRequest request)
    {
        try
        {
            if (id != request.CommentId)
            {
                return BadRequest("Comment ID mismatch");
            }

            // Validate content
            if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
            {
                return BadRequest("Comment content must be between 1 and 2000 characters");
            }

            var comment = await _context.Comments.FindAsync(id);

            if (comment == null)
            {
                return NotFound("Comment not found");
            }

            if (comment.IsDeleted)
            {
                return BadRequest("Cannot update deleted comment");
            }

            // Verify user owns the comment
            if (comment.UserId != request.UserId)
            {
                return Forbid();
            }

            comment.Content = request.Content.Trim();
            comment.IsEdited = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var commentDto = await MapSingleCommentToDtoAsync(comment, false);

            return Ok(commentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating comment ID {CommentId}", id);
            return StatusCode(500, "An error occurred while updating the comment");
        }
    }

    /// <summary>
    /// Delete a comment (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteComment(long id, [FromQuery] int userId)
    {
        try
        {
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null)
            {
                return NotFound("Comment not found");
            }

            if (comment.IsDeleted)
            {
                return BadRequest("Comment already deleted");
            }

            // Verify user owns the comment
            if (comment.UserId != userId)
            {
                return Forbid();
            }

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Comment deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment ID {CommentId}", id);
            return StatusCode(500, "An error occurred while deleting the comment");
        }
    }

    /// <summary>
    /// Get a single comment by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CommentDto>> GetCommentById(long id)
    {
        try
        {
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null || comment.IsDeleted)
            {
                return NotFound("Comment not found");
            }

            var commentDto = await MapSingleCommentToDtoAsync(comment, true);

            return Ok(commentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comment ID {CommentId}", id);
            return StatusCode(500, "An error occurred while fetching the comment");
        }
    }

    /// <summary>
    /// Get comment count for a specific target
    /// </summary>
    [HttpGet("count")]
    [AllowAnonymous]
    public async Task<ActionResult<int>> GetCommentCount(
        [FromQuery] CommentTargetType targetType,
        [FromQuery] int targetId)
    {
        try
        {
            var count = await _context.Comments
                .Where(c => c.TargetType == targetType 
                    && c.TargetId == targetId 
                    && !c.IsDeleted)
                .CountAsync();

            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comment count for {TargetType} with ID {TargetId}", targetType, targetId);
            return StatusCode(500, "An error occurred while fetching comment count");
        }
    }

    [HttpGet("received-summary")]
    public async Task<ActionResult> GetReceivedCommentSummary(
        [FromQuery] int userId,
        [FromQuery] int windowMinutes = 60,
        [FromQuery] bool includeSelfActions = true)
    {
        if (windowMinutes <= 0)
        {
            windowMinutes = 60;
        }

        var sinceUtc = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var summary = await BuildCommentSummaryForOwner(userId, sinceUtc, includeSelfActions);

        return Ok(new
        {
            userId,
            windowMinutes,
            sinceUtc,
            includeSelfActions,
            commentCountLastHour = summary.count,
            latestComment = summary.latest,
            updatedAt = DateTime.UtcNow,
        });
    }

    // Convenience wrapper for mapping a single already-tracked comment (create/update/get-by-id call sites).
    private async Task<CommentDto> MapSingleCommentToDtoAsync(Comment comment, bool includeReplies)
    {
        var dtos = await BuildCommentDtosAsync(new List<Comment> { comment }, includeReplies);
        return dtos[0];
    }

    // Maps a page of top-level comments (and, optionally, their first-level replies) to DTOs using
    // batched queries: one query for reply data, one for user identities, one for artist identities.
    // No per-comment queries are issued, regardless of how many comments/authors are in the page.
    private async Task<List<CommentDto>> BuildCommentDtosAsync(List<Comment> topLevelComments, bool includeReplies)
    {
        if (topLevelComments.Count == 0)
        {
            return new List<CommentDto>();
        }

        var repliesByParent = new Dictionary<long, List<Comment>>();
        var replyCounts = new Dictionary<long, int>();

        if (includeReplies)
        {
            var topLevelIds = topLevelComments.Select(c => c.Id).ToList();

            // Single query for every first-level reply across the whole page of comments.
            var allReplies = await _context.Comments
                .AsNoTracking()
                .Where(c => c.ParentCommentId != null && topLevelIds.Contains(c.ParentCommentId.Value) && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            foreach (var group in allReplies.GroupBy(c => c.ParentCommentId!.Value))
            {
                replyCounts[group.Key] = group.Count();
                repliesByParent[group.Key] = group.Take(3).ToList(); // Preview only the first 3 replies.
            }
        }

        // Resolve author identities (base user + artist profile) for top-level comments and their previewed replies in one pass.
        var allComments = topLevelComments.Concat(repliesByParent.Values.SelectMany(replies => replies)).ToList();
        var (userLookup, artistLookup) = await LoadAuthorLookupsAsync(allComments);

        return topLevelComments
            .Select(comment => BuildCommentDto(comment, userLookup, artistLookup, replyCounts, repliesByParent))
            .ToList();
    }

    private static CommentDto BuildCommentDto(
        Comment comment,
        IReadOnlyDictionary<int, UserInfoDto> userLookup,
        IReadOnlyDictionary<int, (string Title, string? ImageUrl)> artistLookup,
        IReadOnlyDictionary<long, int> replyCounts,
        IReadOnlyDictionary<long, List<Comment>> repliesByParent)
    {
        var user = userLookup.TryGetValue(comment.UserId, out var foundUser)
            ? foundUser
            : new UserInfoDto { Id = comment.UserId, Name = "Unknown User", Email = null, Image = null };

        var (authorDisplayName, authorImage, authorEntityType) = ResolveAuthorDisplay(comment, user, artistLookup);

        var dto = new CommentDto
        {
            Id = comment.Id,
            TargetType = comment.TargetType,
            TargetId = comment.TargetId,
            UserId = comment.UserId,
            User = user,
            Content = comment.Content,
            ParentCommentId = comment.ParentCommentId,
            AuthorContextId = comment.AuthorContextId,
            AuthorEntityType = authorEntityType,
            AuthorEntityId = comment.AuthorEntityId,
            AuthorDisplayName = authorDisplayName,
            AuthorImage = authorImage,
            IsEdited = comment.IsEdited,
            IsDeleted = comment.IsDeleted,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            ReplyCount = replyCounts.TryGetValue(comment.Id, out var count) ? count : 0,
        };

        if (repliesByParent.TryGetValue(comment.Id, out var replies))
        {
            dto.Replies = replies
                .Select(reply => BuildCommentDto(reply, userLookup, artistLookup, replyCounts, repliesByParent))
                .ToList();
        }

        return dto;
    }

    // Resolves display name/image for the identity a comment was posted as (artist profile, or base user for older/unset comments).
    private static (string DisplayName, string? Image, string EntityType) ResolveAuthorDisplay(
        Comment comment,
        UserInfoDto baseUser,
        IReadOnlyDictionary<int, (string Title, string? ImageUrl)> artistLookup)
    {
        if (string.Equals(comment.AuthorEntityType, "artist", StringComparison.OrdinalIgnoreCase)
            && comment.AuthorEntityId.HasValue
            && artistLookup.TryGetValue(comment.AuthorEntityId.Value, out var artist))
        {
            return (artist.Title, artist.ImageUrl, "artist");
        }

        // Fallback: base user identity (covers older comments where AuthorEntityId is null, or unresolved entities)
        return (baseUser.Name, baseUser.Image, "user");
    }

    // Batch-loads every user and artist identity referenced by the given comments in exactly two queries total.
    private async Task<(Dictionary<int, UserInfoDto> Users, Dictionary<int, (string Title, string? ImageUrl)> Artists)> LoadAuthorLookupsAsync(List<Comment> comments)
    {
        var userIds = comments.Select(c => c.UserId).Distinct().ToList();
        var artistIds = comments
            .Where(c => string.Equals(c.AuthorEntityType, "artist", StringComparison.OrdinalIgnoreCase) && c.AuthorEntityId.HasValue)
            .Select(c => c.AuthorEntityId!.Value)
            .Distinct()
            .ToList();

        var users = await _context.Set<NextAuthUser>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserInfoDto
            {
                Id = u.Id,
                Name = u.Name ?? "Unknown User",
                Email = u.Email,
                Image = u.Image
            })
            .ToDictionaryAsync(u => u.Id);

        var artistLookup = new Dictionary<int, (string Title, string? ImageUrl)>();
        if (artistIds.Count > 0)
        {
            var artists = await _context.Set<Artist>()
                .AsNoTracking()
                .Where(a => artistIds.Contains(a.ArtistID))
                .Select(a => new { a.ArtistID, a.Title, ImageUrl = a.ProfilePic != null ? a.ProfilePic.URL : null })
                .ToListAsync();

            foreach (var artist in artists)
            {
                artistLookup[artist.ArtistID] = (artist.Title, artist.ImageUrl);
            }
        }

        return (users, artistLookup);
    }

    private int? GetAuthenticatedUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId");

        return int.TryParse(claim, out var userId) ? userId : null;
    }

    private async Task BroadcastCommentSummaryToOwners(Comment comment, bool includeSelfActions = true)
    {
        var ownerUserIds = await GetTargetOwnerUserIds(comment.TargetType, comment.TargetId);
        if (!ownerUserIds.Any())
        {
            return;
        }

        var recipients = includeSelfActions
            ? ownerUserIds.Distinct().ToList()
            : ownerUserIds.Where(userId => userId != comment.UserId).Distinct().ToList();
        if (!recipients.Any())
        {
            return;
        }

        var sinceUtc = DateTime.UtcNow.AddHours(-1);
        foreach (var ownerUserId in recipients)
        {
            var summary = await BuildCommentSummaryForOwner(ownerUserId, sinceUtc, includeSelfActions);
            await _hubContext.Clients.Group($"user-{ownerUserId}")
                .SendAsync("NotificationSummaryUpdated", new
                {
                    type = "comments",
                    includeSelfActions,
                    commentCountLastHour = summary.count,
                    latestComment = summary.latest,
                    timestamp = DateTime.UtcNow,
                });
        }
    }

    private async Task<(int count, object? latest)> BuildCommentSummaryForOwner(int ownerUserId, DateTime sinceUtc, bool includeSelfActions)
    {
        var ownedArtistIds = await _context.Set<Linker_UserToArtist>()
            .AsNoTracking()
            .Where(link => link.UserID == ownerUserId)
            .Select(link => link.ArtistID)
            .Distinct()
            .ToListAsync();

        var ownedListingIds = await _context.Listings
            .AsNoTracking()
            .Where(listing => ownedArtistIds.Contains(listing.ArtistID))
            .Select(listing => listing.ListingID)
            .ToListAsync();

        var ownedBlogIds = await _context.Blogs
            .AsNoTracking()
            .Where(blog => blog.UserID == ownerUserId)
            .Select(blog => blog.BlogID)
            .ToListAsync();

        var ownedUserIds = new List<int> { ownerUserId };

        var candidateComments = await _context.Comments
            .AsNoTracking()
            .Where(comment => !comment.IsDeleted
                && comment.CreatedAt >= sinceUtc
                && (includeSelfActions || comment.UserId != ownerUserId)
                && (
                    (comment.TargetType == CommentTargetType.Artist && ownedArtistIds.Contains(comment.TargetId))
                    || (comment.TargetType == CommentTargetType.Listing && ownedListingIds.Contains(comment.TargetId))
                    || (comment.TargetType == CommentTargetType.Blog && ownedBlogIds.Contains(comment.TargetId))
                    || (comment.TargetType == CommentTargetType.News && ownedBlogIds.Contains(comment.TargetId))
                    || (comment.TargetType == CommentTargetType.User && ownedUserIds.Contains(comment.TargetId))
                ))
            .OrderByDescending(comment => comment.CreatedAt)
            .Select(comment => new
            {
                comment.Id,
                comment.UserId,
                comment.Content,
                comment.TargetType,
                comment.TargetId,
                comment.CreatedAt,
            })
            .ToListAsync();

        var count = candidateComments.Count;
        var latest = candidateComments.FirstOrDefault();
        if (latest == null)
        {
            return (count, null);
        }

        var commenter = await _context.Set<NextAuthUser>()
            .AsNoTracking()
            .Where(user => user.Id == latest.UserId)
            .Select(user => new { user.Name, user.Image })
            .FirstOrDefaultAsync();

        return (count, new
        {
            commentId = latest.Id,
            commenterName = string.IsNullOrWhiteSpace(commenter?.Name) ? "Someone" : commenter.Name,
            commenterImage = commenter?.Image,
            contentPreview = latest.Content.Length > 120 ? latest.Content[..120] : latest.Content,
            targetType = latest.TargetType.ToString(),
            href = await BuildCommentHref(latest.TargetType, latest.TargetId, latest.Id),
            createdAt = latest.CreatedAt,
        });
    }

    private async Task<List<int>> GetTargetOwnerUserIds(CommentTargetType targetType, int targetId)
    {
        if (targetType == CommentTargetType.Artist)
        {
            return await _context.Set<Linker_UserToArtist>()
                .AsNoTracking()
                .Where(link => link.ArtistID == targetId)
                .Select(link => link.UserID)
                .Distinct()
                .ToListAsync();
        }

        if (targetType == CommentTargetType.Listing)
        {
            return await (
                from listing in _context.Listings.AsNoTracking()
                join link in _context.Set<Linker_UserToArtist>().AsNoTracking()
                    on listing.ArtistID equals link.ArtistID
                where listing.ListingID == targetId
                select link.UserID
            )
            .Distinct()
            .ToListAsync();
        }

        if (targetType == CommentTargetType.Blog)
        {
            return await _context.Blogs
                .AsNoTracking()
                .Where(blog => blog.BlogID == targetId)
                .Select(blog => blog.UserID)
                .Distinct()
                .ToListAsync();
        }

        if (targetType == CommentTargetType.News)
        {
            return await _context.Blogs
                .AsNoTracking()
                .Where(blog => blog.BlogID == targetId)
                .Select(blog => blog.UserID)
                .Distinct()
                .ToListAsync();
        }

        if (targetType == CommentTargetType.User)
        {
            var exists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.UserID == targetId);

            return exists ? new List<int> { targetId } : new List<int>();
        }

        return new List<int>();
    }

    private async Task<string> BuildCommentHref(CommentTargetType targetType, int targetId, long commentId)
    {
        if (targetType == CommentTargetType.Listing)
        {
            var listingRoute = await (
                from listing in _context.Listings.AsNoTracking()
                join artist in _context.Artists.AsNoTracking()
                    on listing.ArtistID equals artist.ArtistID
                where listing.ListingID == targetId
                select new { ArtistPath = artist.Path, ListingPath = listing.Path }
            ).FirstOrDefaultAsync();

            if (listingRoute != null)
            {
                return $"/artists/{listingRoute.ArtistPath}/listings/{listingRoute.ListingPath}?commentId={commentId}#comments-section";
            }
        }

        if (targetType == CommentTargetType.Artist)
        {
            var artistPath = await _context.Artists
                .AsNoTracking()
                .Where(artist => artist.ArtistID == targetId)
                .Select(artist => artist.Path)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(artistPath))
            {
                return $"/artists/{artistPath}?commentId={commentId}#comments-section";
            }
        }

        if (targetType == CommentTargetType.Blog)
        {
            var blogPath = await _context.Blogs
                .AsNoTracking()
                .Where(blog => blog.BlogID == targetId)
                .Select(blog => blog.Path)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(blogPath))
            {
                return $"/blogs/{blogPath}?commentId={commentId}#comments-section";
            }
        }

        if (targetType == CommentTargetType.News)
        {
            return $"/news?commentId={commentId}#comments-section";
        }

        if (targetType == CommentTargetType.User)
        {
            var username = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserID == targetId)
                .Select(user => user.Username)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(username))
            {
                return $"/user/{username}?commentId={commentId}#comments";
            }

            return "/user";
        }

        return $"/news?commentId={commentId}#comments-section";
    }
}