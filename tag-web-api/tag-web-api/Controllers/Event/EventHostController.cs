// <copyright file="EventHostController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers
{
    /// <summary>
    /// The collaborators of an event and their roles. Anyone involved in the event can see the list; only the owner (and
    /// staff) can change it. The owner is not in the list: it is the artist or venue the event belongs to.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/event/{eventId:int}/hosts")]
    public class EventHostController : ControllerBase
    {
        private const int MaxHostsPerEvent = 50;

        private readonly TAGDBContext _context;
        private readonly ILogger<EventHostController>? _logger;

        public EventHostController(TAGDBContext context, ILogger<EventHostController>? logger = null)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetHosts(int eventId)
        {
            var (@event, role, failure) = await LoadAsync(eventId, EventAction.View);
            if (failure != null)
            {
                return failure;
            }

            var hosts = await _context.EventHosts.AsNoTracking()
                .Where(h => h.EventID == eventId)
                .OrderBy(h => h.EventHostID)
                .ToListAsync();

            var rows = new List<object>();
            foreach (var host in hosts)
            {
                rows.Add(new
                {
                    host.EventHostID,
                    host.HostType,
                    host.HostID,
                    Name = await NameOfAsync(host.HostType, host.HostID),
                    Role = host.Role.ToString(),
                });
            }

            var owner = @event!.ArtistID is int artistId
                ? new { HostType = EventHost.ArtistType, HostID = artistId, Name = await NameOfAsync(EventHost.ArtistType, artistId) }
                : new { HostType = EventHost.VenueType, HostID = @event.VenueID, Name = await NameOfAsync(EventHost.VenueType, @event.VenueID) };

            return Ok(new
            {
                MyRole = role.ToString(),
                CanManageHosts = EventAccess.Allows(role, EventAction.ManageHosts),
                Owner = owner,
                Hosts = rows,
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddHost(int eventId, [FromBody] EventHostRequest request)
        {
            var (@event, _, failure) = await LoadAsync(eventId, EventAction.ManageHosts);
            if (failure != null)
            {
                return failure;
            }

            var error = await ValidateAsync(request, @event!, requireTarget: true);
            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            var hostType = EventHost.HostTypes.First(t => string.Equals(t, request.HostType, StringComparison.OrdinalIgnoreCase));
            var hostId = request.HostID!.Value;
            if (await _context.EventHosts.AnyAsync(h => h.EventID == eventId && h.HostType == hostType && h.HostID == hostId))
            {
                return Conflict(new { message = "That host is already on this event. Change their role instead." });
            }

            if (await _context.EventHosts.CountAsync(h => h.EventID == eventId) >= MaxHostsPerEvent)
            {
                return BadRequest(new { message = $"An event can have at most {MaxHostsPerEvent} hosts." });
            }

            var userId = VenueAccess.GetUserId(User)!.Value;
            var host = new EventHost
            {
                EventID = eventId,
                HostType = hostType,
                HostID = hostId,
                Role = request.Role!.Value,
                CreatedByUserID = userId,
            };
            _context.EventHosts.Add(host);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Failed to add a host to event {EventId}", eventId);
                return Conflict(new { message = "The host could not be saved." });
            }

            await WriteAuditAsync("Event host added", "host-add", $"eventId={eventId};hostType={hostType};hostId={hostId};role={host.Role};actingUserId={userId}", userId);
            return Created($"api/event/{eventId}/hosts", new { host.EventHostID, host.HostType, host.HostID, Role = host.Role.ToString() });
        }

        [HttpPut("{hostRowId:int}")]
        public async Task<IActionResult> ChangeRole(int eventId, int hostRowId, [FromBody] EventHostRequest request)
        {
            var (@event, _, failure) = await LoadAsync(eventId, EventAction.ManageHosts);
            if (failure != null)
            {
                return failure;
            }

            var error = await ValidateAsync(request, @event!, requireTarget: false);
            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            var host = await _context.EventHosts.FirstOrDefaultAsync(h => h.EventHostID == hostRowId && h.EventID == eventId);
            if (host == null)
            {
                return NotFound();
            }

            host.Role = request.Role!.Value;
            await _context.SaveChangesAsync();

            var userId = VenueAccess.GetUserId(User)!.Value;
            await WriteAuditAsync("Event host role changed", "host-role", $"eventId={eventId};hostRowId={hostRowId};role={host.Role};actingUserId={userId}", userId);
            return Ok(new { host.EventHostID, host.HostType, host.HostID, Role = host.Role.ToString() });
        }

        [HttpDelete("{hostRowId:int}")]
        public async Task<IActionResult> RemoveHost(int eventId, int hostRowId)
        {
            var (_, _, failure) = await LoadAsync(eventId, EventAction.ManageHosts);
            if (failure != null)
            {
                return failure;
            }

            var host = await _context.EventHosts.FirstOrDefaultAsync(h => h.EventHostID == hostRowId && h.EventID == eventId);
            if (host == null)
            {
                return NotFound();
            }

            _context.EventHosts.Remove(host);
            await _context.SaveChangesAsync();

            var userId = VenueAccess.GetUserId(User)!.Value;
            await WriteAuditAsync("Event host removed", "host-remove", $"eventId={eventId};hostType={host.HostType};hostId={host.HostID};actingUserId={userId}", userId);
            return NoContent();
        }

        // Loads the event and checks the caller may do the action. An event that does not exist, or that the caller has
        // nothing to do with, is reported the same way, so this can't be used to find out which events exist.
        private async Task<(Event? Event, EventRole Role, IActionResult? Failure)> LoadAsync(int eventId, EventAction action)
        {
            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return (null, EventRole.None, Unauthorized());
            }

            var @event = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == eventId);
            if (@event == null)
            {
                return (null, EventRole.None, NotFound());
            }

            var role = await EventAccess.GetRoleAsync(_context, @event, userId.Value);
            if (!EventAccess.Allows(role, EventAction.View))
            {
                return (null, role, NotFound());
            }

            if (!EventAccess.Allows(role, action))
            {
                return (null, role, StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to change who hosts this event." }));
            }

            if (@event.StatusID == EventStatus.ModerationBlocked && !await VenueAccess.IsStaffAsync(_context, userId.Value))
            {
                return (null, role, NotFound());
            }

            return (@event, role, null);
        }

        private async Task<string?> ValidateAsync(EventHostRequest? request, Event @event, bool requireTarget)
        {
            if (request?.Role == null || !EventHost.IsAssignable(request.Role.Value))
            {
                return "Choose a role: Admin, Staff, CheckIn or ReadOnly.";
            }

            if (!requireTarget)
            {
                return null;
            }

            var hostType = EventHost.HostTypes.FirstOrDefault(t => string.Equals(t, request.HostType, StringComparison.OrdinalIgnoreCase));
            if (hostType == null)
            {
                return "The host must be a User, an Artist or a Venue.";
            }

            if (request.HostID is not > 0)
            {
                return "Choose who to add.";
            }

            var id = request.HostID.Value;
            var exists = hostType switch
            {
                EventHost.UserType => await _context.Users.AnyAsync(u => u.UserID == id),
                EventHost.ArtistType => await _context.Artists.AnyAsync(a => a.ArtistID == id),
                _ => await _context.Set<Venue>().AnyAsync(v => v.VenueID == id && !v.IsModerationBlocked),
            };
            if (!exists)
            {
                return $"That {hostType.ToLowerInvariant()} does not exist.";
            }

            // The owner already has full control; adding it again would only confuse who owns the event.
            var isOwner = (hostType == EventHost.ArtistType && @event.ArtistID == id)
                || (hostType == EventHost.VenueType && @event.ArtistID == null && @event.VenueID == id);
            return isOwner ? "That is the owner of the event." : null;
        }

        private async Task<string> NameOfAsync(string hostType, int hostId)
        {
            var name = hostType switch
            {
                EventHost.UserType => await _context.Users.AsNoTracking().Where(u => u.UserID == hostId).Select(u => u.Username).FirstOrDefaultAsync(),
                EventHost.ArtistType => await _context.Artists.AsNoTracking().Where(a => a.ArtistID == hostId).Select(a => a.Title_Plaintext ?? a.Title).FirstOrDefaultAsync(),
                _ => await _context.Set<Venue>().AsNoTracking().Where(v => v.VenueID == hostId).Select(v => v.Name).FirstOrDefaultAsync(),
            };
            return name ?? string.Empty;
        }

        private async Task WriteAuditAsync(string shortText, string operation, string loggedData, int userId)
        {
            try
            {
                _context.Set<Log>().Add(new Log
                {
                    ShortText = shortText,
                    Tags = $"scope=audit;entity=event;event=host;operation={operation};result=success;channel=db",
                    Critical = false,
                    LoggedData = loggedData,
                    UserID = userId,
                    LogTimestamp = DateTime.UtcNow,
                });
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The change itself is already saved; a missing audit row must not turn it into an error.
            }
        }
    }

    public class EventHostRequest
    {
        // User, Artist or Venue. Not needed when only the role changes.
        public string? HostType { get; set; }

        public int? HostID { get; set; }

        // Admin, Staff, CheckIn or ReadOnly, by name or number.
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public EventRole? Role { get; set; }
    }
}
