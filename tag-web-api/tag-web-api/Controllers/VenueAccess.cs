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

    /// <summary>
    /// Contacts, contact links and addresses are shared by venues, artists, users and vendors. These checks only apply to the
    /// ones that belong to a venue, which only that venue's owners (and staff) may see when private, or change.
    /// </summary>
    internal static class VenueDataAccess
    {
        // The venues a contact is linked to.
        public static Task<List<int>> VenueIdsForContactAsync(TAGDBContext context, int contactId)
        {
            return context.Set<Linker_EntityToContact>()
                .AsNoTracking()
                .Where(l => l.ContactID == contactId && l.EntityType == LinkedEntityTypes.Venue)
                .Select(l => l.EntityID)
                .Distinct()
                .ToListAsync();
        }

        // The venues that use an address, either as their own address or through one of their contacts.
        public static async Task<List<int>> VenueIdsForAddressAsync(TAGDBContext context, int addressId)
        {
            var own = await context.Set<Venue>()
                .AsNoTracking()
                .Where(v => v.AddressID == addressId)
                .Select(v => v.VenueID)
                .ToListAsync()
                .ConfigureAwait(false);

            var throughContacts = await context.Set<Linker_EntityToContact>()
                .AsNoTracking()
                .Where(l => l.EntityType == LinkedEntityTypes.Venue && l.Contact.AddressID == addressId)
                .Select(l => l.EntityID)
                .ToListAsync()
                .ConfigureAwait(false);

            return own.Union(throughContacts).ToList();
        }

        // True when the user may manage every one of these venues. Staff manage all of them. An empty list is not venue
        // data at all, so there is nothing to protect and the answer is true.
        public static async Task<bool> CanManageAllAsync(TAGDBContext context, IEnumerable<int> venueIds, int? userId)
        {
            var ids = venueIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return true;
            }

            if (userId == null)
            {
                return false;
            }

            if (await VenueAccess.IsStaffAsync(context, userId.Value).ConfigureAwait(false))
            {
                return true;
            }

            var owned = await context.Set<Linker_UserToVenue>()
                .AsNoTracking()
                .Where(l => l.UserID == userId.Value && ids.Contains(l.VenueID))
                .Select(l => l.VenueID)
                .Distinct()
                .CountAsync()
                .ConfigureAwait(false);
            return owned == ids.Count;
        }
    }
}
