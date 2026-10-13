using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContestController : ControllerBase
{
    private readonly TAGDBContext _context;

    public ContestController(TAGDBContext context)
    {
        _context = context;
    }

    // Create Contest (Staff Only)
    [HttpPost("create")]
    public async Task<IActionResult> CreateContest([FromBody] Contest contest)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Generate slug if not provided
        if (string.IsNullOrEmpty(contest.Slug))
            contest.Slug = contest.Title.ToLower().Replace(" ", "-");

        _context.Contests.Add(contest);
        await _context.SaveChangesAsync();
        return Ok(contest);
    }

    // Fetch Active Contests
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Contest>>> GetActiveContests()
    {
        return await _context.Contests
            .Where(c => c.Status == "Active" && c.EndDate >= DateTime.UtcNow)
            .OrderBy(c => c.EndDate)
            .ToListAsync();
    }

    // Fetch Archive Contests
    [HttpGet("archive")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Contest>>> GetArchiveContests()
    {
        return await _context.Contests
            .Where(c => c.Status == "Archived" || c.EndDate < DateTime.UtcNow)
            .OrderByDescending(c => c.EndDate)
            .ToListAsync();
    }

    // Fetch Contest by ID with participating listings
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContestDetail(string slug)
    {
        var contest = await _context.Contests
            .Include(c => c.Entries)
                .ThenInclude(e => e.Listing)
             .Include(c => c.Entries)
                .ThenInclude(e => e.Artist)
            .FirstOrDefaultAsync(c => c.Slug == slug);

        if (contest == null) return NotFound();

        // Note: You would join with your Listings table here to return 
        // the actual Art details (Title, ImageUrl, etc.)
        return Ok(contest);
    }

    // Participate in Contest
    [HttpPost("participate")]
    public async Task<IActionResult> Participate([FromBody] ParticipationRequest request)
    {
        //var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        //var artistId = Guid.Parse(userIdString);

        Listing listing = await _context.Listings.FirstOrDefaultAsync(x => x.ListingID == request.ListingIds.FirstOrDefault());

        var entries = request.ListingIds.Select(listingId => new ContestEntry
        {
            ContestId = request.ContestId,
            ListingId = listingId,
            ArtistId = listing.ArtistID,
            JoinedAt = DateTime.UtcNow
        });

        // Use Upsert or check for duplicates to avoid Primary Key crashes
        foreach (var entry in entries)
        {
            bool exists = await _context.ContestEntries
                .AnyAsync(e => e.ContestId == entry.ContestId && e.ListingId == entry.ListingId);

            if (!exists) _context.ContestEntries.Add(entry);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Successfully joined the contest" });
    }
}

public record ParticipationRequest(int ContestId, List<int> ListingIds);