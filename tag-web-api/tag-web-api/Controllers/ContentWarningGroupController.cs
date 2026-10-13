// <copyright file="ContentWarningGroupController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ContentWarningGroupController : ControllerBase
{
    private readonly TAGDBContext _context;

    public ContentWarningGroupController(TAGDBContext context)
    {
        _context = context;
    }

    // GET: api/ContentWarningGroup
    [HttpGet]
    public async Task<ActionResult<List<ContentWarningGroup>>> GetAll()
    {
        var groups = await _context.ContentWarningGroups
            .Include(g => g.Items.OrderBy(i => i.DisplayOrder))
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();

        return groups;
    }

    // GET: api/ContentWarningGroup/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ContentWarningGroup>> GetById(int id)
    {
        var group = await _context.ContentWarningGroups
            .Include(g => g.Items.OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();
        return group;
    }
}