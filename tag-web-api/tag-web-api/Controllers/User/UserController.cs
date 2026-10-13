// <copyright file="UserController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/user")]
// SECURITY NOTE: This is an authentication/identity surface area.
// Treat all changes as high-risk and keep access limited to admin/system requests.
public class UserController : ControllerBase
{
    private readonly TAGDBContext context;

    public UserController(TAGDBContext context)
    {
        this.context = context;
    }

    [HttpGet(Name = "GetUsers")]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await this.context.Users
            //.Where(u => u.IsPublished && !u.HideFromPublic && !u.IsModerationBlocked)
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.UserID,
                DisplayName = u.PreferredName ?? u.FirstName ?? u.Username ?? u.EmailOne,
                u.Username,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return this.Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NextAuthUser>> Get(int id)
    {
        var authUser = await this.context.NextAuthUsers
            .FirstOrDefaultAsync(u => u.Id == id)
            .ConfigureAwait(false);

        if (authUser == null)
        {
            return this.NotFound();
        }

        return this.Ok(authUser);
    }

    [HttpGet("by-email")]
    public async Task<ActionResult<NextAuthUser>> GetByEmail([FromQuery] string email)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return this.BadRequest(new { message = "Email is required." });
        }

        var authUser = await this.context.NextAuthUsers
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail)
            .ConfigureAwait(false);

        if (authUser == null)
        {
            return this.NotFound();
        }

        return this.Ok(authUser);
    }

    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
    }
}
