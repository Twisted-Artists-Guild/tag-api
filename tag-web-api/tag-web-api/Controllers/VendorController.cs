// <copyright file="VendorController.cs" company="Twisted Artists Guild">
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
    public class VendorController : ControllerBase
    {
        private static readonly Regex ValidSlugRegex = new Regex(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);
        private readonly TAGDBContext context;
        private readonly ILogger<VendorController> logger;

        public VendorController(TAGDBContext context, ILogger<VendorController> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Vendor>>> GetVendors()
        {
            var vendors = await this.context.Set<Vendor>()
                .Where(v => v.IsPublished && !v.IsModerationBlocked)
                .Include(v => v.LogoPic)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(vendors.Select(MapVendorForApi));
        }

        [HttpGet("admin/unpublished")]
        public async Task<ActionResult<IEnumerable<Vendor>>> GetUnpublishedVendors([FromQuery] int moderatorUserId)
        {
            if (!await this.IsModeratorAsync(moderatorUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            var vendors = await this.context.Set<Vendor>()
                .Where(v => !v.IsPublished || v.IsModerationBlocked)
                .Include(v => v.LogoPic)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(vendors.Select(MapVendorForApi));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Vendor>> GetVendor(int id)
        {
            var vendor = await this.context.Set<Vendor>()
                .Include(v => v.LogoPic)
                .FirstOrDefaultAsync(v => v.VendorID == id)
                .ConfigureAwait(false);

            if (vendor == null || !vendor.IsPublished || vendor.IsModerationBlocked)
            {
                return this.NotFound();
            }

            return this.Ok(MapVendorForApi(vendor));
        }

        [HttpGet("byID/{id}")]
        public async Task<ActionResult<Vendor>> GetVendorById(int id)
        {
            var vendor = await this.context.Set<Vendor>()
                .Include(v => v.LogoPic)
                .FirstOrDefaultAsync(v => v.VendorID == id)
                .ConfigureAwait(false);
            if (vendor == null)
            {
                return this.NotFound();
            }

            return this.Ok(MapVendorForApi(vendor));
        }

        [HttpGet("check-slug/{slug}")]
        public async Task<ActionResult<object>> CheckVendorSlug(string slug, [FromQuery] int? excludeId = null)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest(new { available = false, message = "Invalid slug format." });
            }

            var isUnique = await this.IsVendorSlugUniqueAsync(normalizedSlug, excludeId).ConfigureAwait(false);
            if (!isUnique)
            {
                return this.Conflict(new { available = false, message = "Slug is already in use." });
            }

            return this.Ok(new { available = true, slug = normalizedSlug });
        }

        [HttpGet("by-slug/{slug}")]
        public async Task<ActionResult<Vendor>> GetVendorBySlug(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest(new { message = "Invalid slug format." });
            }

            var vendorCandidates = await this.context.Set<Vendor>()
                .AsNoTracking()
                .Select(v => new
                {
                    v.VendorID,
                    v.CompanyName,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var matchedVendorId = vendorCandidates
                .Where(v => !string.IsNullOrWhiteSpace(v.CompanyName))
                .Select(v => new
                {
                    v.VendorID,
                    Slug = ToSlug(v.CompanyName),
                })
                .Where(v => v.Slug == normalizedSlug)
                .Select(v => (int?)v.VendorID)
                .FirstOrDefault();

            if (!matchedVendorId.HasValue)
            {
                return this.NotFound();
            }

            var vendor = await this.context.Set<Vendor>()
                .AsNoTracking()
                .Include(v => v.LogoPic)
                .FirstOrDefaultAsync(v => v.VendorID == matchedVendorId.Value)
                .ConfigureAwait(false);

            if (vendor == null)
            {
                return this.NotFound();
            }

            return this.Ok(MapVendorForApi(vendor));
        }

        [HttpPost("reserve-slug")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VendorSlugReservationResponse>> ReserveVendorSlug([FromBody] VendorSlugReservationRequest request)
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

            if (!await this.IsVendorSlugUniqueAsync(normalizedSlug).ConfigureAwait(false))
            {
                return this.Conflict("Slug is already in use.");
            }

            var displayTitle = NormalizeDisplayName(request.Title, normalizedSlug);
            var vendor = new Vendor
            {
                CompanyName = displayTitle,
                ContractExpires = DateTime.UtcNow.AddYears(1),
                LinkToContract = string.Empty,
                LinkToMSA = string.Empty,
                MSA_Executed = DateTime.UtcNow,
                NotesOnContracts = string.Empty,
                NotesOnVendors = string.Empty,
                POCEmail = NormalizeOptionalString(request.Email),
                POCName = string.Empty,
                POCPhone = string.Empty,
                IsPublished = false,
                IsModerationBlocked = false,
            };

            this.context.Set<Vendor>().Add(vendor);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Vendor slug reserved",
                tags: "scope=audit;entity=vendor;event=slug;operation=reserve;result=success;channel=db",
                loggedData: $"vendorId={vendor.VendorID};slug={ToSlug(vendor.CompanyName)}")
                .ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetVendorById), new { id = vendor.VendorID }, new VendorSlugReservationResponse
            {
                VendorID = vendor.VendorID,
                Slug = ToSlug(vendor.CompanyName),
                Title = vendor.CompanyName,
                Email = vendor.POCEmail,
            });
        }

        [HttpPatch("{id}/update-slug")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VendorSlugReservationResponse>> UpdateVendorSlug(int id, [FromBody] VendorSlugUpdateRequest request)
        {
            if (request == null)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var vendor = await this.context.Set<Vendor>().FindAsync(id).ConfigureAwait(false);
            if (vendor == null)
            {
                return this.NotFound(new { message = $"Vendor with ID {id} not found" });
            }

            var normalizedSlug = NormalizeSlug(request.Slug);
            if (!ValidSlugRegex.IsMatch(normalizedSlug))
            {
                return this.BadRequest("Invalid slug format.");
            }

            if (!await this.IsVendorSlugUniqueAsync(normalizedSlug, id).ConfigureAwait(false))
            {
                return this.Conflict("Slug is already in use.");
            }

            vendor.CompanyName = NormalizeDisplayName(request.Title, normalizedSlug);
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                vendor.POCEmail = NormalizeOptionalString(request.Email);
            }

            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Vendor slug updated",
                tags: "scope=audit;entity=vendor;event=slug;operation=update;result=success;channel=db",
                loggedData: $"vendorId={vendor.VendorID};slug={ToSlug(vendor.CompanyName)}")
                .ConfigureAwait(false);

            return this.Ok(new VendorSlugReservationResponse
            {
                VendorID = vendor.VendorID,
                Slug = ToSlug(vendor.CompanyName),
                Title = vendor.CompanyName,
                Email = vendor.POCEmail,
            });
        }

        [HttpPost]
        public async Task<ActionResult<Vendor>> PostVendor(Vendor vendor)
        {
            if (vendor == null)
            {
                return this.BadRequest();
            }

            this.context.Set<Vendor>().Add(vendor);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Vendor created",
                tags: "scope=audit;entity=vendor;event=profile;operation=create;result=success;channel=db",
                loggedData: $"vendorId={vendor.VendorID};companyName={vendor.CompanyName}")
                .ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetVendor), new { id = vendor.VendorID }, vendor);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutVendor(int id, Vendor vendor)
        {
            if (vendor == null)
            {
                return this.BadRequest();
            }

            if (id != vendor.VendorID)
            {
                return this.BadRequest();
            }

            this.context.Entry(vendor).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);

                await this.TryWriteAuditLogAsync(
                    shortText: "Vendor updated",
                    tags: "scope=audit;entity=vendor;event=profile;operation=update;result=success;channel=db",
                    loggedData: $"vendorId={vendor.VendorID};companyName={vendor.CompanyName}")
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.VendorExists(id))
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
        public async Task<IActionResult> DeleteVendor(int id)
        {
            var vendor = await this.context.Set<Vendor>().FindAsync(id).ConfigureAwait(false);
            if (vendor == null)
            {
                return this.NotFound();
            }

            this.context.Set<Vendor>().Remove(vendor);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Vendor deleted",
                tags: "scope=audit;entity=vendor;event=profile;operation=delete;result=success;channel=db",
                critical: true,
                loggedData: $"vendorId={id};companyName={vendor.CompanyName}")
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
                this.logger.LogError(ex, "Failed to write vendor audit log. Tags: {Tags}", tags);
            }
        }

        private bool VendorExists(int id)
        {
            return this.context.Set<Vendor>().Any(e => e.VendorID == id);
        }

        private static string NormalizeSlug(string slug)
        {
            return string.IsNullOrWhiteSpace(slug) ? string.Empty : slug.Trim().ToLower(CultureInfo.InvariantCulture);
        }

        private static string NormalizeOptionalString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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

        private async Task<bool> IsVendorSlugUniqueAsync(string slug, int? excludeVendorId = null)
        {
            var query = this.context.Set<Vendor>().AsQueryable();
            if (excludeVendorId.HasValue)
            {
                query = query.Where(v => v.VendorID != excludeVendorId.Value);
            }

            var normalizedExisting = await query
                .Select(v => v.CompanyName)
                .ToListAsync()
                .ConfigureAwait(false);

            return normalizedExisting.All(name => ToSlug(name) != slug);
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

        private static object MapVendorForApi(Vendor vendor)
        {
            return new
            {
                vendor.VendorID,
                CompanyName = CoalescePlaintext(vendor.CompanyName_Plaintext, vendor.CompanyName),
                CompanyNameRichtext = vendor.CompanyName,
                vendor.ContractExpires,
                vendor.LinkToContract,
                vendor.LinkToMSA,
                vendor.MSA_Executed,
                NotesOnContracts = CoalescePlaintext(vendor.NotesOnContracts_Plaintext, vendor.NotesOnContracts),
                NotesOnContractsRichtext = vendor.NotesOnContracts,
                NotesOnVendors = CoalescePlaintext(vendor.NotesOnVendors_Plaintext, vendor.NotesOnVendors),
                NotesOnVendorsRichtext = vendor.NotesOnVendors,
                vendor.POCEmail,
                vendor.POCName,
                vendor.POCPhone,
                vendor.IsPublished,
                vendor.IsModerationBlocked,
                vendor.LogoPicID,
                vendor.LogoPic,
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
