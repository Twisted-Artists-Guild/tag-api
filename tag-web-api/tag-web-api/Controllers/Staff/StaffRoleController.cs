// <copyright file="StaffRoleController.cs" company="Twisted Artists Guild">
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
    public class StaffRoleController : ControllerBase
    {
        private readonly TAGDBContext context;

        public StaffRoleController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<StaffRole>>> GetStaffRoles()
        {
            return await this.context.Set<StaffRole>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<StaffRole>> GetStaffRole(int id)
        {
            var staffRole = await this.context.Set<StaffRole>().FindAsync(id).ConfigureAwait(false);

            if (staffRole == null)
            {
                return this.NotFound();
            }

            return staffRole;
        }

        [HttpPost]
        public async Task<ActionResult<StaffRole>> PostStaffRole(StaffRole staffRole)
        {
            this.context.Set<StaffRole>().Add(staffRole);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetStaffRole), new { id = staffRole.StaffRoleID }, staffRole);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutStaffRole(int id, StaffRole staffRole)
        {
            if (id != staffRole.StaffRoleID)
            {
                return this.BadRequest();
            }

            this.context.Entry(staffRole).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.StaffRoleExists(id))
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
        public async Task<IActionResult> DeleteStaffRole(int id)
        {
            var staffRole = await this.context.Set<StaffRole>().FindAsync(id).ConfigureAwait(false);
            if (staffRole == null)
            {
                return this.NotFound();
            }

            this.context.Set<StaffRole>().Remove(staffRole);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool StaffRoleExists(int id)
        {
            return this.context.Set<StaffRole>().Any(e => e.StaffRoleID == id);
        }
    }
}
