// <copyright file="ContentPreferenceController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

//[Authorize]
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentPreferenceController : ControllerBase
{
    private readonly TAGDBContext context;

    public ContentPreferenceController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContentWarningGroupDto>>> Get(int userId)
    {
        //TODO: Replace user id hardcode from parameter to get from token
        //// Get the User ID from the JWT token (NextAuth sub)
        //var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //if (!Guid.TryParse(userIdString, out Guid userId))
        //{
        //    return Unauthorized();
        //}        

        // Fetch groups and items including the user's specific preferences
        var groups = await this.context.Set<ContentWarningGroup>()
            .Include(g => g.Items)
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync()
            .ConfigureAwait(false);

        // Fetch preferences for the current user
        var userPrefs = await this.context.Set<UserContentPreference>()
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.ItemId, p => p.PreferenceMode)
            .ConfigureAwait(false);

        var result = groups.Select(g => new ContentWarningGroupDto
        {
            Title = g.Title,
            Items = g.Items.OrderBy(i => i.DisplayOrder).Select(i => new ContentWarningItemDto
            {
                Id = i.Id,
                Key = i.KeyName,
                Label = i.Label,
                Note = i.Note ?? string.Empty,
                Policy = i.PolicyType,
                DefaultHidden = i.DefaultHidden,
                UserPreference = userPrefs.ContainsKey(i.Id) ? userPrefs[i.Id] : string.Empty
            }).ToList()
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SavePreferences(List<UserPreferenceUpdateDto> preferences)
    {
        //TODO: Replace user id hardcode from parameter to get from token
        //var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //if (!Guid.TryParse(userIdString, out Guid userId))
        //{
        //    return Unauthorized();
        //}
        int userId = preferences.FirstOrDefault().UserId;

        foreach (var pref in preferences)
        {
            var existing = await this.context.Set<UserContentPreference>()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ItemId == pref.ItemId)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.PreferenceMode = pref.PreferenceMode;
                existing.UpdatedAt = DateTime.UtcNow;
                this.context.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                this.context.Set<UserContentPreference>().Add(new UserContentPreference
                {
                    UserId = userId,
                    ItemId = pref.ItemId,
                    PreferenceMode = pref.PreferenceMode,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return NoContent();
    }
}

public class ContentWarningGroupDto
{
    public string Title { get; set; } = string.Empty;
    public List<ContentWarningItemDto> Items { get; set; } = new();
}

public class ContentWarningItemDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Policy { get; set; } = string.Empty;
    public bool DefaultHidden { get; set; }
    public string UserPreference { get; set; } = string.Empty;
}

public class UserPreferenceUpdateDto
{
    //TODO: Replace user id hardcode from parameter to get from token
    public int UserId { get; set; }
    public int ItemId { get; set; }
    public string PreferenceMode { get; set; } = string.Empty;
}