// <copyright file="BannedReasonController.cs" company="Twisted Artists Guild">
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
public class BannedReasonController : ControllerBase
{
    private readonly TAGDBContext context;

    public BannedReasonController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetBannedReasons")]
    public async Task<ActionResult<IEnumerable<BannedReason>>> Get()
    {
        return await this.context.Set<BannedReason>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BannedReason>> Get(int id)
    {
        var bannedReason = await this.context.Set<BannedReason>().FindAsync(id).ConfigureAwait(false);
        if (bannedReason == null)
        {
            return this.NotFound();
        }

        return bannedReason;
    }

    [HttpPost]
    public async Task<ActionResult<BannedReason>> Create(BannedReason bannedReason)
    {
        this.context.Set<BannedReason>().Add(bannedReason);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = bannedReason.BannedReasonID }, bannedReason);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BannedReason bannedReason)
    {
        if (id != bannedReason.BannedReasonID)
        {
            return this.BadRequest();
        }

        this.context.Entry(bannedReason).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.BannedReasonExists(id))
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
        var bannedReason = await this.context.Set<BannedReason>().FindAsync(id).ConfigureAwait(false);
        if (bannedReason == null)
        {
            return this.NotFound();
        }

        this.context.Set<BannedReason>().Remove(bannedReason);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool BannedReasonExists(int id)
    {
        return this.context.Set<BannedReason>().Any(e => e.BannedReasonID == id);
    }
}
