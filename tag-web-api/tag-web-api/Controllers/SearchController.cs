using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// Provides global search capabilities across multiple entities. Only public content is returned: artists, listings
/// and venues that are published and not blocked, and published events. Users are returned only to signed-in callers.
/// </summary>
[ApiController]
[Route("search")]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private const int MaxLimit = 50;

    private readonly TAGDBContext _context;

    public SearchController(TAGDBContext context)
    {
        _context = context;
    }

    private static int ClampLimit(int limit) => Math.Clamp(limit, 1, MaxLimit);

    /// <summary>
    /// Searches published Artists by Title, Byline, Statement, or SEOTags.
    /// Route: /search/artist
    /// </summary>
    [HttpGet("artist")]
    public async Task<IActionResult> SearchArtists([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var needle = q.ToLower();
        var query = _context.Artists.AsNoTracking().Where(a =>
            a.IsPublished && !a.IsModerationBlocked &&
            ((a.Title != null && a.Title.ToLower().Contains(needle)) ||
            (a.Byline != null && a.Byline.ToLower().Contains(needle)) ||
            (a.Statement != null && a.Statement.ToLower().Contains(needle)) ||
            (a.SEOTags != null && a.SEOTags.ToLower().Contains(needle)))
        );

        var results = await query.Take(ClampLimit(limit)).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches published Listings (of published artists) by Title, Description, or Path.
    /// Route: /search/listing
    /// </summary>
    [HttpGet("listing")]
    public async Task<IActionResult> SearchListings([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var needle = q.ToLower();
        var query = _context.Listings.AsNoTracking().Where(l =>
            l.IsPublished && !l.IsModerationBlocked &&
            l.Artist != null && l.Artist.IsPublished && !l.Artist.IsModerationBlocked &&
            ((l.Title != null && l.Title.ToLower().Contains(needle)) ||
            (l.Description != null && l.Description.ToLower().Contains(needle)) ||
            (l.Path != null && l.Path.ToLower().Contains(needle)))
        );

        var results = await query.Take(ClampLimit(limit)).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches published Events by Title, Description, Note, or PointOfContact.
    /// Route: /search/event
    /// </summary>
    [HttpGet("event")]
    public async Task<IActionResult> SearchEvents([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var needle = q.ToLower();
        var query = _context.Events.AsNoTracking().Where(e =>
            e.StatusID == EventStatus.Published &&
            ((e.Title != null && e.Title.ToLower().Contains(needle)) ||
            (e.Description != null && e.Description.ToLower().Contains(needle)) ||
            (e.Note != null && e.Note.ToLower().Contains(needle)) ||
            (e.PointOfContact != null && e.PointOfContact.ToLower().Contains(needle)))
        );

        var results = await query.Take(ClampLimit(limit)).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches Users. Signed-in callers only. Everyone finds public members by name or username, and the result carries
    /// no private fields. Staff can also search by email and see every user, as the staff lookup needs.
    /// Route: /search/user
    /// </summary>
    [HttpGet("user")]
    [Authorize]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var callerId = VenueAccess.GetUserId(User);
        if (callerId == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var isStaff = await VenueAccess.IsStaffAsync(_context, callerId.Value).ConfigureAwait(false);

        var needle = q.ToLower();
        var query = _context.Users.AsNoTracking().AsQueryable();
        if (isStaff)
        {
            query = query.Where(u =>
                (u.FirstName != null && u.FirstName.ToLower().Contains(needle)) ||
                (u.FamName != null && u.FamName.ToLower().Contains(needle)) ||
                (u.PreferredName != null && u.PreferredName.ToLower().Contains(needle)) ||
                (u.Username != null && u.Username.ToLower().Contains(needle)) ||
                u.EmailOne.ToLower().Contains(needle) ||
                (u.EmailTwo != null && u.EmailTwo.ToLower().Contains(needle)));
        }
        else
        {
            // Public members only, and never matched or shown by email.
            query = query.Where(u =>
                u.IsPublished && !u.IsModerationBlocked && !u.HideFromPublic && !u.MembershipBanned &&
                (u.UserPrivacy == null || !u.UserPrivacy.HideProfileFromPublic) &&
                ((u.FirstName != null && u.FirstName.ToLower().Contains(needle)) ||
                (u.FamName != null && u.FamName.ToLower().Contains(needle)) ||
                (u.PreferredName != null && u.PreferredName.ToLower().Contains(needle)) ||
                (u.Username != null && u.Username.ToLower().Contains(needle))));
        }

        var users = await query.OrderBy(u => u.UserID).Take(ClampLimit(limit)).ToListAsync(cancellationToken);
        return Ok(users.Select(u => new UserSearchResultDto
        {
            UserID = u.UserID,
            Username = u.Username,
            FirstName = u.FirstName,
            FamName = u.FamName,
            PreferredName = u.PreferredName,
            EmailOne = isStaff ? u.EmailOne : null,
        }));
    }

    /// <summary>
    /// Searches published Venues by Name. Only a small public summary of each venue is returned.
    /// Route: /search/venue
    /// </summary>
    [HttpGet("venue")]
    public async Task<IActionResult> SearchVenues([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var needle = q.ToLower();
        var venues = await _context.Venues.AsNoTracking()
            .Where(v => v.IsPublished && !v.IsModerationBlocked && (v.Name_Plaintext ?? v.Name).ToLower().Contains(needle))
            .OrderBy(v => v.VenueID)
            .Take(ClampLimit(limit))
            .ToListAsync(cancellationToken);

        return Ok(venues.Select(v => new VenueSearchResultDto
        {
            VenueID = v.VenueID,
            Name = VenueController.CoalescePlaintext(v.Name_Plaintext, v.Name) ?? string.Empty,
            Slug = VenueController.ToSlug(v.Name),
            Description = VenueController.CoalescePlaintext(v.Description_Plaintext, v.Description),
        }));
    }
}

/// <summary>A user found by search. EmailOne is filled in only for staff.</summary>
public class UserSearchResultDto
{
    public int UserID { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public string? FamName { get; set; }

    public string? PreferredName { get; set; }

    public string? EmailOne { get; set; }
}

public class VenueSearchResultDto
{
    public int VenueID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }
}
