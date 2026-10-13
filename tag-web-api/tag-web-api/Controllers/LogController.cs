// <copyright file="LogController.cs" company="Twisted Artists Guild">
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
    public class LogController : ControllerBase
    {
        private readonly TAGDBContext context;

        public LogController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/Log
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Log>>> GetLogs()
        {
            return await this.context.Set<Log>().ToListAsync().ConfigureAwait(false);
        }

        // GET: api/Log/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Log>> GetLog(int id)
        {
            var log = await this.context.Set<Log>().FindAsync(id).ConfigureAwait(false);

            if (log == null)
            {
                return this.NotFound();
            }

            return log;
        }

        // POST: api/Log
        [HttpPost]
        public async Task<ActionResult<Log>> PostLog(Log log)
        {
            this.context.Set<Log>().Add(log);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction("GetLog", new { id = log.LogID }, log);
        }

        // PUT: api/Log/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLog(int id, Log log)
        {
            if (id != log.LogID)
            {
                return this.BadRequest();
            }

            this.context.Entry(log).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.LogExists(id))
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

        // DELETE: api/Log/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLog(int id)
        {
            var log = await this.context.Set<Log>().FindAsync(id).ConfigureAwait(false);
            if (log == null)
            {
                return this.NotFound();
            }

            this.context.Set<Log>().Remove(log);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool LogExists(int id)
        {
            return this.context.Set<Log>().Any(e => e.LogID == id);
        }
    }
}
