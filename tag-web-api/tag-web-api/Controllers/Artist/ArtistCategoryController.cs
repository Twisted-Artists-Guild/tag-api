// <copyright file="ArtistCategoryController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using System.Collections.Generic;
using System.Linq;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistCategoryController : ControllerBase
{
    private readonly TAGDBContext context;

    public ArtistCategoryController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetArtistCategories")]
    public async Task<ActionResult<IEnumerable<ArtistCategory>>> Get()
    {
        return await this.context.Set<ArtistCategory>()
            .AsNoTracking()
            .OrderBy(ac => ac.ArtistCategoryID)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    [HttpGet("roots")]
    public async Task<ActionResult<IEnumerable<ArtistCategory>>> GetRoots()
    {
        var roots = await this.context.Set<ArtistCategory>()
            .AsNoTracking()
            .Where(ac => ac.ParentArtistCategoryID == null)
            .OrderBy(ac => ac.ArtistCategoryID)
            .ToListAsync()
            .ConfigureAwait(false);

        return roots;
    }

    [HttpGet("tree")]
    public async Task<ActionResult<IEnumerable<ArtistCategoryTreeNodeDto>>> GetTree()
    {
        var categories = await this.context.Set<ArtistCategory>()
            .AsNoTracking()
            .OrderBy(ac => ac.ArtistCategoryID)
            .ToListAsync()
            .ConfigureAwait(false);

        var nodesById = categories.ToDictionary(
            c => c.ArtistCategoryID,
            c => new ArtistCategoryTreeNodeDto
            {
                ArtistCategoryID = c.ArtistCategoryID,
                Category = c.Category,
                CategoryKey = c.CategoryKey,
                Description = c.Description,
                Tags = c.Tags,
                ParentArtistCategoryID = c.ParentArtistCategoryID,
                SubCategories = new List<ArtistCategoryTreeNodeDto>(),
            });

        foreach (var node in nodesById.Values)
        {
            if (node.ParentArtistCategoryID.HasValue && nodesById.TryGetValue(node.ParentArtistCategoryID.Value, out var parent))
            {
                parent.SubCategories.Add(node);
            }
        }

        var rootNodes = nodesById.Values
            .Where(n => n.ParentArtistCategoryID == null)
            .OrderBy(n => n.ArtistCategoryID)
            .ToList();

        return rootNodes;
    }

    [HttpGet("getByID/{id}")]
    public async Task<ActionResult<ArtistCategory>> GetById(int id)
    {
        var artistCategory = await this.context.Set<ArtistCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ac => ac.ArtistCategoryID == id)
            .ConfigureAwait(false);

        if (artistCategory == null)
        {
            return this.NotFound();
        }

        return artistCategory;
    }

    [HttpGet("{category}")]
    public async Task<ActionResult<ArtistCategory>> GetByCategoryKey(string category)
    {
        var artistCategory = await this.context.Set<ArtistCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ac => ac.CategoryKey == category)
            .ConfigureAwait(false);

        if (artistCategory == null)
        {
            return this.NotFound();
        }

        return artistCategory;
    }

    public class ArtistCategoryTreeNodeDto
    {
        public int ArtistCategoryID { get; set; }

        public string Category { get; set; } = string.Empty;

        public string CategoryKey { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;

        public int? ParentArtistCategoryID { get; set; }

        public List<ArtistCategoryTreeNodeDto> SubCategories { get; set; } = new();
    }
}
