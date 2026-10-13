// <copyright file="ResolutionController.cs" company="Twisted Artists Guild">
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
    public class ResolutionController : ControllerBase
    {
        private readonly TAGDBContext context;

        public ResolutionController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/Resolution
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Resolution>>> GetResolutions()
        {
            return await this.context.Set<Resolution>().ToListAsync().ConfigureAwait(false);
        }

        // GET: api/Resolution/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Resolution>> GetResolution(int id)
        {
            var resolution = await this.context.Set<Resolution>().FindAsync(id).ConfigureAwait(false);

            if (resolution == null)
            {
                return this.NotFound();
            }

            return resolution;
        }

        // POST: api/Resolution
        [HttpPost]
        public async Task<ActionResult<Resolution>> PostResolution(Resolution resolution)
        {
            this.context.Set<Resolution>().Add(resolution);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction("GetResolution", new { id = resolution.ResolutionID }, resolution);
        }

        // PUT: api/Resolution/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutResolution(int id, Resolution resolution)
        {
            if (id != resolution.ResolutionID)
            {
                return this.BadRequest();
            }

            this.context.Entry(resolution).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.ResolutionExists(id))
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

        // DELETE: api/Resolution/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResolution(int id)
        {
            var resolution = await this.context.Set<Resolution>().FindAsync(id).ConfigureAwait(false);
            if (resolution == null)
            {
                return this.NotFound();
            }

            this.context.Set<Resolution>().Remove(resolution);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool ResolutionExists(int id)
        {
            return this.context.Set<Resolution>().Any(e => e.ResolutionID == id);
        }
    }
}
