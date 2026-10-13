// <copyright file="LogoController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LogoController : ControllerBase
{
    private readonly TAGDBContext _context;
    private readonly IConfiguration _configuration;

    public LogoController(TAGDBContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("{entityType}/{entityId}")]
    public async Task<ActionResult<object>> GetActiveLogo(string entityType, int entityId)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return BadRequest(new { message = "Invalid entity type. Supported values: artist, vendor, venue, event." });
        }

        var entity = await LoadEntityAsync(normalizedEntityType, entityId).ConfigureAwait(false);
        if (entity == null)
        {
            return NotFound(new { message = $"{normalizedEntityType} #{entityId} was not found." });
        }

        var pictureId = GetCurrentLogoPictureId(entity);
        var picture = pictureId.HasValue
            ? await _context.Pictures.AsNoTracking().FirstOrDefaultAsync(p => p.PictureID == pictureId.Value).ConfigureAwait(false)
            : null;

        return Ok(new
        {
            entityType = normalizedEntityType,
            entityId,
            logoPic = picture,
            logoPicID = pictureId,
        });
    }

    [HttpGet("{entityType}/{entityId}/history")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<LogoHistoryDto>>> GetLogoHistory(string entityType, int entityId)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return BadRequest(new { message = "Invalid entity type. Supported values: artist, vendor, venue, event." });
        }

        var entity = await LoadEntityAsync(normalizedEntityType, entityId).ConfigureAwait(false);
        if (entity == null)
        {
            return NotFound(new { message = $"{normalizedEntityType} #{entityId} was not found." });
        }

        var history = await _context.LogoHistory
            .AsNoTracking()
            .Where(h => h.EntityType == normalizedEntityType && h.EntityID == entityId)
            .OrderByDescending(h => h.CreatedUtc)
            .Select(h => new LogoHistoryDto
            {
                LogoHistoryID = h.LogoHistoryID,
                EntityType = h.EntityType,
                EntityID = h.EntityID,
                PictureID = h.PictureID,
                IsActive = h.IsActive,
                IsArchived = h.IsArchived,
                CreatedUtc = h.CreatedUtc,
                ArchivedUtc = h.ArchivedUtc,
                RestoredUtc = h.RestoredUtc,
                Title = h.Picture.Title,
                Url = h.Picture.URL,
                NormalizedUrl = h.Picture.NormalizedURL ?? h.Picture.URL,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return Ok(history);
    }

    [HttpPost("{entityType}/{entityId}")]
    [Authorize]
    public async Task<ActionResult<object>> AssignLogo(string entityType, int entityId, [FromBody] LogoAssociationRequest request)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return BadRequest(new { message = "Invalid entity type. Supported values: artist, vendor, venue, event." });
        }

        if (request == null || request.PictureID <= 0)
        {
            return BadRequest(new { message = "A valid PictureID is required." });
        }

        var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
        if (!userId.HasValue || !await IsAuthorizedToManageLogoAsync(normalizedEntityType, entityId, userId.Value).ConfigureAwait(false))
        {
            return Forbid();
        }

        var picture = await _context.Pictures.FirstOrDefaultAsync(p => p.PictureID == request.PictureID).ConfigureAwait(false);
        if (picture == null)
        {
            return NotFound(new { message = $"Picture #{request.PictureID} does not exist." });
        }

        if (!IsValidLogoImage(picture, request.ContentType, request.FileSizeBytes))
        {
            return BadRequest(new { message = "The selected logo must be an image file under the configured size limit." });
        }

        var entity = await LoadEntityForUpdateAsync(normalizedEntityType, entityId).ConfigureAwait(false);
        if (entity == null)
        {
            return NotFound(new { message = $"{normalizedEntityType} #{entityId} was not found." });
        }

        var now = DateTime.UtcNow;
        var currentPictureId = GetCurrentLogoPictureId(entity);

        if (currentPictureId.HasValue && currentPictureId.Value == request.PictureID)
        {
            var existingActiveHistory = await _context.LogoHistory
                .FirstOrDefaultAsync(h => h.EntityType == normalizedEntityType && h.EntityID == entityId && h.IsActive && h.PictureID == request.PictureID)
                .ConfigureAwait(false);

            if (existingActiveHistory == null)
            {
                _context.LogoHistory.Add(new LogoHistory
                {
                    EntityType = normalizedEntityType,
                    EntityID = entityId,
                    PictureID = request.PictureID,
                    IsActive = true,
                    CreatedUtc = now,
                });
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
            return Ok(new { entityType = normalizedEntityType, entityId, logoPicID = request.PictureID, message = "The selected logo is already active." });
        }

        var previousActiveHistory = await _context.LogoHistory
            .Where(h => h.EntityType == normalizedEntityType && h.EntityID == entityId && h.IsActive)
            .OrderByDescending(h => h.CreatedUtc)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (previousActiveHistory != null)
        {
            previousActiveHistory.IsActive = false;
            previousActiveHistory.IsArchived = true;
            previousActiveHistory.ArchivedUtc = now;
        }
        else if (currentPictureId.HasValue)
        {
            _context.LogoHistory.Add(new LogoHistory
            {
                EntityType = normalizedEntityType,
                EntityID = entityId,
                PictureID = currentPictureId.Value,
                IsActive = false,
                IsArchived = true,
                ArchivedUtc = now,
                CreatedUtc = now,
            });
        }

        var historyEntry = new LogoHistory
        {
            EntityType = normalizedEntityType,
            EntityID = entityId,
            PictureID = request.PictureID,
            IsActive = true,
            IsArchived = false,
            CreatedUtc = now,
            PreviousLogoHistoryID = previousActiveHistory?.LogoHistoryID,
        };

        _context.LogoHistory.Add(historyEntry);
        SetCurrentLogoPictureId(entity, request.PictureID);

        await _context.SaveChangesAsync().ConfigureAwait(false);

        return Ok(new
        {
            entityType = normalizedEntityType,
            entityId,
            logoPicID = request.PictureID,
            replacedPreviousLogoPicID = currentPictureId,
            historyID = historyEntry.LogoHistoryID,
        });
    }

    [HttpPost("{entityType}/{entityId}/restore/{historyId}")]
    [Authorize]
    public async Task<ActionResult<object>> RestoreLogo(string entityType, int entityId, int historyId)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return BadRequest(new { message = "Invalid entity type. Supported values: artist, vendor, venue, event." });
        }

        var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
        if (!userId.HasValue || !await IsAuthorizedToManageLogoAsync(normalizedEntityType, entityId, userId.Value).ConfigureAwait(false))
        {
            return Forbid();
        }

        var history = await _context.LogoHistory
            .FirstOrDefaultAsync(h => h.LogoHistoryID == historyId && h.EntityType == normalizedEntityType && h.EntityID == entityId)
            .ConfigureAwait(false);

        if (history == null)
        {
            return NotFound(new { message = "Logo history record was not found." });
        }

        var entity = await LoadEntityForUpdateAsync(normalizedEntityType, entityId).ConfigureAwait(false);
        if (entity == null)
        {
            return NotFound(new { message = $"{normalizedEntityType} #{entityId} was not found." });
        }

        var currentPictureId = GetCurrentLogoPictureId(entity);
        var activeHistory = await _context.LogoHistory
            .Where(h => h.EntityType == normalizedEntityType && h.EntityID == entityId && h.IsActive)
            .OrderByDescending(h => h.CreatedUtc)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (activeHistory != null && activeHistory.LogoHistoryID != history.LogoHistoryID)
        {
            activeHistory.IsActive = false;
            activeHistory.IsArchived = true;
            activeHistory.ArchivedUtc = DateTime.UtcNow;
        }

        history.IsActive = true;
        history.IsArchived = false;
        history.RestoredUtc = DateTime.UtcNow;
        SetCurrentLogoPictureId(entity, history.PictureID);

        await _context.SaveChangesAsync().ConfigureAwait(false);

        return Ok(new
        {
            entityType = normalizedEntityType,
            entityId,
            restoredLogoHistoryID = history.LogoHistoryID,
            logoPicID = history.PictureID,
            previousLogoPicID = currentPictureId,
        });
    }

    [HttpDelete("{entityType}/{entityId}")]
    [Authorize]
    public async Task<IActionResult> ArchiveLogo(string entityType, int entityId)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        if (normalizedEntityType == null)
        {
            return BadRequest(new { message = "Invalid entity type. Supported values: artist, vendor, venue, event." });
        }

        var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
        if (!userId.HasValue || !await IsAuthorizedToManageLogoAsync(normalizedEntityType, entityId, userId.Value).ConfigureAwait(false))
        {
            return Forbid();
        }

        var entity = await LoadEntityForUpdateAsync(normalizedEntityType, entityId).ConfigureAwait(false);
        if (entity == null)
        {
            return NotFound(new { message = $"{normalizedEntityType} #{entityId} was not found." });
        }

        var currentPictureId = GetCurrentLogoPictureId(entity);
        if (!currentPictureId.HasValue)
        {
            return Ok(new { message = "No active logo is currently associated with this entity." });
        }

        var activeHistory = await _context.LogoHistory
            .Where(h => h.EntityType == normalizedEntityType && h.EntityID == entityId && h.IsActive)
            .OrderByDescending(h => h.CreatedUtc)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (activeHistory != null)
        {
            activeHistory.IsActive = false;
            activeHistory.IsArchived = true;
            activeHistory.ArchivedUtc = DateTime.UtcNow;
        }

        SetCurrentLogoPictureId(entity, null);
        await _context.SaveChangesAsync().ConfigureAwait(false);

        return NoContent();
    }

    [HttpPost("{entityType}/{entityId}/archive")]
    [Authorize]
    public Task<IActionResult> ArchiveLogoAlias(string entityType, int entityId)
    {
        return ArchiveLogo(entityType, entityId);
    }

    private static bool IsValidLogoImage(Picture picture, string? contentType, long? fileSizeBytes)
    {
        var maxBytes = 5 * 1024 * 1024;

        if (fileSizeBytes.HasValue && fileSizeBytes.Value > maxBytes)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var url = picture.URL ?? picture.NormalizedURL ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var extension = Path.GetExtension(url).TrimStart('.');
        return extension.Equals("png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("svg", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int?> GetCurrentUserIdAsync()
    {
        if (User == null)
        {
            return null;
        }

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserID")
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userIdValue) || !int.TryParse(userIdValue, out var parsedUserId))
        {
            return null;
        }

        return parsedUserId;
    }

    private async Task<bool> IsAuthorizedToManageLogoAsync(string entityType, int entityId, int userId)
    {
        var isModerator = await _context.Users.AsNoTracking().AnyAsync(u => u.UserID == userId && u.Moderator).ConfigureAwait(false);
        if (isModerator)
        {
            return true;
        }

        return entityType switch
        {
            "artist" => await _context.Set<Linker_UserToArtist>().AsNoTracking().AnyAsync(l => l.UserID == userId && l.ArtistID == entityId).ConfigureAwait(false),
            "event" => await _context.Events.AsNoTracking().AnyAsync(e => e.EventID == entityId && _context.Set<Linker_UserToArtist>().AsNoTracking().Any(l => l.UserID == userId && l.ArtistID == e.ArtistID)).ConfigureAwait(false),
            _ => false,
        };
    }

    private async Task<object?> LoadEntityAsync(string entityType, int entityId)
    {
        return entityType switch
        {
            "artist" => await _context.Artists.AsNoTracking().Include(a => a.LogoPic).FirstOrDefaultAsync(a => a.ArtistID == entityId).ConfigureAwait(false),
            "vendor" => await _context.Vendors.AsNoTracking().Include(v => v.LogoPic).FirstOrDefaultAsync(v => v.VendorID == entityId).ConfigureAwait(false),
            "venue" => await _context.Venues.AsNoTracking().Include(v => v.LogoPic).FirstOrDefaultAsync(v => v.VenueID == entityId).ConfigureAwait(false),
            "event" => await _context.Events.AsNoTracking().Include(e => e.LogoPic).FirstOrDefaultAsync(e => e.EventID == entityId).ConfigureAwait(false),
            _ => null,
        };
    }

    private async Task<object?> LoadEntityForUpdateAsync(string entityType, int entityId)
    {
        return entityType switch
        {
            "artist" => await _context.Artists.Include(a => a.LogoPic).FirstOrDefaultAsync(a => a.ArtistID == entityId).ConfigureAwait(false),
            "vendor" => await _context.Vendors.Include(v => v.LogoPic).FirstOrDefaultAsync(v => v.VendorID == entityId).ConfigureAwait(false),
            "venue" => await _context.Venues.Include(v => v.LogoPic).FirstOrDefaultAsync(v => v.VenueID == entityId).ConfigureAwait(false),
            "event" => await _context.Events.Include(e => e.LogoPic).FirstOrDefaultAsync(e => e.EventID == entityId).ConfigureAwait(false),
            _ => null,
        };
    }

    private static int? GetCurrentLogoPictureId(object entity)
    {
        return entity switch
        {
            Artist artist => artist.LogoPicID,
            Vendor vendor => vendor.LogoPicID,
            Venue venue => venue.LogoPicID,
            Event @event => @event.LogoPicID,
            _ => null,
        };
    }

    private static void SetCurrentLogoPictureId(object entity, int? pictureId)
    {
        switch (entity)
        {
            case Artist artist:
                artist.LogoPicID = pictureId;
                break;
            case Vendor vendor:
                vendor.LogoPicID = pictureId;
                break;
            case Venue venue:
                venue.LogoPicID = pictureId;
                break;
            case Event @event:
                @event.LogoPicID = pictureId;
                break;
        }
    }

    private static string? NormalizeEntityType(string? entityType)
    {
        var normalized = entityType?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.ToLowerInvariant() switch
        {
            "artist" => "artist",
            "vendor" => "vendor",
            "venue" => "venue",
            "event" => "event",
            _ => null,
        };
    }

    public class LogoAssociationRequest
    {
        public int PictureID { get; set; }

        public string? ContentType { get; set; }

        public long? FileSizeBytes { get; set; }

        public string? Reason { get; set; }
    }

    public class LogoHistoryDto
    {
        public int LogoHistoryID { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public int EntityID { get; set; }

        public int PictureID { get; set; }

        public bool IsActive { get; set; }

        public bool IsArchived { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? ArchivedUtc { get; set; }

        public DateTime? RestoredUtc { get; set; }

        public string? Title { get; set; }

        public string? Url { get; set; }

        public string? NormalizedUrl { get; set; }
    }
}
