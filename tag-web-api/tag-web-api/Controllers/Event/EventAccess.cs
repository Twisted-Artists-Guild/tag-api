// <copyright file="EventAccess.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    /// <summary>What a caller wants to do with an event.</summary>
    internal enum EventAction
    {
        View,
        CheckIn,
        Edit,
        Delete,
        ManageHosts,
    }

    /// <summary>
    /// Who may do what with an event. The one place that decides it, so every endpoint agrees. The caller always comes from
    /// the token. The owner is the event's artist (that artist's users), or the venue for a venue-hosted event (the venue's
    /// owners); platform staff count as owner. Everyone else needs a row in EventHosts, directly or through an artist or
    /// venue they run, and gets the highest role they are given.
    /// </summary>
    internal static class EventAccess
    {
        public static async Task<EventRole> GetRoleAsync(TAGDBContext context, Event @event, int userId)
        {
            if (await VenueAccess.IsStaffAsync(context, userId).ConfigureAwait(false))
            {
                return EventRole.Owner;
            }

            var ownsEvent = @event.ArtistID is int artistId
                ? await ArtistAccess.IsOwnerAsync(context, artistId, userId).ConfigureAwait(false)
                : await VenueAccess.IsOwnerAsync(context, @event.VenueID, userId).ConfigureAwait(false);
            if (ownsEvent)
            {
                return EventRole.Owner;
            }

            var hosts = await context.EventHosts
                .AsNoTracking()
                .Where(h => h.EventID == @event.EventID)
                .Select(h => new { h.HostType, h.HostID, h.Role })
                .ToListAsync()
                .ConfigureAwait(false);
            if (hosts.Count == 0)
            {
                return EventRole.None;
            }

            var artistIds = hosts.Any(h => h.HostType == EventHost.ArtistType)
                ? (await context.Set<Linker_UserToArtist>().AsNoTracking().Where(l => l.UserID == userId).Select(l => l.ArtistID).ToListAsync().ConfigureAwait(false)).ToHashSet()
                : new HashSet<int>();
            var venueIds = hosts.Any(h => h.HostType == EventHost.VenueType)
                ? (await context.Set<Linker_UserToVenue>().AsNoTracking().Where(l => l.UserID == userId).Select(l => l.VenueID).ToListAsync().ConfigureAwait(false)).ToHashSet()
                : new HashSet<int>();

            return hosts
                .Where(h => h.HostType switch
                {
                    EventHost.UserType => h.HostID == userId,
                    EventHost.ArtistType => artistIds.Contains(h.HostID),
                    EventHost.VenueType => venueIds.Contains(h.HostID),
                    _ => false,
                })
                .Select(h => h.Role)
                .DefaultIfEmpty(EventRole.None)
                .Max();
        }

        public static bool Allows(EventRole role, EventAction action) => role >= action switch
        {
            EventAction.View => EventRole.ReadOnly,
            EventAction.CheckIn => EventRole.CheckIn,
            EventAction.Edit => EventRole.Admin,
            EventAction.Delete => EventRole.Owner,
            EventAction.ManageHosts => EventRole.Owner,
            _ => EventRole.Owner,
        };

        public static async Task<bool> CanAsync(TAGDBContext context, Event @event, int userId, EventAction action) =>
            Allows(await GetRoleAsync(context, @event, userId).ConfigureAwait(false), action);
    }
}
