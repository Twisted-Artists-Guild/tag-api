// <copyright file="LinkerVendorToUserController.cs" company="Twisted Artists Guild">
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
    public class Linker_VendorToUserController : ControllerBase
    {
        private readonly TAGDBContext context;

        public Linker_VendorToUserController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/Linker_VendorToUser
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Linker_VendorToUser>>> GetLinker_VendorToUsers()
        {
            return await this.context.Set<Linker_VendorToUser>().ToListAsync().ConfigureAwait(false);
        }

        // GET: api/Linker_VendorToUser/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Linker_VendorToUser>> GetLinker_VendorToUser(int id)
        {
            var linker_VendorToUser = await this.context.Set<Linker_VendorToUser>().FindAsync(id).ConfigureAwait(false);

            if (linker_VendorToUser == null)
            {
                return this.NotFound();
            }

            return linker_VendorToUser;
        }

        // POST: api/Linker_VendorToUser
        [HttpPost]
        public async Task<ActionResult<Linker_VendorToUser>> PostLinker_VendorToUser(Linker_VendorToUser linker_VendorToUser)
        {
            this.context.Set<Linker_VendorToUser>().Add(linker_VendorToUser);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetLinker_VendorToUser), new { id = linker_VendorToUser.Linker_VendorToUserID }, linker_VendorToUser);
        }

        // PUT: api/Linker_VendorToUser/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLinker_VendorToUser(int id, Linker_VendorToUser linker_VendorToUser)
        {
            if (id != linker_VendorToUser.Linker_VendorToUserID)
            {
                return this.BadRequest();
            }

            this.context.Entry(linker_VendorToUser).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.Linker_VendorToUserExists(id))
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

        // DELETE: api/Linker_VendorToUser/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLinker_VendorToUser(int id)
        {
            var linker_VendorToUser = await this.context.Set<Linker_VendorToUser>().FindAsync(id).ConfigureAwait(false);
            if (linker_VendorToUser == null)
            {
                return this.NotFound();
            }

            this.context.Set<Linker_VendorToUser>().Remove(linker_VendorToUser);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool Linker_VendorToUserExists(int id)
        {
            return this.context.Set<Linker_VendorToUser>().Any(e => e.Linker_VendorToUserID == id);
        }
    }
}
