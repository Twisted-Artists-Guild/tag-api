// <copyright file="StaffController.cs" company="Twisted Artists Guild">
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
    public class StaffController : ControllerBase
    {
        private readonly TAGDBContext context;

        public StaffController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Staff>>> GetStaff()
        {
            return await this.context.Set<Staff>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Staff>> GetStaff(int id)
        {
            var staff = await this.context.Set<Staff>().FindAsync(id).ConfigureAwait(false);

            if (staff == null)
            {
                return this.NotFound();
            }

            return staff;
        }

        [HttpPost]
        public async Task<ActionResult<Staff>> PostStaff(Staff staff)
        {
            this.context.Set<Staff>().Add(staff);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetStaff), new { id = staff.StaffID }, staff);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutStaff(int id, Staff staff)
        {
            if (id != staff.StaffID)
            {
                return this.BadRequest();
            }

            this.context.Entry(staff).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.StaffExists(id))
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
        public async Task<IActionResult> DeleteStaff(int id)
        {
            var staff = await this.context.Set<Staff>().FindAsync(id).ConfigureAwait(false);
            if (staff == null)
            {
                return this.NotFound();
            }

            this.context.Set<Staff>().Remove(staff);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool StaffExists(int id)
        {
            return this.context.Set<Staff>().Any(e => e.StaffID == id);
        }
    }
}
