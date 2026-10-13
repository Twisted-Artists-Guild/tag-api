// <copyright file="EventCategoryController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using System.Linq;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventCategoryController : ControllerBase
{
    private readonly TAGDBContext context;

    public EventCategoryController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetEventCategories")]
    public async Task<ActionResult<IEnumerable<EventCategory>>> Get()
    {
        return await this.context.Set<EventCategory>()
            .AsNoTracking()
            .OrderBy(ec => ec.EventCategoryID)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    [HttpGet("getByID/{id}")]
    public async Task<ActionResult<EventCategory>> GetById(int id)
    {
        var eventCategory = await this.context.Set<EventCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.EventCategoryID == id)
            .ConfigureAwait(false);

        if (eventCategory == null)
        {
            return this.NotFound();
        }

        return eventCategory;
    }

    [HttpGet("{category}")]
    public async Task<ActionResult<EventCategory>> GetByCategoryKey(string category)
    {
        var eventCategory = await this.context.Set<EventCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.CategoryKey == category)
            .ConfigureAwait(false);

        if (eventCategory == null)
        {
            return this.NotFound();
        }

        return eventCategory;
    }
}
