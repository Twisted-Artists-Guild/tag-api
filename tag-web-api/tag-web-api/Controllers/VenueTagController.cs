// <copyright file="VenueTagController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// Free-form amenity tags for venues. Owners and staff choose the tags of a venue, anyone can ask for suggestions, and
/// staff can list, merge and remove tags. Tags are normalized (trimmed, lowercase, single spaces) and validated, so a
/// tag can never carry markup.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VenueTagController : ControllerBase
{
    private const int MaxRequestedTags = 100;
    private const int MaxSuggestions = 20;
    private const int DefaultSuggestions = 10;
    private const int MaxAdminPageSize = 100;
    private const int DefaultAdminPageSize = 50;
    private const int MaxSearchLength = 50;

    private readonly TAGDBContext context;

    public VenueTagController(TAGDBContext context)
    {
        this.context = context;
    }

    /// <summary>The most used tags that start with the typed text, for the tag input. An empty prefix returns the most used tags.</summary>
    [HttpGet("suggest")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<VenueTagSuggestionDto>>> SuggestTags([FromQuery] string? q = null, [FromQuery] int limit = DefaultSuggestions)
    {
        var prefix = (q ?? string.Empty).Trim().ToLowerInvariant();
        if (prefix.Length > VenueTag.NameMaxLength)
        {
            return this.Ok(Array.Empty<VenueTagSuggestionDto>());
        }

        var counts = this.context.Set<Linker_VenueToTag>()
            .GroupBy(link => link.VenueTagID)
            .Select(group => new { TagId = group.Key, Count = group.Count() });

        var suggestions = await (
            from tag in this.context.Set<VenueTag>().AsNoTracking()
            join usage in counts on tag.VenueTagID equals usage.TagId
            where tag.Name.StartsWith(prefix)
            orderby usage.Count descending, tag.Name
            select new VenueTagSuggestionDto { Name = tag.Name, VenueCount = usage.Count })
            .Take(Math.Clamp(limit, 1, MaxSuggestions))
            .ToListAsync()
            .ConfigureAwait(false);

        return this.Ok(suggestions);
    }

    /// <summary>Staff only. Tags with how many venues use them, in pages (afterId is the cursor). Optional name search.</summary>
    [HttpGet("admin/all")]
    public async Task<ActionResult<VenueTagAdminPageDto>> GetAllTags([FromQuery] string? search = null, [FromQuery] int afterId = 0, [FromQuery] int limit = DefaultAdminPageSize)
    {
        var (_, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var pageSize = Math.Clamp(limit, 1, MaxAdminPageSize);
        var query = this.context.Set<VenueTag>().AsNoTracking().Where(t => t.VenueTagID > Math.Max(afterId, 0));

        var needle = (search ?? string.Empty).Trim().ToLowerInvariant();
        if (needle.Length > MaxSearchLength)
        {
            needle = needle[..MaxSearchLength];
        }

        if (needle.Length > 0)
        {
            query = query.Where(t => t.Name.Contains(needle));
        }

        var rows = await query.OrderBy(t => t.VenueTagID).Take(pageSize + 1).ToListAsync().ConfigureAwait(false);
        var page = rows.Take(pageSize).ToList();
        var ids = page.Select(t => t.VenueTagID).ToList();

        var counts = await this.context.Set<Linker_VenueToTag>()
            .AsNoTracking()
            .Where(link => ids.Contains(link.VenueTagID))
            .GroupBy(link => link.VenueTagID)
            .Select(group => new { TagId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.TagId, x => x.Count)
            .ConfigureAwait(false);

        return this.Ok(new VenueTagAdminPageDto
        {
            Items = page.Select(t => new VenueTagAdminDto { VenueTagID = t.VenueTagID, Name = t.Name, VenueCount = counts.TryGetValue(t.VenueTagID, out var count) ? count : 0 }).ToList(),
            NextAfterId = rows.Count > pageSize ? page[^1].VenueTagID : null,
        });
    }

    /// <summary>Staff only. Removes a tag from every venue and deletes it.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTag(int id)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var tag = await this.context.Set<VenueTag>().FirstOrDefaultAsync(t => t.VenueTagID == id).ConfigureAwait(false);
        if (tag == null)
        {
            return this.NotFound();
        }

        var links = await this.context.Set<Linker_VenueToTag>().Where(l => l.VenueTagID == id).ToListAsync().ConfigureAwait(false);
        this.context.Set<Linker_VenueToTag>().RemoveRange(links);
        this.context.Set<VenueTag>().Remove(tag);
        this.context.Set<Log>().Add(BuildAuditLog("Venue tag removed", "remove", $"tag={tag.Name};venues={links.Count}", staffId!.Value));
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    /// <summary>Staff only. Moves every venue from this tag to the target tag, then deletes this tag. Venues that already have the target keep one copy.</summary>
    [HttpPost("{id:int}/merge")]
    public async Task<ActionResult<VenueTagAdminDto>> MergeTag(int id, [FromBody] VenueTagMergeRequest request)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        if (request == null || request.TargetTagId <= 0)
        {
            return this.BadRequest(new { message = "TargetTagId is required." });
        }

        if (request.TargetTagId == id)
        {
            return this.BadRequest(new { message = "A tag can't be merged into itself." });
        }

        var source = await this.context.Set<VenueTag>().FirstOrDefaultAsync(t => t.VenueTagID == id).ConfigureAwait(false);
        var target = await this.context.Set<VenueTag>().FirstOrDefaultAsync(t => t.VenueTagID == request.TargetTagId).ConfigureAwait(false);
        if (source == null || target == null)
        {
            return this.NotFound();
        }

        var sourceLinks = await this.context.Set<Linker_VenueToTag>().Where(l => l.VenueTagID == id).ToListAsync().ConfigureAwait(false);
        var alreadyTagged = (await this.context.Set<Linker_VenueToTag>()
            .Where(l => l.VenueTagID == target.VenueTagID)
            .Select(l => l.VenueID)
            .ToListAsync()
            .ConfigureAwait(false)).ToHashSet();

        var moved = 0;
        foreach (var link in sourceLinks)
        {
            if (alreadyTagged.Contains(link.VenueID))
            {
                this.context.Set<Linker_VenueToTag>().Remove(link);
            }
            else
            {
                link.VenueTagID = target.VenueTagID;
                moved++;
            }
        }

        this.context.Set<VenueTag>().Remove(source);
        this.context.Set<Log>().Add(BuildAuditLog("Venue tags merged", "merge", $"from={source.Name};to={target.Name};moved={moved};duplicates={sourceLinks.Count - moved}", staffId!.Value));
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        var count = await this.context.Set<Linker_VenueToTag>().CountAsync(l => l.VenueTagID == target.VenueTagID).ConfigureAwait(false);
        return this.Ok(new VenueTagAdminDto { VenueTagID = target.VenueTagID, Name = target.Name, VenueCount = count });
    }

    /// <summary>Replaces the tags of a venue. Owners and staff only. Tags are normalized and de-duplicated; new tags are created.</summary>
    [HttpPut("~/api/Venue/{venueId:int}/tags")]
    public async Task<ActionResult<IEnumerable<string>>> SetVenueTags(int venueId, [FromBody] VenueTagsRequest request)
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
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to change the tags of this venue." });
        }

        if (request?.Tags == null)
        {
            return this.BadRequest(new { message = "Tags is required (use an empty list to clear the tags)." });
        }

        if (request.Tags.Count > MaxRequestedTags)
        {
            return this.BadRequest(new { message = $"Too many tags. A venue can have at most {VenueTag.MaxTagsPerVenue}." });
        }

        var names = new List<string>();
        foreach (var raw in request.Tags)
        {
            if (!VenueTag.TryNormalize(raw, out var name, out var error))
            {
                return this.BadRequest(new { message = error });
            }

            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        if (names.Count > VenueTag.MaxTagsPerVenue)
        {
            return this.BadRequest(new { message = $"A venue can have at most {VenueTag.MaxTagsPerVenue} tags." });
        }

        try
        {
            await this.ApplyTagsAsync(venueId, userId.Value, names).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Someone created the same new tag at the same moment. Starting over finds the tag they created.
            this.context.ChangeTracker.Clear();
            await this.ApplyTagsAsync(venueId, userId.Value, names).ConfigureAwait(false);
        }

        return this.Ok(names.OrderBy(n => n, StringComparer.Ordinal));
    }

    private async Task ApplyTagsAsync(int venueId, int actingUserId, List<string> names)
    {
        var links = await this.context.Set<Linker_VenueToTag>()
            .Include(l => l.Tag)
            .Where(l => l.VenueID == venueId)
            .ToListAsync()
            .ConfigureAwait(false);

        var currentNames = links.Select(l => l.Tag!.Name).ToHashSet();
        var removed = links.Where(l => !names.Contains(l.Tag!.Name)).ToList();
        var addedNames = names.Where(n => !currentNames.Contains(n)).ToList();
        if (removed.Count == 0 && addedNames.Count == 0)
        {
            return;
        }

        var knownTags = await this.context.Set<VenueTag>().Where(t => addedNames.Contains(t.Name)).ToListAsync().ConfigureAwait(false);
        this.context.Set<Linker_VenueToTag>().RemoveRange(removed);
        foreach (var name in addedNames)
        {
            var tag = knownTags.FirstOrDefault(t => t.Name == name) ?? new VenueTag { Name = name };
            this.context.Set<Linker_VenueToTag>().Add(new Linker_VenueToTag { VenueID = venueId, Tag = tag });
        }

        this.context.Set<Log>().Add(new Log
        {
            ShortText = "Venue tags changed",
            Tags = "scope=audit;entity=venue;event=tag;operation=set;result=success;channel=db",
            Critical = false,
            LoggedData = $"venueId={venueId};actingUserId={actingUserId};added={string.Join(",", addedNames)};removed={string.Join(",", removed.Select(l => l.Tag!.Name))}",
            UserID = actingUserId,
            LogTimestamp = DateTime.UtcNow,
        });

        // The links, any new tags and the audit entry are saved together.
        await this.context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static Log BuildAuditLog(string shortText, string operation, string details, int actingUserId)
    {
        return new Log
        {
            ShortText = shortText,
            Tags = $"scope=audit;entity=venueTag;event=tag;operation={operation};result=success;channel=db",
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
            return (null, this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can manage venue tags." }));
        }

        return (userId, null);
    }
}

public class VenueTagsRequest
{
    public List<string?>? Tags { get; set; }
}

public class VenueTagMergeRequest
{
    public int TargetTagId { get; set; }
}

public class VenueTagSuggestionDto
{
    public string Name { get; set; } = string.Empty;

    public int VenueCount { get; set; }
}

public class VenueTagAdminDto
{
    public int VenueTagID { get; set; }

    public string Name { get; set; } = string.Empty;

    public int VenueCount { get; set; }
}

public class VenueTagAdminPageDto
{
    public List<VenueTagAdminDto> Items { get; set; } = new();

    public int? NextAfterId { get; set; }
}
