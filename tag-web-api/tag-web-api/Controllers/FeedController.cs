// <copyright file="FeedController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FeedController : ControllerBase
{
    private readonly TAGDBContext _context;
    private static readonly Random _rng = new();

    public FeedController(TAGDBContext context)
    {
        _context = context;
    }

    // GET: api/Feed — Main bloomscroll feed (paginated, with sub-algorithm filtering)
    [HttpGet]
    public async Task<ActionResult<object>> GetFeed(
        [FromQuery] string? algorithm = "latest",
        [FromQuery] string? entityType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] int? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.Set<FeedPost>()
            .Where(p => p.IsPublished && !p.IsModerationBlocked)
            .Include(p => p.Author)
            .Include(p => p.Picture)
            .AsQueryable();

        // Sub-algorithm filters
        if (!string.IsNullOrEmpty(entityType) && entityId.HasValue)
        {
            query = query.Where(p =>
                (p.AuthorEntityType != null && p.AuthorEntityType.ToLower() == entityType.ToLower() && p.AuthorEntityID == entityId.Value) ||
                (p.SharedEntityType != null && p.SharedEntityType.ToLower() == entityType.ToLower() && p.SharedEntityID == entityId.Value));
        }

        if (userId.HasValue)
        {
            query = query.Where(p => p.AuthorUserID == userId.Value);
        }

        // Sorting algorithm
        query = algorithm?.ToLower() switch
        {
            "trending" => query.OrderByDescending(p => p.Impressions.Count).ThenByDescending(p => p.CreatedAt),
            "oldest" => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt), // "latest" default
        };

        var totalCount = await query.CountAsync();

        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<FeedPostSummaryDTO>();
        foreach (var post in posts)
        {
            items.Add(await MapToSummary(post));
        }

        return new
        {
            items,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            algorithm,
        };
    }

    // GET: api/Feed/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<FeedPostSummaryDTO>> GetById(int id)
    {
        var post = await _context.Set<FeedPost>()
            .Include(p => p.Author)
            .Include(p => p.Picture)
            .FirstOrDefaultAsync(p => p.FeedPostID == id && p.IsPublished && !p.IsModerationBlocked);

        if (post == null) return NotFound();
        return await MapToSummary(post);
    }

    // POST: api/Feed
    [HttpPost]
    public async Task<ActionResult<FeedPostSummaryDTO>> Create(CreateFeedPostDTO dto, [FromQuery] int authorUserId)
    {
        var post = new FeedPost
        {
            AuthorUserID = authorUserId,
            AuthorEntityType = dto.AuthorEntityType,
            AuthorEntityID = dto.AuthorEntityID,
            PostType = dto.PostType ?? "General",
            Body = dto.Body,
            Body_Plaintext = StripHtml(dto.Body),
            SharedEntityType = dto.SharedEntityType,
            SharedEntityID = dto.SharedEntityID,
            SharedURL = BuildShareURL(dto.SharedEntityType, dto.SharedEntityID),
            PictureID = dto.PictureID,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Set<FeedPost>().Add(post);
        await _context.SaveChangesAsync();

        // Reload with navigation
        var saved = await _context.Set<FeedPost>()
            .Include(p => p.Author)
            .Include(p => p.Picture)
            .FirstAsync(p => p.FeedPostID == post.FeedPostID);

        return CreatedAtAction(nameof(GetById), new { id = post.FeedPostID }, await MapToSummary(saved));
    }

    // DELETE: api/Feed/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int userId)
    {
        var post = await _context.Set<FeedPost>().FindAsync(id);
        if (post == null) return NotFound();
        if (post.AuthorUserID != userId) return Forbid();

        post.IsPublished = false;
        post.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/Feed/suggest-hello-world — Generate a quirky first post
    [HttpPost("suggest-hello-world")]
    public async Task<ActionResult<FeedPostSummaryDTO>> SuggestHelloWorld(SuggestHelloWorldDTO dto, [FromQuery] int userId)
    {
        string entityName = "the universe";
        string entityPath = "";
        string? authorEntityType = dto.EntityType;
        int? authorEntityID = dto.EntityID;

        if (string.Equals(dto.EntityType, "Artist", StringComparison.OrdinalIgnoreCase) && dto.EntityID.HasValue)
        {
            var artist = await _context.Artists.FindAsync(dto.EntityID.Value);
            if (artist != null)
            {
                entityName = StripHtml(artist.Title) ?? "this artist";
                entityPath = $"/artists/{artist.Path}";
            }
        }
        else if (string.Equals(dto.EntityType, "Listing", StringComparison.OrdinalIgnoreCase) && dto.EntityID.HasValue)
        {
            var listing = await _context.Listings.Include(l => l.Artist).FirstOrDefaultAsync(l => l.ListingID == dto.EntityID.Value);
            if (listing != null)
            {
                entityName = StripHtml(listing.Title) ?? "this masterpiece";
                entityPath = $"/artists/{listing.Artist?.Path}/listings/{listing.Path}";
                authorEntityType = "Artist";
                authorEntityID = listing.ArtistID;
            }
        }

        var quips = new[]
        {
            $"🎨 *clears throat* \n\nHello world, it's {entityName}. We've arrived and we brought art. You're welcome.",
            $"🌱 {entityName} just sprouted from the creative void. Please water us with follows and reactions. We thrive on validation.",
            $"📢 BREAKING: {entityName} has officially joined the feed. Local art scene reportedly \"shook.\" More at 11.",
            $"✨ Plot twist: {entityName} exists now. The main character energy is immaculate. No notes.",
            $"🚀 Houston, {entityName} has launched. Trajectory: straight into your bloomscroll. ETA: right now.",
            $"🎭 {entityName} enters stage left. The audience gasps. A single rose is thrown. This is our origin story.",
            $"🖼️ Someone told {entityName} to \"put yourself out there.\" So here we are. Out here. Thriving. Slightly nervous.",
            $"🌊 {entityName} is making waves. Tiny ones, for now. But every tsunami starts with a vibe check.",
            $"🎪 Roll up, roll up! {entityName} has joined the guild. Tricks include: making art and pretending to be chill about it.",
            $"🔮 The algorithm whispered: \"{entityName} is about to do something cool.\" We don't know what yet. But stay tuned.",
        };

        var body = quips[_rng.Next(quips.Length)];

        var post = new FeedPost
        {
            AuthorUserID = userId,
            AuthorEntityType = authorEntityType,
            AuthorEntityID = authorEntityID,
            PostType = "HelloWorld",
            Body = body,
            Body_Plaintext = body,
            SharedEntityType = dto.EntityType,
            SharedEntityID = dto.EntityID,
            SharedURL = entityPath,
            IsPublished = true,
            IsSuggestedPost = true,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Set<FeedPost>().Add(post);
        await _context.SaveChangesAsync();

        var saved = await _context.Set<FeedPost>()
            .Include(p => p.Author)
            .Include(p => p.Picture)
            .FirstAsync(p => p.FeedPostID == post.FeedPostID);

        return CreatedAtAction(nameof(GetById), new { id = post.FeedPostID }, await MapToSummary(saved));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string? BuildShareURL(string? entityType, int? entityId)
    {
        if (string.IsNullOrEmpty(entityType) || !entityId.HasValue) return null;

        return entityType.ToLower() switch
        {
            "listing" => $"/listings/{entityId.Value}",
            "artist" => $"/artists/{entityId.Value}",
            "event" => $"/events/{entityId.Value}",
            "vendor" => $"/vendors/{entityId.Value}",
            "venue" => $"/venues/{entityId.Value}",
            _ => null,
        };
    }

    private async Task<FeedPostSummaryDTO> MapToSummary(FeedPost post)
    {
        string? authorEntityName = null;
        string? authorEntityPath = null;
        object? sharedPreview = null;

        // Resolve author entity name
        if (!string.IsNullOrEmpty(post.AuthorEntityType) && post.AuthorEntityID.HasValue)
        {
            if (string.Equals(post.AuthorEntityType, "Artist", StringComparison.OrdinalIgnoreCase))
            {
                var artist = await _context.Artists.AsNoTracking().Include(a => a.ProfilePic)
                    .FirstOrDefaultAsync(a => a.ArtistID == post.AuthorEntityID.Value);
                if (artist != null) { authorEntityName = StripHtml(artist.Title); authorEntityPath = $"/artists/{artist.Path}"; }
            }
            else if (string.Equals(post.AuthorEntityType, "Vendor", StringComparison.OrdinalIgnoreCase))
            {
                var vendor = await _context.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.VendorID == post.AuthorEntityID.Value);
                if (vendor != null) { authorEntityName = StripHtml(vendor.CompanyName); }
            }
            else if (string.Equals(post.AuthorEntityType, "Venue", StringComparison.OrdinalIgnoreCase))
            {
                var venue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueID == post.AuthorEntityID.Value);
                if (venue != null) { authorEntityName = StripHtml(venue.Name); }
            }
        }

        // Resolve shared entity preview
        if (!string.IsNullOrEmpty(post.SharedEntityType) && post.SharedEntityID.HasValue)
        {
            sharedPreview = await BuildSharedPreview(post.SharedEntityType, post.SharedEntityID.Value);
        }

        var reactionCount = await _context.Set<FeedPostImpression>()
            .CountAsync(i => i.FeedPostId == post.FeedPostID);

        var commentCount = await _context.Comments
            .CountAsync(c => c.TargetType == CommentTargetType.FeedPost && c.TargetId == post.FeedPostID && !c.IsDeleted);

        return new FeedPostSummaryDTO
        {
            FeedPostID = post.FeedPostID,
            AuthorUserID = post.AuthorUserID,
            AuthorName = post.Author?.PreferredName ?? post.Author?.Username ?? post.Author?.FirstName,
            AuthorImage = post.Author?.ProfilePic?.URL,
            AuthorEntityType = post.AuthorEntityType,
            AuthorEntityID = post.AuthorEntityID,
            AuthorEntityName = authorEntityName,
            AuthorEntityPath = authorEntityPath,
            PostType = post.PostType,
            Body = post.Body,
            Body_Plaintext = post.Body_Plaintext,
            SharedEntityType = post.SharedEntityType,
            SharedEntityID = post.SharedEntityID,
            SharedURL = post.SharedURL,
            SharedEntityPreview = sharedPreview,
            PictureID = post.PictureID,
            PictureURL = post.Picture?.URL,
            IsSuggestedPost = post.IsSuggestedPost,
            CreatedAt = post.CreatedAt,
            CommentCount = commentCount,
            ReactionCount = reactionCount,
        };
    }

    private async Task<object?> BuildSharedPreview(string entityType, int entityId)
    {
        if (string.Equals(entityType, "Listing", StringComparison.OrdinalIgnoreCase))
        {
            var listing = await _context.Listings.AsNoTracking().Include(l => l.CoverPic).Include(l => l.Artist)
                .FirstOrDefaultAsync(l => l.ListingID == entityId && l.IsPublished && !l.IsModerationBlocked);
            if (listing == null) return null;
            return new { type = "Listing", id = listing.ListingID, title = StripHtml(listing.Title), image = listing.CoverPic?.URL, price = listing.Price, artistName = StripHtml(listing.Artist?.Title), path = $"/artists/{listing.Artist?.Path}/listings/{listing.Path}" };
        }

        if (string.Equals(entityType, "Artist", StringComparison.OrdinalIgnoreCase))
        {
            var artist = await _context.Artists.AsNoTracking().Include(a => a.ProfilePic)
                .FirstOrDefaultAsync(a => a.ArtistID == entityId && a.IsPublished && !a.IsModerationBlocked);
            if (artist == null) return null;
            return new { type = "Artist", id = artist.ArtistID, title = StripHtml(artist.Title), image = artist.ProfilePic?.URL, byline = StripHtml(artist.Byline), path = $"/artists/{artist.Path}" };
        }

        if (string.Equals(entityType, "Event", StringComparison.OrdinalIgnoreCase))
        {
            var evt = await _context.Events.AsNoTracking().Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.EventID == entityId && e.StatusID == EventStatus.Published);
            if (evt == null) return null;
            return new { type = "Event", id = evt.EventID, title = StripHtml(evt.Title), venue = StripHtml(evt.Venue?.Name), startTime = evt.StartTime, path = $"/events/{evt.Path}" };
        }

        return null;
    }

    private static string? StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var noTags = Regex.Replace(value, "(?is)<[^>]+>", " ");
        return noTags.Replace("&nbsp;", " ").Replace("&amp;", "&").Trim();
    }
}