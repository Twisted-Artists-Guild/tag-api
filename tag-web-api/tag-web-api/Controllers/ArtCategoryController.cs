// <copyright file="ArtCategoryController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArtCategoryController : ControllerBase
{
    private readonly TAGDBContext context;

    public ArtCategoryController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetArtCategories")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ArtCategoryFlatDto>>> Get()
    {
        var categories = await this.context.Set<ArtCategory>()
            .AsNoTracking()
            .OrderBy(ac => ac.ArtCategoryID)
            .Select(ac => new ArtCategoryFlatDto
            {
                ArtCategoryID = ac.ArtCategoryID,
                Category = ac.Category,
                CategoryKey = ac.CategoryKey,
                Description = ac.Description,
                Tags = ac.Tags,
                ParentArtCategoryID = ac.ParentArtCategoryID,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return categories;
    }

    [HttpGet("roots")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ArtCategoryFlatDto>>> GetRoots()
    {
        var roots = await this.context.Set<ArtCategory>()
            .AsNoTracking()
            .Where(ac => ac.ParentArtCategoryID == null)
            .OrderBy(ac => ac.ArtCategoryID)
            .Select(ac => new ArtCategoryFlatDto
            {
                ArtCategoryID = ac.ArtCategoryID,
                Category = ac.Category,
                CategoryKey = ac.CategoryKey,
                Description = ac.Description,
                Tags = ac.Tags,
                ParentArtCategoryID = ac.ParentArtCategoryID,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return roots;
    }

    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ArtCategoryTreeNodeDto>>> GetTree()
    {
        var categories = await this.context.Set<ArtCategory>()
            .AsNoTracking()
            .OrderBy(ac => ac.ArtCategoryID)
            .ToListAsync()
            .ConfigureAwait(false);

        var nodesById = categories.ToDictionary(
            c => c.ArtCategoryID,
            c => new ArtCategoryTreeNodeDto
            {
                ArtCategoryID = c.ArtCategoryID,
                Category = c.Category,
                CategoryKey = c.CategoryKey,
                Description = c.Description,
                Tags = c.Tags,
                ParentArtCategoryID = c.ParentArtCategoryID,
                SubCategories = new List<ArtCategoryTreeNodeDto>(),
            });

        foreach (var node in nodesById.Values)
        {
            if (node.ParentArtCategoryID.HasValue && nodesById.TryGetValue(node.ParentArtCategoryID.Value, out var parent))
            {
                parent.SubCategories.Add(node);
            }
        }

        var rootNodes = nodesById.Values
            .Where(n => n.ParentArtCategoryID == null)
            .OrderBy(n => n.ArtCategoryID)
            .ToList();

        return rootNodes;
    }

    [HttpGet("getByID/{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArtCategoryFlatDto>> Get(int id)
    {
        var artCategory = await this.context.Set<ArtCategory>()
            .AsNoTracking()
            .Where(ac => ac.ArtCategoryID == id)
            .Select(ac => new ArtCategoryFlatDto
            {
                ArtCategoryID = ac.ArtCategoryID,
                Category = ac.Category,
                CategoryKey = ac.CategoryKey,
                Description = ac.Description,
                Tags = ac.Tags,
                ParentArtCategoryID = ac.ParentArtCategoryID,
            })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (artCategory == null)
        {
            return this.NotFound();
        }

        return artCategory;
    }

    [HttpGet("{category}")]
    [AllowAnonymous]
    public IActionResult GetArtCategoryByCategory(string category)
    {
        var artCategory = this.context.Set<ArtCategory>()
            .AsNoTracking()
            .Where(ac => ac.CategoryKey == category)
            .Select(ac => new ArtCategoryFlatDto
            {
                ArtCategoryID = ac.ArtCategoryID,
                Category = ac.Category,
                CategoryKey = ac.CategoryKey,
                Description = ac.Description,
                Tags = ac.Tags,
                ParentArtCategoryID = ac.ParentArtCategoryID,
            })
            .FirstOrDefault();

        if (artCategory == null)
        {
            return this.NotFound();
        }

        return this.Ok(artCategory);
    }

    [HttpPost]
    public async Task<ActionResult<ArtCategory>> Create(ArtCategory artCategory)
    {
        if (artCategory == null)
        {
            return this.BadRequest();
        }

        this.context.Set<ArtCategory>().Add(artCategory);
        await this.context.SaveChangesAsync().ConfigureAwait(false);
        return this.CreatedAtAction(nameof(this.Get), new { id = artCategory.ArtCategoryID }, artCategory);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ArtCategory artCategory)
    {
        if (artCategory == null)
        {
            return this.BadRequest();
        }

        if (id != artCategory.ArtCategoryID)
        {
            return this.BadRequest();
        }

        this.context.Entry(artCategory).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.ArtCategoryExists(id))
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
        var artCategory = await this.context.Set<ArtCategory>().FindAsync(id).ConfigureAwait(false);
        if (artCategory == null)
        {
            return this.NotFound();
        }

        this.context.Set<ArtCategory>().Remove(artCategory);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        return this.NoContent();
    }

    private bool ArtCategoryExists(int id)
    {
        return this.context.Set<ArtCategory>().Any(e => e.ArtCategoryID == id);
    }

    public class ArtCategoryTreeNodeDto
    {
        public int ArtCategoryID { get; set; }

        public string Category { get; set; } = string.Empty;

        public string CategoryKey { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;

        public int? ParentArtCategoryID { get; set; }

        public List<ArtCategoryTreeNodeDto> SubCategories { get; set; } = new();
    }

    public class ArtCategoryFlatDto
    {
        public int ArtCategoryID { get; set; }

        public string Category { get; set; } = string.Empty;

        public string CategoryKey { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;

        public int? ParentArtCategoryID { get; set; }
    }
}
