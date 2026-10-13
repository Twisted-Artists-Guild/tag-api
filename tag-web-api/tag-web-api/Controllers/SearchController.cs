using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// Provides global search capabilities across multiple entities.
/// </summary>
[ApiController]
[Route("search")]
// [Authorize] // Uncomment if search endpoints should only be for authenticated users
public class SearchController : ControllerBase
{
    private readonly TAGDBContext _context;

    public SearchController(TAGDBContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Searches Artists by Title, Byline, Statement, or SEOTags.
    /// Route: /search/artist
    /// </summary>
    [HttpGet("artist")]
    public async Task<IActionResult> SearchArtists([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var query = _context.Artists.AsNoTracking().Where(a =>
            (a.Title != null && a.Title.Contains(q)) ||
            (a.Byline != null && a.Byline.Contains(q)) ||
            (a.Statement != null && a.Statement.Contains(q)) ||
            (a.SEOTags != null && a.SEOTags.Contains(q))
        );

        var results = await query.Take(limit).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches Listings by Title, Description, or Path.
    /// Route: /search/listing
    /// </summary>
    [HttpGet("listing")]
    public async Task<IActionResult> SearchListings([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var query = _context.Listings.AsNoTracking().Where(l =>
            (l.Title != null && l.Title.Contains(q)) ||
            (l.Description != null && l.Description.Contains(q)) ||
            (l.Path != null && l.Path.Contains(q))
        );

        var results = await query.Take(limit).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches Events by Title, Description, Note, or PointOfContact.
    /// Route: /search/event
    /// </summary>
    [HttpGet("event")]
    public async Task<IActionResult> SearchEvents([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var query = _context.Events.AsNoTracking().Where(e =>
            (e.Title != null && e.Title.Contains(q)) ||
            (e.Description != null && e.Description.Contains(q)) ||
            (e.Note != null && e.Note.Contains(q)) ||
            (e.PointOfContact != null && e.PointOfContact.Contains(q))
        );

        var results = await query.Take(limit).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches Users by FirstName, FamName, Username, EmailOne, or EmailTwo.
    /// Route: /search/user
    /// </summary>
    [HttpGet("user")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var query = _context.Users.AsNoTracking().Where(u =>
            (u.FirstName != null && u.FirstName.Contains(q)) ||
            (u.FamName != null && u.FamName.Contains(q)) ||
            (u.Username != null && u.Username.Contains(q)) ||
            (u.EmailOne != null && u.EmailOne.Contains(q)) ||
            (u.EmailTwo != null && u.EmailTwo.Contains(q))
        );

        var results = await query.Take(limit).ToListAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches Venues by Name.
    /// Route: /search/venue
    /// </summary>
    [HttpGet("venue")]
    public async Task<IActionResult> SearchVenues([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<object>());

        var query = _context.Venues.AsNoTracking().Where(v =>
            (v.Name != null && v.Name.Contains(q))
        );

        var results = await query.Take(limit).ToListAsync(cancellationToken);
        return Ok(results);
    }
}