// <copyright file="VenueAccess.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using System.Security.Claims;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    /// <summary>
    /// Who the caller is and what they may do with a venue. The authenticated user always comes from the token,
    /// never from the request. Staff are users flagged as moderators.
    /// </summary>
    internal static class VenueAccess
    {
        public static int? GetUserId(ClaimsPrincipal? user)
        {
            if (user == null)
            {
                return null;
            }

            var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("userId");

            return int.TryParse(value, out var userId) && userId > 0 ? userId : null;
        }

        public static Task<bool> IsStaffAsync(TAGDBContext context, int userId)
        {
            return context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserID == userId && u.Moderator);
        }

        public static Task<bool> IsOwnerAsync(TAGDBContext context, int venueId, int userId)
        {
            return context.Set<Linker_UserToVenue>()
                .AsNoTracking()
                .AnyAsync(l => l.VenueID == venueId && l.UserID == userId);
        }

        public static async Task<bool> CanManageAsync(TAGDBContext context, int venueId, int userId)
        {
            return await IsStaffAsync(context, userId).ConfigureAwait(false)
                || await IsOwnerAsync(context, venueId, userId).ConfigureAwait(false);
        }
    }
}
