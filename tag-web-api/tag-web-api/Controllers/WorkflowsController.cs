using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private static readonly HashSet<string> AllowedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "user",
        "artist",
        "venue",
        "vendor",
        "listing",
    };

    private static readonly Dictionary<string, List<WorkflowDefinitionStepDto>> FallbackDefinitionsByEntityType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["user"] = new()
        {
            new() { StepKey = "accepted_tc", StepLabel = "Accepted Terms", StepOrder = 10, IsRequiredForPublish = true },
            new() { StepKey = "reserved_slug", StepLabel = "Reserved Username", StepOrder = 20, IsRequiredForPublish = true },
            new() { StepKey = "completed_profile_core", StepLabel = "Completed Core Profile", StepOrder = 30, IsRequiredForPublish = true },
            new() { StepKey = "completed_primary_contacts", StepLabel = "Completed Primary Contacts", StepOrder = 40, IsRequiredForPublish = true },
            new() { StepKey = "completed_privacy", StepLabel = "Completed Privacy", StepOrder = 50, IsRequiredForPublish = true },
            new() { StepKey = "completed_media", StepLabel = "Completed Media", StepOrder = 60, IsRequiredForPublish = true },
            new() { StepKey = "completed_preferences", StepLabel = "Completed Preferences", StepOrder = 70, IsRequiredForPublish = true },
            new() { StepKey = "published", StepLabel = "Published", StepOrder = 80, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "tutorial_bloomscroll", StepLabel = "Tutorial: Bloomscroll", StepOrder = 90, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "tutorial_contest_voting", StepLabel = "Tutorial: Contest Voting", StepOrder = 100, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "completed_followup_form", StepLabel = "Completed Follow-up Form", StepOrder = 110, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "first_post", StepLabel = "First Post", StepOrder = 120, IsRequiredForPublish = false, IsPostPublish = true },
        },
        ["artist"] = new()
        {
            new() { StepKey = "accepted_tc", StepLabel = "Accepted Terms", StepOrder = 10, IsRequiredForPublish = true },
            new() { StepKey = "reserved_slug", StepLabel = "Reserved Slug", StepOrder = 20, IsRequiredForPublish = true },
            new() { StepKey = "added_bio", StepLabel = "Completed Profile", StepOrder = 30, IsRequiredForPublish = true },
            new() { StepKey = "private_contacts", StepLabel = "Completed Primary Contacts", StepOrder = 40, IsRequiredForPublish = true },
            new() { StepKey = "uploaded_photos", StepLabel = "Completed Media", StepOrder = 50, IsRequiredForPublish = true },
            new() { StepKey = "added_contacts", StepLabel = "Completed Public Contacts", StepOrder = 60, IsRequiredForPublish = true },
            new() { StepKey = "published", StepLabel = "Published", StepOrder = 70, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "tutorial_first_listing", StepLabel = "Tutorial: First Listing", StepOrder = 80, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "first_post", StepLabel = "First Post", StepOrder = 90, IsRequiredForPublish = false, IsPostPublish = true },
        },
        ["vendor"] = new()
        {
            new() { StepKey = "accepted_tc", StepLabel = "Accepted Terms", StepOrder = 10, IsRequiredForPublish = true },
            new() { StepKey = "reserved_slug", StepLabel = "Reserved Slug", StepOrder = 20, IsRequiredForPublish = true },
            new() { StepKey = "completed_business_details", StepLabel = "Completed Business Details", StepOrder = 30, IsRequiredForPublish = true },
            new() { StepKey = "completed_primary_contacts", StepLabel = "Completed Primary Contacts", StepOrder = 40, IsRequiredForPublish = true },
            new() { StepKey = "completed_media", StepLabel = "Completed Media", StepOrder = 50, IsRequiredForPublish = true },
            new() { StepKey = "completed_public_contacts", StepLabel = "Completed Public Contacts", StepOrder = 60, IsRequiredForPublish = true },
            new() { StepKey = "published", StepLabel = "Published", StepOrder = 70, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "first_post", StepLabel = "First Post", StepOrder = 80, IsRequiredForPublish = false, IsPostPublish = true },
        },
        ["venue"] = new()
        {
            new() { StepKey = "accepted_tc", StepLabel = "Accepted Terms", StepOrder = 10, IsRequiredForPublish = true },
            new() { StepKey = "reserved_slug", StepLabel = "Reserved Slug", StepOrder = 20, IsRequiredForPublish = true },
            new() { StepKey = "completed_venue_profile", StepLabel = "Completed Venue Profile", StepOrder = 30, IsRequiredForPublish = true },
            new() { StepKey = "completed_primary_contacts", StepLabel = "Completed Primary Contacts", StepOrder = 40, IsRequiredForPublish = true },
            new() { StepKey = "completed_media", StepLabel = "Completed Media", StepOrder = 50, IsRequiredForPublish = true },
            new() { StepKey = "completed_public_contacts", StepLabel = "Completed Public Contacts", StepOrder = 60, IsRequiredForPublish = true },
            new() { StepKey = "published", StepLabel = "Published", StepOrder = 70, IsRequiredForPublish = false, IsPostPublish = true },
            new() { StepKey = "first_post", StepLabel = "First Post", StepOrder = 80, IsRequiredForPublish = false, IsPostPublish = true },
        },
        ["listing"] = new()
        {
            new() { StepKey = "added_listing_details", StepLabel = "Added Listing Details", StepOrder = 10, IsRequiredForPublish = true },
            new() { StepKey = "uploaded_listing_media", StepLabel = "Uploaded Listing Media", StepOrder = 20, IsRequiredForPublish = true },
            new() { StepKey = "set_listing_price", StepLabel = "Set Listing Price", StepOrder = 30, IsRequiredForPublish = true },
            new() { StepKey = "published", StepLabel = "Published", StepOrder = 40, IsRequiredForPublish = false, IsPostPublish = true },
        },
    };

    private readonly TAGDBContext context;
    private readonly ILogger<WorkflowsController> logger;

    public WorkflowsController(TAGDBContext context, ILogger<WorkflowsController> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    [HttpGet("{entityType}/{entityId:int}")]
    public async Task<ActionResult<WorkflowSummaryResponse>> GetWorkflow(string entityType, int entityId, [FromQuery] string? workflowName = null)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return this.BadRequest("Unsupported entityType. Allowed values: user, artist, venue, vendor, listing.");
        }

        var normalizedWorkflowName = NormalizeWorkflowName(workflowName);

        if (!await this.EntityExistsAsync(normalizedEntityType, entityId).ConfigureAwait(false))
        {
            return this.NotFound("Entity not found.");
        }

        var steps = await this.GetWorkflowStepsAsync(normalizedEntityType, entityId, normalizedWorkflowName).ConfigureAwait(false);
        var workflowDefinitionSteps = await this.GetWorkflowDefinitionStepsAsync(normalizedEntityType, normalizedWorkflowName).ConfigureAwait(false);
        var requiredSteps = workflowDefinitionSteps
            .Where(step => step.IsRequiredForPublish)
            .Select(step => step.StepKey)
            .ToList();
        var completedRequiredCount = requiredSteps.Count(step => steps.Any(s => s.StepKey.Equals(step, StringComparison.OrdinalIgnoreCase) && s.IsCompleted));
        var percentComplete = requiredSteps.Count == 0 ? 100 : (int)Math.Round((completedRequiredCount * 100.0) / requiredSteps.Count, MidpointRounding.AwayFromZero);
        var isPublished = await this.GetPublishedStatusAsync(normalizedEntityType, entityId).ConfigureAwait(false);
        var isModerationBlocked = await this.GetModerationBlockedStatusAsync(normalizedEntityType, entityId).ConfigureAwait(false);

        return this.Ok(new WorkflowSummaryResponse
        {
            EntityType = normalizedEntityType,
            EntityId = entityId,
            RequiredSteps = requiredSteps,
            WorkflowDefinitionSteps = workflowDefinitionSteps,
            Steps = steps,
            PercentComplete = percentComplete,
            IsPublished = isPublished,
            IsModerationBlocked = isModerationBlocked,
            UserArtistInGoodStanding = normalizedEntityType == "user" ? await this.context.Users.Where(u => u.UserID == entityId).Select(u => u.ArtistInGoodStanding).FirstOrDefaultAsync().ConfigureAwait(false) : null,
            UserMembershipBanned = normalizedEntityType == "user" ? await this.context.Users.Where(u => u.UserID == entityId).Select(u => u.MembershipBanned).FirstOrDefaultAsync().ConfigureAwait(false) : null,
        });
    }

    [HttpPut("user/{userId:int}/dues-status")]
    public async Task<ActionResult> SetUserDuesStatus(int userId, [FromBody] UserDuesStatusUpdateRequest request)
    {
        var actingUserId = request?.ModeratorUserID;
        if (!actingUserId.HasValue)
        {
            return this.BadRequest("ModeratorUserID is required.");
        }

        if (!await this.IsModeratorAsync(actingUserId.Value).ConfigureAwait(false))
        {
            return this.Forbid();
        }

        var user = await this.context.Users.FirstOrDefaultAsync(u => u.UserID == userId).ConfigureAwait(false);
        if (user == null)
        {
            return this.NotFound("User not found.");
        }

        var now = DateTime.UtcNow;
        user.ArtistInGoodStanding = request?.ArtistInGoodStanding ?? false;

        await this.UpsertWorkflowStepInternalAsync(
            "user",
            userId,
            "dues_current",
            user.ArtistInGoodStanding,
            actingUserId,
            now).ConfigureAwait(false);

        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "User dues status updated",
            tags: "scope=audit;entity=user;event=workflow_dues_status;operation=update;result=success;channel=db",
            userId: userId,
            loggedData: $"userId={userId};artistInGoodStanding={user.ArtistInGoodStanding};updatedBy={actingUserId.Value}")
            .ConfigureAwait(false);

        return this.NoContent();
    }

    [HttpPut("user/{userId:int}/membership-ban")]
    public async Task<ActionResult> SetUserMembershipBan(int userId, [FromBody] UserMembershipBanUpdateRequest request)
    {
        var actingUserId = request?.ModeratorUserID;
        if (!actingUserId.HasValue)
        {
            return this.BadRequest("ModeratorUserID is required.");
        }

        if (!await this.IsModeratorAsync(actingUserId.Value).ConfigureAwait(false))
        {
            return this.Forbid();
        }

        var user = await this.context.Users.FirstOrDefaultAsync(u => u.UserID == userId).ConfigureAwait(false);
        if (user == null)
        {
            return this.NotFound("User not found.");
        }

        var now = DateTime.UtcNow;
        var isBanned = request?.MembershipBanned ?? false;

        user.MembershipBanned = isBanned;
        user.BannedDate = isBanned ? now : null;
        user.BannedReason = isBanned ? string.IsNullOrWhiteSpace(request?.BannedReason) ? "Membership banned by moderator workflow." : request!.BannedReason!.Trim() : null;

        // Keep this behavior strict in prototype: a banned account cannot be published.
        if (isBanned)
        {
            user.IsPublished = false;
            user.IsModerationBlocked = true;
        }

        await this.UpsertWorkflowStepInternalAsync(
            "user",
            userId,
            "membership_banned",
            isBanned,
            actingUserId,
            now).ConfigureAwait(false);

        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "User membership ban status updated",
            tags: "scope=audit;entity=user;event=workflow_membership_ban;operation=update;result=success;channel=db",
            critical: isBanned,
            userId: userId,
            loggedData: $"userId={userId};membershipBanned={isBanned};updatedBy={actingUserId.Value};reason={user.BannedReason}")
            .ConfigureAwait(false);

        return this.NoContent();
    }

    [HttpPut("{entityType}/{entityId:int}/steps/{stepKey}")]
    public async Task<ActionResult<WorkflowStepDto>> UpsertStep(string entityType, int entityId, string stepKey, [FromBody] WorkflowStepUpdateRequest request)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return this.BadRequest("Unsupported entityType. Allowed values: user, artist, venue, vendor, listing.");
        }

        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return this.BadRequest("stepKey is required.");
        }

        if (!await this.EntityExistsAsync(normalizedEntityType, entityId).ConfigureAwait(false))
        {
            return this.NotFound("Entity not found.");
        }

        var normalizedStepKey = NormalizeStepKey(stepKey);
        var now = DateTime.UtcNow;

        var result = await this.UpsertWorkflowStepInternalAsync(
            normalizedEntityType,
            entityId,
            normalizedStepKey,
            request?.IsCompleted ?? false,
            request?.UpdatedByUserID,
            now).ConfigureAwait(false);

        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.Ok(result);
    }

    [HttpPost("upsert-step")]
    public async Task<ActionResult<WorkflowStepDto>> UpsertStepPost([FromBody] UpsertWorkflowStepRequest request)
    {
        if (request == null)
        {
            return this.BadRequest("Request body is required.");
        }

        var normalizedEntityType = NormalizeEntityType(request.EntityType);
        if (normalizedEntityType == null)
        {
            return this.BadRequest("Unsupported entityType. Allowed values: user, artist, venue, vendor, listing.");
        }

        if (request.EntityId <= 0)
        {
            return this.BadRequest("EntityId must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.StepKey))
        {
            return this.BadRequest("StepKey is required.");
        }

        if (!await this.EntityExistsAsync(normalizedEntityType, request.EntityId).ConfigureAwait(false))
        {
            return this.NotFound("Entity not found.");
        }

        var normalizedStepKey = NormalizeStepKey(request.StepKey);
        var normalizedWorkflowName = NormalizeWorkflowName(request.WorkflowName);
        var now = DateTime.UtcNow;

        var row = await this.context.UnifiedWorkflows
            .FirstOrDefaultAsync(w => w.EntityID == request.EntityId
                && w.EntityType == normalizedEntityType
                && w.WorkflowName == normalizedWorkflowName
                && w.StepKey == normalizedStepKey)
            .ConfigureAwait(false);

        if (row == null)
        {
            row = new UnifiedWorkflow
            {
                EntityID = request.EntityId,
                EntityType = normalizedEntityType,
                WorkflowName = normalizedWorkflowName,
                StepKey = normalizedStepKey,
            };
            this.context.UnifiedWorkflows.Add(row);
        }

        row.IsCompleted = request.IsCompleted;
        row.CompletedAt = request.IsCompleted ? now : null;
        row.UpdatedAt = now;
        row.UpdatedByUserID = request.UpdatedByUserID;

        var result = new WorkflowStepDto
        {
            StepKey = row.StepKey,
            IsCompleted = row.IsCompleted,
            CompletedAt = row.CompletedAt,
            UpdatedAt = row.UpdatedAt,
            UpdatedByUserID = row.UpdatedByUserID,
        };

        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.Ok(result);
    }

    [HttpPut("{entityType}/{entityId:int}/moderation")]
    public async Task<ActionResult> SetModeration(string entityType, int entityId, [FromBody] ModerationUpdateRequest request)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return this.BadRequest("Unsupported entityType. Allowed values: user, artist, venue, vendor, listing.");
        }

        if (!await this.EntityExistsAsync(normalizedEntityType, entityId).ConfigureAwait(false))
        {
            return this.NotFound("Entity not found.");
        }

        await this.SetModerationBlockedStatusAsync(normalizedEntityType, entityId, request?.IsModerationBlocked ?? false).ConfigureAwait(false);

        // Moderation blocks publication immediately.
        if (request?.IsModerationBlocked == true)
        {
            await this.SetPublishedStatusAsync(normalizedEntityType, entityId, false).ConfigureAwait(false);
        }

        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "Entity moderation status updated",
            tags: $"scope=audit;entity={normalizedEntityType};event=workflow_moderation;operation=update;result=success;channel=db",
            critical: request?.IsModerationBlocked == true,
            userId: request?.UpdatedByUserID,
            artistId: normalizedEntityType == "artist" ? entityId : null,
            listingId: normalizedEntityType == "listing" ? entityId : null,
            loggedData: $"entityType={normalizedEntityType};entityId={entityId};isModerationBlocked={request?.IsModerationBlocked ?? false};updatedBy={request?.UpdatedByUserID}")
            .ConfigureAwait(false);

        return this.NoContent();
    }

    [HttpPut("{entityType}/{entityId:int}/publish")]
    public async Task<ActionResult> SetPublished(string entityType, int entityId, [FromBody] PublishUpdateRequest request)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return this.BadRequest("Unsupported entityType. Allowed values: user, artist, venue, vendor, listing.");
        }

        if (!await this.EntityExistsAsync(normalizedEntityType, entityId).ConfigureAwait(false))
        {
            return this.NotFound("Entity not found.");
        }

        var publish = request?.IsPublished ?? false;
        var enforceRequiredSteps = request?.EnforceRequiredSteps ?? true;
        var now = DateTime.UtcNow;

        if (publish)
        {
            var isModerationBlocked = await this.GetModerationBlockedStatusAsync(normalizedEntityType, entityId).ConfigureAwait(false);
            if (isModerationBlocked)
            {
                return this.BadRequest("Entity is moderation blocked and cannot be published.");
            }

            if (enforceRequiredSteps)
            {
                var steps = await this.GetWorkflowStepsAsync(normalizedEntityType, entityId, "default").ConfigureAwait(false);
                var requiredSteps = (await this.GetWorkflowDefinitionStepsAsync(normalizedEntityType, "default").ConfigureAwait(false))
                    .Where(step => step.IsRequiredForPublish)
                    .Select(step => step.StepKey)
                    .ToList();
                var missing = requiredSteps
                    .Where(step => !steps.Any(s => s.StepKey.Equals(step, StringComparison.OrdinalIgnoreCase) && s.IsCompleted))
                    .ToList();

                if (missing.Count > 0)
                {
                    return this.BadRequest(new
                    {
                        message = "Required workflow steps are not complete.",
                        missingSteps = missing,
                    });
                }
            }
        }

        await this.SetPublishedStatusAsync(normalizedEntityType, entityId, publish).ConfigureAwait(false);
        await this.UpsertWorkflowStepInternalAsync(normalizedEntityType, entityId, "published", publish, request?.UpdatedByUserID, now).ConfigureAwait(false);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "Entity publish status updated",
            tags: $"scope=audit;entity={normalizedEntityType};event=workflow_publish;operation=update;result=success;channel=db",
            userId: request?.UpdatedByUserID,
            artistId: normalizedEntityType == "artist" ? entityId : null,
            listingId: normalizedEntityType == "listing" ? entityId : null,
            loggedData: $"entityType={normalizedEntityType};entityId={entityId};isPublished={publish};updatedBy={request?.UpdatedByUserID}")
            .ConfigureAwait(false);

        return this.NoContent();
    }

    private async Task TryWriteAuditLogAsync(
        string shortText,
        string tags,
        bool critical = false,
        string? longText = null,
        string? loggedData = null,
        int? userId = null,
        int? artistId = null,
        int? listingId = null)
    {
        var sanitizedShortText = SanitizeForLog(shortText) ?? string.Empty;
        var sanitizedTags = SanitizeForLog(tags) ?? string.Empty;
        var sanitizedLongText = SanitizeForLog(longText);
        var sanitizedLoggedData = SanitizeForLog(loggedData);

        try
        {
            this.context.Set<Log>().Add(new Log
            {
                ShortText = sanitizedShortText,
                Tags = sanitizedTags,
                Critical = critical,
                LongText = sanitizedLongText,
                LoggedData = sanitizedLoggedData,
                UserID = userId,
                ArtistID = artistId,
                ListingID = listingId,
                LogTimestamp = DateTime.UtcNow,
            });

            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to write workflow audit log. Tags: {Tags}", sanitizedTags);
        }
    }

    private static string? SanitizeForLog(string? value)
    {
        return value?
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    private static string? NormalizeEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return null;
        }

        var normalized = entityType.Trim().ToLowerInvariant();
        return AllowedEntityTypes.Contains(normalized) ? normalized : null;
    }

    private static List<WorkflowDefinitionStepDto> GetFallbackWorkflowDefinitionSteps(string entityType)
    {
        return FallbackDefinitionsByEntityType.TryGetValue(entityType, out var definitions)
            ? definitions
                .Select(step => new WorkflowDefinitionStepDto
                {
                    StepKey = step.StepKey,
                    StepLabel = step.StepLabel,
                    StepOrder = step.StepOrder,
                    IsRequiredForPublish = step.IsRequiredForPublish,
                    IsPostPublish = step.IsPostPublish,
                })
                .ToList()
            : new List<WorkflowDefinitionStepDto>();
    }

    private async Task<List<WorkflowDefinitionStepDto>> GetWorkflowDefinitionStepsAsync(string entityType, string workflowName)
    {
        try
        {
            var definitions = await this.context.Database
                .SqlQueryRaw<WorkflowDefinitionStepDto>(
                    @"SELECT
                                                ""StepKey"" AS ""StepKey"",
                                                ""StepLabel"" AS ""StepLabel"",
                                                ""StepOrder"" AS ""StepOrder"",
                                                ""IsRequiredForPublish"" AS ""IsRequiredForPublish"",
                                                ""IsPostPublish"" AS ""IsPostPublish""
                                            FROM ""WorkflowStepDefinitions""
                                            WHERE ""EntityType"" = {0} AND ""WorkflowName"" = {1}
                                            ORDER BY ""StepOrder"", ""StepKey""",
                    entityType,
                    workflowName)
                .ToListAsync()
                .ConfigureAwait(false);

            if (definitions.Count > 0)
            {
                return definitions;
            }

            if (!string.Equals(workflowName, "default", StringComparison.OrdinalIgnoreCase))
            {
                definitions = await this.context.Database
                    .SqlQueryRaw<WorkflowDefinitionStepDto>(
                        @"SELECT
                                                        ""StepKey"" AS ""StepKey"",
                                                        ""StepLabel"" AS ""StepLabel"",
                                                        ""StepOrder"" AS ""StepOrder"",
                                                        ""IsRequiredForPublish"" AS ""IsRequiredForPublish"",
                                                        ""IsPostPublish"" AS ""IsPostPublish""
                                                    FROM ""WorkflowStepDefinitions""
                                                    WHERE ""EntityType"" = {0} AND ""WorkflowName"" = 'default'
                                                    ORDER BY ""StepOrder"", ""StepKey""",
                        entityType)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (definitions.Count > 0)
                {
                    return definitions;
                }
            }
        }
        catch
        {
            // Fallback definitions are used until WorkflowStepDefinitions exists everywhere.
        }

        return GetFallbackWorkflowDefinitionSteps(entityType);
    }

    private static string NormalizeStepKey(string stepKey)
    {
        return stepKey.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string NormalizeWorkflowName(string? workflowName)
    {
        return string.IsNullOrWhiteSpace(workflowName) ? "default" : workflowName.Trim().ToLowerInvariant();
    }

    private async Task<bool> EntityExistsAsync(string entityType, int entityId)
    {
        return entityType switch
        {
            "user" => await this.context.Users.AnyAsync(e => e.UserID == entityId).ConfigureAwait(false),
            "artist" => await this.context.Artists.AnyAsync(e => e.ArtistID == entityId).ConfigureAwait(false),
            "venue" => await this.context.Venues.AnyAsync(e => e.VenueID == entityId).ConfigureAwait(false),
            "vendor" => await this.context.Vendors.AnyAsync(e => e.VendorID == entityId).ConfigureAwait(false),
            "listing" => await this.context.Listings.AnyAsync(e => e.ListingID == entityId).ConfigureAwait(false),
            _ => false,
        };
    }

    private async Task<List<WorkflowStepDto>> GetWorkflowStepsAsync(string entityType, int entityId, string workflowName)
    {
        return await this.context.UnifiedWorkflows
            .Where(w => w.EntityID == entityId 
                && w.EntityType == entityType 
                && w.WorkflowName == workflowName)
            .OrderBy(w => w.StepKey)
            .Select(w => new WorkflowStepDto
            {
                StepKey = w.StepKey,
                IsCompleted = w.IsCompleted,
                CompletedAt = w.CompletedAt,
                UpdatedAt = w.UpdatedAt,
                UpdatedByUserID = w.UpdatedByUserID,
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private async Task<WorkflowStepDto> UpsertWorkflowStepInternalAsync(string entityType, int entityId, string stepKey, bool isCompleted, int? updatedByUserID, DateTime now)
    {
        var normalizedStepKey = NormalizeStepKey(stepKey);
        var workflowName = NormalizeWorkflowName(null);

        var row = await this.context.UnifiedWorkflows
            .FirstOrDefaultAsync(w => w.EntityID == entityId 
                && w.EntityType == entityType 
                && w.WorkflowName == workflowName 
                && w.StepKey == normalizedStepKey)
            .ConfigureAwait(false);

        if (row == null)
        {
            row = new UnifiedWorkflow
            {
                EntityID = entityId,
                EntityType = entityType,
                WorkflowName = workflowName,
                StepKey = normalizedStepKey,
            };
            this.context.UnifiedWorkflows.Add(row);
        }

        row.IsCompleted = isCompleted;
        row.CompletedAt = isCompleted ? now : null;
        row.UpdatedAt = now;
        row.UpdatedByUserID = updatedByUserID;

        return new WorkflowStepDto
        {
            StepKey = row.StepKey,
            IsCompleted = row.IsCompleted,
            CompletedAt = row.CompletedAt,
            UpdatedAt = row.UpdatedAt,
            UpdatedByUserID = row.UpdatedByUserID,
        };
    }

    private async Task<bool> GetPublishedStatusAsync(string entityType, int entityId)
    {
        return entityType switch
        {
            "user" => await this.context.Users.Where(e => e.UserID == entityId).Select(e => e.IsPublished).FirstAsync().ConfigureAwait(false),
            "artist" => await this.context.Artists.Where(e => e.ArtistID == entityId).Select(e => e.IsPublished).FirstAsync().ConfigureAwait(false),
            "venue" => await this.context.Venues.Where(e => e.VenueID == entityId).Select(e => e.IsPublished).FirstAsync().ConfigureAwait(false),
            "vendor" => await this.context.Vendors.Where(e => e.VendorID == entityId).Select(e => e.IsPublished).FirstAsync().ConfigureAwait(false),
            "listing" => await this.context.Listings.Where(e => e.ListingID == entityId).Select(e => e.IsPublished).FirstAsync().ConfigureAwait(false),
            _ => false,
        };
    }

    private async Task<bool> GetModerationBlockedStatusAsync(string entityType, int entityId)
    {
        return entityType switch
        {
            "user" => await this.context.Users.Where(e => e.UserID == entityId).Select(e => e.IsModerationBlocked).FirstAsync().ConfigureAwait(false),
            "artist" => await this.context.Artists.Where(e => e.ArtistID == entityId).Select(e => e.IsModerationBlocked).FirstAsync().ConfigureAwait(false),
            "venue" => await this.context.Venues.Where(e => e.VenueID == entityId).Select(e => e.IsModerationBlocked).FirstAsync().ConfigureAwait(false),
            "vendor" => await this.context.Vendors.Where(e => e.VendorID == entityId).Select(e => e.IsModerationBlocked).FirstAsync().ConfigureAwait(false),
            "listing" => await this.context.Listings.Where(e => e.ListingID == entityId).Select(e => e.IsModerationBlocked).FirstAsync().ConfigureAwait(false),
            _ => false,
        };
    }

    private async Task SetPublishedStatusAsync(string entityType, int entityId, bool isPublished)
    {
        switch (entityType)
        {
            case "user":
                await this.context.Users.Where(e => e.UserID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublished, isPublished)).ConfigureAwait(false);
                break;
            case "artist":
                await this.context.Artists.Where(e => e.ArtistID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublished, isPublished)).ConfigureAwait(false);
                break;
            case "venue":
                await this.context.Venues.Where(e => e.VenueID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublished, isPublished)).ConfigureAwait(false);
                break;
            case "vendor":
                await this.context.Vendors.Where(e => e.VendorID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublished, isPublished)).ConfigureAwait(false);
                break;
            case "listing":
                await this.context.Listings.Where(e => e.ListingID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublished, isPublished)).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("Unsupported entityType.");
        }
    }

    private async Task SetModerationBlockedStatusAsync(string entityType, int entityId, bool isModerationBlocked)
    {
        switch (entityType)
        {
            case "user":
                await this.context.Users.Where(e => e.UserID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsModerationBlocked, isModerationBlocked)).ConfigureAwait(false);
                break;
            case "artist":
                await this.context.Artists.Where(e => e.ArtistID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsModerationBlocked, isModerationBlocked)).ConfigureAwait(false);
                break;
            case "venue":
                await this.context.Venues.Where(e => e.VenueID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsModerationBlocked, isModerationBlocked)).ConfigureAwait(false);
                break;
            case "vendor":
                await this.context.Vendors.Where(e => e.VendorID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsModerationBlocked, isModerationBlocked)).ConfigureAwait(false);
                break;
            case "listing":
                await this.context.Listings.Where(e => e.ListingID == entityId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsModerationBlocked, isModerationBlocked)).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("Unsupported entityType.");
        }
    }

    private async Task<bool> IsModeratorAsync(int userId)
    {
        return await this.context.Users
            .Where(u => u.UserID == userId)
            .Select(u => u.Moderator)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }
}

public class WorkflowStepUpdateRequest
{
    public bool IsCompleted { get; set; }

    public int? UpdatedByUserID { get; set; }
}

public class UpsertWorkflowStepRequest
{
    public int EntityId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string StepKey { get; set; } = string.Empty;

    public string? WorkflowName { get; set; }

    public bool IsCompleted { get; set; } = true;

    public int? UpdatedByUserID { get; set; }
}

public class ModerationUpdateRequest
{
    public bool IsModerationBlocked { get; set; }

    public int? UpdatedByUserID { get; set; }
}

public class PublishUpdateRequest
{
    public bool IsPublished { get; set; }

    public bool EnforceRequiredSteps { get; set; } = true;

    public int? UpdatedByUserID { get; set; }
}

public class WorkflowStepDto
{
    public string StepKey { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedByUserID { get; set; }
}

public class WorkflowSummaryResponse
{
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public List<string> RequiredSteps { get; set; } = new();

    public List<WorkflowDefinitionStepDto> WorkflowDefinitionSteps { get; set; } = new();

    public List<WorkflowStepDto> Steps { get; set; } = new();

    public int PercentComplete { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    public bool? UserArtistInGoodStanding { get; set; }

    public bool? UserMembershipBanned { get; set; }
}

public class WorkflowDefinitionStepDto
{
    public string StepKey { get; set; } = string.Empty;

    public string StepLabel { get; set; } = string.Empty;

    public int StepOrder { get; set; }

    public bool IsRequiredForPublish { get; set; }

    public bool IsPostPublish { get; set; }
}

public class UserDuesStatusUpdateRequest
{
    public bool ArtistInGoodStanding { get; set; }

    public int? ModeratorUserID { get; set; }
}

public class UserMembershipBanUpdateRequest
{
    public bool MembershipBanned { get; set; }

    public string? BannedReason { get; set; }

    public int? ModeratorUserID { get; set; }
}
