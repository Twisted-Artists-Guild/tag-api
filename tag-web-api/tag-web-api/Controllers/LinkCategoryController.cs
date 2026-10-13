// <copyright file="LinkCategoryController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LinkCategoryController : ControllerBase
    {
        private readonly TAGDBContext context;

        public LinkCategoryController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/linkcategory
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LinkCategory>>> GetLinkCategories()
        {
            return await this.context.Set<LinkCategory>()
                .AsNoTracking()
                .OrderBy(lc => lc.LinkCategoryID)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        // GET: api/linkcategory/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<LinkCategory>> GetLinkCategory(int id)
        {
            var category = await this.context.Set<LinkCategory>()
                .AsNoTracking()
                .Include(lc => lc.SubCategories)
                .FirstOrDefaultAsync(lc => lc.LinkCategoryID == id)
                .ConfigureAwait(false);

            if (category == null)
            {
                return this.NotFound();
            }

            return category;
        }

        // GET: api/linkcategory/tree — full hierarchy, roots only (with nested SubCategories)
        [HttpGet("tree")]
        public async Task<ActionResult<IEnumerable<LinkCategory>>> GetTree()
        {
            var roots = await this.context.Set<LinkCategory>()
                .AsNoTracking()
                .Where(lc => lc.ParentLinkCategoryID == null)
                .Include(lc => lc.SubCategories!)
                    .ThenInclude(lc => lc.SubCategories)
                .OrderBy(lc => lc.LinkCategoryID)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(roots);
        }
    }
}
