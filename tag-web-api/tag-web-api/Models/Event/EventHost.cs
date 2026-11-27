// <copyright file="EventHost.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>
/// Someone besides the owner who takes part in an event: a user, an artist (all of the artist's users) or a venue (all of
/// the venue's owners), with the role they have on that event. The owner is the artist or venue on the event itself, so
/// an event always has exactly one owner and this table only holds collaborators.
/// </summary>
public class EventHost
{
    public const string UserType = "User";

    public const string ArtistType = "Artist";

    public const string VenueType = "Venue";

    public static readonly string[] HostTypes = { UserType, ArtistType, VenueType };

    public int EventHostID { get; set; }

    public int EventID { get; set; }

    [Required]
    [MaxLength(16)]
    public string HostType { get; set; } = UserType;

    public int HostID { get; set; }

    // Admin, Staff, CheckIn or ReadOnly. Owner is not assignable.
    public EventRole Role { get; set; } = EventRole.ReadOnly;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedByUserID { get; set; }

    [JsonIgnore]
    public Event? Event { get; set; }

    public static bool IsAssignable(EventRole role) => role is EventRole.Admin or EventRole.Staff or EventRole.CheckIn or EventRole.ReadOnly;
}
