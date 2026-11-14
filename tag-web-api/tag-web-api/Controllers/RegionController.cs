// <copyright file="RegionController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// The staff-managed list of regional community groupings shared by venues and artists, and the regions of each venue.
/// Anyone can read the active list. Only staff can create, edit or deactivate regions. Owners and staff choose the
/// regions of a venue. Regions are never deleted, so deactivating one never breaks the venues and artists that use it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RegionController : ControllerBase
{
    // Markup and control characters never belong in a region name.
    private static readonly Regex UnsafeTextRegex = new(@"[<>\p{Cc}]", RegexOptions.Compiled);

    private readonly TAGDBContext context;

    public RegionController(TAGDBContext context)
    {
        this.context = context;
    }

    /// <summary>The regions that can currently be chosen. Optionally only those covering one state, for example <c>?state=GA</c>.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<RegionDto>>> GetActiveRegions([FromQuery] string? state = null)
    {
        var query = this.context.Set<Region>().AsNoTracking().Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(state))
        {
            var code = state.Trim().ToUpper(CultureInfo.InvariantCulture);
            if (!UsStates.Names.ContainsKey(code))
            {
                return this.Ok(Array.Empty<RegionDto>());
            }

            query = query.Where(r => r.States.Contains(code));
        }

        var regions = await query.OrderBy(r => r.Name).ToListAsync().ConfigureAwait(false);
        return this.Ok(regions.Select(ToDto));
    }

    /// <summary>Staff only. Every region, active or not, with how many venues and artists use it.</summary>
    [HttpGet("admin/all")]
    public async Task<ActionResult<IEnumerable<RegionAdminDto>>> GetAllRegions()
    {
        var (_, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var regions = await this.context.Set<Region>().AsNoTracking().OrderBy(r => r.Name).ToListAsync().ConfigureAwait(false);

        var venueCounts = await this.context.Set<Linker_VenueToRegion>()
            .AsNoTracking()
            .GroupBy(l => l.RegionID)
            .Select(g => new { RegionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RegionId, x => x.Count)
            .ConfigureAwait(false);

        var artistCounts = await this.context.Set<Linker_ArtistToRegion>()
            .AsNoTracking()
            .GroupBy(l => l.RegionID)
            .Select(g => new { RegionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RegionId, x => x.Count)
            .ConfigureAwait(false);

        return this.Ok(regions.Select(r => ToAdminDto(
            r,
            venueCounts.TryGetValue(r.RegionID, out var venues) ? venues : 0,
            artistCounts.TryGetValue(r.RegionID, out var artists) ? artists : 0)));
    }

    [HttpPost]
    public async Task<ActionResult<RegionAdminDto>> CreateRegion([FromBody] RegionRequest request)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var validationError = Validate(request, out var name, out var type, out var states);
        if (validationError != null)
        {
            return this.BadRequest(new { message = validationError });
        }

        var slug = VenueController.ToSlug(name!);
        if (slug.Length == 0)
        {
            return this.BadRequest(new { message = "Name must contain at least one letter or digit." });
        }

        var existing = await this.context.Set<Region>().AsNoTracking().ToListAsync().ConfigureAwait(false);
        if (existing.Any(r => r.Slug == slug || string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return this.Conflict(new { message = "A region with this name already exists." });
        }

        var region = new Region { Name = name!, Slug = slug, Type = type!, States = states!, IsActive = true };

        // The region and its audit entry are saved together, so a change never exists without its audit entry.
        this.context.Set<Region>().Add(region);
        this.context.Set<Log>().Add(BuildAuditLog("Region created", "create", $"slug={slug};type={type};states={string.Join(",", states!)}", staffId!.Value));
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.Created($"api/Region/{region.RegionID}", ToAdminDto(region, 0, 0));
    }

    /// <summary>Edits a region's name, type and states. The slug stays the same so shared filter links keep working.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<RegionAdminDto>> UpdateRegion(int id, [FromBody] RegionRequest request)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var region = await this.context.Set<Region>().FirstOrDefaultAsync(r => r.RegionID == id).ConfigureAwait(false);
        if (region == null)
        {
            return this.NotFound();
        }

        var validationError = Validate(request, out var name, out var type, out var states);
        if (validationError != null)
        {
            return this.BadRequest(new { message = validationError });
        }

        var nameTaken = await this.context.Set<Region>()
            .AnyAsync(r => r.RegionID != id && r.Name.ToLower() == name!.ToLower())
            .ConfigureAwait(false);
        if (nameTaken)
        {
            return this.Conflict(new { message = "A region with this name already exists." });
        }

        var before = $"{region.Name}|{region.Type}|{string.Join(",", region.States)}";
        region.Name = name!;
        region.Type = type!;
        region.States = states!;

        this.context.Set<Log>().Add(BuildAuditLog("Region updated", "update", $"slug={region.Slug};from={before};to={region.Name}|{region.Type}|{string.Join(",", region.States)}", staffId!.Value));
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.Ok(await this.ToAdminDtoWithCountsAsync(region).ConfigureAwait(false));
    }

    /// <summary>Deactivates or reactivates a region. Venues and artists keep a deactivated region; it just can't be chosen again.</summary>
    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<RegionAdminDto>> SetRegionActive(int id, [FromBody] RegionActiveRequest request)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        if (request == null)
        {
            return this.BadRequest();
        }

        var region = await this.context.Set<Region>().FirstOrDefaultAsync(r => r.RegionID == id).ConfigureAwait(false);
        if (region == null)
        {
            return this.NotFound();
        }

        if (region.IsActive != request.IsActive)
        {
            region.IsActive = request.IsActive;
            this.context.Set<Log>().Add(BuildAuditLog(
                request.IsActive ? "Region activated" : "Region deactivated",
                request.IsActive ? "activate" : "deactivate",
                $"slug={region.Slug};name={region.Name}",
                staffId!.Value));
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        return this.Ok(await this.ToAdminDtoWithCountsAsync(region).ConfigureAwait(false));
    }

    /// <summary>
    /// Replaces the regions of a venue. Owners and staff only. Regions the venue already has can stay even when they
    /// were deactivated, but a deactivated region can't be newly added.
    /// </summary>
    [HttpPut("~/api/Venue/{venueId:int}/regions")]
    public async Task<ActionResult<IEnumerable<RegionDto>>> SetVenueRegions(int venueId, [FromBody] VenueRegionsRequest request)
    {
        var userId = VenueAccess.GetUserId(this.User);
        if (userId == null)
        {
            return this.Unauthorized();
        }

        if (!await this.context.Set<Venue>().AnyAsync(v => v.VenueID == venueId).ConfigureAwait(false))
        {
            return this.NotFound();
        }

        // Permission is decided before anything about the request is validated or applied.
        if (!await VenueAccess.CanManageAsync(this.context, venueId, userId.Value).ConfigureAwait(false))
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to change the regions of this venue." });
        }

        if (request?.RegionIds == null)
        {
            return this.BadRequest(new { message = "RegionIds is required (use an empty list to clear the regions)." });
        }

        var wanted = request.RegionIds.Distinct().ToList();
        if (wanted.Any(id => id <= 0))
        {
            return this.BadRequest(new { message = "Region ids must be positive numbers." });
        }

        var regions = await this.context.Set<Region>()
            .Where(r => wanted.Contains(r.RegionID))
            .ToListAsync()
            .ConfigureAwait(false);

        var unknown = wanted.Except(regions.Select(r => r.RegionID)).ToList();
        if (unknown.Count > 0)
        {
            return this.BadRequest(new { message = $"Unknown region id: {string.Join(", ", unknown)}." });
        }

        var links = await this.context.Set<Linker_VenueToRegion>()
            .Where(l => l.VenueID == venueId)
            .ToListAsync()
            .ConfigureAwait(false);
        var currentIds = links.Select(l => l.RegionID).ToHashSet();

        var unavailable = regions.FirstOrDefault(r => !r.IsActive && !currentIds.Contains(r.RegionID));
        if (unavailable != null)
        {
            return this.BadRequest(new { message = $"The region '{unavailable.Name}' is no longer available." });
        }

        var removed = links.Where(l => !wanted.Contains(l.RegionID)).ToList();
        var added = wanted.Where(id => !currentIds.Contains(id)).ToList();
        if (removed.Count > 0 || added.Count > 0)
        {
            this.context.Set<Linker_VenueToRegion>().RemoveRange(removed);
            foreach (var regionId in added)
            {
                this.context.Set<Linker_VenueToRegion>().Add(new Linker_VenueToRegion { VenueID = venueId, RegionID = regionId });
            }

            this.context.Set<Log>().Add(new Log
            {
                ShortText = "Venue regions changed",
                Tags = "scope=audit;entity=venue;event=region;operation=set;result=success;channel=db",
                Critical = false,
                LoggedData = $"venueId={venueId};actingUserId={userId.Value};added={string.Join(",", added)};removed={string.Join(",", removed.Select(l => l.RegionID))}",
                UserID = userId.Value,
                LogTimestamp = DateTime.UtcNow,
            });
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        return this.Ok(regions.OrderBy(r => r.Name, StringComparer.Ordinal).Select(ToDto));
    }

    // Returns an error message, or null and the trimmed, validated values.
    private static string? Validate(RegionRequest? request, out string? name, out string? type, out List<string>? states)
    {
        name = null;
        type = null;
        states = null;

        var trimmed = request?.Name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Name is required.";
        }

        if (trimmed.Length > Region.NameMaxLength)
        {
            return $"Name must be at most {Region.NameMaxLength} characters.";
        }

        if (UnsafeTextRegex.IsMatch(trimmed))
        {
            return "Name must not contain markup characters (< or >) or control characters.";
        }

        if (!Region.TryNormalize(request!.Type, request.States, out var normalizedType, out var normalizedStates, out var error))
        {
            return error;
        }

        name = trimmed;
        type = normalizedType;
        states = normalizedStates;
        return null;
    }

    private async Task<RegionAdminDto> ToAdminDtoWithCountsAsync(Region region)
    {
        var venues = await this.context.Set<Linker_VenueToRegion>().CountAsync(l => l.RegionID == region.RegionID).ConfigureAwait(false);
        var artists = await this.context.Set<Linker_ArtistToRegion>().CountAsync(l => l.RegionID == region.RegionID).ConfigureAwait(false);
        return ToAdminDto(region, venues, artists);
    }

    private static RegionDto ToDto(Region region) => new()
    {
        RegionID = region.RegionID,
        Name = region.Name,
        Slug = region.Slug,
        Type = region.Type,
        States = region.States.ToList(),
        IsActive = region.IsActive,
    };

    private static RegionAdminDto ToAdminDto(Region region, int venueCount, int artistCount) => new()
    {
        RegionID = region.RegionID,
        Name = region.Name,
        Slug = region.Slug,
        Type = region.Type,
        States = region.States.ToList(),
        IsActive = region.IsActive,
        VenueCount = venueCount,
        ArtistCount = artistCount,
    };

    private static Log BuildAuditLog(string shortText, string operation, string details, int actingUserId)
    {
        return new Log
        {
            ShortText = shortText,
            Tags = $"scope=audit;entity=region;event=region;operation={operation};result=success;channel=db",
            Critical = false,
            LoggedData = $"{details};actingUserId={actingUserId}",
            UserID = actingUserId,
            LogTimestamp = DateTime.UtcNow,
        };
    }

    // Signed in and flagged as staff, or the response to send back.
    private async Task<(int? UserId, ActionResult? Error)> RequireStaffAsync()
    {
        var userId = VenueAccess.GetUserId(this.User);
        if (userId == null)
        {
            return (null, this.Unauthorized());
        }

        if (!await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false))
        {
            return (null, this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can manage regions." }));
        }

        return (userId, null);
    }
}

public class RegionRequest
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public List<string?>? States { get; set; }
}

public class RegionActiveRequest
{
    public bool IsActive { get; set; }
}

public class VenueRegionsRequest
{
    public List<int>? RegionIds { get; set; }
}

public class RegionDto
{
    public int RegionID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public List<string> States { get; set; } = new();

    public bool IsActive { get; set; }
}

public class RegionAdminDto : RegionDto
{
    public int VenueCount { get; set; }

    public int ArtistCount { get; set; }
}
