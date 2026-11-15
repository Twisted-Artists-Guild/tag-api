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
        private const int DescriptionMaxLength = 20000;
        private const int VenueNameMaxLength = 1000;
        private const string AddressNotFoundMessage = "AddressID is required and must reference an existing address.";
        private const string PendingAddressLine = "Pending venue address";
        private static readonly Regex ZipCodeRegex = new Regex(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled);
        private readonly TAGDBContext context;
        private readonly ILogger<VenueController> logger;

        public VenueController(TAGDBContext context, ILogger<VenueController> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Venue>>> GetVenues([FromQuery(Name = "tag")] string[]? tag = null)
        {
            var query = this.context.Set<Venue>().Where(v => v.IsPublished && !v.IsModerationBlocked);

            // A venue has to carry every requested tag. A tag that can't exist matches no venue.
            foreach (var requested in (tag ?? Array.Empty<string>()).Take(VenueTag.MaxTagsPerVenue))
            {
                if (!VenueTag.TryNormalize(requested, out var tagName, out _))
                {
                    return this.Ok(Array.Empty<object>());
                }

                query = query.Where(v => v.TagLinks.Any(link => link.Tag!.Name == tagName));
            }

            var venues = await query
                .IncludeVenueDetails()
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(venues.Select(MapVenueForApi));
        }

        [HttpGet("admin/unpublished")]
        public async Task<ActionResult<IEnumerable<Venue>>> GetUnpublishedVenues()
        {
            // Staff status comes from the token; a user id supplied by the caller is never trusted.
            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            if (!await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false))
            {
                return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can list unpublished venues." });
            }

            var venues = await this.context.Set<Venue>()
                .Where(v => !v.IsPublished || v.IsModerationBlocked)
                .IncludeVenueDetails()
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(venues.Select(MapVenueForApi));
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Venue>> GetVenue(int id)
        {
            var venue = await this.context.Set<Venue>()
                .IncludeVenueDetails()
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
                .IncludeVenueDetails()
                .FirstOrDefaultAsync(v => v.VenueID == id)
                .ConfigureAwait(false);
            if (venue == null || !await this.CanViewVenueAsync(venue).ConfigureAwait(false))
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
        [AllowAnonymous]
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
                .IncludeVenueDetails()
                .FirstOrDefaultAsync(v => v.VenueID == matchedVenueId.Value)
                .ConfigureAwait(false);

            if (venue == null || !await this.CanViewVenueAsync(venue).ConfigureAwait(false))
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

            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
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
                AddressLine1 = PendingAddressLine,
                City = "Pending",
                State = string.Empty,
                Country = "Pending",
                ZipCode = string.Empty,
                Region = string.Empty,
                OperationHours = string.Empty,
            };

            // No placeholder website or phone: both are optional and are added later through the contacts workflow.
            // The address, the venue and the creator's owner link are saved in a single SaveChanges, so a failure
            // cannot leave an orphaned address or an ownerless venue behind.
            var venue = new Venue
            {
                Name = displayTitle,
                Address = address,
                IsPublished = false,
                IsModerationBlocked = false,
            };

            this.context.Set<Venue>().Add(venue);
            this.context.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue
            {
                UserID = userId.Value,
                Venue = venue,
                Role = Linker_UserToVenue.OwnerRole,
            });
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue slug reserved",
                tags: "scope=audit;entity=venue;event=slug;operation=reserve;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};slug={ToSlug(venue.Name)};ownerUserId={userId.Value}",
                userId: userId)
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

            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            var venue = await this.context.Set<Venue>().FindAsync(id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound(new { message = $"Venue with ID {id} not found" });
            }

            if (!await VenueAccess.CanManageAsync(this.context, id, userId.Value).ConfigureAwait(false))
            {
                return this.Forbidden("You are not allowed to change this venue.");
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
        public async Task<ActionResult<Venue>> PostVenue([FromBody] VenueCreateRequest request)
        {
            if (request == null)
            {
                return this.BadRequest();
            }

            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            var validationError = ValidateVenueRequest(request);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            if (!await this.AddressExistsAsync(request.AddressID).ConfigureAwait(false))
            {
                return this.BadRequest(AddressNotFoundMessage);
            }

            // A venue is created unpublished and unblocked; the creator becomes its owner.
            var venue = new Venue
            {
                Name = request.Name!.Trim(),
                AddressID = request.AddressID,
                ExternalLinkID = request.ExternalLinkID,
                PhoneContactID = request.PhoneContactID,
                Description = request.Description,
                EventSourceUrl = request.EventSourceUrl,
                IsPublished = false,
                IsModerationBlocked = false,
            };

            this.context.Set<Venue>().Add(venue);
            this.context.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue
            {
                UserID = userId.Value,
                Venue = venue,
                Role = Linker_UserToVenue.OwnerRole,
            });
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue created",
                tags: "scope=audit;entity=venue;event=profile;operation=create;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};name={venue.Name};ownerUserId={userId.Value}",
                userId: userId)
                .ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetVenueById), new { id = venue.VenueID }, MapVenueForApi(venue));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutVenue(int id, [FromBody] VenueUpdateRequest request)
        {
            if (request == null || id != request.VenueID)
            {
                return this.BadRequest();
            }

            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            var venue = await this.context.Set<Venue>().FirstOrDefaultAsync(v => v.VenueID == id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound();
            }

            // Permission is decided before anything about the request is validated or applied.
            var isStaff = await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false);
            if (!isStaff && !await VenueAccess.IsOwnerAsync(this.context, id, userId.Value).ConfigureAwait(false))
            {
                return this.Forbidden("You are not allowed to edit this venue.");
            }

            if (request.IsModerationBlocked.HasValue && request.IsModerationBlocked.Value != venue.IsModerationBlocked && !isStaff)
            {
                return this.Forbidden("Only staff can change the moderation status of a venue.");
            }

            var validationError = ValidateVenueRequest(request);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            if (!await this.AddressExistsAsync(request.AddressID).ConfigureAwait(false))
            {
                return this.BadRequest(AddressNotFoundMessage);
            }

            // A venue is listed as soon as it is published, so it needs the address from the onboarding form first.
            if (request.IsPublished == true && !venue.IsPublished && await this.IsPlaceholderAddressAsync(request.AddressID).ConfigureAwait(false))
            {
                return this.BadRequest("Add the venue's address before publishing.");
            }

            // Only whitelisted fields are copied. The logo and the plaintext copies are managed elsewhere.
            venue.Name = request.Name!.Trim();
            venue.AddressID = request.AddressID;
            venue.ExternalLinkID = request.ExternalLinkID;
            venue.PhoneContactID = request.PhoneContactID;
            venue.Description = request.Description;
            venue.EventSourceUrl = request.EventSourceUrl;

            if (request.IsPublished.HasValue)
            {
                venue.IsPublished = request.IsPublished.Value;
            }

            if (request.IsModerationBlocked.HasValue)
            {
                venue.IsModerationBlocked = request.IsModerationBlocked.Value;
            }

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.VenueExists(id))
                {
                    return this.NotFound();
                }

                throw;
            }

            await this.TryWriteAuditLogAsync(
                shortText: "Venue updated",
                tags: "scope=audit;entity=venue;event=profile;operation=update;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};name={venue.Name}",
                userId: userId)
                .ConfigureAwait(false);

            return this.NoContent();
        }

        // Whether the signed-in user may edit this venue. The edit pages use it to turn everyone else away.
        [HttpGet("{id:int}/access")]
        public async Task<IActionResult> GetVenueAccess(int id)
        {
            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            if (!await this.context.Set<Venue>().AnyAsync(v => v.VenueID == id).ConfigureAwait(false))
            {
                return this.NotFound();
            }

            var isStaff = await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false);
            var isOwner = await VenueAccess.IsOwnerAsync(this.context, id, userId.Value).ConfigureAwait(false);
            return this.Ok(new { canManage = isStaff || isOwner, isStaff, isOwner });
        }

        // Saves the venue's street address. Owners and staff only. The address row is only ever the venue's own: when
        // another venue still shares it, this venue gets a new row instead, so one owner can never change another venue.
        [HttpPut("{id:int}/address")]
        public async Task<IActionResult> PutVenueAddress(int id, [FromBody] VenueAddressRequest request)
        {
            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            var venue = await this.context.Set<Venue>().FirstOrDefaultAsync(v => v.VenueID == id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound();
            }

            // Permission is decided before anything about the request is validated or applied.
            if (!await VenueAccess.CanManageAsync(this.context, id, userId.Value).ConfigureAwait(false))
            {
                return this.Forbidden("You are not allowed to edit this venue.");
            }

            if (request == null)
            {
                return this.BadRequest("An address is required.");
            }

            var validationError = ValidateAddressRequest(request);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            var address = await this.context.Set<Address>().FirstOrDefaultAsync(a => a.AddressID == venue.AddressID).ConfigureAwait(false);
            var isShared = address != null
                && await this.context.Set<Venue>().AnyAsync(v => v.AddressID == address.AddressID && v.VenueID != id).ConfigureAwait(false);
            if (address == null || isShared)
            {
                address = new Address();
                this.context.Set<Address>().Add(address);
                venue.Address = address;
            }

            address.AddressLine1 = request.AddressLine1;
            address.AddressLine2 = request.AddressLine2;
            address.City = request.City;
            address.State = request.State;
            address.ZipCode = request.ZipCode;
            address.Country = "USA";

            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue address updated",
                tags: "scope=audit;entity=venue;event=address;operation=update;result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};city={address.City};state={address.State}",
                userId: userId)
                .ConfigureAwait(false);

            return this.NoContent();
        }

        // Saves which picture is the venue's active profile or cover picture. Owners and staff only. The picture must be a
        // file directly inside this venue's own folder for that kind, so a venue can never point at another venue's image,
        // at an archived older version, or at a file somewhere else. A null pictureID clears the active picture.
        [HttpPut("{id:int}/pictures/{kind}")]
        public async Task<IActionResult> PutActivePicture(int id, string kind, [FromBody] VenueActivePictureRequest request)
        {
            var normalizedKind = kind?.Trim().ToLowerInvariant();
            if (normalizedKind != "profile" && normalizedKind != "cover")
            {
                return this.NotFound();
            }

            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            var venue = await this.context.Set<Venue>().FirstOrDefaultAsync(v => v.VenueID == id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound();
            }

            // Permission is decided before anything about the request is validated or applied.
            if (!await VenueAccess.CanManageAsync(this.context, id, userId.Value).ConfigureAwait(false))
            {
                return this.Forbidden("You are not allowed to change the pictures of this venue.");
            }

            if (request == null)
            {
                return this.BadRequest("A request body is required.");
            }

            if (request.PictureID.HasValue)
            {
                var picture = await this.context.Set<Picture>().AsNoTracking().FirstOrDefaultAsync(p => p.PictureID == request.PictureID.Value).ConfigureAwait(false);
                if (picture == null)
                {
                    return this.BadRequest($"Picture #{request.PictureID.Value} does not exist.");
                }

                if (!IsPictureInVenueFolder(picture, id, normalizedKind))
                {
                    return this.BadRequest($"The picture must be an image in this venue's {normalizedKind} folder.");
                }
            }

            if (normalizedKind == "profile")
            {
                venue.ProfilePicID = request.PictureID;
            }
            else
            {
                venue.CoverPicID = request.PictureID;
            }

            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue picture changed",
                tags: $"scope=audit;entity=venue;event=picture;operation=set-{normalizedKind};result=success;channel=db",
                loggedData: $"venueId={venue.VenueID};kind={normalizedKind};pictureId={request.PictureID?.ToString(CultureInfo.InvariantCulture) ?? "none"}",
                userId: userId)
                .ConfigureAwait(false);

            return this.Ok(new { profilePicID = venue.ProfilePicID, coverPicID = venue.CoverPicID });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVenue(int id)
        {
            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return this.Unauthorized();
            }

            var venue = await this.context.Set<Venue>().FindAsync(id).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound();
            }

            if (!await VenueAccess.CanManageAsync(this.context, id, userId.Value).ConfigureAwait(false))
            {
                return this.Forbidden("You are not allowed to delete this venue.");
            }

            this.context.Set<Venue>().Remove(venue);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Venue deleted",
                tags: "scope=audit;entity=venue;event=profile;operation=delete;result=success;channel=db",
                critical: true,
                loggedData: $"venueId={id};name={venue.Name}",
                userId: userId)
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

        internal static string ToSlug(string value)
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

        private ObjectResult Forbidden(string message)
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message });
        }

        private Task<bool> AddressExistsAsync(int addressId)
        {
            return this.context.Set<Address>().AnyAsync(a => a.AddressID == addressId);
        }

        // True when the picture is a file directly inside platformpics/venue/{id}/{kind}/ (not in an archive subfolder).
        public static bool IsPictureInVenueFolder(Picture picture, int venueId, string kind)
        {
            var folder = $"/platformpics/venue/{venueId.ToString(CultureInfo.InvariantCulture)}/{kind}/";
            foreach (var candidate in new[] { picture.URL, picture.NormalizedURL })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                var path = candidate.Split('?', '#')[0];
                if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var absolutePath = Uri.UnescapeDataString(uri.AbsolutePath);
                var index = absolutePath.IndexOf(folder, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                var fileName = absolutePath.Substring(index + folder.Length);
                if (fileName.Length == 0 || fileName.Contains('/', StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return !string.IsNullOrWhiteSpace(picture.URL) || !string.IsNullOrWhiteSpace(picture.NormalizedURL);
        }

        // The address a venue gets when its slug is reserved, before the owner has entered the real one.
        private Task<bool> IsPlaceholderAddressAsync(int addressId)
        {
            return this.context.Set<Address>().AnyAsync(a => a.AddressID == addressId && (a.AddressLine1 == PendingAddressLine || a.City == "Pending"));
        }

        // Trims and validates the writable address fields in place; returns an error message, or null when valid.
        // The state becomes its two-letter code, whether the caller sent the code or the full name.
        private static string? ValidateAddressRequest(VenueAddressRequest request)
        {
            request.AddressLine1 = request.AddressLine1?.Trim();
            request.AddressLine2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? null : request.AddressLine2.Trim();
            request.City = request.City?.Trim();
            request.State = request.State?.Trim();
            request.ZipCode = request.ZipCode?.Trim();

            if (string.IsNullOrEmpty(request.AddressLine1))
            {
                return "Street address is required.";
            }

            if (request.AddressLine1.Length > 255 || (request.AddressLine2?.Length ?? 0) > 255)
            {
                return "Address lines must be at most 255 characters.";
            }

            if (string.IsNullOrEmpty(request.City))
            {
                return "City is required.";
            }

            if (request.City.Length > 100)
            {
                return "City must be at most 100 characters.";
            }

            var state = request.State ?? string.Empty;
            var stateCode = UsStates.Names.Keys.FirstOrDefault(code => string.Equals(code, state, StringComparison.OrdinalIgnoreCase))
                ?? UsStates.Names.FirstOrDefault(pair => string.Equals(pair.Value, state, StringComparison.OrdinalIgnoreCase)).Key;
            if (stateCode == null)
            {
                return "State must be a US state or DC.";
            }

            request.State = stateCode;

            if (string.IsNullOrEmpty(request.ZipCode) || !ZipCodeRegex.IsMatch(request.ZipCode))
            {
                return "Zip code must be 5 digits, or 5 digits, a dash and 4 digits.";
            }

            return null;
        }

        // Published, unblocked venues are visible to any signed-in user; drafts and blocked venues only to their owners and staff.
        private async Task<bool> CanViewVenueAsync(Venue venue)
        {
            if (venue.IsPublished && !venue.IsModerationBlocked)
            {
                return true;
            }

            var userId = VenueAccess.GetUserId(this.User);
            return userId != null
                && await VenueAccess.CanManageAsync(this.context, venue.VenueID, userId.Value).ConfigureAwait(false);
        }

        // Normalizes and validates the writable venue fields; returns an error message, or null when valid.
        private static string? ValidateVenueRequest(VenueWriteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return "Name is required.";
            }

            if (request.Name.Trim().Length > VenueNameMaxLength)
            {
                return $"Name must be at most {VenueNameMaxLength} characters.";
            }

            if (request.AddressID <= 0)
            {
                return AddressNotFoundMessage;
            }

            var eventSourceUrl = request.EventSourceUrl?.Trim();
            if (string.IsNullOrEmpty(eventSourceUrl))
            {
                request.EventSourceUrl = null;
            }
            else
            {
                if (eventSourceUrl.Length > Venue.EventSourceUrlMaxLength)
                {
                    return $"EventSourceUrl must be at most {Venue.EventSourceUrlMaxLength} characters.";
                }

                if (!Uri.TryCreate(eventSourceUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return "EventSourceUrl must be an absolute http or https URL.";
                }

                request.EventSourceUrl = eventSourceUrl;
            }

            if (request.Description != null && request.Description.Length > DescriptionMaxLength)
            {
                return $"Description must be at most {DescriptionMaxLength} characters.";
            }

            return null;
        }

        internal static object MapVenueForApi(Venue venue)
        {
            return new
            {
                venue.VenueID,
                Name = CoalescePlaintext(venue.Name_Plaintext, venue.Name),
                NameRichtext = venue.Name,
                Slug = ToSlug(venue.Name),
                Description = CoalescePlaintext(venue.Description_Plaintext, venue.Description),
                DescriptionRichtext = venue.Description,
                venue.EventSourceUrl,
                venue.AddressID,
                venue.ExternalLinkID,
                venue.PhoneContactID,
                venue.IsPublished,
                venue.IsModerationBlocked,
                venue.LogoPicID,
                venue.LogoPic,
                venue.ProfilePicID,
                venue.ProfilePic,
                venue.CoverPicID,
                venue.CoverPic,
                Categories = (venue.CategoryLinks ?? new List<Linker_VenueToCategory>())
                    .Where(link => link.Category != null)
                    .Select(link => link.Category!)
                    .OrderBy(category => category.SortOrder)
                    .ThenBy(category => category.Name)
                    .Select(category => new { category.VenueCategoryID, category.Name, category.Slug, category.IsActive })
                    .ToList(),
                Tags = (venue.TagLinks ?? new List<Linker_VenueToTag>())
                    .Where(link => link.Tag != null)
                    .Select(link => link.Tag!.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList(),
                Regions = (venue.RegionLinks ?? new List<Linker_VenueToRegion>())
                    .Where(link => link.Region != null)
                    .Select(link => link.Region!)
                    .OrderBy(region => region.Name, StringComparer.Ordinal)
                    .Select(region => new { region.RegionID, region.Name, region.Slug, region.Type, region.States, region.IsActive })
                    .ToList(),
                venue.Address,
                venue.ExternalLink,
                venue.PhoneContact,
            };
        }

        internal static string? CoalescePlaintext(string? plaintext, string? richtext)
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
