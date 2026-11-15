// <copyright file="EventController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private const int MaxManageListSize = 200;
        private const int MaxTitleLength = 255;
        private const int MaxDescriptionLength = 50_000;
        private const int MaxPointOfContactLength = 1000;

        private readonly TAGDBContext _context;

        public EventController(TAGDBContext context)
        {
            _context = context;
        }

        // Lists published events. With venueId and/or upcoming=true the list is narrowed, ordered by start time and
        // capped (50 by default, never more than 50), so a venue page can show what is coming up without loading everything.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents([FromQuery] int? venueId = null, [FromQuery] bool upcoming = false, [FromQuery] int? limit = null)
        {
            if (_context.Events == null)
            {
                return NotFound();
            }

            var query = _context.Events.Where(e => e.StatusID == EventStatus.Published);
            var narrowed = venueId.HasValue || upcoming;

            if (venueId.HasValue)
            {
                query = query.Where(e => e.VenueID == venueId.Value);
            }

            if (upcoming)
            {
                // An event that has started but not ended is still on.
                var now = DateTime.UtcNow;
                query = query.Where(e => e.EndTime >= now || e.StartTime >= now);
            }

            if (narrowed)
            {
                query = query.OrderBy(e => e.StartTime).Take(Math.Clamp(limit ?? 50, 1, 50));
            }

            var events = await query
                .Include(e => e.Artist)
                .Include(e => e.Venue)
                .Include(e => e.LogoPic)
                .ToListAsync();

            return Ok(events.Select(MapEventForApi));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            if (_context.Events == null)
            {
                return NotFound();
            }
            var @event = await _context.Events
                .Include(e => e.Artist)
                .Include(e => e.Venue)
                .Include(e => e.LogoPic)
                .FirstOrDefaultAsync(e => e.EventID == id && e.StatusID != EventStatus.ModerationBlocked);

            if (@event == null)
            {
                return NotFound();
            }

            var userId = VenueAccess.GetUserId(User);
            var canManage = userId.HasValue && await CanManageEventAsync(@event, userId.Value);

            // A draft or an event waiting for review is private to the people who manage it.
            if (!canManage && (@event.StatusID == EventStatus.Draft || @event.StatusID == EventStatus.PendingReview))
            {
                return NotFound();
            }

            return Ok(MapEventForApi(@event, canManage));
        }

        // Every event a venue's managers can see in the venue portal: the venue's own events in any state, plus other
        // people's events at the venue once they are published (those are view only). Staff see everything.
        [HttpGet("venue/{venueId:int}/manage")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetVenueEventsForManagement(int venueId)
        {
            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            if (!await VenueAccess.CanManageAsync(_context, venueId, userId.Value))
            {
                return Forbidden("You are not allowed to manage the events of this venue.");
            }

            var isStaff = await VenueAccess.IsStaffAsync(_context, userId.Value);
            var query = _context.Events.Where(e => e.VenueID == venueId);
            if (!isStaff)
            {
                query = query.Where(e => e.StatusID != EventStatus.ModerationBlocked && (e.ArtistID == null || e.StatusID == EventStatus.Published));
            }

            var ownedArtistIds = (await _context.Set<Linker_UserToArtist>()
                .AsNoTracking()
                .Where(l => l.UserID == userId.Value)
                .Select(l => l.ArtistID)
                .ToListAsync()).ToHashSet();

            var events = await query
                .OrderByDescending(e => e.StartTime)
                .Take(MaxManageListSize)
                .Include(e => e.Artist)
                .Include(e => e.Venue)
                .Include(e => e.LogoPic)
                .ToListAsync();

            return Ok(events.Select(e => MapEventForApi(e, isStaff || e.ArtistID == null || ownedArtistIds.Contains(e.ArtistID.Value))));
        }

        [HttpGet("path/{path}")]
        public async Task<ActionResult<Event>> GetEventByPath(string path)
        {
            if (_context.Events == null)
            {
                return NotFound();
            }
            var @event = await _context.Events
                .Include(e => e.Artist)
                .Include(e => e.Venue)
                .Include(e => e.LogoPic)
                .FirstOrDefaultAsync(e => e.Path == path && e.StatusID == EventStatus.Published);

            if (@event == null)
            {
                return NotFound();
            }

            return Ok(MapEventForApi(@event));
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutEvent(int id, Event @event)
        {
            if (id != @event.EventID)
            {
                return BadRequest("ID mismatch");
            }

            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var existing = await _context.Events.FirstOrDefaultAsync(e => e.EventID == id);
            if (existing == null)
            {
                return NotFound();
            }

            if (!await CanManageEventAsync(existing, userId.Value))
            {
                return Forbidden("You are not allowed to change this event.");
            }

            var isStaff = await VenueAccess.IsStaffAsync(_context, userId.Value);
            if (!isStaff && !IsEditable(existing.StatusID))
            {
                return Conflict(new { message = "This event can no longer be edited." });
            }

            // Moving an event to another venue needs the same permission as creating one there.
            if (@event.VenueID != existing.VenueID)
            {
                var denied = await CheckCanHostAsync(existing.ArtistID, @event.VenueID, userId.Value);
                if (denied != null)
                {
                    return denied;
                }
            }

            if (!string.IsNullOrWhiteSpace(@event.Path) && @event.Path != existing.Path)
            {
                if (await _context.Events.AnyAsync(e => e.Path == @event.Path && e.EventID != id))
                {
                    return Conflict(new { message = "That event path is already in use." });
                }

                existing.Path = @event.Path;
            }

            // Only staff can reassign an event to another artist or change its status. Everyone else changes the
            // status through publish, unpublish and cancel, which check the event first.
            if (isStaff)
            {
                existing.ArtistID = @event.ArtistID is > 0 ? @event.ArtistID : null;
                existing.StatusID = @event.StatusID;
            }

            existing.VenueID = @event.VenueID;
            existing.Cost = @event.Cost;
            existing.Description = @event.Description;
            existing.Description_Plaintext = @event.Description_Plaintext;
            existing.Doors = @event.Doors;
            existing.EndTime = @event.EndTime;
            existing.MaxOccupancy = @event.MaxOccupancy;
            existing.MinimumAge = @event.MinimumAge;
            existing.Note = @event.Note;
            existing.Note_Plaintext = @event.Note_Plaintext;
            existing.PointOfContact = @event.PointOfContact;
            existing.StartTime = @event.StartTime;
            existing.Title = @event.Title;
            existing.Title_Plaintext = @event.Title_Plaintext;
            existing.EventCategoryID = @event.EventCategoryID;
            existing.GalleryID = @event.GalleryID;
            existing.CoverPicID = @event.CoverPicID;
            existing.ProfilePicID = @event.ProfilePicID;
            existing.LogoPicID = @event.LogoPicID;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return Conflict(new { message = "The event could not be saved.", error = ex.InnerException?.Message ?? ex.Message });
            }

            return NoContent();
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Event>> PostEvent(Event @event)
        {
            if (_context.Events == null)
            {
                return Problem("Entity set 'TAGDBContext.Events' is null.");
            }

            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            // No artist means the venue hosts the event, which only the venue's owners (or staff) may do.
            var artistId = @event.ArtistID is > 0 ? @event.ArtistID : null;
            var denied = await CheckCanHostAsync(artistId, @event.VenueID, userId.Value);
            if (denied != null)
            {
                return denied;
            }

            if (await _context.Events.AnyAsync(e => e.Path == @event.Path))
            {
                return Conflict(new { message = "That event path is already in use." });
            }

            // Take only the event's own fields from the request, never related records or an id.
            @event.EventID = 0;
            @event.ArtistID = artistId;
            @event.Artist = null;
            @event.Venue = null!;
            @event.EventCategory = null;
            @event.Gallery = null;
            @event.CoverPic = null;
            @event.ProfilePic = null;
            @event.LogoPic = null;
            if (!await VenueAccess.IsStaffAsync(_context, userId.Value))
            {
                @event.StatusID = EventStatus.Draft;
            }

            _context.Events.Add(@event);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEvent", new { id = @event.EventID }, @event);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            if (_context.Events == null)
            {
                return NotFound();
            }

            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            if (!await CanManageEventAsync(@event, userId.Value))
            {
                return Forbidden("You are not allowed to delete this event.");
            }

            // Once an event has been public it is cancelled rather than deleted, so people who saw it can see what happened.
            if (@event.StatusID != EventStatus.Draft && !await VenueAccess.IsStaffAsync(_context, userId.Value))
            {
                return Conflict(new { message = "Only a draft can be deleted. Cancel the event instead." });
            }

            _context.Events.Remove(@event);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "This event is still used by other records, so it can't be deleted. Cancel it instead." });
            }

            return NoContent();
        }

        // Creates a draft hosted by the venue itself. Only the venue's owners (or staff) can do this. A draft stays
        // private until it is published.
        [HttpPost("venue/{venueId:int}")]
        [Authorize]
        public async Task<IActionResult> CreateVenueEvent(int venueId, [FromBody] EventDetailsRequest request)
        {
            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var denied = await CheckCanHostAsync(null, venueId, userId.Value);
            if (denied != null)
            {
                return denied;
            }

            var error = ValidateDetails(request, creating: true);
            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            var venueEvent = new Event
            {
                ArtistID = null,
                VenueID = venueId,
                StatusID = EventStatus.Draft,
                Path = await UniquePathAsync(request.Title!),
                Description = string.Empty,
                PointOfContact = string.Empty,
            };
            ApplyDetails(venueEvent, request);
            if (!request.Doors.HasValue)
            {
                venueEvent.Doors = venueEvent.StartTime;
            }

            _context.Events.Add(venueEvent);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return Conflict(new { message = "The event could not be saved.", error = ex.InnerException?.Message ?? ex.Message });
            }

            await WriteAuditAsync("Event created", "create", $"eventId={venueEvent.EventID};venueId={venueId};actingUserId={userId.Value}", userId.Value);
            return CreatedAtAction(nameof(GetEvent), new { id = venueEvent.EventID }, MapEventForApi(venueEvent, true));
        }

        // Changes the details of an event. Only fields that are sent change. A published event has to stay complete.
        [HttpPut("{id:int}/details")]
        [Authorize]
        public async Task<IActionResult> UpdateEventDetails(int id, [FromBody] EventDetailsRequest request)
        {
            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var @event = await _context.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventID == id);
            if (@event == null)
            {
                return NotFound();
            }

            if (!await CanManageEventAsync(@event, userId.Value))
            {
                return Forbidden("You are not allowed to change this event.");
            }

            if (!IsEditable(@event.StatusID) && !await VenueAccess.IsStaffAsync(_context, userId.Value))
            {
                return Conflict(new { message = "This event can no longer be edited." });
            }

            var error = ValidateDetails(request, creating: false);
            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            ApplyDetails(@event, request);
            if (@event.EndTime <= @event.StartTime)
            {
                return BadRequest(new { message = "The end time must be after the start time." });
            }

            if (@event.StatusID == EventStatus.Published)
            {
                var problems = GetPublishProblems(@event, checkNotEnded: false);
                if (problems.Count > 0)
                {
                    return BadRequest(new { message = "A published event must stay complete. Unpublish it first to leave these out.", problems });
                }
            }

            await _context.SaveChangesAsync();
            await WriteAuditAsync("Event updated", "update", $"eventId={id};venueId={@event.VenueID};actingUserId={userId.Value}", userId.Value);
            return Ok(MapEventForApi(@event, true));
        }

        // Makes a draft public, once it has the minimum information an event needs.
        [HttpPost("{id:int}/publish")]
        [Authorize]
        public Task<IActionResult> PublishEvent(int id) => ChangeStatusAsync(id, EventStatus.Published, "publish");

        // Takes a published event back to a draft.
        [HttpPost("{id:int}/unpublish")]
        [Authorize]
        public Task<IActionResult> UnpublishEvent(int id) => ChangeStatusAsync(id, EventStatus.Draft, "unpublish");

        // Marks an event cancelled. Staff can do this to any event, which is how a disputed event is pulled.
        [HttpPost("{id:int}/cancel")]
        [Authorize]
        public Task<IActionResult> CancelEvent(int id) => ChangeStatusAsync(id, EventStatus.Cancelled, "cancel");

        private async Task<IActionResult> ChangeStatusAsync(int id, EventStatus target, string operation)
        {
            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var @event = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Artist)
                .FirstOrDefaultAsync(e => e.EventID == id);
            if (@event == null)
            {
                return NotFound();
            }

            if (!await CanManageEventAsync(@event, userId.Value))
            {
                return Forbidden("You are not allowed to change this event.");
            }

            var from = @event.StatusID;
            if (from == target)
            {
                return Ok(MapEventForApi(@event, true));
            }

            var allowed = target switch
            {
                EventStatus.Published => from == EventStatus.Draft || from == EventStatus.PendingReview,
                EventStatus.Draft => from == EventStatus.Published || from == EventStatus.PendingReview,
                EventStatus.Cancelled => from == EventStatus.Draft || from == EventStatus.Published || from == EventStatus.PendingReview,
                _ => false,
            };
            if (!allowed)
            {
                return Conflict(new { message = $"An event that is {from.ToString().ToLowerInvariant()} can't be {operation}ed." });
            }

            if (target == EventStatus.Published)
            {
                var problems = GetPublishProblems(@event, checkNotEnded: true);
                if (problems.Count > 0)
                {
                    return BadRequest(new { message = "This event is not ready to publish.", problems });
                }
            }

            @event.StatusID = target;
            await _context.SaveChangesAsync();

            var isStaff = await VenueAccess.IsStaffAsync(_context, userId.Value);
            await WriteAuditAsync(
                $"Event {operation}",
                operation,
                $"eventId={id};venueId={@event.VenueID};from={from};to={target};staff={isStaff};actingUserId={userId.Value}",
                userId.Value);
            return Ok(MapEventForApi(@event, true));
        }

        /// <summary>
        /// Reserves a slug for a new event by creating a minimal event record
        /// </summary>
        /// <param name="request">The event slug reservation request</param>
        /// <returns>The created event with ID and slug</returns>
        [HttpPost("reserve-slug")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [Authorize]
        public async Task<ActionResult<EventSlugReservationResponse>> ReserveEventSlug([FromBody] EventSlugReservationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var artistId = request.ArtistID is > 0 ? request.ArtistID : null;
            var denied = await CheckCanHostAsync(artistId, request.VenueID, userId.Value);
            if (denied != null)
            {
                return denied;
            }

            if (await _context.Events.AnyAsync(e => e.Path == request.Slug))
            {
                return Conflict(new { message = "That event path is already in use." });
            }

            var minRequiredDate = DateTime.UtcNow.AddHours(1);

            var newEvent = new Event
            {
                Path = request.Slug,
                Title = request.Title ?? request.Slug,
                Description = request.Description ?? string.Empty,
                StartTime = request.StartTime ?? minRequiredDate,
                EndTime = request.EndTime ?? minRequiredDate.AddHours(2),
                Doors = request.Doors ?? minRequiredDate.AddMinutes(-30),
                MaxOccupancy = request.MaxOccupancy ?? 100,
                MinimumAge = request.MinimumAge ?? 0,
                PointOfContact = string.Empty,
                VenueID = request.VenueID,
                ArtistID = artistId
            };

            try
            {
                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                var response = new EventSlugReservationResponse
                {
                    EventID = newEvent.EventID,
                    Path = newEvent.Path,
                    Title = newEvent.Title
                };

                return CreatedAtAction(nameof(GetEvent), new { id = newEvent.EventID }, response);
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to reserve slug", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Updates only the slug/path for an existing event
        /// </summary>
        /// <param name="id">The event ID</param>
        /// <param name="request">The slug update request</param>
        /// <returns>The updated event</returns>
        [HttpPatch("{id}/update-slug")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [Authorize]
        public async Task<ActionResult<EventSlugReservationResponse>> UpdateEventSlug(int id, [FromBody] EventSlugUpdateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingEvent = await _context.Events.FindAsync(id);
            if (existingEvent == null)
            {
                return NotFound(new { message = $"Event with ID {id} not found" });
            }

            var userId = VenueAccess.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            if (!await CanManageEventAsync(existingEvent, userId.Value))
            {
                return Forbidden("You are not allowed to change this event.");
            }

            if (existingEvent.Path == request.Slug)
            {
                return Ok(new EventSlugReservationResponse
                {
                    EventID = existingEvent.EventID,
                    Path = existingEvent.Path,
                    Title = existingEvent.Title
                });
            }

            if (await _context.Events.AnyAsync(e => e.Path == request.Slug && e.EventID != id))
            {
                return Conflict(new { message = "That event path is already in use." });
            }

            existingEvent.Path = request.Slug;

            try
            {
                await _context.SaveChangesAsync();

                var response = new EventSlugReservationResponse
                {
                    EventID = existingEvent.EventID,
                    Path = existingEvent.Path,
                    Title = existingEvent.Title
                };

                return Ok(response);
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to update slug", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

        private bool EventExists(int id)
        {
            return (_context.Events?.Any(e => e.EventID == id)).GetValueOrDefault();
        }

        private ObjectResult Forbidden(string message) => StatusCode(StatusCodes.Status403Forbidden, new { message });

        private static bool IsEditable(EventStatus status) =>
            status == EventStatus.Draft || status == EventStatus.PendingReview || status == EventStatus.Published;

        // Who runs an event: an artist's event belongs to that artist's users. An event with no artist is hosted by its
        // venue, so the venue's owners run it. A venue's owners can see, but not change, an artist's event at the venue.
        // Staff can manage every event.
        private async Task<bool> CanManageEventAsync(Event @event, int userId)
        {
            if (await VenueAccess.IsStaffAsync(_context, userId))
            {
                return true;
            }

            return @event.ArtistID is int artistId
                ? await ArtistAccess.IsOwnerAsync(_context, artistId, userId)
                : await VenueAccess.IsOwnerAsync(_context, @event.VenueID, userId);
        }

        // Checks that the user may create an event for this artist at this venue. With no artist the venue hosts the
        // event. Returns the error to send, or null when it is allowed.
        private async Task<ActionResult?> CheckCanHostAsync(int? artistId, int venueId, int userId)
        {
            var venue = await _context.Set<Venue>()
                .AsNoTracking()
                .Where(v => v.VenueID == venueId)
                .Select(v => new { v.IsPublished, v.IsModerationBlocked })
                .FirstOrDefaultAsync();
            if (venue == null)
            {
                return BadRequest(new { message = "That venue does not exist." });
            }

            if (await VenueAccess.IsStaffAsync(_context, userId))
            {
                return null;
            }

            var ownsVenue = await VenueAccess.IsOwnerAsync(_context, venueId, userId);
            if (artistId.HasValue)
            {
                if (!await ArtistAccess.IsOwnerAsync(_context, artistId.Value, userId))
                {
                    return Forbidden("You are not allowed to create events for this artist.");
                }
            }
            else if (!ownsVenue)
            {
                return Forbidden("Only the venue's owners or staff can create events hosted by the venue.");
            }

            // An artist can tag any public venue. A venue that is still a draft is only visible to its own owners.
            if (venue.IsModerationBlocked || (!venue.IsPublished && !ownsVenue))
            {
                return BadRequest(new { message = "That venue does not exist." });
            }

            if (artistId.HasValue && !await _context.Artists.AnyAsync(a => a.ArtistID == artistId.Value))
            {
                return BadRequest(new { message = "That artist does not exist." });
            }

            return null;
        }

        // What an event needs before it can be public. The venue has to be public too.
        private static List<string> GetPublishProblems(Event @event, bool checkNotEnded)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(CoalescePlaintext(@event.Title_Plaintext, @event.Title)))
            {
                problems.Add("Add a title.");
            }

            if (string.IsNullOrWhiteSpace(CoalescePlaintext(@event.Description_Plaintext, @event.Description)))
            {
                problems.Add("Add a description.");
            }

            if (string.IsNullOrWhiteSpace(@event.PointOfContact))
            {
                problems.Add("Add a point of contact.");
            }

            if (@event.EndTime <= @event.StartTime)
            {
                problems.Add("The end time must be after the start time.");
            }

            if (@event.Doors > @event.StartTime)
            {
                problems.Add("Doors must open at or before the start time.");
            }

            // Event times are local wall-clock times, so allow for the furthest time zone before calling an event over.
            if (checkNotEnded && @event.EndTime < DateTime.UtcNow.AddHours(-14))
            {
                problems.Add("The event has already ended.");
            }

            if (@event.Venue != null && (!@event.Venue.IsPublished || @event.Venue.IsModerationBlocked))
            {
                problems.Add("Publish the venue before publishing its events.");
            }

            return problems;
        }

        private static string? ValidateDetails(EventDetailsRequest request, bool creating)
        {
            if (request == null)
            {
                return "Send the event details.";
            }

            if (creating)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return "A title is required.";
                }

                if (!request.StartTime.HasValue || !request.EndTime.HasValue)
                {
                    return "A start time and an end time are required.";
                }
            }

            if (request.Title != null)
            {
                var title = request.Title.Trim();
                if (title.Length == 0)
                {
                    return "The title can't be empty.";
                }

                if (title.Length > MaxTitleLength)
                {
                    return $"The title can be at most {MaxTitleLength} characters.";
                }
            }

            if (request.Description != null && request.Description.Length > MaxDescriptionLength)
            {
                return "The description is too long.";
            }

            if (request.PointOfContact != null && request.PointOfContact.Trim().Length > MaxPointOfContactLength)
            {
                return $"The point of contact can be at most {MaxPointOfContactLength} characters.";
            }

            if (request.Cost is < 0 or > 1_000_000)
            {
                return "The cost must be between 0 and 1,000,000.";
            }

            if (request.MinimumAge is < 0 or > 120)
            {
                return "The minimum age must be between 0 and 120.";
            }

            if (request.MaxOccupancy is < 0 or > 1_000_000)
            {
                return "The maximum occupancy must be between 0 and 1,000,000.";
            }

            foreach (var time in new[] { request.StartTime, request.EndTime, request.Doors })
            {
                if (time.HasValue && (time.Value.Year < 2000 || time.Value.Year > 2100))
                {
                    return "That date is not valid.";
                }
            }

            if (request.StartTime.HasValue && request.EndTime.HasValue && ToUtc(request.EndTime.Value) <= ToUtc(request.StartTime.Value))
            {
                return "The end time must be after the start time.";
            }

            return null;
        }

        // Copies the fields that were sent. The title is stored as safe markup with its plain text beside it, like the
        // other titles; the description arrives as rich text and its plain text is worked out here.
        private static void ApplyDetails(Event @event, EventDetailsRequest request)
        {
            if (request.Title != null)
            {
                var title = request.Title.Trim();
                @event.Title = WebUtility.HtmlEncode(title);
                @event.Title_Plaintext = title;
            }

            if (request.Description != null)
            {
                @event.Description = request.Description.Trim();
                @event.Description_Plaintext = StripHtmlToPlaintext(@event.Description) ?? string.Empty;
            }

            if (request.PointOfContact != null)
            {
                @event.PointOfContact = request.PointOfContact.Trim();
            }

            if (request.StartTime.HasValue)
            {
                @event.StartTime = ToUtc(request.StartTime.Value);
            }

            if (request.EndTime.HasValue)
            {
                @event.EndTime = ToUtc(request.EndTime.Value);
            }

            if (request.Doors.HasValue)
            {
                @event.Doors = ToUtc(request.Doors.Value);
            }

            if (request.Cost.HasValue)
            {
                @event.Cost = request.Cost.Value;
            }

            if (request.MinimumAge.HasValue)
            {
                @event.MinimumAge = request.MinimumAge.Value;
            }

            if (request.MaxOccupancy.HasValue)
            {
                @event.MaxOccupancy = request.MaxOccupancy.Value;
            }
        }

        // The database stores UTC. A time with no zone is taken as UTC, which is how the site shows event times.
        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        private async Task<string> UniquePathAsync(string title)
        {
            var slug = Regex.Replace(title.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug.Length > 60)
            {
                slug = slug[..60].Trim('-');
            }

            var baseSlug = slug.Length == 0 ? "event" : slug;
            var candidate = baseSlug;
            for (var attempt = 2; await _context.Events.AnyAsync(e => e.Path == candidate); attempt++)
            {
                candidate = attempt <= 50 ? $"{baseSlug}-{attempt}" : $"{baseSlug}-{Guid.NewGuid():N}";
            }

            return candidate;
        }

        private async Task WriteAuditAsync(string shortText, string operation, string loggedData, int userId)
        {
            try
            {
                _context.Set<Log>().Add(new Log
                {
                    ShortText = shortText,
                    Tags = $"scope=audit;entity=event;event=lifecycle;operation={operation};result=success;channel=db",
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

        private static object MapEventForApi(Event @event) => MapEventForApi(@event, null);

        private static object MapEventForApi(Event @event, bool? canManage)
        {
            return new
            {
                CanManage = canManage,
                @event.EventID,
                @event.ArtistID,
                @event.Cost,
                Description = CoalescePlaintext(@event.Description_Plaintext, @event.Description),
                DescriptionRichtext = @event.Description,
                @event.Doors,
                @event.EndTime,
                @event.MaxOccupancy,
                @event.MinimumAge,
                Note = CoalescePlaintext(@event.Note_Plaintext, @event.Note),
                NoteRichtext = @event.Note,
                @event.PointOfContact,
                @event.StartTime,
                Title = CoalescePlaintext(@event.Title_Plaintext, @event.Title),
                TitleRichtext = @event.Title,
                @event.VenueID,
                @event.Path,
                @event.EventCategoryID,
                @event.GalleryID,
                @event.CoverPicID,
                @event.ProfilePicID,
                @event.LogoPicID,
                @event.Artist,
                @event.EventCategory,
                @event.Venue,
                @event.Gallery,
                @event.CoverPic,
                @event.ProfilePic,
                @event.LogoPic,
                @event.StatusID,
                Status = @event.StatusID.ToString(),
            };
        }

        private static string? CoalescePlaintext(string? plaintext, string? richtext)
        {
            return !string.IsNullOrWhiteSpace(plaintext)
                ? plaintext
                : StripHtmlToPlaintext(richtext);
        }

        private static string? StripHtmlToPlaintext(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var withoutScripts = Regex.Replace(value, "(?is)<(script|style)[^>]*>.*?</\\1>", " ");
            var withBreaks = Regex.Replace(withoutScripts, "(?i)<br\\s*/?>", "\n");
            var withBlockBreaks = Regex.Replace(withBreaks, "(?i)</(p|div|li|h[1-6]|tr|section|article|blockquote)>", "\n");
            var noTags = Regex.Replace(withBlockBreaks, "(?is)<[^>]+>", " ");
            var normalized = Regex.Replace(noTags, @"[ \t\r]+", " ");
            normalized = Regex.Replace(normalized, @"\n\s*\n+", "\n");
            var decoded = normalized
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");

            var trimmed = decoded.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }

    public class EventSlugReservationRequest
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Slug { get; set; }

        [StringLength(255)]
        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public DateTime? Doors { get; set; }

        public int? MaxOccupancy { get; set; }

        public int? MinimumAge { get; set; }

        [Required]
        public int VenueID { get; set; }

        // Leave this out for an event the venue hosts itself.
        public int? ArtistID { get; set; }
    }

    public class EventDetailsRequest
    {
        public string? Title { get; set; }

        // Rich text (HTML).
        public string? Description { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public DateTime? Doors { get; set; }

        public decimal? Cost { get; set; }

        public int? MinimumAge { get; set; }

        public int? MaxOccupancy { get; set; }

        public string? PointOfContact { get; set; }
    }

    public class EventSlugUpdateRequest
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Slug { get; set; }
    }

    public class EventSlugReservationResponse
    {
        public int EventID { get; set; }
        public string Path { get; set; }
        public string Title { get; set; }
    }
}
