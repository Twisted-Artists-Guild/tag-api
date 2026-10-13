// <copyright file="PhoneContactLabelController.cs" company="Twisted Artists Guild">
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
    public class PhoneContactLabelController : ControllerBase
    {
        private readonly TAGDBContext context;

        public PhoneContactLabelController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PhoneContactLabel>>> GetPhoneContactLabels()
        {
            return await this.context.Set<PhoneContactLabel>()
                .AsNoTracking()
                .OrderBy(l => l.PhoneContactLabelID)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
