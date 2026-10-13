// <copyright file="ExternalLinkController.cs" company="Twisted Artists Guild">
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
public class ExternalLinkController : ControllerBase
{
    private readonly TAGDBContext context;

    public ExternalLinkController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExternalLink>>> Get()
    {
        return await this.context.Set<ExternalLink>()
            .AsNoTracking()
            .Include(el => el.LinkCategory)
            .OrderBy(el => el.ExternalLinkID)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExternalLink>> Get(int id)
    {
        var externalLink = await this.context.Set<ExternalLink>()
            .AsNoTracking()
            .Include(el => el.LinkCategory)
            .FirstOrDefaultAsync(el => el.ExternalLinkID == id)
            .ConfigureAwait(false);
        if (externalLink == null)
        {
            return this.NotFound();
        }

        return externalLink;
    }

    [HttpPost]
    public async Task<ActionResult<ExternalLink>> Create(ExternalLink externalLink)
    {
        this.context.Set<ExternalLink>().Add(externalLink);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = externalLink.ExternalLinkID }, externalLink);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ExternalLink externalLink)
    {
        if (id != externalLink.ExternalLinkID)
        {
            return this.BadRequest();
        }

        this.context.Entry(externalLink).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.ExternalLinkExists(id))
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
        var externalLink = await this.context.Set<ExternalLink>().FindAsync(id).ConfigureAwait(false);
        if (externalLink == null)
        {
            return this.NotFound();
        }

        this.context.Set<ExternalLink>().Remove(externalLink);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool ExternalLinkExists(int id)
    {
        return this.context.Set<ExternalLink>().Any(e => e.ExternalLinkID == id);
    }
}
