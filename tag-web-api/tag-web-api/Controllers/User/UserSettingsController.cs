// <copyright file="UserSettingsController.cs" company="Twisted Artists Guild">
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
    public class UserSettingsController : ControllerBase
    {
        private readonly TAGDBContext context;

        public UserSettingsController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserSettings>>> GetUserSettings()
        {
            return await this.context.Set<UserSettings>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserSettings>> GetUserSettings(int id)
        {
            var userSettings = await this.context.Set<UserSettings>().FindAsync(id).ConfigureAwait(false);

            if (userSettings == null)
            {
                return this.NotFound();
            }

            return userSettings;
        }

        [HttpGet("by-user/{userId}")]
        public async Task<ActionResult<UserSettings>> GetUserSettingsByUserId(int userId)
        {
            var userSettings = await this.context.Set<UserSettings>()
                .FirstOrDefaultAsync(row => row.UserID == userId)
                .ConfigureAwait(false);

            if (userSettings == null)
            {
                return this.NotFound();
            }

            return userSettings;
        }

        [HttpPost]
        public async Task<ActionResult<UserSettings>> PostUserSettings(UserSettings userSettings)
        {
            this.context.Set<UserSettings>().Add(userSettings);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetUserSettings), new { id = userSettings.UserSettingsID }, userSettings);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUserSettings(int id, UserSettings userSettings)
        {
            if (id != userSettings.UserSettingsID)
            {
                return this.BadRequest();
            }

            this.context.Entry(userSettings).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.UserSettingsExists(id))
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
        public async Task<IActionResult> DeleteUserSettings(int id)
        {
            var userSettings = await this.context.Set<UserSettings>().FindAsync(id).ConfigureAwait(false);
            if (userSettings == null)
            {
                return this.NotFound();
            }

            this.context.Set<UserSettings>().Remove(userSettings);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool UserSettingsExists(int id)
        {
            return this.context.Set<UserSettings>().Any(e => e.UserSettingsID == id);
        }
    }
}
