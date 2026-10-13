// <copyright file="ContactController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers.Contact
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        // Social platforms are not blocked — they are handled via Social Handles instead.
        // URLs pointing to these domains are redirected to that flow rather than saved here.
        private static readonly HashSet<string> SocialHandleDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "instagram.com",
            "tiktok.com",
            "x.com",
            "twitter.com",
            "facebook.com",
            "fb.com",
            "youtube.com",
            "youtu.be",
            "threads.net",
            "snapchat.com",
            "mastodon.social",
            "linkedin.com",
            "discord.com",
            "telegram.org",
            "whatsapp.com",
            "reddit.com",
        };

        // Categories used by the Social Handles UI flow.
        private static readonly HashSet<string> SocialHandleCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "instagram",
            "tiktok",
            "x",
            "facebook",
            "youtube",
            "threads",
            "twitter",
            "snapchat",
            "mastodon",
            "linkedin",
            "discord",
            "telegram",
            "whatsapp",
            "reddit",
        };

        // Adult-content domains are fully blocked and cannot be added anywhere on the platform.
        private static readonly HashSet<string> BlockedAdultDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "onlyfans.com",
            "fansly.com",
            "pornhub.com",
            "xvideos.com",
            "xnxx.com",
            "redtube.com",
            "youporn.com",
            "chaturbate.com",
            "cam4.com",
            "stripchat.com",
            "manyvids.com",
        };

        private static readonly HashSet<string> ValidContexts = new(StringComparer.OrdinalIgnoreCase)
        {
            "artist", "user", "venue", "vendor",
        };

        private readonly TAGDBContext context;

        public ContactController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/contact
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Contact>>> GetContacts()
        {
            return await this.PublicContactsQuery()
                .Include(c => c.Address)
                .Include(c => c.PhoneContact)
                .OrderBy(c => c.ContactID)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        // GET: api/contact/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Contact>> GetContact(int id)
        {
            var contact = await this.PublicContactsQuery()
                .Include(c => c.Address)
                .Include(c => c.PhoneContact)
                .FirstOrDefaultAsync(c => c.ContactID == id)
                .ConfigureAwait(false);

            if (contact == null)
            {
                return this.NotFound();
            }

            return contact;
        }

        // GET: api/contact/artist/4
        // Returns linked contacts and entity primary contact details.
        // includePrivate=false keeps public-safe behavior for profile pages.
        [HttpGet("{context}/{entityId:int}")]
        public async Task<ActionResult<object>> GetContactsByContext(string context, int entityId, [FromQuery] bool includePrivate = false)
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                return this.BadRequest($"Invalid context. Valid values: {string.Join(", ", ValidContexts)}.");
            }

            if (!ValidContexts.Contains(context))
            {
                return this.BadRequest($"Invalid context. Valid values: {string.Join(", ", ValidContexts)}.");
            }

            var normalizedContext = context.Trim().ToLowerInvariant();
            var entityType = NormalizeContextToEntityType(normalizedContext);

            if (!includePrivate && !await this.IsEntityPubliclyVisibleAsync(normalizedContext, entityId).ConfigureAwait(false))
            {
                return this.NotFound();
            }

            // Load linked contacts for this entity.
            var linksQuery = includePrivate
                ? this.context.Set<Linker_EntityToContact>().AsNoTracking()
                : this.PublicEntityLinksQuery();

            linksQuery = linksQuery.Where(l => l.EntityType == entityType && l.EntityID == entityId);

            var linkedContacts = await linksQuery
                .Include(l => l.Contact)
                    .ThenInclude(c => c.Address)
                .Include(l => l.Contact)
                    .ThenInclude(c => c.PhoneContact)
                        .ThenInclude(pc => pc != null ? pc.PhoneContactLabel : null)
                .Include(l => l.Contact)
                    .ThenInclude(c => c.ContactLabel)
                .Include(l => l.Contact)
                    .ThenInclude(c => c.PhoneContact)
                        .ThenInclude(pc => pc != null ? pc.ContactLabel : null)
                .OrderBy(l => l.DisplayOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            // Filter out private rows unless the caller explicitly requests private records.
            var filteredLinks = linkedContacts.Where(l => l.Contact != null && (includePrivate || l.Scope != ContactScope.Private))
                .ToList();

            var primaryLink = filteredLinks
                .OrderBy(l => l.DisplayOrder)
                .ThenBy(l => l.Linker_EntityToContactID)
                .FirstOrDefault();
            var primaryContact = primaryLink?.Contact;
            object? primaryPhone = null;
            object? primaryAddress = null;

            if (primaryContact != null && (includePrivate || primaryLink?.Scope != ContactScope.Private))
            {
                if (string.Equals(primaryContact.ContactType, "address", StringComparison.OrdinalIgnoreCase))
                {
                    primaryAddress = primaryContact.Address;
                }

                if (string.Equals(primaryContact.ContactType, "phone", StringComparison.OrdinalIgnoreCase))
                {
                    primaryPhone = primaryContact.PhoneContact;
                }
            }

            // A single primary-scoped contact cannot represent both phone and address.
            // If one type is missing, fall back to the first linked contact of that type.
            var firstAddressContact = filteredLinks
                .Select(l => l.Contact)
                .FirstOrDefault(c => c != null &&
                    string.Equals(c.ContactType, "address", StringComparison.OrdinalIgnoreCase) &&
                    c.Address != null);
            if (primaryAddress == null)
            {
                primaryAddress = firstAddressContact?.Address;
            }

            var firstPhoneContact = filteredLinks
                .Select(l => l.Contact)
                .FirstOrDefault(c => c != null &&
                    string.Equals(c.ContactType, "phone", StringComparison.OrdinalIgnoreCase) &&
                    c.PhoneContact != null);
            if (primaryPhone == null)
            {
                primaryPhone = firstPhoneContact?.PhoneContact;
            }

            return this.Ok(new
            {
                context = normalizedContext,
                entityId,
                primaryContactId = primaryLink?.ContactID,
                primaryPhone,
                primaryAddress,
                contacts = filteredLinks.Select(l => new
                {
                    linkId = l.Linker_EntityToContactID,
                    contactId = l.ContactID,
                    displayOrder = l.DisplayOrder,
                    contactType = l.Contact.ContactType,
                    isPrivate = l.Scope == ContactScope.Private,
                    label = l.Contact.ContactLabel != null ? l.Contact.ContactLabel.Label : l.Contact.Label,
                    category = l.Contact.Category,
                    value = l.Contact.ContactType == "phone"
                        ? l.Contact.PhoneContact?.PhoneNumber
                        : l.Contact.Value,
                    phoneLabel = l.Contact.PhoneContact?.ContactLabel?.Label ?? l.Contact.PhoneContact?.PhoneContactLabel?.Label,
                    address = l.Contact.Address == null ? null : new
                    {
                        line1 = l.Contact.Address.AddressLine1,
                        line2 = l.Contact.Address.AddressLine2,
                        city = l.Contact.Address.City,
                        state = l.Contact.Address.State,
                        region = l.Contact.Address.Region,
                        postalCode = l.Contact.Address.ZipCode,
                        country = l.Contact.Address.Country,
                        hours = l.Contact.Address.OperationHours,
                    },
                    handle = l.Contact.Handle,
                    description = l.Contact.Description,
                }),
            });
        }

        [HttpPost]
        public async Task<ActionResult<Contact>> PostContact(Contact contact)
        {
            var validationError = ValidateContact(contact);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            this.context.Set<Contact>().Add(contact);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetContact), new { id = contact.ContactID }, contact);
        }

        // POST: api/contact/manage
        // Creates a Contact (+ optional Address/PhoneContact) and links it to an owner entity.
        [HttpPost("manage")]
        public async Task<ActionResult<object>> PostManagedContact(ManageContactRequest request)
        {
            if (request == null)
            {
                return this.BadRequest("Request body is required.");
            }

            var normalizedContext = request.Context?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedContext) || !ValidContexts.Contains(normalizedContext))
            {
                return this.BadRequest($"Invalid context. Valid values: {string.Join(", ", ValidContexts)}.");
            }

            if (request.EntityID <= 0)
            {
                return this.BadRequest("EntityID must be a positive integer.");
            }

            var normalizedLabel = request.Label?.Trim();
            var resolvedLabel = await this.ResolveContactLabelAsync(normalizedLabel).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(normalizedLabel) && resolvedLabel == null)
            {
                return this.BadRequest("Label is not recognized. Select one of the shared contact labels.");
            }

            var resolvedLabelId = resolvedLabel?.ContactLabelID;
            var resolvedLabelText = resolvedLabel?.Label ?? normalizedLabel;

            if (!this.EntityExists(normalizedContext, request.EntityID))
            {
                return this.NotFound($"{normalizedContext} entity {request.EntityID} was not found.");
            }

            var normalizedType = request.ContactType?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                return this.BadRequest("ContactType is required.");
            }

            if (normalizedType is not ("address" or "phone" or "email" or "url"))
            {
                return this.BadRequest("ContactType must be one of: address, phone, email, url.");
            }

            var defaultPrivate = normalizedContext == "user";

            Address? address = null;
            PhoneContact? phoneContact = null;

            if (normalizedType == "address")
            {
                if (string.IsNullOrWhiteSpace(request.AddressLine1))
                {
                    return this.BadRequest("AddressLine1 is required for address contacts.");
                }

                if (string.IsNullOrWhiteSpace(request.Country))
                {
                    return this.BadRequest("Country is required for address contacts.");
                }

                address = new Address
                {
                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    AddressLine3 = request.AddressLine3,
                    AddressLine4 = request.AddressLine4,
                    City = request.City,
                    State = request.State,
                    ZipCode = request.ZipCode,
                    Country = request.Country,
                    Region = !string.IsNullOrWhiteSpace(request.Region)
                        ? request.Region
                        : request.State ?? "Unknown",
                    OperationHours = request.OperationHours,
                    ContactLabelID = resolvedLabelId,
                };

                this.context.Addresses.Add(address);
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }

            if (normalizedType == "phone")
            {
                var phoneValue = !string.IsNullOrWhiteSpace(request.PhoneNumber)
                    ? request.PhoneNumber
                    : request.Value;

                if (string.IsNullOrWhiteSpace(phoneValue))
                {
                    return this.BadRequest("PhoneNumber (or Value) is required for phone contacts.");
                }

                phoneContact = new PhoneContact
                {
                    PhoneContactLabelID = request.PhoneContactLabelID,
                    ContactLabelID = resolvedLabelId,
                    PhoneNumber = phoneValue,
                    Description = request.PhoneDescription,
                };

                this.context.PhoneContacts.Add(phoneContact);
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }

            if ((normalizedType == "email" || normalizedType == "url") && string.IsNullOrWhiteSpace(request.Value))
            {
                return this.BadRequest("Value is required for email and url contacts.");
            }

            if (normalizedType == "url")
            {
                if (!TryNormalizeUrl(request.Value, out var normalizedUrl))
                {
                    return this.BadRequest("Value must be a valid URL.");
                }

                var looksLikeSocialHandleSubmission =
                    !string.IsNullOrWhiteSpace(request.Handle) ||
                    (!string.IsNullOrWhiteSpace(request.Category) &&
                     SocialHandleCategories.Contains(request.Category.Trim()));

                if (IsBlockedUrl(normalizedUrl, looksLikeSocialHandleSubmission, out var blockedReason))
                {
                    return this.BadRequest(blockedReason);
                }

                request.Value = normalizedUrl;
            }

            var contact = new Models.Contact
            {
                ContactType = normalizedType,
                Label = resolvedLabelText,
                ContactLabelID = resolvedLabelId,
                Category = request.Category,
                Value = normalizedType == "phone"
                    ? null
                    : request.Value,
                Handle = request.Handle,
                Description = request.Description,
                AddressID = address?.AddressID,
                PhoneContactID = phoneContact?.PhoneContactID,
            };

            this.context.Contacts.Add(contact);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            var isPrivate = request.IsPrivate ?? defaultPrivate;
            var scope = isPrivate ? ContactScope.Private : ContactScope.Secondary;
            var entityType = NormalizeContextToEntityType(normalizedContext);

            var link = new Linker_EntityToContact
            {
                ContactID = contact.ContactID,
                EntityType = entityType,
                EntityID = request.EntityID,
                Scope = scope,
                DisplayOrder = request.DisplayOrder ?? 0,
            };

            this.context.Linker_EntityToContacts.Add(link);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(
                nameof(this.GetContact),
                new { id = contact.ContactID },
                new
                {
                    context = normalizedContext,
                    entityId = request.EntityID,
                    contactId = contact.ContactID,
                    linkId = link.Linker_EntityToContactID,
                    isPrivate,
                    primaryUpdated = !isPrivate && (request.DisplayOrder ?? 0) == 0,
                });
        }

        [HttpPut("order/{context}/{entityId:int}")]
        public async Task<IActionResult> PutContactOrder(string context, int entityId, UpdateContactOrderRequest request)
        {
            if (request == null || request.LinkIds == null || request.LinkIds.Count == 0)
            {
                return this.BadRequest("LinkIds are required.");
            }

            if (string.IsNullOrWhiteSpace(context))
            {
                return this.BadRequest($"Invalid context. Valid values: {string.Join(", ", ValidContexts)}.");
            }

            var normalizedContext = context.Trim().ToLowerInvariant();
            if (!ValidContexts.Contains(normalizedContext))
            {
                return this.BadRequest($"Invalid context. Valid values: {string.Join(", ", ValidContexts)}.");
            }

            if (!this.EntityExists(normalizedContext, entityId))
            {
                return this.NotFound($"{normalizedContext} entity {entityId} was not found.");
            }

            var normalizedIds = request.LinkIds.Distinct().ToArray();
            if (normalizedIds.Length == 0)
            {
                return this.BadRequest("LinkIds are required.");
            }

            var entityType = NormalizeContextToEntityType(normalizedContext);

            var query = this.context.Set<Linker_EntityToContact>()
                .Where(l => normalizedIds.Contains(l.Linker_EntityToContactID))
                .Where(l => l.EntityType == entityType && l.EntityID == entityId);

            var links = await query.ToListAsync().ConfigureAwait(false);
            if (links.Count != normalizedIds.Length)
            {
                return this.BadRequest("Some link IDs do not belong to the requested entity context.");
            }

            var orderMap = request.LinkIds.Select((id, index) => new { id, index })
                .GroupBy(x => x.id)
                .ToDictionary(g => g.Key, g => g.First().index);

            foreach (var link in links)
            {
                link.DisplayOrder = orderMap[link.Linker_EntityToContactID];
            }

            await this.context.SaveChangesAsync().ConfigureAwait(false);
            return this.NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutContact(int id, Contact contact)
        {
            if (id != contact.ContactID)
            {
                return this.BadRequest();
            }

            var validationError = ValidateContact(contact);
            if (validationError != null)
            {
                return this.BadRequest(validationError);
            }

            this.context.Entry(contact).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.ContactExists(id))
                {
                    return this.NotFound();
                }

                throw;
            }

            return this.NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(int id)
        {
            var contact = await this.context.Set<Contact>().FindAsync(id).ConfigureAwait(false);
            if (contact == null)
            {
                return this.NotFound();
            }

            this.context.Set<Contact>().Remove(contact);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private static string? ValidateContact(Contact contact)
        {
            var normalizedType = contact.ContactType?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                return "ContactType is required.";
            }

            switch (normalizedType)
            {
                case "address":
                    if (!contact.AddressID.HasValue)
                    {
                        return "Address contacts require AddressID.";
                    }

                    if (contact.PhoneContactID.HasValue || !string.IsNullOrWhiteSpace(contact.Value))
                    {
                        return "Address contacts cannot set PhoneContactID or Value.";
                    }

                    break;
                case "phone":
                    if (!contact.PhoneContactID.HasValue)
                    {
                        return "Phone contacts require PhoneContactID.";
                    }

                    if (contact.AddressID.HasValue || !string.IsNullOrWhiteSpace(contact.Value))
                    {
                        return "Phone contacts cannot set AddressID or Value.";
                    }

                    break;
                case "email":
                case "url":
                    if (string.IsNullOrWhiteSpace(contact.Value))
                    {
                        return "Phone, email, and url contacts require Value.";
                    }

                    if (contact.AddressID.HasValue || contact.PhoneContactID.HasValue)
                    {
                        return "Email and url contacts cannot set AddressID or PhoneContactID.";
                    }

                    break;
                default:
                    return "ContactType must be one of: address, phone, email, url.";
            }

            contact.ContactType = normalizedType;
            return null;
        }

        private static string NormalizeContextToEntityType(string normalizedContext)
        {
            return normalizedContext switch
            {
                "artist" => LinkedEntityTypes.Artist,
                "user" => LinkedEntityTypes.User,
                "venue" => LinkedEntityTypes.Venue,
                "vendor" => LinkedEntityTypes.Vendor,
                _ => normalizedContext,
            };
        }

        private IQueryable<Contact> PublicContactsQuery()
        {
            return this.context.Set<Contact>()
                .AsNoTracking()
                .Where(c =>
                    c.EntityLinks.Any(l => l.Scope != ContactScope.Private));
        }

        private IQueryable<Linker_EntityToContact> PublicEntityLinksQuery()
        {
            return this.context.Set<Linker_EntityToContact>()
                .AsNoTracking()
                .Where(l => l.Scope != ContactScope.Private);
        }

        private async Task<bool> IsEntityPubliclyVisibleAsync(string contextName, int entityId)
        {
            return contextName switch
            {
                "artist" => await this.context.Artists
                    .AnyAsync(a => a.ArtistID == entityId && a.IsPublished && !a.IsModerationBlocked)
                    .ConfigureAwait(false),
                "user" => await this.context.Users
                    .Include(u => u.UserPrivacy)
                    .AnyAsync(u =>
                        u.UserID == entityId &&
                        u.IsPublished &&
                        !u.IsModerationBlocked &&
                        !u.HideFromPublic &&
                        (u.UserPrivacy == null || !u.UserPrivacy.HideProfileFromPublic))
                    .ConfigureAwait(false),
                "venue" => await this.context.Venues
                    .AnyAsync(v => v.VenueID == entityId && v.IsPublished && !v.IsModerationBlocked)
                    .ConfigureAwait(false),
                "vendor" => await this.context.Vendors
                    .AnyAsync(v => v.VendorID == entityId && v.IsPublished && !v.IsModerationBlocked)
                    .ConfigureAwait(false),
                _ => false,
            };
        }

        private bool EntityExists(string contextName, int entityId)
        {
            return contextName switch
            {
                "artist" => this.context.Artists.Any(a => a.ArtistID == entityId),
                "user" => this.context.Users.Any(u => u.UserID == entityId),
                "venue" => this.context.Venues.Any(v => v.VenueID == entityId),
                "vendor" => this.context.Vendors.Any(v => v.VendorID == entityId),
                _ => false,
            };
        }

        private bool ContactExists(int id)
        {
            return this.context.Set<Contact>().Any(e => e.ContactID == id);
        }

        private async Task<ContactLabel?> ResolveContactLabelAsync(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            var normalized = label.Trim().ToLowerInvariant();
            return await this.context.Set<ContactLabel>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cl => cl.Label.ToLower() == normalized)
                .ConfigureAwait(false);
        }

        private static bool TryNormalizeUrl(string? rawValue, out string normalizedUrl)
        {
            normalizedUrl = string.Empty;
            var value = rawValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = $"https://{value}";
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            normalizedUrl = parsed.ToString();
            return true;
        }

        private static bool IsBlockedUrl(string normalizedUrl, bool allowSocialDomains, out string reason)
        {
            reason = string.Empty;

            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var parsed))
            {
                reason = "URL is invalid.";
                return true;
            }

            var host = parsed.Host.Trim().ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host[4..];
            }

            if (!allowSocialDomains && SocialHandleDomains.Any(d => host == d || host.EndsWith($".{d}", StringComparison.Ordinal)))
            {
                reason = "That platform belongs in Social Handles. Please add it there instead.";
                return true;
            }

            if (BlockedAdultDomains.Any(d => host == d || host.EndsWith($".{d}", StringComparison.Ordinal)))
            {
                reason = "Adult-content domains are not supported for profile links.";
                return true;
            }

            return false;
        }
    }

    public sealed class ManageContactRequest
    {
        public string? Context { get; set; }

        public int EntityID { get; set; }

        public string? ContactType { get; set; }

        public string? Label { get; set; }

        public string? Category { get; set; }

        public string? Value { get; set; }

        public string? Handle { get; set; }

        public string? Description { get; set; }

        public bool? IsPrivate { get; set; }

        public int? DisplayOrder { get; set; }

        public string? PhoneNumber { get; set; }

        public string? PhoneDescription { get; set; }

        public int? PhoneContactLabelID { get; set; }

        public string? AddressLine1 { get; set; }

        public string? AddressLine2 { get; set; }

        public string? AddressLine3 { get; set; }

        public string? AddressLine4 { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        public string? ZipCode { get; set; }

        public string? Country { get; set; }

        public string? Region { get; set; }

        public string? OperationHours { get; set; }
    }

    public sealed class UpdateContactOrderRequest
    {
        public List<int> LinkIds { get; set; } = new();
    }
}