// <copyright file="LinkerUserFavoriteController.cs" company="Twisted Artists Guild">
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
    public class Linker_UserFavoriteController : ControllerBase
    {
        private readonly TAGDBContext context;

        public Linker_UserFavoriteController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/Linker_UserFavorite
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Linker_UserFavorite>>> GetLinker_UserFavorites()
        {
            return await this.context.Set<Linker_UserFavorite>().ToListAsync().ConfigureAwait(false);
        }

        // GET: api/Linker_UserFavorite/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Linker_UserFavorite>> GetLinker_UserFavorite(int id)
        {
            var linker_UserFavorite = await this.context.Set<Linker_UserFavorite>().FindAsync(id).ConfigureAwait(false);

            if (linker_UserFavorite == null)
            {
                return this.NotFound();
            }

            return linker_UserFavorite;
        }

        // POST: api/Linker_UserFavorite
        [HttpPost]
        public async Task<ActionResult<Linker_UserFavorite>> PostLinker_UserFavorite(Linker_UserFavorite linker_UserFavorite)
        {
            this.context.Set<Linker_UserFavorite>().Add(linker_UserFavorite);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetLinker_UserFavorite), new { id = linker_UserFavorite.Linker_UserFavoriteID }, linker_UserFavorite);
        }

        // PUT: api/Linker_UserFavorite/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLinker_UserFavorite(int id, Linker_UserFavorite linker_UserFavorite)
        {
            if (id != linker_UserFavorite.Linker_UserFavoriteID)
            {
                return this.BadRequest();
            }

            this.context.Entry(linker_UserFavorite).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.Linker_UserFavoriteExists(id))
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

        // DELETE: api/Linker_UserFavorite/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLinker_UserFavorite(int id)
        {
            var linker_UserFavorite = await this.context.Set<Linker_UserFavorite>().FindAsync(id).ConfigureAwait(false);
            if (linker_UserFavorite == null)
            {
                return this.NotFound();
            }

            this.context.Set<Linker_UserFavorite>().Remove(linker_UserFavorite);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool Linker_UserFavoriteExists(int id)
        {
            return this.context.Set<Linker_UserFavorite>().Any(e => e.Linker_UserFavoriteID == id);
        }
    }
}
