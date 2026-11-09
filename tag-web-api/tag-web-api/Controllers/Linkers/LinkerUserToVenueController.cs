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
        private const int MaxVenuePageSize = 100;
        private const int MaxUserLookupResults = 25;
        private const int MinUserSearchLength = 2;

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

        // GET: api/Linker_UserToVenue/admin/venues?search=&unownedOnly=false&afterId=0&limit=25
        // Staff only. Venues with their owners, oldest first, in pages (afterId is the cursor). Unowned venues can be isolated.
        [HttpGet("admin/venues")]
        public async Task<ActionResult<VenueAdminPageDto>> GetVenuesForStaff(
            [FromQuery] string? search = null,
            [FromQuery] bool unownedOnly = false,
            [FromQuery] int afterId = 0,
            [FromQuery] int limit = 25)
        {
            var staff = await this.RequireStaffAsync().ConfigureAwait(false);
            if (staff.Error != null)
            {
                return staff.Error;
            }

            var pageSize = Math.Clamp(limit, 1, MaxVenuePageSize);
            var query = this.context.Set<Venue>().AsNoTracking().Where(v => v.VenueID > Math.Max(afterId, 0));

            var term = search?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(v => (v.Name_Plaintext ?? v.Name).ToLower().Contains(term));
            }

            if (unownedOnly)
            {
                query = query.Where(v => !this.context.Set<Linker_UserToVenue>().Any(l => l.VenueID == v.VenueID));
            }

            // One extra row tells us whether another page exists.
            var rows = await query.OrderBy(v => v.VenueID).Take(pageSize + 1).ToListAsync().ConfigureAwait(false);
            var hasMore = rows.Count > pageSize;
            var page = rows.Take(pageSize).ToList();

            var venueIds = page.Select(v => v.VenueID).ToList();
            var links = await this.context.Set<Linker_UserToVenue>()
                .AsNoTracking()
                .Where(l => venueIds.Contains(l.VenueID))
                .ToListAsync()
                .ConfigureAwait(false);

            var userIds = links.Select(l => l.UserID).Distinct().ToList();
            var users = await this.context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserID))
                .ToDictionaryAsync(u => u.UserID)
                .ConfigureAwait(false);

            var result = new VenueAdminPageDto
            {
                NextAfterId = hasMore ? page[^1].VenueID : null,
                Items = page.Select(v => new VenueAdminRowDto
                {
                    VenueID = v.VenueID,
                    Name = VenueController.CoalescePlaintext(v.Name_Plaintext, v.Name) ?? string.Empty,
                    Slug = VenueController.ToSlug(v.Name),
                    IsPublished = v.IsPublished,
                    IsModerationBlocked = v.IsModerationBlocked,
                    Owners = links
                        .Where(l => l.VenueID == v.VenueID)
                        .Select(l => ToOwnerDto(l.UserID, l.Role, users.GetValueOrDefault(l.UserID)))
                        .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                }).ToList(),
            };

            return this.Ok(result);
        }

        // GET: api/Linker_UserToVenue/admin/users?search=ann&limit=10
        // Staff only. A narrow user lookup for choosing an owner: no birth date, ban reason or other private fields.
        [HttpGet("admin/users")]
        public async Task<ActionResult<IEnumerable<UserLookupDto>>> FindUsersForStaff([FromQuery] string? search = null, [FromQuery] int limit = 10)
        {
            var staff = await this.RequireStaffAsync().ConfigureAwait(false);
            if (staff.Error != null)
            {
                return staff.Error;
            }

            var term = search?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(term) || term.Length < MinUserSearchLength)
            {
                return this.Ok(Array.Empty<UserLookupDto>());
            }

            var users = await this.context.Users
                .AsNoTracking()
                .Where(u =>
                    (u.FirstName != null && u.FirstName.ToLower().Contains(term))
                    || (u.FamName != null && u.FamName.ToLower().Contains(term))
                    || (u.PreferredName != null && u.PreferredName.ToLower().Contains(term))
                    || (u.Username != null && u.Username.ToLower().Contains(term))
                    || u.EmailOne.ToLower().Contains(term))
                .OrderBy(u => u.UserID)
                .Take(Math.Clamp(limit, 1, MaxUserLookupResults))
                .ToListAsync()
                .ConfigureAwait(false);

            return this.Ok(users.Select(u =>
            {
                var owner = ToOwnerDto(u.UserID, string.Empty, u);
                return new UserLookupDto { UserID = u.UserID, DisplayName = owner.DisplayName, Username = u.Username, Email = u.EmailOne };
            }));
        }

        // POST: api/Linker_UserToVenue/venue/5/owners   { "userID": 7 }
        // Staff only. Makes the user an owner of the venue. The change and its audit entry are saved together.
        [HttpPost("venue/{venueId}/owners")]
        public async Task<ActionResult<VenueOwnerDto>> AssignOwner(int venueId, [FromBody] VenueOwnerAssignRequest request)
        {
            var staff = await this.RequireStaffAsync().ConfigureAwait(false);
            if (staff.Error != null)
            {
                return staff.Error;
            }

            if (request == null || venueId <= 0 || request.UserID <= 0)
            {
                return this.BadRequest(new { message = "A venue and a user are required." });
            }

            var venue = await this.context.Set<Venue>().AsNoTracking().FirstOrDefaultAsync(v => v.VenueID == venueId).ConfigureAwait(false);
            if (venue == null)
            {
                return this.NotFound(new { message = "Venue not found." });
            }

            var user = await this.context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == request.UserID).ConfigureAwait(false);
            if (user == null)
            {
                return this.NotFound(new { message = "User not found." });
            }

            if (await VenueAccess.IsOwnerAsync(this.context, venueId, request.UserID).ConfigureAwait(false))
            {
                return this.Conflict(new { message = "This user already owns the venue." });
            }

            this.context.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue
            {
                UserID = request.UserID,
                VenueID = venueId,
                Role = Linker_UserToVenue.OwnerRole,
            });
            this.context.Set<Log>().Add(BuildAuditLog("Venue owner assigned", "assign", venueId, request.UserID, staff.UserId!.Value));

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                // A concurrent request linked the same pair first; the unique index rejected this one.
                return this.Conflict(new { message = "This user already owns the venue." });
            }

            return this.StatusCode(StatusCodes.Status201Created, ToOwnerDto(user.UserID, Linker_UserToVenue.OwnerRole, user));
        }

        // DELETE: api/Linker_UserToVenue/venue/5/owners/7
        // Staff only. Removes the user as an owner. Reassigning a venue is a removal plus an assignment.
        [HttpDelete("venue/{venueId}/owners/{userId}")]
        public async Task<IActionResult> RemoveOwner(int venueId, int userId)
        {
            var staff = await this.RequireStaffAsync().ConfigureAwait(false);
            if (staff.Error != null)
            {
                return staff.Error;
            }

            var link = await this.context.Set<Linker_UserToVenue>()
                .FirstOrDefaultAsync(l => l.VenueID == venueId && l.UserID == userId)
                .ConfigureAwait(false);
            if (link == null)
            {
                return this.NotFound(new { message = "This user is not an owner of the venue." });
            }

            this.context.Set<Linker_UserToVenue>().Remove(link);
            this.context.Set<Log>().Add(BuildAuditLog("Venue owner removed", "remove", venueId, userId, staff.UserId!.Value));
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private static VenueOwnerDto ToOwnerDto(int userId, string role, User? user)
        {
            var fullName = $"{user?.FirstName} {user?.FamName}".Trim();
            var displayName = new[] { user?.PreferredName, fullName, user?.Username, user?.EmailOne }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? $"User {userId}";

            return new VenueOwnerDto
            {
                UserID = userId,
                DisplayName = displayName,
                Username = user?.Username,
                Email = user?.EmailOne,
                Role = role,
            };
        }

        // Written in the same SaveChanges as the ownership change, so a change can never exist without its audit entry.
        private static Log BuildAuditLog(string shortText, string operation, int venueId, int ownerUserId, int actingUserId)
        {
            return new Log
            {
                ShortText = shortText,
                Tags = $"scope=audit;entity=venue;event=owner;operation={operation};result=success;channel=db",
                Critical = false,
                LoggedData = $"venueId={venueId};ownerUserId={ownerUserId};actingUserId={actingUserId}",
                UserID = actingUserId,
                LogTimestamp = DateTime.UtcNow,
            };
        }

        // Signed in and flagged as staff, or the response to send back.
        private async Task<(int? UserId, ActionResult? Error)> RequireStaffAsync()
        {
            var userId = VenueAccess.GetUserId(this.User);
            if (userId == null)
            {
                return (null, this.Unauthorized());
            }

            if (!await VenueAccess.IsStaffAsync(this.context, userId.Value).ConfigureAwait(false))
            {
                return (null, this.StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can manage venue owners." }));
            }

            return (userId, null);
        }
    }
}
