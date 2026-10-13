// <copyright file="DigitalDeliverySpecsController.cs" company="Twisted Artists Guild">
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
public class DigitalDeliverySpecsController : ControllerBase
{
    private readonly TAGDBContext context;

    public DigitalDeliverySpecsController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DigitalDeliverySpecs>>> Get()
    {
        return await this.context.Set<DigitalDeliverySpecs>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DigitalDeliverySpecs>> Get(int id)
    {
        var digitalDeliverySpecs = await this.context.Set<DigitalDeliverySpecs>().FindAsync(id).ConfigureAwait(false);
        if (digitalDeliverySpecs == null)
        {
            return this.NotFound();
        }

        return digitalDeliverySpecs;
    }

    [HttpPost]
    public async Task<ActionResult<DigitalDeliverySpecs>> Create(DigitalDeliverySpecs digitalDeliverySpecs)
    {
        this.context.Set<DigitalDeliverySpecs>().Add(digitalDeliverySpecs);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = digitalDeliverySpecs.DigitalDeliverySpecsID }, digitalDeliverySpecs);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, DigitalDeliverySpecs digitalDeliverySpecs)
    {
        if (id != digitalDeliverySpecs.DigitalDeliverySpecsID)
        {
            return this.BadRequest();
        }

        this.context.Entry(digitalDeliverySpecs).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.DigitalDeliverySpecsExists(id))
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
        var digitalDeliverySpecs = await this.context.Set<DigitalDeliverySpecs>().FindAsync(id).ConfigureAwait(false);
        if (digitalDeliverySpecs == null)
        {
            return this.NotFound();
        }

        this.context.Set<DigitalDeliverySpecs>().Remove(digitalDeliverySpecs);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool DigitalDeliverySpecsExists(int id)
    {
        return this.context.Set<DigitalDeliverySpecs>().Any(e => e.DigitalDeliverySpecsID == id);
    }
}
