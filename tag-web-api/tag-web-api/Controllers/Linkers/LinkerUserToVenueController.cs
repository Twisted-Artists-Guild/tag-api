// <copyright file="LinkerUserToVenueController.cs" company="Twisted Artists Guild">
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
    public class Linker_UserToVenueController : ControllerBase
    {
        private readonly TAGDBContext context;

        public Linker_UserToVenueController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/Linker_UserToVenue/byUserID/5
        // The venues a user manages, drafts included. A user can only list their own venues; staff can list anyone's.
        [HttpGet("byUserID/{userId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetVenuesByUserId(int userId)
        {
            var currentUserId = VenueAccess.GetUserId(this.User);
            if (currentUserId == null)
            {
                return this.Unauthorized();
            }

            if (currentUserId.Value != userId && !await VenueAccess.IsStaffAsync(this.context, currentUserId.Value).ConfigureAwait(false))
            {
                return this.StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only list your own venues." });
            }

            var venueIds = await this.context.Set<Linker_UserToVenue>()
                .AsNoTracking()
                .Where(link => link.UserID == userId)
                .Select(link => link.VenueID)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            if (venueIds.Count == 0)
            {
                return this.Ok(Array.Empty<object>());
            }

            var venues = await this.context.Set<Venue>()
                .AsNoTracking()
                .Include(venue => venue.LogoPic)
                .Where(venue => venueIds.Contains(venue.VenueID))
                .OrderBy(venue => venue.Name)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(venues.Select(VenueController.MapVenueForApi));
        }
    }
}
