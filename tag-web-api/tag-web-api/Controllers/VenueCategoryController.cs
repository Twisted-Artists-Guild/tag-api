// <copyright file="VenueCategoryController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

/// <summary>
/// The staff-managed list of venue categories, and the categories of each venue. Anyone can read the active list.
/// Only staff can create, rename or deactivate categories. Owners and staff choose the categories of a venue.
/// Categories are never deleted, so deactivating one never breaks the venues that already use it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VenueCategoryController : ControllerBase
{
    private const int SortOrderStep = 10;

    // Markup and control characters never belong in a category name.
    private static readonly Regex UnsafeTextRegex = new(@"[<>\p{Cc}]", RegexOptions.Compiled);

    private readonly TAGDBContext context;

    public VenueCategoryController(TAGDBContext context)
    {
        this.context = context;
    }

    /// <summary>The categories a venue can currently be given, in display order.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<VenueCategoryDto>>> GetActiveCategories()
    {
        var categories = await this.context.Set<VenueCategory>()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync()
            .ConfigureAwait(false);

        return this.Ok(categories.Select(ToDto));
    }

    /// <summary>Staff only. Every category, active or not, with how many venues use it.</summary>
    [HttpGet("admin/all")]
    public async Task<ActionResult<IEnumerable<VenueCategoryAdminDto>>> GetAllCategories()
    {
        var (_, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var categories = await this.context.Set<VenueCategory>()
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync()
            .ConfigureAwait(false);

        var counts = await this.context.Set<Linker_VenueToCategory>()
            .AsNoTracking()
            .GroupBy(l => l.VenueCategoryID)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count)
            .ConfigureAwait(false);

        return this.Ok(categories.Select(c => ToAdminDto(c, counts.TryGetValue(c.VenueCategoryID, out var count) ? count : 0)));
    }

    [HttpPost]
    public async Task<ActionResult<VenueCategoryAdminDto>> CreateCategory([FromBody] VenueCategoryRequest request)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var name = ValidateName(request?.Name, out var nameError);
        if (name == null)
        {
            return this.BadRequest(new { message = nameError });
        }

        var slug = VenueController.ToSlug(name);
        if (slug.Length == 0)
        {
            return this.BadRequest(new { message = "Name must contain at least one letter or digit." });
        }

        var existing = await this.context.Set<VenueCategory>().AsNoTracking().ToListAsync().ConfigureAwait(false);
        if (existing.Any(c => c.Slug == slug || string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return this.Conflict(new { message = "A category with this name already exists." });
        }

        var category = new VenueCategory
        {
            Name = name,
            Slug = slug,
            IsActive = true,
            SortOrder = request!.SortOrder ?? (existing.Count == 0 ? SortOrderStep : existing.Max(c => c.SortOrder) + SortOrderStep),
        };

        // The category and its audit entry are saved together, so a change never exists without its audit entry.
        this.context.Set<VenueCategory>().Add(category);
        this.context.Set<Log>().Add(BuildAuditLog("Venue category created", "create", $"slug={slug};name={name}", staffId!.Value));
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.Created($"api/VenueCategory/{category.VenueCategoryID}", ToAdminDto(category, 0));
    }

    /// <summary>Renames a category and optionally changes its position. The slug stays the same so shared filter links keep working.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VenueCategoryAdminDto>> UpdateCategory(int id, [FromBody] VenueCategoryRequest request)
    {
        var (staffId, error) = await this.RequireStaffAsync().ConfigureAwait(false);
        if (error != null)
        {
            return error;
        }

        var category = await this.context.Set<VenueCategory>().FirstOrDefaultAsync(c => c.VenueCategoryID == id).ConfigureAwait(false);
        if (category == null)
        {
            return this.NotFound();
        }

        var name = ValidateName(request?.Name, out var nameError);
        if (name == null)
        {
            return this.BadRequest(new { message = nameError });
        }

        var nameTaken = await this.context.Set<VenueCategory>()
            .AnyAsync(c => c.VenueCategoryID != id && c.Name.ToLower() == name.ToLower())
            .ConfigureAwait(false);
        if (nameTaken)
        {
            return this.Conflict(new { message = "A category with this name already exists." });
        }

        var previousName = category.Name;
        category.Name = name;
        if (request!.SortOrder.HasValue)
        {
            category.SortOrder = request.SortOrder.Value;
        }

        this.context.Set<Log>().Add(BuildAuditLog("Venue category updated", "update", $"slug={category.Slug};from={previousName};to={name}", staffId!.Value));
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        var count = await this.context.Set<Linker_VenueToCategory>().CountAsync(l => l.VenueCategoryID == id).ConfigureAwait(false);
        return this.Ok(ToAdminDto(category, count));
    }

    /// <summary>Deactivates or reactivates a category. Venues keep a deactivated category; it just can't be chosen again.</summary>
    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<VenueCategoryAdminDto>> SetCategoryActive(int id, [FromBody] VenueCategoryActiveRequest request)
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

        var category = await this.context.Set<VenueCategory>().FirstOrDefaultAsync(c => c.VenueCategoryID == id).ConfigureAwait(false);
        if (category == null)
        {
            return this.NotFound();
        }

        if (category.IsActive != request.IsActive)
        {
            category.IsActive = request.IsActive;
            this.context.Set<Log>().Add(BuildAuditLog(
                request.IsActive ? "Venue category activated" : "Venue category deactivated",
                request.IsActive ? "activate" : "deactivate",
                $"slug={category.Slug};name={category.Name}",
                staffId!.Value));
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        var count = await this.context.Set<Linker_VenueToCategory>().CountAsync(l => l.VenueCategoryID == id).ConfigureAwait(false);
        return this.Ok(ToAdminDto(category, count));
    }

    /// <summary>
    /// Replaces the categories of a venue. Owners and staff only. Categories the venue already has can stay even when
    /// they were deactivated, but a deactivated category can't be newly added.
    /// </summary>
    [HttpPut("~/api/Venue/{venueId:int}/categories")]
    public async Task<ActionResult<IEnumerable<VenueCategoryDto>>> SetVenueCategories(int venueId, [FromBody] VenueCategoriesRequest request)
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
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to change the categories of this venue." });
        }

        if (request?.CategoryIds == null)
        {
            return this.BadRequest(new { message = "CategoryIds is required (use an empty list to clear the categories)." });
        }

        var wanted = request.CategoryIds.Distinct().ToList();
        if (wanted.Any(id => id <= 0))
        {
            return this.BadRequest(new { message = "Category ids must be positive numbers." });
        }

        var categories = await this.context.Set<VenueCategory>()
            .Where(c => wanted.Contains(c.VenueCategoryID))
            .ToListAsync()
            .ConfigureAwait(false);

        var unknown = wanted.Except(categories.Select(c => c.VenueCategoryID)).ToList();
        if (unknown.Count > 0)
        {
            return this.BadRequest(new { message = $"Unknown category id: {string.Join(", ", unknown)}." });
        }

        var links = await this.context.Set<Linker_VenueToCategory>()
            .Where(l => l.VenueID == venueId)
            .ToListAsync()
            .ConfigureAwait(false);
        var currentIds = links.Select(l => l.VenueCategoryID).ToHashSet();

        var unavailable = categories.FirstOrDefault(c => !c.IsActive && !currentIds.Contains(c.VenueCategoryID));
        if (unavailable != null)
        {
            return this.BadRequest(new { message = $"The category '{unavailable.Name}' is no longer available." });
        }

        var removed = links.Where(l => !wanted.Contains(l.VenueCategoryID)).ToList();
        var added = wanted.Where(id => !currentIds.Contains(id)).ToList();
        if (removed.Count > 0 || added.Count > 0)
        {
            this.context.Set<Linker_VenueToCategory>().RemoveRange(removed);
            foreach (var categoryId in added)
            {
                this.context.Set<Linker_VenueToCategory>().Add(new Linker_VenueToCategory { VenueID = venueId, VenueCategoryID = categoryId });
            }

            this.context.Set<Log>().Add(new Log
            {
                ShortText = "Venue categories changed",
                Tags = "scope=audit;entity=venue;event=category;operation=set;result=success;channel=db",
                Critical = false,
                LoggedData = $"venueId={venueId};actingUserId={userId.Value};added={string.Join(",", added)};removed={string.Join(",", removed.Select(l => l.VenueCategoryID))}",
                UserID = userId.Value,
                LogTimestamp = DateTime.UtcNow,
            });
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        return this.Ok(categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(ToDto));
    }

    private static string? ValidateName(string? rawName, out string? error)
    {
        error = null;
        var name = rawName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            error = "Name is required.";
            return null;
        }

        if (name.Length > VenueCategory.NameMaxLength)
        {
            error = $"Name must be at most {VenueCategory.NameMaxLength} characters.";
            return null;
        }

        if (UnsafeTextRegex.IsMatch(name))
        {
            error = "Name must not contain markup characters (< or >) or control characters.";
            return null;
        }

        return name;
    }

    private static VenueCategoryDto ToDto(VenueCategory category) => new()
    {
        VenueCategoryID = category.VenueCategoryID,
        Name = category.Name,
        Slug = category.Slug,
        IsActive = category.IsActive,
    };

    private static VenueCategoryAdminDto ToAdminDto(VenueCategory category, int venueCount) => new()
    {
        VenueCategoryID = category.VenueCategoryID,
        Name = category.Name,
        Slug = category.Slug,
        IsActive = category.IsActive,
        SortOrder = category.SortOrder,
        VenueCount = venueCount,
    };

    private static Log BuildAuditLog(string shortText, string operation, string details, int actingUserId)
    {
        return new Log
        {
            ShortText = shortText,
            Tags = $"scope=audit;entity=venueCategory;event=category;operation={operation};result=success;channel=db",
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
            return (null, this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can manage venue categories." }));
        }

        return (userId, null);
    }
}

public class VenueCategoryRequest
{
    public string? Name { get; set; }

    public int? SortOrder { get; set; }
}

public class VenueCategoryActiveRequest
{
    public bool IsActive { get; set; }
}

public class VenueCategoriesRequest
{
    public List<int>? CategoryIds { get; set; }
}

public class VenueCategoryDto
{
    public int VenueCategoryID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class VenueCategoryAdminDto : VenueCategoryDto
{
    public int SortOrder { get; set; }

    public int VenueCount { get; set; }
}
