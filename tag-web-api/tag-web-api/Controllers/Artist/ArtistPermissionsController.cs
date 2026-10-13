// <copyright file="ArtistPermissionsController.cs" company="Twisted Artists Guild">
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
public class ArtistPermissionsController : ControllerBase
{
    private readonly TAGDBContext context;

    public ArtistPermissionsController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetArtistPermissions")]
    public async Task<ActionResult<IEnumerable<ArtistPermissions>>> Get()
    {
        return await this.context.Set<ArtistPermissions>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ArtistPermissions>> Get(int id)
    {
        var artistPermissions = await this.context.Set<ArtistPermissions>().FindAsync(id).ConfigureAwait(false);
        if (artistPermissions == null)
        {
            return this.NotFound();
        }

        return artistPermissions;
    }

    [HttpPost]
    public async Task<ActionResult<ArtistPermissions>> Create(ArtistPermissions artistPermissions)
    {
        this.context.Set<ArtistPermissions>().Add(artistPermissions);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = artistPermissions.ArtistPermissionsID }, artistPermissions);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ArtistPermissions artistPermissions)
    {
        if (id != artistPermissions.ArtistPermissionsID)
        {
            return this.BadRequest();
        }

        this.context.Entry(artistPermissions).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.ArtistPermissionsExists(id))
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
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var artistPermissions = await this.context.Set<ArtistPermissions>().FindAsync(id).ConfigureAwait(false);
        if (artistPermissions == null)
        {
            return this.NotFound();
        }

        this.context.Set<ArtistPermissions>().Remove(artistPermissions);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool ArtistPermissionsExists(int id)
    {
        return this.context.Set<ArtistPermissions>().Any(e => e.ArtistPermissionsID == id);
    }
}
