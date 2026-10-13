// <copyright file="LinkerEntityToContactController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers.Contact
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    public class LinkerEntityToContactController : ControllerBase
    {
        private readonly TAGDBContext context;

        public LinkerEntityToContactController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Linker_EntityToContact>>> GetLinks()
        {
            return await this.context.Set<Linker_EntityToContact>()
                .Include(l => l.Contact)
                .OrderBy(l => l.DisplayOrder)
                .ThenBy(l => l.Linker_EntityToContactID)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Linker_EntityToContact>> GetLink(int id)
        {
            var link = await this.context.Set<Linker_EntityToContact>()
                .Include(l => l.Contact)
                .FirstOrDefaultAsync(l => l.Linker_EntityToContactID == id)
                .ConfigureAwait(false);

            if (link == null)
            {
                return this.NotFound();
            }

            return link;
        }

        [HttpGet("by-entity/{entityType}/{entityId}")]
        public async Task<ActionResult<IEnumerable<Linker_EntityToContact>>> GetLinksByEntity(string entityType, int entityId)
        {
            if (!LinkedEntityTypes.All.Contains(entityType))
            {
                return this.BadRequest($"Invalid entity type '{entityType}'. Valid types: {string.Join(", ", LinkedEntityTypes.All)}");
            }

            var links = await this.context.Set<Linker_EntityToContact>()
                .Include(l => l.Contact)
                .Where(l => l.EntityType == entityType && l.EntityID == entityId)
                .OrderBy(l => l.DisplayOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(links);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Linker_EntityToContact>> PostLink(LinkerCreateRequest request)
        {
            var validationError = ValidateRequest(request.EntityType, request.EntityID);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            var contact = await this.context.Set<Contact>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ContactID == request.ContactID)
                .ConfigureAwait(false);

            if (contact == null)
            {
                return this.BadRequest($"Contact {request.ContactID} was not found.");
            }

            if (!await this.EntityExistsAsync(request.EntityType, request.EntityID).ConfigureAwait(false))
            {
                return this.BadRequest($"{request.EntityType} with ID {request.EntityID} was not found.");
            }

            var link = new Linker_EntityToContact
            {
                ContactID = request.ContactID,
                EntityType = request.EntityType,
                EntityID = request.EntityID,
                DisplayOrder = request.DisplayOrder,
                Scope = request.Scope,
            };

            this.context.Set<Linker_EntityToContact>().Add(link);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetLink), new { id = link.Linker_EntityToContactID }, link);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutLink(int id, Linker_EntityToContact link)
        {
            if (id != link.Linker_EntityToContactID)
            {
                return this.BadRequest();
            }

            var validationError = ValidateRequest(link.EntityType, link.EntityID);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            var contact = await this.context.Set<Contact>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ContactID == link.ContactID)
                .ConfigureAwait(false);

            if (contact == null)
            {
                return this.BadRequest($"Contact {link.ContactID} was not found.");
            }

            if (!Enum.IsDefined(typeof(ContactScope), link.Scope))
            {
                link.Scope = ContactScope.Secondary;
            }

            this.context.Entry(link).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.LinkExists(id))
                {
                    return this.NotFound();
                }

                throw;
            }

            return this.NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteLink(int id)
        {
            var link = await this.context.Set<Linker_EntityToContact>().FindAsync(id).ConfigureAwait(false);
            if (link == null)
            {
                return this.NotFound();
            }

            this.context.Set<Linker_EntityToContact>().Remove(link);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private static string? ValidateRequest(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType))
            {
                return "EntityType is required.";
            }

            if (!LinkedEntityTypes.All.Contains(entityType))
            {
                return $"Invalid entity type '{entityType}'. Valid types: {string.Join(", ", LinkedEntityTypes.All)}";
            }

            if (entityId <= 0)
            {
                return "EntityID must be a positive integer.";
            }

            return null;
        }

        private async Task<bool> EntityExistsAsync(string entityType, int entityId)
        {
            return entityType switch
            {
                LinkedEntityTypes.User => await this.context.Users.AnyAsync(u => u.UserID == entityId).ConfigureAwait(false),
                LinkedEntityTypes.Artist => await this.context.Artists.AnyAsync(a => a.ArtistID == entityId).ConfigureAwait(false),
                LinkedEntityTypes.Venue => await this.context.Venues.AnyAsync(v => v.VenueID == entityId).ConfigureAwait(false),
                LinkedEntityTypes.Vendor => await this.context.Vendors.AnyAsync(v => v.VendorID == entityId).ConfigureAwait(false),
                _ => false,
            };
        }

        private bool LinkExists(int id)
        {
            return this.context.Set<Linker_EntityToContact>().Any(e => e.Linker_EntityToContactID == id);
        }
    }

    /// <summary>
    /// Request DTO for creating a new entity-to-contact link.
    /// </summary>
    public sealed class LinkerCreateRequest
    {
        public int ContactID { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public int EntityID { get; set; }

        public int DisplayOrder { get; set; }

        public ContactScope Scope { get; set; } = ContactScope.Secondary;
    }
}