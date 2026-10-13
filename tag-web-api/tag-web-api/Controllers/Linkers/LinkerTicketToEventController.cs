// <copyright file="LinkerTicketToEventController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class Linker_TicketToEventController : ControllerBase
{
    private readonly TAGDBContext context;

    public Linker_TicketToEventController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Linker_TicketToEvent>>> Get()
    {
        return await this.context.Set<Linker_TicketToEvent>().ToListAsync().ConfigureAwait(false);
    }

    [HttpPost]
    public async Task<ActionResult<Linker_TicketToEvent>> Create(Linker_TicketToEvent linker_TicketToEvent)
    {
        this.context.Set<Linker_TicketToEvent>().Add(linker_TicketToEvent);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = linker_TicketToEvent.Linker_TicketToEventID }, linker_TicketToEvent);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Linker_TicketToEvent linker_TicketToEvent)
    {
        if (id != linker_TicketToEvent.Linker_TicketToEventID)
        {
            return this.BadRequest();
        }

        this.context.Entry(linker_TicketToEvent).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.Linker_TicketToEventExists(id))
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
    public async Task<IActionResult> Delete(int id)
    {
        var linker_TicketToEvent = await this.context.Set<Linker_TicketToEvent>().FindAsync(id).ConfigureAwait(false);
        if (linker_TicketToEvent == null)
        {
            return this.NotFound();
        }

        this.context.Set<Linker_TicketToEvent>().Remove(linker_TicketToEvent);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool Linker_TicketToEventExists(int id)
    {
        return this.context.Set<Linker_TicketToEvent>().Any(e => e.Linker_TicketToEventID == id);
    }
}
