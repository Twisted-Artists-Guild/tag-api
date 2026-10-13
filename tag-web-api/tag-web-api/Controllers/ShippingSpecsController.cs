// <copyright file="ShippingSpecsController.cs" company="Twisted Artists Guild">
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
    public class ShippingSpecsController : ControllerBase
    {
        private readonly TAGDBContext context;

        public ShippingSpecsController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShippingSpecs>>> GetShippingSpecs()
        {
            return await this.context.Set<ShippingSpecs>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ShippingSpecs>> GetShippingSpecs(int id)
        {
            var shippingSpecs = await this.context.Set<ShippingSpecs>().FindAsync(id).ConfigureAwait(false);

            if (shippingSpecs == null)
            {
                return this.NotFound();
            }

            return shippingSpecs;
        }

        [HttpPost]
        public async Task<ActionResult<ShippingSpecs>> PostShippingSpecs(ShippingSpecs shippingSpecs)
        {
            this.context.Set<ShippingSpecs>().Add(shippingSpecs);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetShippingSpecs), new { id = shippingSpecs.ShippingSpecsID }, shippingSpecs);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutShippingSpecs(int id, ShippingSpecs shippingSpecs)
        {
            if (id != shippingSpecs.ShippingSpecsID)
            {
                return this.BadRequest();
            }

            this.context.Entry(shippingSpecs).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.ShippingSpecsExists(id))
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
        public async Task<IActionResult> DeleteShippingSpecs(int id)
        {
            var shippingSpecs = await this.context.Set<ShippingSpecs>().FindAsync(id).ConfigureAwait(false);
            if (shippingSpecs == null)
            {
                return this.NotFound();
            }

            this.context.Set<ShippingSpecs>().Remove(shippingSpecs);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool ShippingSpecsExists(int id)
        {
            return this.context.Set<ShippingSpecs>().Any(e => e.ShippingSpecsID == id);
        }
    }
}
