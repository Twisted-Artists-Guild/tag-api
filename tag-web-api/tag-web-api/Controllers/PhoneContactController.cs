// <copyright file="PhoneContactController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PhoneContactController : ControllerBase
    {
        private const int DefaultLabelId = 2;
        private readonly TAGDBContext context;

        public PhoneContactController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/PhoneContact
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PhoneContact>>> GetPhoneContacts()
        {
            return await this.context.Set<PhoneContact>()
                .Include(pc => pc.PhoneContactLabel)
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);
        }

        // GET: api/PhoneContact/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PhoneContact>> GetPhoneContact(int id)
        {
            var phoneContact = await this.context.Set<PhoneContact>()
                .Include(pc => pc.PhoneContactLabel)
                .AsNoTracking()
                .FirstOrDefaultAsync(pc => pc.PhoneContactID == id)
                .ConfigureAwait(false);

            if (phoneContact == null)
            {
                return this.NotFound();
            }

            return phoneContact;
        }

        // POST: api/PhoneContact
        [HttpPost]
        public async Task<ActionResult<PhoneContact>> PostPhoneContact(PhoneContact phoneContact)
        {
            if (phoneContact.PhoneContactLabelID <= 0)
            {
                phoneContact.PhoneContactLabelID = DefaultLabelId;
            }

            this.context.Set<PhoneContact>().Add(phoneContact);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction("GetPhoneContact", new { id = phoneContact.PhoneContactID }, phoneContact);
        }

        // PUT: api/PhoneContact/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPhoneContact(int id, PhoneContact phoneContact)
        {
            if (id != phoneContact.PhoneContactID)
            {
                return this.BadRequest();
            }

            if (phoneContact.PhoneContactLabelID <= 0)
            {
                phoneContact.PhoneContactLabelID = DefaultLabelId;
            }

            this.context.Entry(phoneContact).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.PhoneContactExists(id))
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

        // DELETE: api/PhoneContact/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoneContact(int id)
        {
            var phoneContact = await this.context.Set<PhoneContact>().FindAsync(id).ConfigureAwait(false);
            if (phoneContact == null)
            {
                return this.NotFound();
            }

            this.context.Set<PhoneContact>().Remove(phoneContact);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool PhoneContactExists(int id)
        {
            return this.context.Set<PhoneContact>().Any(e => e.PhoneContactID == id);
        }
    }
}
