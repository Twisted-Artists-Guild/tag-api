// <copyright file="VenueImportController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// Staff-only bulk import used to seed the venue directory (manually or from a crawler). Each row is a name and an
/// address. Rows that match an existing venue by normalized name and street address update it instead of creating a
/// duplicate, so a batch can be re-run safely. New venues are published and have no owner.
/// </summary>
[ApiController]
[Route("api/venue/admin/import")]
[Authorize]
public class VenueImportController : ControllerBase
{
    public const int MaxRowsPerRequest = 200;

    private const int MaxRequestBytes = 512 * 1024;
    private const int NameMaxLength = 1000;
    private const int AddressLineMaxLength = 255;
    private const int ShortTextMaxLength = 100;
    private const int ZipMaxLength = 20;

    public const string StatusCreated = "created";
    public const string StatusUpdated = "updated";
    public const string StatusUnchanged = "unchanged";
    public const string StatusSkipped = "skipped";

    // Markup and control characters never belong in imported names or addresses; this also keeps crawled text from
    // carrying HTML into pages that render venue names.
    private static readonly Regex UnsafeTextRegex = new(@"[<>\p{Cc}]", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> AddressAbbreviations = new(StringComparer.Ordinal)
    {
        ["street"] = "st", ["avenue"] = "ave", ["road"] = "rd", ["boulevard"] = "blvd", ["drive"] = "dr",
        ["lane"] = "ln", ["court"] = "ct", ["place"] = "pl", ["highway"] = "hwy", ["parkway"] = "pkwy",
        ["suite"] = "ste", ["north"] = "n", ["south"] = "s", ["east"] = "e", ["west"] = "w",
        ["northeast"] = "ne", ["northwest"] = "nw", ["southeast"] = "se", ["southwest"] = "sw",
    };

    private readonly TAGDBContext context;
    private readonly ILogger<VenueImportController> logger;

    public VenueImportController(TAGDBContext context, ILogger<VenueImportController> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    /// <summary>
    /// Creates or updates venues from a batch of up to <see cref="MaxRowsPerRequest"/> rows. Invalid rows are
    /// reported in the response and never fail the rest of the batch.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VenueImportResponse>> ImportVenues([FromBody] VenueImportRequest request)
    {
        // Staff status comes from the token; no caller-supplied identity is trusted.
        var userId = VenueAccess.GetUserId(this.User);
        if (userId == null)
        {
            return this.Unauthorized();
        }

        if (!await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false))
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can import venues." });
        }

        if (request?.Venues == null || request.Venues.Count == 0)
        {
            return this.BadRequest(new { message = "Venues must contain at least one row." });
        }

        if (request.Venues.Count > MaxRowsPerRequest)
        {
            return this.BadRequest(new { message = $"A batch can contain at most {MaxRowsPerRequest} rows; send the rest in another request." });
        }

        var rows = new List<VenueImportRowResult>(request.Venues.Count);
        var parsed = new List<(int Index, ParsedRow Row)>();
        for (var i = 0; i < request.Venues.Count; i++)
        {
            var error = TryParse(request.Venues[i], out var row);
            if (error != null)
            {
                rows.Add(new VenueImportRowResult { Index = i, Name = request.Venues[i]?.Name?.Trim(), Status = StatusSkipped, Message = error });
                continue;
            }

            parsed.Add((i, row!));
        }

        var existing = await this.LoadExistingAsync(parsed.Select(p => p.Row.City).ToList()).ConfigureAwait(false);
        var slugOwners = existing.SlugOwners;
        var known = existing.ByKey;

        // Rows that point at a venue created in this batch get its id once the batch is saved.
        var newVenueRows = new List<(VenueImportRowResult Result, Venue Venue)>();
        foreach (var (index, row) in parsed)
        {
            var result = new VenueImportRowResult { Index = index, Name = row.Name };
            rows.Add(result);

            if (known.TryGetValue(row.Key, out var match))
            {
                this.ApplyToExisting(match, row, existing.OwnedVenueIds, result);
                if (match.IsNew)
                {
                    newVenueRows.Add((result, match.Venue));
                }

                continue;
            }

            var slug = VenueController.ToSlug(row.Name);
            if (slug.Length == 0)
            {
                result.Status = StatusSkipped;
                result.Message = "Name must contain at least one letter or digit.";
                continue;
            }

            if (slugOwners.TryGetValue(slug, out var otherName))
            {
                result.Status = StatusSkipped;
                result.Message = $"Another venue ('{otherName}') already uses the same page URL as this name. Make the name distinct, for example by adding the city or neighborhood.";
                continue;
            }

            var venue = new Venue
            {
                Name = row.Name,
                Name_Plaintext = row.Name,
                IsPublished = true,
                IsModerationBlocked = false,
                EventSourceUrl = row.EventSourceUrl,
                Address = new Address
                {
                    AddressLine1 = row.AddressLine1,
                    AddressLine2 = row.AddressLine2,
                    City = row.City,
                    State = row.State,
                    ZipCode = row.ZipCode ?? string.Empty,
                    Country = row.Country ?? string.Empty,
                    Region = row.RegionHint ?? string.Empty,
                    OperationHours = string.Empty,
                },
            };

            this.context.Set<Venue>().Add(venue);
            newVenueRows.Add((result, venue));
            result.Status = StatusCreated;
            slugOwners[slug] = row.Name;
            known[row.Key] = new KnownVenue { Venue = venue, IsNew = true, FirstIndex = index };
        }

        // New venues, their addresses and any updates are saved together, so a failure cannot leave a half-applied batch.
        if (this.context.ChangeTracker.HasChanges())
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        foreach (var (result, venue) in newVenueRows)
        {
            result.VenueID = venue.VenueID;
        }

        rows = rows.OrderBy(r => r.Index).ToList();
        var response = new VenueImportResponse
        {
            Total = rows.Count,
            Created = rows.Count(r => r.Status == StatusCreated),
            Updated = rows.Count(r => r.Status == StatusUpdated),
            Unchanged = rows.Count(r => r.Status == StatusUnchanged),
            Skipped = rows.Count(r => r.Status == StatusSkipped),
            Rows = rows,
        };

        await this.TryWriteAuditLogAsync(userId.Value, response).ConfigureAwait(false);
        return this.Ok(response);
    }

    // Applies a matching row to a venue that already exists (or was created earlier in this batch).
    private void ApplyToExisting(KnownVenue match, ParsedRow row, ISet<int> ownedVenueIds, VenueImportRowResult result)
    {
        var venue = match.Venue;
        result.VenueID = match.IsNew ? null : venue.VenueID;
        result.Message = match.IsNew ? $"Same venue as row {match.FirstIndex} in this batch." : null;

        // Owner-managed and blocked venues are never changed by an import.
        if (!match.IsNew && (venue.IsModerationBlocked || ownedVenueIds.Contains(venue.VenueID)))
        {
            result.Status = StatusUnchanged;
            result.Message = venue.IsModerationBlocked
                ? "Venue is blocked by moderation; not modified."
                : "Venue is managed by an owner; not modified.";
            return;
        }

        var changed = false;
        if (row.EventSourceUrl != null && !string.Equals(venue.EventSourceUrl, row.EventSourceUrl, StringComparison.Ordinal))
        {
            venue.EventSourceUrl = row.EventSourceUrl;
            changed = true;
        }

        if (row.RegionHint != null && venue.Address != null && !string.Equals(venue.Address.Region, row.RegionHint, StringComparison.Ordinal))
        {
            venue.Address.Region = row.RegionHint;
            changed = true;
        }

        result.Status = changed ? StatusUpdated : StatusUnchanged;
    }

    private async Task<ExistingVenues> LoadExistingAsync(IReadOnlyCollection<string> cities)
    {
        // A page URL comes from the name alone, so every venue name counts when checking for a clash.
        var names = await this.context.Set<Venue>()
            .AsNoTracking()
            .Select(v => new { v.Name })
            .ToListAsync()
            .ConfigureAwait(false);

        var slugOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var venueName in names)
        {
            var slug = VenueController.ToSlug(venueName.Name);
            if (slug.Length > 0)
            {
                slugOwners.TryAdd(slug, venueName.Name);
            }
        }

        // Only venues in the batch's cities can match a row, so only those are loaded for matching.
        var lowerCities = cities.Select(c => c.ToLowerInvariant()).Distinct().ToList();
        var candidates = await this.context.Set<Venue>()
            .Include(v => v.Address)
            .Where(v => v.Address != null && v.Address.City != null && lowerCities.Contains(v.Address.City.ToLower()))
            .ToListAsync()
            .ConfigureAwait(false);

        var byKey = new Dictionary<string, KnownVenue>(StringComparer.Ordinal);
        foreach (var venue in candidates)
        {
            var key = BuildKey(VenueController.CoalescePlaintext(venue.Name_Plaintext, venue.Name), venue.Address!.AddressLine1, venue.Address.City, venue.Address.State);
            byKey.TryAdd(key, new KnownVenue { Venue = venue });
        }

        var candidateIds = candidates.Select(v => v.VenueID).ToList();
        var owned = await this.context.Set<Linker_UserToVenue>()
            .AsNoTracking()
            .Where(l => candidateIds.Contains(l.VenueID))
            .Select(l => l.VenueID)
            .Distinct()
            .ToListAsync()
            .ConfigureAwait(false);

        return new ExistingVenues(slugOwners, byKey, owned.ToHashSet());
    }

    // Written after the batch is saved so the entry can list the venue ids it created and updated.
    private async Task TryWriteAuditLogAsync(int actingUserId, VenueImportResponse response)
    {
        try
        {
            string Ids(string status) => string.Join(",", response.Rows.Where(r => r.Status == status && r.VenueID.HasValue).Select(r => r.VenueID!.Value));

            this.context.Set<Log>().Add(new Log
            {
                ShortText = "Venue import",
                Tags = "scope=audit;entity=venue;event=import;operation=import;result=success;channel=db",
                Critical = false,
                LoggedData = $"actingUserId={actingUserId};rows={response.Total};created={response.Created};updated={response.Updated};unchanged={response.Unchanged};skipped={response.Skipped};createdVenueIds={Ids(StatusCreated)};updatedVenueIds={Ids(StatusUpdated)}",
                UserID = actingUserId,
                LogTimestamp = DateTime.UtcNow,
            });

            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Venue import audit log could not be written for user {UserId}.", actingUserId);
        }
    }

    // Returns an error message, or null and the trimmed, validated row.
    private static string? TryParse(VenueImportItem? item, out ParsedRow? row)
    {
        row = null;
        if (item == null)
        {
            return "Row is empty.";
        }

        var name = Clean(item.Name);
        var line1 = Clean(item.AddressLine1);
        var line2 = Clean(item.AddressLine2);
        var city = Clean(item.City);
        var state = Clean(item.State);
        var zip = Clean(item.ZipCode);
        var country = Clean(item.Country);
        var region = Clean(item.RegionHint);
        var category = Clean(item.CategoryHint);

        if (name == null) { return "Name is required."; }
        if (line1 == null) { return "AddressLine1 is required."; }
        if (city == null) { return "City is required."; }
        if (state == null) { return "State is required."; }

        var lengthError = CheckLength("Name", name, NameMaxLength)
            ?? CheckLength("AddressLine1", line1, AddressLineMaxLength)
            ?? CheckLength("AddressLine2", line2, AddressLineMaxLength)
            ?? CheckLength("City", city, ShortTextMaxLength)
            ?? CheckLength("State", state, ShortTextMaxLength)
            ?? CheckLength("ZipCode", zip, ZipMaxLength)
            ?? CheckLength("Country", country, ShortTextMaxLength)
            ?? CheckLength("RegionHint", region, ShortTextMaxLength)
            ?? CheckLength("CategoryHint", category, ShortTextMaxLength);
        if (lengthError != null)
        {
            return lengthError;
        }

        foreach (var (field, value) in new[] { ("Name", name), ("AddressLine1", line1), ("AddressLine2", line2), ("City", city), ("State", state), ("ZipCode", zip), ("Country", country), ("RegionHint", region), ("CategoryHint", category) })
        {
            if (value != null && UnsafeTextRegex.IsMatch(value))
            {
                return $"{field} must not contain markup characters (< or >) or control characters.";
            }
        }

        string? eventSourceUrl = null;
        var rawUrl = item.EventSourceUrl?.Trim();
        if (!string.IsNullOrEmpty(rawUrl))
        {
            if (rawUrl.Length > Venue.EventSourceUrlMaxLength)
            {
                return $"EventSourceUrl must be at most {Venue.EventSourceUrlMaxLength} characters.";
            }

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                return "EventSourceUrl must be an absolute http or https URL without embedded credentials.";
            }

            eventSourceUrl = rawUrl;
        }

        row = new ParsedRow
        {
            Name = name,
            AddressLine1 = line1,
            AddressLine2 = line2,
            City = city,
            State = state,
            ZipCode = zip,
            Country = country,
            EventSourceUrl = eventSourceUrl,
            RegionHint = region,
            Key = BuildKey(name, line1, city, state),
        };
        return null;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? CheckLength(string field, string? value, int max)
    {
        return value != null && value.Length > max ? $"{field} must be at most {max} characters." : null;
    }

    // The same venue written two ways (case, punctuation, "Street" or "St.") produces the same key.
    // The zip code is left out on purpose because crawled data often lacks it.
    internal static string BuildKey(string? name, string? street, string? city, string? state)
    {
        return string.Join("|", Compact(name), Compact(NormalizeStreet(street)), Compact(city), Compact(state));
    }

    private static string NormalizeStreet(string? street)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            return string.Empty;
        }

        var tokens = Regex.Split(street.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 0)
            .Select(t => AddressAbbreviations.TryGetValue(t, out var abbreviation) ? abbreviation : t);
        return string.Join(" ", tokens);
    }

    private static string Compact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch == '&')
            {
                builder.Append("and");
            }
            else if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private sealed class ParsedRow
    {
        public string Name { get; init; } = string.Empty;

        public string AddressLine1 { get; init; } = string.Empty;

        public string? AddressLine2 { get; init; }

        public string City { get; init; } = string.Empty;

        public string State { get; init; } = string.Empty;

        public string? ZipCode { get; init; }

        public string? Country { get; init; }

        public string? EventSourceUrl { get; init; }

        public string? RegionHint { get; init; }

        public string Key { get; init; } = string.Empty;
    }

    private sealed class KnownVenue
    {
        public Venue Venue { get; init; } = null!;

        public bool IsNew { get; init; }

        public int FirstIndex { get; init; }
    }

    private sealed record ExistingVenues(Dictionary<string, string> SlugOwners, Dictionary<string, KnownVenue> ByKey, HashSet<int> OwnedVenueIds);
}

/// <summary>A batch of venues to import.</summary>
public class VenueImportRequest
{
    public List<VenueImportItem?>? Venues { get; set; }
}

/// <summary>
/// One venue to import. Everything is nullable on purpose so a bad row is reported on its own instead of failing the batch.
/// CategoryHint is validated but not stored yet, because venue categories do not exist yet.
/// </summary>
public class VenueImportItem
{
    public string? Name { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? ZipCode { get; set; }

    public string? Country { get; set; }

    public string? EventSourceUrl { get; set; }

    public string? CategoryHint { get; set; }

    public string? RegionHint { get; set; }
}

public class VenueImportResponse
{
    public int Total { get; set; }

    public int Created { get; set; }

    public int Updated { get; set; }

    public int Unchanged { get; set; }

    public int Skipped { get; set; }

    public List<VenueImportRowResult> Rows { get; set; } = new();
}

public class VenueImportRowResult
{
    // Zero-based position of the row in the request.
    public int Index { get; set; }

    public string? Name { get; set; }

    // created, updated, unchanged or skipped.
    public string Status { get; set; } = string.Empty;

    public int? VenueID { get; set; }

    public string? Message { get; set; }
}
