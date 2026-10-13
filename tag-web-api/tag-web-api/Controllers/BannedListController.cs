// <copyright file="BannedListController.cs" company="Twisted Artists Guild">
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
public class BannedListController : ControllerBase
{
    private readonly TAGDBContext context;

    public BannedListController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetBannedLists")]
    public async Task<ActionResult<IEnumerable<BannedList>>> Get()
    {
        return await this.context.Set<BannedList>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BannedList>> Get(int id)
    {
        var bannedList = await this.context.Set<BannedList>().FindAsync(id).ConfigureAwait(false);
        if (bannedList == null)
        {
            return this.NotFound();
        }

        return bannedList;
    }

    [HttpPost]
    public async Task<ActionResult<BannedList>> Create(BannedList bannedList)
    {
        this.context.Set<BannedList>().Add(bannedList);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = bannedList.BannedListID }, bannedList);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BannedList bannedList)
    {
        if (id != bannedList.BannedListID)
        {
            return this.BadRequest();
        }

        this.context.Entry(bannedList).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.BannedListExists(id))
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
        var bannedList = await this.context.Set<BannedList>().FindAsync(id).ConfigureAwait(false);
        if (bannedList == null)
        {
            return this.NotFound();
        }

        this.context.Set<BannedList>().Remove(bannedList);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool BannedListExists(int id)
    {
        return this.context.Set<BannedList>().Any(e => e.BannedListID == id);
    }
}
