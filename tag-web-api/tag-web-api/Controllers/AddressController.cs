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

    // Lists every address, so it is for staff only. The public pages read the address that comes with a venue or contact.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Address>>> Get()
    {
        var userId = VenueAccess.GetUserId(this.User);
        if (userId == null)
        {
            return this.Unauthorized();
        }

        if (!await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false))
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can list every address." });
        }

        return await this.context.Set<Address>().ToListAsync().ConfigureAwait(false);
    }

    // An address that belongs to a venue is only readable when it is the address of a public venue, or a public contact of
    // one, or the caller manages the venue. Anything else about the address is answered as if it did not exist.
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Address>> Get(int id)
    {
        var address = await this.context.Set<Address>().FindAsync(id).ConfigureAwait(false);
        if (address == null || !await this.CanReadAsync(id).ConfigureAwait(false))
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

        if (!await this.CanChangeAsync(id).ConfigureAwait(false))
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to change this address." });
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

        if (!await this.CanChangeAsync(id).ConfigureAwait(false))
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to delete this address." });
        }

        this.context.Set<Address>().Remove(address);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    // Changing an address that a venue uses needs every one of those venues' owners, or staff.
    private async Task<bool> CanChangeAsync(int addressId)
    {
        var venueIds = await VenueDataAccess.VenueIdsForAddressAsync(this.context, addressId).ConfigureAwait(false);
        return await VenueDataAccess.CanManageAllAsync(this.context, venueIds, VenueAccess.GetUserId(this.User)).ConfigureAwait(false);
    }

    private async Task<bool> CanReadAsync(int addressId)
    {
        var venueIds = await VenueDataAccess.VenueIdsForAddressAsync(this.context, addressId).ConfigureAwait(false);
        if (venueIds.Count == 0)
        {
            return true;
        }

        var isPublic = await this.context.Set<Venue>()
            .AnyAsync(v => v.IsPublished && !v.IsModerationBlocked && (
                v.AddressID == addressId
                || this.context.Set<Linker_EntityToContact>().Any(l =>
                    l.EntityType == LinkedEntityTypes.Venue && l.EntityID == v.VenueID && l.Scope != ContactScope.Private && l.Contact.AddressID == addressId)))
            .ConfigureAwait(false);

        return isPublic || await VenueDataAccess.CanManageAllAsync(this.context, venueIds, VenueAccess.GetUserId(this.User)).ConfigureAwait(false);
    }

    private bool AddressExists(int id)
    {
        return this.context.Set<Address>().Any(e => e.AddressID == id);
    }
}
