// <copyright file="VenueController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VenueController : ControllerBase
    {
        private static readonly Regex ValidSlugRegex = new Regex(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);
        private readonly TAGDBContext context;
        private readonly ILogger<VenueController> logger;

        public VenueController(TAGDBContext context, ILogger<VenueController> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Venue>>> GetVenues()
        {
            var venues = await this.context.Set<Venue>()
                .Where(v => v.IsPublished && !v.IsModerationBlocked)
                .Include(v => v.LogoPic)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(venues.Select(MapVenueForApi));
        }

        [HttpGet("admin/unpublished")]
        public async Task<ActionResult<IEnumerable<Venue>>> GetUnpublishedVenues([FromQuery] int moderatorUserId)
        {
            if (!await this.IsModeratorAsync(moderatorUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            var venues = await this.context.Set<Venue>()
                .Where(v => !v.IsPublished || v.IsModerationBlocked)
                .Include(v => v.LogoPic)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(venues.Select(MapVenueForApi));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Venue>> GetVenue(int id)
        {
            var venue = await this.context.Set<Venue>()
                .Include(v => v.LogoPic)
                .FirstOrDefaultAsync(v => v.VenueID == id)
                .ConfigureAwait(false);

            if (venue == null || !venue.IsPublished || venue.IsModerationBlocked)
            {
                return this.NotFound();
            }

            return this.Ok(MapVenueForApi(venue));
        }

        [HttpGet("byID/{id}")]
        public async Task<ActionResult<Venue>> GetVenueById(int id)
        {
            var venue = await this.context.Set<Venue>()
                .Include(v => v.LogoPic)
                .FirstOrDefaultAsync(v => v.VenueID == id)
                .ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound();
            }

            return this.Ok(MapVenueForApi(venue));
        }

        [HttpGet("check-slug/{slug}")]
        public async Task<ActionResult<object>> CheckVenueSlug(string slug, [FromQuery] int? excludeId = null)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest(new { available = false, message = "Invalid slug format." });
            }

            var isUnique = await this.IsVenueSlugUniqueAsync(normalizedSlug, excludeId).ConfigureAwait(false);
            if (!isUnique)
            {
                return this.Conflict(new { available = false, message = "Slug is already in use." });
            }

            return this.Ok(new { available = true, slug = normalizedSlug });
        }

        [HttpGet("by-slug/{slug}")]
        public async Task<ActionResult<Venue>> GetVenueBySlug(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest(new { message = "Invalid slug format." });
            }

            var venueCandidates = await this.context.Set<Venue>()
                .AsNoTracking()
                .Select(v => new
                {
                    v.VenueID,
                    v.Name,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var matchedVenueId = venueCandidates
                .Where(v => !string.IsNullOrWhiteSpace(v.Name))
                .Select(v => new
                {
                    v.VenueID,
                    Slug = ToSlug(v.Name),
                })
                .Where(v => v.Slug == normalizedSlug)
                .Select(v => (int?)v.VenueID)
                .FirstOrDefault();

            if (!matchedVenueId.HasValue)
            {
                return this.NotFound();
            }

            var venue = await this.context.Set<Venue>()
                .AsNoTracking()
                .Include(v => v.LogoPic)
                .FirstOrDefaultAsync(v => v.VenueID == matchedVenueId.Value)
                .ConfigureAwait(false);

            if (venue == null)
            {
                return this.NotFound();
            }

            return this.Ok(MapVenueForApi(venue));
        }

        [HttpPost("reserve-slug")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VenueSlugReservationResponse>> ReserveVenueSlug([FromBody] VenueSlugReservationRequest request)
        {
            if (request == null)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var normalizedSlug = NormalizeSlug(request.Slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest("Invalid slug format.");
            }

            if (!await this.IsVenueSlugUniqueAsync(normalizedSlug).ConfigureAwait(false))
            {
                return this.Conflict("Slug is already in use.");
            }

            var displayTitle = NormalizeDisplayName(request.Title, normalizedSlug);

            var address = new Address
            {
                AddressLine1 = "Pending venue address",
                City = "Pending",
                State = string.Empty,
                Country = "Pending",
                ZipCode = string.Empty,
                Region = string.Empty,
                OperationHours = string.Empty,
            };

            var link = new ExternalLink
            {
                URL = "https://example.com",
                Description = "Pending venue website",
                Handle = normalizedSlug,
            };

            var phone = new PhoneContact
            {
                PhoneNumber = "000-000-0000",
                Description = "Pending venue phone",
            };

            this.context.Set<Address>().Add(address);
            this.context.Set<ExternalLink>().Add(link);
            this.context.Set<PhoneContact>().Add(phone);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            var venue = new Venue
            {
                Name = displayTitle,
                AddressID = address.AddressID,
                ExternalLinkID = link.ExternalLinkID,
                PhoneContactID = phone.PhoneContactID,
                IsPublished = false,
                IsModerationBlocked = false,
            };

            this.context.Set<Venue>().Add(venue);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue slug reserved",
                tags: "scope=audit;entity=venue;event=slug;operation=reserve;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};slug={ToSlug(venue.Name)}")
                .ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetVenueById), new { id = venue.VenueID }, new VenueSlugReservationResponse
            {
                VenueID = venue.VenueID,
                Slug = ToSlug(venue.Name),
                Title = venue.Name,
            });
        }

        [HttpPatch("{id}/update-slug")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VenueSlugReservationResponse>> UpdateVenueSlug(int id, [FromBody] VenueSlugUpdateRequest request)
        {
            if (request == null)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var venue = await this.context.Set<Venue>().FindAsync(id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound(new { message = $"Venue with ID {id} not found" });
            }

            var normalizedSlug = NormalizeSlug(request.Slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest("Invalid slug format.");
            }

            if (!await this.IsVenueSlugUniqueAsync(normalizedSlug, id).ConfigureAwait(false))
            {
                return this.Conflict("Slug is already in use.");
            }

            venue.Name = NormalizeDisplayName(request.Title, normalizedSlug);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue slug updated",
                tags: "scope=audit;entity=venue;event=slug;operation=update;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};slug={ToSlug(venue.Name)}")
                .ConfigureAwait(false);

            return this.Ok(new VenueSlugReservationResponse
            {
                VenueID = venue.VenueID,
                Slug = ToSlug(venue.Name),
                Title = venue.Name,
            });
        }

        [HttpPost]
        public async Task<ActionResult<Venue>> PostVenue(Venue venue)
        {
            if (venue == null)
            {
                return this.BadRequest();
            }

            this.context.Set<Venue>().Add(venue);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue created",
                tags: "scope=audit;entity=venue;event=profile;operation=create;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};name={venue.Name}")
                .ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetVenue), new { id = venue.VenueID }, venue);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutVenue(int id, Venue venue)
        {
            if (venue == null)
            {
                return this.BadRequest();
            }

            if (id != venue.VenueID)
            {
                return this.BadRequest();
            }

            this.context.Entry(venue).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);

                await this.TryWriteAuditLogAsync(
                    shortText: "Venue updated",
                    tags: "scope=audit;entity=venue;event=profile;operation=update;result=success;channel=db",
                    loggedData: $"venueId={venue.VenueID};name={venue.Name}")
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.VenueExists(id))
                {
                    return this.NotFound();
                }
                else
                {
                    throw;
                }
            }

            return this.NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVenue(int id)
        {
            var venue = await this.context.Set<Venue>().FindAsync(id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound();
            }

            this.context.Set<Venue>().Remove(venue);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue deleted",
                tags: "scope=audit;entity=venue;event=profile;operation=delete;result=success;channel=db",
                critical: true,
                loggedData: $"venueId={id};name={venue.Name}")
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
            try
            {
                this.context.Set<Log>().Add(new Log
                {
                    ShortText = shortText,
                    Tags = tags,
                    Critical = critical,
                    LongText = longText,
                    LoggedData = loggedData,
                    UserID = userId,
                    ArtistID = artistId,
                    ListingID = listingId,
                    LogTimestamp = DateTime.UtcNow,
                });

                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to write venue audit log. Tags: {Tags}", tags);
            }
        }

        private bool VenueExists(int id)
        {
            return this.context.Set<Venue>().Any(e => e.VenueID == id);
        }

        private static string NormalizeSlug(string slug)
        {
            return string.IsNullOrWhiteSpace(slug) ? string.Empty : slug.Trim().ToLower(CultureInfo.InvariantCulture);
        }

        private static string NormalizeDisplayName(string? title, string fallbackSlug)
        {
            var trimmedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            return string.IsNullOrWhiteSpace(trimmedTitle) ? fallbackSlug : trimmedTitle;
        }

        private static string ToSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = Regex.Replace(value.Trim().ToLower(CultureInfo.InvariantCulture), @"[^a-z0-9\s-]", string.Empty);
            sanitized = Regex.Replace(sanitized, @"\s+", "-");
            sanitized = Regex.Replace(sanitized, @"-+", "-");
            return sanitized.Trim('-');
        }

        private async Task<bool> IsVenueSlugUniqueAsync(string slug, int? excludeVenueId = null)
        {
            var query = this.context.Set<Venue>().AsQueryable();
            if (excludeVenueId.HasValue)
            {
                query = query.Where(v => v.VenueID != excludeVenueId.Value);
            }

            var existingNames = await query
                .Select(v => v.Name)
                .ToListAsync()
                .ConfigureAwait(false);

            return existingNames.All(name => ToSlug(name) != slug);
        }

        private async Task<bool> IsModeratorAsync(int userId)
        {
            if (userId <= 0)
            {
                return false;
            }

            return await this.context.Users
                .Where(u => u.UserID == userId)
                .Select(u => u.Moderator)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        private static object MapVenueForApi(Venue venue)
        {
            return new
            {
                venue.VenueID,
                Name = CoalescePlaintext(venue.Name_Plaintext, venue.Name),
                NameRichtext = venue.Name,
                venue.AddressID,
                venue.ExternalLinkID,
                venue.PhoneContactID,
                venue.IsPublished,
                venue.IsModerationBlocked,
                venue.LogoPicID,
                venue.LogoPic,
                venue.Address,
                venue.ExternalLink,
                venue.PhoneContact,
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
}
