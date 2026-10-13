// <copyright file="UserPrivacyController.cs" company="Twisted Artists Guild">
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
    public class UserPrivacyController : ControllerBase
    {
        private readonly TAGDBContext context;

        public UserPrivacyController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserPrivacy>>> GetUserPrivacies([FromQuery] int moderatorUserId)
        {
            if (!await this.IsModeratorAsync(moderatorUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            return await this.context.Set<UserPrivacy>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserPrivacy>> GetUserPrivacy(int id, [FromQuery] int viewerUserId)
        {
            var userPrivacy = await this.context.Set<UserPrivacy>().FindAsync(id).ConfigureAwait(false);

            if (userPrivacy == null)
            {
                return this.NotFound();
            }

            if (!await this.HasPrivilegedPrivacyAccessAsync(userPrivacy.UserID, viewerUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            return userPrivacy;
        }

        [HttpGet("by-user/{userId}")]
        public async Task<ActionResult<UserPrivacy>> GetUserPrivacyByUserId(int userId, [FromQuery] int viewerUserId)
        {
            if (!await this.HasPrivilegedPrivacyAccessAsync(userId, viewerUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            var userPrivacy = await this.context.Set<UserPrivacy>()
                .FirstOrDefaultAsync(row => row.UserID == userId)
                .ConfigureAwait(false);

            if (userPrivacy == null)
            {
                return this.NotFound();
            }

            return userPrivacy;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<UserPrivacy>> PostUserPrivacy(UserPrivacy userPrivacy, [FromQuery] int viewerUserId)
        {
            ArgumentNullException.ThrowIfNull(userPrivacy);

            if (!await this.HasPrivilegedPrivacyAccessAsync(userPrivacy.UserID, viewerUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            this.context.Set<UserPrivacy>().Add(userPrivacy);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetUserPrivacy), new { id = userPrivacy.UserPrivacyID, viewerUserId }, userPrivacy);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutUserPrivacy(int id, UserPrivacy userPrivacy, [FromQuery] int viewerUserId)
        {
            ArgumentNullException.ThrowIfNull(userPrivacy);

            if (id != userPrivacy.UserPrivacyID)
            {
                return this.BadRequest();
            }

            var existing = await this.context.Set<UserPrivacy>()
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.UserPrivacyID == id)
                .ConfigureAwait(false);
            if (existing == null)
            {
                return this.NotFound();
            }

            if (userPrivacy.UserID != existing.UserID)
            {
                return this.BadRequest("UserID cannot be changed.");
            }

            if (!await this.HasPrivilegedPrivacyAccessAsync(existing.UserID, viewerUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            this.context.Entry(userPrivacy).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.UserPrivacyExists(id))
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
        [Authorize]
        public async Task<IActionResult> DeleteUserPrivacy(int id, [FromQuery] int viewerUserId)
        {
            var userPrivacy = await this.context.Set<UserPrivacy>().FindAsync(id).ConfigureAwait(false);
            if (userPrivacy == null)
            {
                return this.NotFound();
            }

            if (!await this.HasPrivilegedPrivacyAccessAsync(userPrivacy.UserID, viewerUserId).ConfigureAwait(false))
            {
                return this.Forbid();
            }

            this.context.Set<UserPrivacy>().Remove(userPrivacy);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool UserPrivacyExists(int id)
        {
            return this.context.Set<UserPrivacy>().Any(e => e.UserPrivacyID == id);
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

        private async Task<bool> IsActiveStaffAsync(int userId)
        {
            if (userId <= 0)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            return await this.context.Staffs
                .AnyAsync(staff => staff.UserID == userId && staff.Active && (!staff.LeaveDate.HasValue || staff.LeaveDate.Value > now))
                .ConfigureAwait(false);
        }

        private async Task<bool> HasPrivilegedPrivacyAccessAsync(int targetUserId, int viewerUserId)
        {
            if (viewerUserId <= 0)
            {
                return false;
            }

            if (targetUserId == viewerUserId)
            {
                return true;
            }

            if (await this.IsModeratorAsync(viewerUserId).ConfigureAwait(false))
            {
                return true;
            }

            return await this.IsActiveStaffAsync(viewerUserId).ConfigureAwait(false);
        }
    }
}
