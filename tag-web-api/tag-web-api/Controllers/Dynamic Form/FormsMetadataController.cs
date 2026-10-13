// <copyright file="FormsMetadataController.cs" company="Twisted Artists Guild">
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
public class FormsMetadataController : ControllerBase
{
    private readonly TAGDBContext context;

    public FormsMetadataController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Forms_Metadata>>> Get()
    {
        return await this.context.Set<Forms_Metadata>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("byID/{id}")]
    public async Task<ActionResult<Forms_Metadata>> Get(int id)
    {
        var forms_Metadata = await this.context.Set<Forms_Metadata>().FindAsync(id).ConfigureAwait(false);
        if (forms_Metadata == null)
        {
            return this.NotFound();
        }

        return forms_Metadata;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Forms_Metadata>> Create(Forms_Metadata forms_Metadata)
    {
        this.context.Set<Forms_Metadata>().Add(forms_Metadata);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = forms_Metadata.Forms_MetadataID }, forms_Metadata);
    }

    [HttpPut("byID/{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, Forms_Metadata forms_Metadata)
    {
        if (id != forms_Metadata.Forms_MetadataID)
        {
            return this.BadRequest();
        }

        this.context.Entry(forms_Metadata).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.Forms_MetadataExists(id))
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

    [HttpDelete("byID/{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var forms_Metadata = await this.context.Set<Forms_Metadata>().FindAsync(id).ConfigureAwait(false);
        if (forms_Metadata == null)
        {
            return this.NotFound();
        }

        this.context.Set<Forms_Metadata>().Remove(forms_Metadata);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool Forms_MetadataExists(int id)
    {
        return this.context.Set<Forms_Metadata>().Any(e => e.Forms_MetadataID == id);
    }

    // GET: api/FormsMetadata/{formName}
    [HttpGet("{formName}")]
    public async Task<ActionResult<IEnumerable<Forms_Metadata>>> GetFormsMetadata(string formName)
    {
        var forms_Metadata = await context.Forms_Metadata
            .Include(f => f.Forms_Fields)
            .Where(f => f.Name == formName)
            .ToListAsync();

        if (forms_Metadata == null || !forms_Metadata.Any())
        {
            return NotFound();
        }

        return forms_Metadata;
    }

    [HttpGet("listOfForms")]
    public async Task<ActionResult<IEnumerable<string>>> GetFormNames()
    {
        var formNames = await context.Forms_Metadata
            .Select(f => f.Name)
            .ToListAsync();

        if (formNames == null || !formNames.Any())
        {
            return NotFound();
        }

        return formNames;
    }
}
