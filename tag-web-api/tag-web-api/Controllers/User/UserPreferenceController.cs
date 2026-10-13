// <copyright file="UserPreferenceController.cs" company="Twisted Artists Guild">
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
    public class UserPreferenceController : ControllerBase
    {
        private readonly TAGDBContext context;

        public UserPreferenceController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserPreference>>> GetUserPreferences()
        {
            return await this.context.Set<UserPreference>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserPreference>> GetUserPreference(int id)
        {
            var userPreference = await this.context.Set<UserPreference>().FindAsync(id).ConfigureAwait(false);

            if (userPreference == null)
            {
                return this.NotFound();
            }

            return userPreference;
        }

        [HttpGet("by-user/{userId}")]
        public async Task<ActionResult<UserPreference>> GetUserPreferenceByUserId(int userId)
        {
            var userPreference = await this.context.Set<UserPreference>()
                .FirstOrDefaultAsync(row => row.UserID == userId)
                .ConfigureAwait(false);

            if (userPreference == null)
            {
                return this.NotFound();
            }

            return userPreference;
        }

        [HttpPost]
        public async Task<ActionResult<UserPreference>> PostUserPreference(UserPreference userPreference)
        {
            this.context.Set<UserPreference>().Add(userPreference);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetUserPreference), new { id = userPreference.UserPreferenceID }, userPreference);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUserPreference(int id, UserPreference userPreference)
        {
            if (id != userPreference.UserPreferenceID)
            {
                return this.BadRequest();
            }

            this.context.Entry(userPreference).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.UserPreferenceExists(id))
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
        public async Task<IActionResult> DeleteUserPreference(int id)
        {
            var userPreference = await this.context.Set<UserPreference>().FindAsync(id).ConfigureAwait(false);
            if (userPreference == null)
            {
                return this.NotFound();
            }

            this.context.Set<UserPreference>().Remove(userPreference);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool UserPreferenceExists(int id)
        {
            return this.context.Set<UserPreference>().Any(e => e.UserPreferenceID == id);
        }
    }
}
