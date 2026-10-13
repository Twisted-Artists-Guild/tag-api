using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TicketTypeController : ControllerBase
    {
        private readonly TAGDBContext context;

        public TicketTypeController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketType>>> GetTicketTypes()
        {
            return await this.context.Set<TicketType>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TicketType>> GetTicketType(int id)
        {
            var ticketType = await this.context.Set<TicketType>().FindAsync(id).ConfigureAwait(false);

            if (ticketType == null)
            {
                return this.NotFound();
            }

            return ticketType;
        }

        [HttpPost]
        public async Task<ActionResult<TicketType>> PostTicketType(TicketType ticketType)
        {
            this.context.Set<TicketType>().Add(ticketType);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(this.GetTicketType), new { id = ticketType.TicketTypeID }, ticketType);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketType(int id, TicketType ticketType)
        {
            if (id != ticketType.TicketTypeID)
            {
                return this.BadRequest();
            }

            this.context.Entry(ticketType).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.TicketTypeExists(id))
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
        public async Task<IActionResult> DeleteTicketType(int id)
        {
            var ticketType = await this.context.Set<TicketType>().FindAsync(id).ConfigureAwait(false);
            if (ticketType == null)
            {
                return this.NotFound();
            }

            this.context.Set<TicketType>().Remove(ticketType);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool TicketTypeExists(int id)
        {
            return this.context.Set<TicketType>().Any(e => e.TicketTypeID == id);
        }
    }
}
