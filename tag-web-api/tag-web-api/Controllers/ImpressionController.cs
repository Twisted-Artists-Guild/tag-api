using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Hubs;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImpressionController : ControllerBase
{
    private readonly TAGDBContext _context;
    private readonly ILogger<ImpressionController> _logger;
    private readonly IHubContext<MessagingHub> _hubContext;

    public ImpressionController(
        TAGDBContext context, 
        ILogger<ImpressionController> logger,
        IHubContext<MessagingHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Gets all primary impressions with counts for a specific target
    /// </summary>
    [HttpGet("primary")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PrimaryImpressionDto>>> GetPrimaryImpressions(
        [FromQuery] TargetType targetType,
        [FromQuery] int targetId)
    {
        try
        {
            // Get all primary impressions
            var primaryImpressions = await _context.PrimaryImpressions
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            var result = new List<PrimaryImpressionDto>();

            foreach (var impression in primaryImpressions)
            {
                int count = 0;

                // Fetch count based on target type
                if (targetType == TargetType.Listing)
                {
                    count = await _context.ListingImpressions
                        .Where(li => li.ListingId == targetId && li.ImpressionId == impression.Id)
                        .CountAsync();
                }
                else if (targetType == TargetType.Artist)
                {
                    count = await _context.ArtistImpressions
                        .Where(ai => ai.ArtistId == targetId && ai.ImpressionId == impression.Id)
                        .CountAsync();
                }
                else if (targetType == TargetType.Comment)
                {
                    count = await _context.CommentImpressions
                        .Where(ci => ci.CommentId == targetId && ci.ImpressionId == impression.Id)
                        .CountAsync();
                }
                else if (targetType == TargetType.Blog)
                {
                    count = await _context.BlogImpressions
                        .Where(bi => bi.BlogId == targetId && bi.ImpressionId == impression.Id)
                        .CountAsync();
                }
                else if (targetType == TargetType.Message)
                {
                    count = await _context.MessageImpressions
                        .Where(mi => mi.MessageId == targetId && mi.ImpressionId == impression.Id)
                        .CountAsync();
                }
                else if (targetType == TargetType.FeedPost)
                {
                    count = await _context.FeedPostImpressions
                        .Where(fi => fi.FeedPostId == targetId && fi.ImpressionId == impression.Id)
                        .CountAsync();
                }

                result.Add(new PrimaryImpressionDto
                {
                    Id = impression.Id,
                    Emoji = impression.Emoji,
                    Name = impression.Name,
                    Label = impression.Label,
                    DisplayOrder = impression.DisplayOrder,
                    Count = count
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching primary impressions for {TargetType} with ID {TargetId}", targetType, targetId);
            return StatusCode(500, "An error occurred while fetching impressions");
        }
    }

    /// <summary>
    /// Gets received reaction totals for the current user across owned artists/listings.
    /// </summary>
    [HttpGet("received-summary")]
    [AllowAnonymous]
    public async Task<ActionResult> GetReceivedSummary(
        [FromQuery] int userId,
        [FromQuery] int windowMinutes = 60,
        [FromQuery] bool includeSelfActions = true)
    {
        if (windowMinutes <= 0)
        {
            windowMinutes = 60;
        }

        var sinceUtc = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var summary = await BuildReceivedReactionSummaryForUser(userId, sinceUtc, includeSelfActions);

        return Ok(new
        {
            userId,
            windowMinutes,
            sinceUtc,
            includeSelfActions,
            reactionCountLastHour = summary.count,
            latestReaction = summary.latest,
            updatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// React to a listing, artist, or comment with an impression
    /// </summary>
    [HttpPost("react")]
    public async Task<ActionResult> ReactToTarget([FromBody] ReactImpressionRequest request)
    {
        try
        {
            // Validate impression exists
            var impressionExists = await _context.PrimaryImpressions
                .AnyAsync(p => p.Id == request.ImpressionId);

            if (!impressionExists)
            {
                return BadRequest("Invalid impression ID");
            }

            if (request.TargetType == TargetType.Listing)
            {
                // Check if user already reacted with this impression
                var existingReaction = await _context.ListingImpressions
                    .FirstOrDefaultAsync(li => li.ListingId == request.TargetId 
                        && li.UserId == request.UserId 
                        && li.ImpressionId == request.ImpressionId);

                var wasRemoved = false;

                if (existingReaction != null)
                {
                    // Remove the reaction (toggle off)
                    _context.ListingImpressions.Remove(existingReaction);
                    await _context.SaveChangesAsync();
                    wasRemoved = true;
                }
                else
                {
                    // Add new reaction
                    var newReaction = new ListingImpression
                    {
                        ListingId = request.TargetId,
                        ImpressionId = request.ImpressionId,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ListingImpressions.Add(newReaction);
                    await _context.SaveChangesAsync();
                }

                await BroadcastReceivedReactionSummaryToOwners(request.TargetType, request.TargetId, request.UserId, includeSelfActions: true);
                return Ok(new { message = wasRemoved ? "Reaction removed" : "Reaction added", removed = wasRemoved });
            }
            else if (request.TargetType == TargetType.Artist)
            {
                // Check if user already reacted with this impression
                var existingReaction = await _context.ArtistImpressions
                    .FirstOrDefaultAsync(ai => ai.ArtistId == request.TargetId 
                        && ai.UserId == request.UserId 
                        && ai.ImpressionId == request.ImpressionId);

                var wasRemoved = false;

                if (existingReaction != null)
                {
                    // Remove the reaction (toggle off)
                    _context.ArtistImpressions.Remove(existingReaction);
                    await _context.SaveChangesAsync();
                    wasRemoved = true;
                }
                else
                {
                    // Add new reaction
                    var newReaction = new ArtistImpression
                    {
                        ArtistId = request.TargetId,
                        ImpressionId = request.ImpressionId,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ArtistImpressions.Add(newReaction);
                    await _context.SaveChangesAsync();
                }

                await BroadcastReceivedReactionSummaryToOwners(request.TargetType, request.TargetId, request.UserId, includeSelfActions: true);
                return Ok(new { message = wasRemoved ? "Reaction removed" : "Reaction added", removed = wasRemoved });
            }
            else if (request.TargetType == TargetType.Comment)
            {
                // Check if user already reacted with this impression
                var existingReaction = await _context.CommentImpressions
                    .FirstOrDefaultAsync(ci => ci.CommentId == request.TargetId 
                        && ci.UserId == request.UserId 
                        && ci.ImpressionId == request.ImpressionId);

                if (existingReaction != null)
                {
                    // Remove the reaction (toggle off)
                    _context.CommentImpressions.Remove(existingReaction);
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Reaction removed", removed = true });
                }
                else
                {
                    // Add new reaction
                    var newReaction = new CommentImpression
                    {
                        CommentId = request.TargetId,
                        ImpressionId = request.ImpressionId,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.CommentImpressions.Add(newReaction);
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Reaction added", removed = false });
                }
            }
            else if (request.TargetType == TargetType.Blog)
            {
                // Check if user already reacted with this impression
                var existingReaction = await _context.BlogImpressions
                    .FirstOrDefaultAsync(bi => bi.BlogId == request.TargetId 
                        && bi.UserId == request.UserId 
                        && bi.ImpressionId == request.ImpressionId);

                if (existingReaction != null)
                {
                    // Remove the reaction (toggle off)
                    _context.BlogImpressions.Remove(existingReaction);
                    await _context.SaveChangesAsync();
                    await BroadcastReceivedReactionSummaryToOwners(request.TargetType, request.TargetId, request.UserId, includeSelfActions: true);
                    return Ok(new { message = "Reaction removed", removed = true });
                }
                else
                {
                    // Add new reaction
                    var newReaction = new BlogImpression
                    {
                        BlogId = request.TargetId,
                        ImpressionId = request.ImpressionId,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.BlogImpressions.Add(newReaction);
                    await _context.SaveChangesAsync();
                    await BroadcastReceivedReactionSummaryToOwners(request.TargetType, request.TargetId, request.UserId, includeSelfActions: true);
                    return Ok(new { message = "Reaction added", removed = false });
                }
            }
            else if (request.TargetType == TargetType.Message)
            {
                // Check if user already reacted with this impression
                var existingReaction = await _context.MessageImpressions
                    .FirstOrDefaultAsync(mi => mi.MessageId == request.TargetId
                        && mi.UserId == request.UserId
                        && mi.ImpressionId == request.ImpressionId);

                bool wasRemoved = false;

                if (existingReaction != null)
                {
                    // Remove the reaction (toggle off)
                    _context.MessageImpressions.Remove(existingReaction);
                    await _context.SaveChangesAsync();
                    wasRemoved = true;
                }
                else
                {
                    // Add new reaction
                    var newReaction = new MessageImpression
                    {
                        MessageId = request.TargetId,
                        ImpressionId = request.ImpressionId,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.MessageImpressions.Add(newReaction);
                    await _context.SaveChangesAsync();
                }

                // 🔴 SIGNALR BROADCAST - Only for Message type
                try
                {
                    // Get the message to find its conversation
                    var message = await _context.Messages
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.MessageID == request.TargetId);

                    if (message?.ConversationId != null)
                    {
                        // Get updated impression counts for this message
                        var impressionCounts = await _context.MessageImpressions
                            .Where(mi => mi.MessageId == request.TargetId)
                            .Include(mi => mi.Impression)
                            .GroupBy(mi => mi.ImpressionId)
                            .Select(g => new
                            {
                                impressionId = g.Key,
                                emoji = g.First().Impression.Emoji,
                                name = g.First().Impression.Name,
                                count = g.Count()
                            })
                            .ToListAsync();

                        // Get impression details
                        var impression = await _context.PrimaryImpressions
                            .FirstOrDefaultAsync(p => p.Id == request.ImpressionId);

                        // Broadcast to all participants in the conversation
                        await _hubContext.Clients.Group($"conversation-{message.ConversationId}")
                            .SendAsync("MessageImpressionUpdated", new
                            {
                                messageId = request.TargetId,
                                userId = request.UserId,
                                impressionId = request.ImpressionId,
                                emoji = impression?.Emoji,
                                name = impression?.Name,
                                removed = wasRemoved,
                                impressions = impressionCounts,
                                timestamp = DateTime.UtcNow
                            });

                        _logger.LogInformation(
                            "Broadcasted message impression update for Message {MessageId} in Conversation {ConversationId}", 
                            request.TargetId, 
                            message.ConversationId);
                    }
                }
                catch (Exception signalREx)
                {
                    // Log but don't fail the request if SignalR broadcast fails
                    _logger.LogError(signalREx, "Failed to broadcast message impression via SignalR");
                }

                return Ok(new { 
                    message = wasRemoved ? "Reaction removed" : "Reaction added", 
                    removed = wasRemoved 
                });
            }
            else if (request.TargetType == TargetType.FeedPost)
            {
                // Check if user already reacted with this impression
                var existingFeedReaction = await _context.FeedPostImpressions
                    .FirstOrDefaultAsync(i => i.FeedPostId == request.TargetId && i.ImpressionId == request.ImpressionId && i.UserId == request.UserId);

                var wasRemoved = false;

                if (existingFeedReaction != null)
                {
                    // Remove the reaction (toggle off)
                    _context.FeedPostImpressions.Remove(existingFeedReaction);
                    await _context.SaveChangesAsync();
                    wasRemoved = true;
                }
                else
                {
                    // Add new reaction
                    var newReaction = new FeedPostImpression
                    {
                        FeedPostId = request.TargetId,
                        ImpressionId = request.ImpressionId,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.FeedPostImpressions.Add(newReaction);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = wasRemoved ? "Reaction removed" : "Reaction added", removed = wasRemoved });
            }
            else
            {
                return BadRequest("Invalid target type");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reacting to {TargetType} with ID {TargetId}", request.TargetType, request.TargetId);
            return StatusCode(500, "An error occurred while processing the reaction");
        }
    }

    private async Task BroadcastReceivedReactionSummaryToOwners(TargetType targetType, int targetId, int reactorUserId, bool includeSelfActions = true)
    {
        List<int> ownerUserIds;

        if (targetType == TargetType.Artist)
        {
            ownerUserIds = await _context.Set<Linker_UserToArtist>()
                .AsNoTracking()
                .Where(link => link.ArtistID == targetId)
                .Select(link => link.UserID)
                .Distinct()
                .ToListAsync();
        }
        else if (targetType == TargetType.Listing)
        {
            ownerUserIds = await (
                from listing in _context.Listings.AsNoTracking()
                join link in _context.Set<Linker_UserToArtist>().AsNoTracking()
                    on listing.ArtistID equals link.ArtistID
                where listing.ListingID == targetId
                select link.UserID
            )
            .Distinct()
            .ToListAsync();
        }
        else if (targetType == TargetType.Blog)
        {
            ownerUserIds = await _context.Blogs
                .AsNoTracking()
                .Where(blog => blog.BlogID == targetId)
                .Select(blog => blog.UserID)
                .Distinct()
                .ToListAsync();
        }
        else if (targetType == TargetType.FeedPost)
        {
            // For FeedPost, assume the owner is the one who posted it, i.e., the UserID in the FeedPost table
            ownerUserIds = await _context.FeedPosts
                .AsNoTracking()
                .Where(feedPost => feedPost.FeedPostID == targetId)
                .Select(feedPost => feedPost.AuthorUserID)
                .Distinct()
                .ToListAsync();
        }
        else
        {
            return;
        }

        var recipients = includeSelfActions
            ? ownerUserIds.Distinct().ToList()
            : ownerUserIds.Where(id => id != reactorUserId).Distinct().ToList();
        if (!recipients.Any())
        {
            return;
        }

        var sinceUtc = DateTime.UtcNow.AddHours(-1);
        foreach (var ownerUserId in recipients)
        {
            var summary = await BuildReceivedReactionSummaryForUser(ownerUserId, sinceUtc, includeSelfActions);
            await _hubContext.Clients.Group($"user-{ownerUserId}")
                .SendAsync("NotificationSummaryUpdated", new
                {
                    type = "reactions",
                    includeSelfActions,
                    reactionCountLastHour = summary.count,
                    latestReaction = summary.latest,
                    timestamp = DateTime.UtcNow
                });
        }
    }

    private async Task<(int count, object? latest)> BuildReceivedReactionSummaryForUser(int userId, DateTime sinceUtc, bool includeSelfActions)
    {
        var ownedArtistIds = await _context.Set<Linker_UserToArtist>()
            .AsNoTracking()
            .Where(link => link.UserID == userId)
            .Select(link => link.ArtistID)
            .Distinct()
            .ToListAsync();

        if (!ownedArtistIds.Any())
        {
            return (0, null);
        }

        var listingReactions = await (
            from impression in _context.ListingImpressions.AsNoTracking()
            join listing in _context.Listings.AsNoTracking()
                on impression.ListingId equals listing.ListingID
            where ownedArtistIds.Contains(listing.ArtistID)
                && (includeSelfActions || impression.UserId != userId)
                && impression.CreatedAt >= sinceUtc
            select new
            {
                impression.UserId,
                impression.CreatedAt,
                TargetType = TargetType.Listing,
                TargetId = listing.ListingID
            }
        ).ToListAsync();

        var artistReactions = await _context.ArtistImpressions
            .AsNoTracking()
            .Where(impression => ownedArtistIds.Contains(impression.ArtistId)
                && (includeSelfActions || impression.UserId != userId)
                && impression.CreatedAt >= sinceUtc)
            .Select(impression => new
            {
                impression.UserId,
                impression.CreatedAt,
                TargetType = TargetType.Artist,
                TargetId = impression.ArtistId
            })
            .ToListAsync();

        var blogReactions = await _context.BlogImpressions
            .AsNoTracking()
            .Join(
                _context.Blogs.AsNoTracking(),
                impression => impression.BlogId,
                blog => blog.BlogID,
                (impression, blog) => new { impression, blog })
            .Where(pair => pair.blog.UserID == userId
                && (includeSelfActions || pair.impression.UserId != userId)
                && pair.impression.CreatedAt >= sinceUtc)
            .Select(pair => new
            {
                pair.impression.UserId,
                pair.impression.CreatedAt,
                TargetType = TargetType.Blog,
                TargetId = pair.impression.BlogId,
            })
            .ToListAsync();

        var reactions = listingReactions
            .Concat(artistReactions)
            .Concat(blogReactions)
            .OrderByDescending(reaction => reaction.CreatedAt)
            .ToList();

        var count = reactions.Count;
        var latest = reactions.FirstOrDefault();
        if (latest == null)
        {
            return (count, null);
        }

        var reactor = await _context.NextAuthUsers
            .AsNoTracking()
            .Where(user => user.Id == latest.UserId)
            .Select(user => new { user.Name, user.Image })
            .FirstOrDefaultAsync();

        return (count, new
        {
            reactorName = string.IsNullOrWhiteSpace(reactor?.Name) ? "Someone" : reactor.Name,
            reactorImage = reactor?.Image,
            targetType = latest.TargetType.ToString(),
            targetId = latest.TargetId,
            href = await BuildReactionHref(latest.TargetType, latest.TargetId),
            createdAt = latest.CreatedAt,
        });
    }

    private async Task<string> BuildReactionHref(TargetType targetType, int targetId)
    {
        if (targetType == TargetType.Listing)
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
                return $"/artists/{listingRoute.ArtistPath}/listings/{listingRoute.ListingPath}";
            }
        }

        if (targetType == TargetType.Artist)
        {
            var artistPath = await _context.Artists
                .AsNoTracking()
                .Where(artist => artist.ArtistID == targetId)
                .Select(artist => artist.Path)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(artistPath))
            {
                return $"/artists/{artistPath}";
            }
        }

        if (targetType == TargetType.Blog)
        {
            var blogPath = await _context.Blogs
                .AsNoTracking()
                .Where(blog => blog.BlogID == targetId)
                .Select(blog => blog.Path)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(blogPath))
            {
                return $"/blogs/{blogPath}";
            }
        }

        if (targetType == TargetType.FeedPost)
        {
            return $"/feed?post={targetId}";
        }

        return "/artists";
    }
}