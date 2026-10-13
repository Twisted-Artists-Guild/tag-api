// <copyright file="ContentReportController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ContentReportController : ControllerBase
{
    private readonly TAGDBContext _context;

    public ContentReportController(TAGDBContext context)
    {
        _context = context;
    }

    // POST: api/ContentReport
    [HttpPost]
    public async Task<ActionResult<ContentReportSummaryDTO>> Create(CreateContentReportDTO dto, [FromQuery] int reporterUserId)
    {
        if (string.IsNullOrWhiteSpace(dto.TargetType) || dto.TargetID <= 0)
        {
            return BadRequest("TargetType and TargetID are required.");
        }

        var report = new ContentReport
        {
            ReporterUserID = reporterUserId,
            TargetType = dto.TargetType,
            TargetID = dto.TargetID,
            TargetURL = dto.TargetURL,
            Description = dto.Description,
            Status = "New",
            Priority = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (dto.LabelIDs.Count > 0)
        {
            var validIds = await _context.ContentWarningItems
                .Where(i => dto.LabelIDs.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync();

            foreach (var id in validIds)
            {
                report.Labels.Add(new ContentReportLabel { ContentWarningItemID = id });
            }
        }

        _context.ContentReports.Add(report);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = report.ContentReportID }, await MapToSummary(report.ContentReportID));
    }

    // GET: api/ContentReport/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ContentReportSummaryDTO>> GetById(int id)
    {
        var summary = await MapToSummary(id);
        if (summary == null) return NotFound();
        return summary;
    }

    // GET: api/ContentReport
    [HttpGet]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? targetType = null,
        [FromQuery] int? priority = null,
        [FromQuery] int? assignedStaffId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = _context.ContentReports
            .Include(r => r.Reporter)
            .Include(r => r.AssignedStaff)
            .Include(r => r.Labels).ThenInclude(l => l.ContentWarningItem).ThenInclude(i => i.Group)
            .Include(r => r.Actions).ThenInclude(a => a.Staff)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status.ToLower() == status.ToLower());

        if (!string.IsNullOrEmpty(targetType))
            query = query.Where(r => r.TargetType.ToLower() == targetType.ToLower());

        if (priority.HasValue)
            query = query.Where(r => r.Priority == priority.Value);

        if (assignedStaffId.HasValue)
            query = query.Where(r => r.AssignedStaffID == assignedStaffId.Value);

        var totalCount = await query.CountAsync();

        var reports = await query
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = reports.Select(MapEntityToSummary).ToList();

        return new
        {
            items,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        };
    }

    // GET: api/ContentReport/by-target/{targetType}/{targetId}
    [HttpGet("by-target/{targetType}/{targetId}")]
    public async Task<ActionResult<List<ContentReportSummaryDTO>>> GetByTarget(string targetType, int targetId)
    {
        var reports = await _context.ContentReports
            .Include(r => r.Reporter)
            .Include(r => r.Labels).ThenInclude(l => l.ContentWarningItem).ThenInclude(i => i.Group)
            .Include(r => r.Actions).ThenInclude(a => a.Staff)
            .Where(r => r.TargetType.ToLower() == targetType.ToLower() && r.TargetID == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reports.Select(MapEntityToSummary).ToList();
    }

    // PATCH: api/ContentReport/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateReportStatusDTO dto)
    {
        var report = await _context.ContentReports.FindAsync(id);
        if (report == null) return NotFound();

        if (!string.IsNullOrEmpty(dto.Status))
            report.Status = dto.Status;

        if (dto.AssignedStaffID.HasValue)
            report.AssignedStaffID = dto.AssignedStaffID.Value;

        if (dto.Priority.HasValue)
            report.Priority = dto.Priority.Value;

        if (dto.ResolutionNote != null)
            report.ResolutionNote = dto.ResolutionNote;

        report.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/ContentReport/action
    [HttpPost("action")]
    public async Task<ActionResult<ModerationActionDTO>> CreateAction(CreateModerationActionDTO dto, [FromQuery] int staffId)
    {
        var report = await _context.ContentReports.FindAsync(dto.ContentReportID);
        if (report == null) return NotFound("Report not found.");

        var action = new ModerationAction
        {
            ContentReportID = dto.ContentReportID,
            StaffID = staffId,
            ActionType = dto.ActionType,
            Note = dto.Note,
            ActionMetadata = dto.ActionMetadata,
            CreatedAt = DateTime.UtcNow,
        };

        _context.ModerationActions.Add(action);

        var sideEffectResult = await ApplyModerationAction(action, report);

        report.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var staff = await _context.Staffs.Include(s => s.User).FirstOrDefaultAsync(s => s.StaffID == staffId);

        return new ModerationActionDTO
        {
            ModerationActionID = action.ModerationActionID,
            ActionType = action.ActionType,
            Note = action.Note,
            ActionMetadata = action.ActionMetadata,
            StaffName = staff?.User?.PreferredName,
            CreatedAt = action.CreatedAt,
            SideEffectSummary = sideEffectResult,
        };
    }

    // ── Side-effect execution ────────────────────────────────────────

    private async Task<string> ApplyModerationAction(ModerationAction action, ContentReport report)
    {
        var actionType = action.ActionType?.Trim() ?? "";

        if (string.Equals(actionType, "Block", StringComparison.OrdinalIgnoreCase))
        {
            await SetModerationBlocked(report.TargetType, report.TargetID, true);
            report.Status = "Resolved";
            return $"{report.TargetType} #{report.TargetID} blocked.";
        }

        if (string.Equals(actionType, "RemoveContent", StringComparison.OrdinalIgnoreCase))
        {
            await SetModerationBlocked(report.TargetType, report.TargetID, true);
            report.Status = "Resolved";
            return $"{report.TargetType} #{report.TargetID} removed (moderation blocked).";
        }

        if (string.Equals(actionType, "Suspend", StringComparison.OrdinalIgnoreCase))
        {
            var result = await SuspendContentOwner(report.TargetType, report.TargetID, action.ActionMetadata, action.Note);
            report.Status = "Resolved";
            return result;
        }

        if (string.Equals(actionType, "ChangeTags", StringComparison.OrdinalIgnoreCase))
        {
            var result = await ChangeContentTags(report.TargetType, report.TargetID, action.ActionMetadata);
            report.Status = "Resolved";
            return result;
        }

        if (string.Equals(actionType, "Dismiss", StringComparison.OrdinalIgnoreCase))
        {
            report.Status = "Dismissed";
            return "Report dismissed.";
        }

        if (string.Equals(actionType, "Escalate", StringComparison.OrdinalIgnoreCase))
        {
            report.Priority = 3;
            report.Status = "UnderReview";
            return "Report escalated to Critical priority.";
        }

        if (string.Equals(actionType, "Note", StringComparison.OrdinalIgnoreCase))
        {
            // Note does not change status
            return "Internal note added.";
        }

        return "Unknown action type — no side effects applied.";
    }

    // ── Block / Unblock ──────────────────────────────────────────────

    private async Task SetModerationBlocked(string targetType, int targetId, bool blocked)
    {
        var type = targetType?.Trim() ?? "";

        if (string.Equals(type, "Listing", StringComparison.OrdinalIgnoreCase))
        {
            var listing = await _context.Listings.FindAsync(targetId);
            if (listing != null) listing.IsModerationBlocked = blocked;
        }
        else if (string.Equals(type, "Artist", StringComparison.OrdinalIgnoreCase))
        {
            var artist = await _context.Artists.FindAsync(targetId);
            if (artist != null) artist.IsModerationBlocked = blocked;
        }
        else if (string.Equals(type, "Comment", StringComparison.OrdinalIgnoreCase))
        {
            var comment = await _context.Comments.FindAsync((long)targetId);
            if (comment != null) comment.IsDeleted = blocked;
        }
        else if (string.Equals(type, "Blog", StringComparison.OrdinalIgnoreCase))
        {
            var blog = await _context.Blogs.FindAsync(targetId);
            if (blog != null) blog.StatusID = blocked ? BlogStatus.ModerationBlocked : BlogStatus.Published;
        }
        else if (string.Equals(type, "Event", StringComparison.OrdinalIgnoreCase))
        {
            var evt = await _context.Events.FindAsync(targetId);
            if (evt != null) evt.StatusID = blocked ? EventStatus.ModerationBlocked : EventStatus.Published;
        }
        else if (string.Equals(type, "User", StringComparison.OrdinalIgnoreCase))
        {
            var user = await _context.Users.FindAsync(targetId);
            if (user != null) user.IsModerationBlocked = blocked;
        }
        else if (string.Equals(type, "Message", StringComparison.OrdinalIgnoreCase))
        {
            var message = await _context.Messages.FindAsync(targetId);
            if (message != null) message.IsDeleted = true;
        }
        else if (string.Equals(type, "FeedPost", StringComparison.OrdinalIgnoreCase))
        {
            var feedPost = await _context.FeedPosts.FindAsync(targetId);
            if (feedPost != null) feedPost.IsModerationBlocked = blocked;
        }
    }

    // ── Suspend User ─────────────────────────────────────────────────

    /// <summary>
    /// Finds the user who owns the reported content and suspends them.
    /// ActionMetadata JSON: { "durationDays": 7 }
    /// If no duration is given, defaults to permanent ban.
    /// </summary>
    private async Task<string> SuspendContentOwner(string targetType, int targetId, string? actionMetadata, string? reason)
    {
        var ownerUserId = await ResolveContentOwnerUserId(targetType, targetId);
        if (ownerUserId == null)
        {
            return $"Could not resolve owner user for {targetType} #{targetId}. No suspension applied.";
        }

        var user = await _context.Users.FindAsync(ownerUserId.Value);
        if (user == null)
        {
            return $"User #{ownerUserId} not found.";
        }

        // Parse duration from metadata
        int? durationDays = null;
        if (!string.IsNullOrWhiteSpace(actionMetadata))
        {
            try
            {
                using var doc = JsonDocument.Parse(actionMetadata);
                if (doc.RootElement.TryGetProperty("durationDays", out var dProp) && dProp.ValueKind == JsonValueKind.Number)
                {
                    durationDays = dProp.GetInt32();
                }
            }
            catch { /* ignore parse errors, treat as permanent */ }
        }

        user.MembershipBanned = true;
        user.BannedDate = DateTime.UtcNow;
        user.BannedReason = reason ?? "Suspended by moderation action.";
        user.IsModerationBlocked = true;

        // Also block the reported content itself
        await SetModerationBlocked(targetType, targetId, true);

        var durationText = durationDays.HasValue ? $"{durationDays.Value} days" : "permanently";
        return $"User #{ownerUserId} ({user.PreferredName ?? user.Username ?? user.EmailOne}) suspended {durationText}. {targetType} #{targetId} also blocked.";
    }

    // ── Change Tags ──────────────────────────────────────────────────

    /// <summary>
    /// Updates content warning tags on the reported content.
    /// ActionMetadata JSON: { "addTagIds": [1, 3], "removeTagIds": [5] }
    /// Tags are linked via the existing ContentWarningItem system.
    /// For Listings: updates ArtCategory. For other entities: logged only.
    /// </summary>
    private async Task<string> ChangeContentTags(string targetType, int targetId, string? actionMetadata)
    {
        if (string.IsNullOrWhiteSpace(actionMetadata))
        {
            return "No tag changes specified in ActionMetadata.";
        }

        int[]? addTagIds = null;
        int[]? removeTagIds = null;
        int? newCategoryId = null;

        try
        {
            using var doc = JsonDocument.Parse(actionMetadata);
            var root = doc.RootElement;

            if (root.TryGetProperty("addTagIds", out var addProp))
                addTagIds = addProp.EnumerateArray().Select(e => e.GetInt32()).ToArray();

            if (root.TryGetProperty("removeTagIds", out var removeProp))
                removeTagIds = removeProp.EnumerateArray().Select(e => e.GetInt32()).ToArray();

            if (root.TryGetProperty("newCategoryId", out var catProp) && catProp.ValueKind == JsonValueKind.Number)
                newCategoryId = catProp.GetInt32();
        }
        catch
        {
            return "Failed to parse ActionMetadata JSON for tag changes.";
        }

        var changes = new List<string>();

        // Change ArtCategory on Listings
        if (newCategoryId.HasValue && string.Equals(targetType, "Listing", StringComparison.OrdinalIgnoreCase))
        {
            var listing = await _context.Listings.FindAsync(targetId);
            if (listing != null)
            {
                var oldCatId = listing.ArtCategoryID;
                listing.ArtCategoryID = newCategoryId.Value;
                changes.Add($"Listing category changed from {oldCatId} to {newCategoryId.Value}.");
            }
        }

        // Change ArtistCategory links on Artists
        if (string.Equals(targetType, "Artist", StringComparison.OrdinalIgnoreCase))
        {
            if (removeTagIds != null && removeTagIds.Length > 0)
            {
                var linksToRemove = await _context.Linker_ArtistToCategories
                    .Where(l => l.ArtistID == targetId && removeTagIds.Contains(l.ArtistCategoryID))
                    .ToListAsync();

                if (linksToRemove.Count > 0)
                {
                    _context.Linker_ArtistToCategories.RemoveRange(linksToRemove);
                    changes.Add($"Removed {linksToRemove.Count} category link(s) from Artist #{targetId}.");
                }
            }

            if (addTagIds != null && addTagIds.Length > 0)
            {
                var existingCatIds = await _context.Linker_ArtistToCategories
                    .Where(l => l.ArtistID == targetId)
                    .Select(l => l.ArtistCategoryID)
                    .ToListAsync();

                var toAdd = addTagIds.Where(id => !existingCatIds.Contains(id)).ToList();
                foreach (var catId in toAdd)
                {
                    _context.Linker_ArtistToCategories.Add(new LinkerArtistToCategory
                    {
                        ArtistID = targetId,
                        ArtistCategoryID = catId,
                    });
                }

                if (toAdd.Count > 0)
                    changes.Add($"Added {toAdd.Count} category link(s) to Artist #{targetId}.");
            }
        }

        if (changes.Count == 0)
        {
            return $"Tag change metadata recorded for {targetType} #{targetId}. No direct tag modifications applied.";
        }

        return string.Join(" ", changes);
    }

    // ── Resolve content owner ────────────────────────────────────────

    /// <summary>
    /// Resolves the UserID of the person who owns/created the reported content.
    /// </summary>
    private async Task<int?> ResolveContentOwnerUserId(string targetType, int targetId)
    {
        var type = targetType?.Trim() ?? "";

        if (string.Equals(type, "User", StringComparison.OrdinalIgnoreCase))
        {
            return targetId;
        }

        if (string.Equals(type, "Blog", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Blogs
                .Where(b => b.BlogID == targetId)
                .Select(b => (int?)b.UserID)
                .FirstOrDefaultAsync();
        }

        if (string.Equals(type, "Comment", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Comments
                .Where(c => c.Id == (long)targetId)
                .Select(c => (int?)c.UserId)
                .FirstOrDefaultAsync();
        }

        if (string.Equals(type, "Message", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Messages
                .Where(m => m.MessageID == targetId)
                .Select(m => (int?)m.FromUserID)
                .FirstOrDefaultAsync();
        }

        if (string.Equals(type, "Listing", StringComparison.OrdinalIgnoreCase))
        {
            var artistId = await _context.Listings
                .Where(l => l.ListingID == targetId)
                .Select(l => (int?)l.ArtistID)
                .FirstOrDefaultAsync();

            if (artistId.HasValue)
            {
                return await ResolveArtistOwnerUserId(artistId.Value);
            }
        }

        if (string.Equals(type, "Artist", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveArtistOwnerUserId(targetId);
        }

        if (string.Equals(type, "Event", StringComparison.OrdinalIgnoreCase))
        {
            var artistId = await _context.Events
                .Where(e => e.EventID == targetId)
                .Select(e => (int?)e.ArtistID)
                .FirstOrDefaultAsync();

            if (artistId.HasValue)
            {
                return await ResolveArtistOwnerUserId(artistId.Value);
            }
        }
        if (string.Equals(type, "FeedPost", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.FeedPosts
                .Where(p => p.FeedPostID == targetId)
                .Select(p => (int?)p.AuthorUserID)
                .FirstOrDefaultAsync();
        }

        return null;
    }

    private async Task<int?> ResolveArtistOwnerUserId(int artistId)
    {
        return await _context.Set<Linker_UserToArtist>()
            .Where(l => l.ArtistID == artistId)
            .Select(l => (int?)l.UserID)
            .FirstOrDefaultAsync();
    }

    // ── Mapping ──────────────────────────────────────────────────────

    private async Task<ContentReportSummaryDTO?> MapToSummary(int reportId)
    {
        var report = await _context.ContentReports
            .Include(r => r.Reporter)
            .Include(r => r.AssignedStaff).ThenInclude(s => s.User)
            .Include(r => r.Labels).ThenInclude(l => l.ContentWarningItem).ThenInclude(i => i.Group)
            .Include(r => r.Actions).ThenInclude(a => a.Staff).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(r => r.ContentReportID == reportId);

        if (report == null) return null;
        return MapEntityToSummary(report);
    }

    private static ContentReportSummaryDTO MapEntityToSummary(ContentReport report)
    {
        return new ContentReportSummaryDTO
        {
            ContentReportID = report.ContentReportID,
            TargetType = report.TargetType,
            TargetID = report.TargetID,
            TargetURL = report.TargetURL,
            Description = report.Description,
            Status = report.Status,
            Priority = report.Priority,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt,
            ReporterName = report.Reporter?.PreferredName,
            ReporterUserID = report.ReporterUserID,
            AssignedStaffName = report.AssignedStaff?.User?.PreferredName,
            AssignedStaffID = report.AssignedStaffID,
            ResolutionNote = report.ResolutionNote,
            Labels = report.Labels.Select(l => new ContentReportLabelDTO
            {
                ContentWarningItemID = l.ContentWarningItemID,
                Label = l.ContentWarningItem?.Label ?? "",
                GroupTitle = l.ContentWarningItem?.Group?.Title ?? "",
            }).ToList(),
            Actions = report.Actions.OrderByDescending(a => a.CreatedAt).Select(a => new ModerationActionDTO
            {
                ModerationActionID = a.ModerationActionID,
                ActionType = a.ActionType,
                Note = a.Note,
                ActionMetadata = a.ActionMetadata,
                StaffName = a.Staff?.User?.PreferredName,
                CreatedAt = a.CreatedAt,
            }).ToList(),
        };
    }
}