// <copyright file="VenueListController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// The public, paged venue directory behind the /venues page. Anyone can call it. Only published, non-blocked venues are
/// ever returned, whether or not they have an owner. Venues come back alphabetically by name, and the cursor from one page
/// is passed to the next, so a long list is never loaded in one go.
/// </summary>
[ApiController]
[Route("api/venue/list")]
[AllowAnonymous]
public class VenueListController : ControllerBase
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 50;

    private const int MaxTextLength = 100;
    private const int MaxFilterValues = 10;
    private const int MaxCursorLength = 2000;
    private const int DescriptionPreviewLength = 200;

    private readonly TAGDBContext context;

    public VenueListController(TAGDBContext context)
    {
        this.context = context;
    }

    /// <summary>
    /// Lists venues. Every filter is optional and they combine: a venue must match all of the filters that are given.
    /// Within a filter that takes several values, <c>category</c> and <c>region</c> match a venue that has ANY of the values,
    /// while <c>tag</c> needs ALL of them. <c>state</c> accepts a code (GA) or a name (Georgia). <c>zip</c> matches by prefix.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<VenueListPageDto>> ListVenues(
        [FromQuery] string? q = null,
        [FromQuery(Name = "category")] string[]? category = null,
        [FromQuery(Name = "tag")] string[]? tag = null,
        [FromQuery(Name = "region")] string[]? region = null,
        [FromQuery] string? city = null,
        [FromQuery] string? state = null,
        [FromQuery] string? zip = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = DefaultPageSize)
    {
        var query = this.context.Set<Venue>().AsNoTracking().Where(v => v.IsPublished && !v.IsModerationBlocked);

        var search = Limit(q);
        if (search.Length > 0)
        {
            query = query.Where(v => (v.Name_Plaintext ?? v.Name).ToLower().Contains(search));
        }

        var cityName = Limit(city);
        if (cityName.Length > 0)
        {
            query = query.Where(v => v.Address != null && v.Address.City != null && v.Address.City.ToLower() == cityName);
        }

        var stateText = Limit(state);
        if (stateText.Length > 0)
        {
            var stateValues = StateValues(stateText);
            query = query.Where(v => v.Address != null && v.Address.State != null && stateValues.Contains(v.Address.State.ToLower()));
        }

        var zipPrefix = ZipPrefix(zip);
        if (zipPrefix.Length > 0)
        {
            query = query.Where(v => v.Address != null && v.Address.ZipCode != null && v.Address.ZipCode.StartsWith(zipPrefix));
        }

        var categorySlugs = Values(category);
        if (categorySlugs.Count > 0)
        {
            query = query.Where(v => v.CategoryLinks.Any(link => categorySlugs.Contains(link.Category!.Slug)));
        }

        var regionSlugs = Values(region);
        if (regionSlugs.Count > 0)
        {
            query = query.Where(v => v.RegionLinks.Any(link => regionSlugs.Contains(link.Region!.Slug)));
        }

        // A venue has to carry every requested tag. A tag that can't exist matches no venue.
        foreach (var requested in (tag ?? Array.Empty<string>()).Take(VenueTag.MaxTagsPerVenue))
        {
            if (!VenueTag.TryNormalize(requested, out var tagName, out _))
            {
                return this.Ok(new VenueListPageDto());
            }

            query = query.Where(v => v.TagLinks.Any(link => link.Tag!.Name == tagName));
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            if (!TryDecodeCursor(cursor, out var cursorKey, out var cursorId))
            {
                return this.BadRequest(new { message = "The cursor is not valid." });
            }

            query = query.Where(v =>
                string.Compare((v.Name_Plaintext ?? v.Name).ToLower(), cursorKey) > 0
                || ((v.Name_Plaintext ?? v.Name).ToLower() == cursorKey && v.VenueID > cursorId));
        }

        var pageSize = Math.Clamp(limit, 1, MaxPageSize);

        // The page is chosen first, using only the sort key and id. The details are loaded afterwards for just those venues.
        var page = await query
            .OrderBy(v => (v.Name_Plaintext ?? v.Name).ToLower())
            .ThenBy(v => v.VenueID)
            .Select(v => new { v.VenueID, SortKey = (v.Name_Plaintext ?? v.Name).ToLower() })
            .Take(pageSize + 1)
            .ToListAsync()
            .ConfigureAwait(false);

        var hasMore = page.Count > pageSize;
        var pageRows = page.Take(pageSize).ToList();
        var ids = pageRows.Select(p => p.VenueID).ToList();

        var venues = await this.context.Set<Venue>()
            .AsNoTracking()
            .IncludeVenueDetails()
            .Include(v => v.Address)
            .Where(v => ids.Contains(v.VenueID))
            .ToListAsync()
            .ConfigureAwait(false);
        var byId = venues.ToDictionary(v => v.VenueID);

        return this.Ok(new VenueListPageDto
        {
            Items = pageRows.Where(p => byId.ContainsKey(p.VenueID)).Select(p => ToItem(byId[p.VenueID])).ToList(),
            NextCursor = hasMore ? EncodeCursor(pageRows[^1].SortKey, pageRows[^1].VenueID) : null,
        });
    }

    private static VenueListItemDto ToItem(Venue venue)
    {
        var description = VenueController.CoalescePlaintext(venue.Description_Plaintext, venue.Description);
        if (description != null && description.Length > DescriptionPreviewLength)
        {
            description = description[..DescriptionPreviewLength].TrimEnd() + "…";
        }

        return new VenueListItemDto
        {
            VenueID = venue.VenueID,
            Name = VenueController.CoalescePlaintext(venue.Name_Plaintext, venue.Name) ?? string.Empty,
            Slug = VenueController.ToSlug(venue.Name),
            Description = description,
            City = venue.Address?.City,
            State = venue.Address?.State,
            LogoUrl = venue.LogoPic?.URL,
            Categories = venue.CategoryLinks
                .Where(link => link.Category != null)
                .Select(link => link.Category!)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .Select(c => new VenueListLabelDto { Name = c.Name, Slug = c.Slug })
                .ToList(),
            Tags = venue.TagLinks
                .Where(link => link.Tag != null)
                .Select(link => link.Tag!.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            Regions = venue.RegionLinks
                .Where(link => link.Region != null)
                .Select(link => link.Region!)
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .Select(r => new VenueListLabelDto { Name = r.Name, Slug = r.Slug })
                .ToList(),
        };
    }

    // Trimmed, lowercase and not too long, so a huge value can't make an expensive query.
    private static string Limit(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLower(CultureInfo.InvariantCulture);
        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    private static List<string> Values(string[]? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(Limit)
            .Where(v => v.Length > 0)
            .Distinct()
            .Take(MaxFilterValues)
            .ToList();
    }

    // Only digits and a hyphen are kept, so the value is safe to match as a prefix.
    private static string ZipPrefix(string? zip)
    {
        var digits = new string((zip ?? string.Empty).Where(c => char.IsAsciiDigit(c) || c == '-').ToArray());
        return digits.Length > 10 ? digits[..10] : digits;
    }

    // "GA" and "Georgia" both match venues saved either way, because addresses hold the state as typed.
    private static List<string> StateValues(string state)
    {
        var values = new List<string> { state };
        var code = state.ToUpper(CultureInfo.InvariantCulture);
        if (UsStates.Names.TryGetValue(code, out var name))
        {
            values.Add(name.ToLower(CultureInfo.InvariantCulture));
        }
        else
        {
            var match = UsStates.Names.FirstOrDefault(pair => string.Equals(pair.Value, state, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
            {
                values.Add(match.Key.ToLower(CultureInfo.InvariantCulture));
            }
        }

        return values.Distinct().ToList();
    }

    // The cursor is the sort key and id of the last venue on the page, packed into one opaque, URL-safe value.
    private static string EncodeCursor(string sortKey, int venueId)
    {
        var json = JsonSerializer.Serialize(new CursorPayload { K = sortKey, I = venueId });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static bool TryDecodeCursor(string cursor, out string sortKey, out int venueId)
    {
        sortKey = string.Empty;
        venueId = 0;
        if (cursor.Length > MaxCursorLength)
        {
            return false;
        }

        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            var payload = JsonSerializer.Deserialize<CursorPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            if (payload?.K == null || payload.I < 0)
            {
                return false;
            }

            sortKey = payload.K;
            venueId = payload.I;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private sealed class CursorPayload
    {
        public string? K { get; set; }

        public int I { get; set; }
    }
}

public class VenueListPageDto
{
    public List<VenueListItemDto> Items { get; set; } = new();

    /// <summary>Pass this as <c>cursor</c> to get the next page. It is null on the last page.</summary>
    public string? NextCursor { get; set; }
}

public class VenueListItemDto
{
    public int VenueID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? LogoUrl { get; set; }

    public List<VenueListLabelDto> Categories { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public List<VenueListLabelDto> Regions { get; set; } = new();
}

public class VenueListLabelDto
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
