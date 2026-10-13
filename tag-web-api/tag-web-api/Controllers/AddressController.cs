// <copyright file="AddressController.cs" company="Twisted Artists Guild">
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
public class AddressController : ControllerBase
{
    private readonly TAGDBContext context;

    public AddressController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Address>>> Get()
    {
        return await this.context.Set<Address>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Address>> Get(int id)
    {
        var address = await this.context.Set<Address>().FindAsync(id).ConfigureAwait(false);
        if (address == null)
        {
            return this.NotFound();
        }

        return address;
    }

    [HttpPost]
    public async Task<ActionResult<Address>> Create(Address address)
    {
        this.context.Set<Address>().Add(address);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = address.AddressID }, address);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Address address)
    {
        if (id != address.AddressID)
        {
            return this.BadRequest();
        }

        this.context.Entry(address).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.AddressExists(id))
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
        var address = await this.context.Set<Address>().FindAsync(id).ConfigureAwait(false);
        if (address == null)
        {
            return this.NotFound();
        }

        this.context.Set<Address>().Remove(address);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool AddressExists(int id)
    {
        return this.context.Set<Address>().Any(e => e.AddressID == id);
    }
}
