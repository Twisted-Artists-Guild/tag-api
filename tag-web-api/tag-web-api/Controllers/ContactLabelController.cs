// <copyright file="ContactLabelController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    public class ContactLabelController : ControllerBase
    {
        private readonly TAGDBContext context;

        public ContactLabelController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ContactLabel>>> GetContactLabels()
        {
            return await this.context.Set<ContactLabel>()
                .AsNoTracking()
                .OrderBy(l => l.ContactLabelID)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
