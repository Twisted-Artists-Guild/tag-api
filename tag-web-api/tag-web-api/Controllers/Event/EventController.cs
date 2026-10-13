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
        private readonly TAGDBContext _context;

        public EventController(TAGDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
        {
            if (_context.Events == null)
            {
                return NotFound();
            }
            var events = await _context.Events
                .Where(e => e.StatusID == EventStatus.Published)
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

            return Ok(MapEventForApi(@event));
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

            _context.Entry(@event).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
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
            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();

            return NoContent();
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
                VenueID = request.VenueID,
                ArtistID = request.ArtistID
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

            if (existingEvent.Path == request.Slug)
            {
                return Ok(new EventSlugReservationResponse
                {
                    EventID = existingEvent.EventID,
                    Path = existingEvent.Path,
                    Title = existingEvent.Title
                });
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

        private static object MapEventForApi(Event @event)
        {
            return new
            {
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

        [Required]
        public int ArtistID { get; set; }
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
