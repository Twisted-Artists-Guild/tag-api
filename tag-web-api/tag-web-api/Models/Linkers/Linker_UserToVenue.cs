// <copyright file="Linker_UserToVenue.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Links a user to a venue they manage. A venue may have several owners, or none (for example a staff-seeded listing).
/// </summary>
public class Linker_UserToVenue
{
    public const string OwnerRole = "Owner";

    public int Linker_UserToVenueID { get; set; }

    [Required]
    public int UserID { get; set; }

    [Required]
    public int VenueID { get; set; }

    [Required]
    [MaxLength(255)]
    public string Role { get; set; } = OwnerRole;

    // Lets a new venue and its owner link be saved in one SaveChanges; never serialized to clients.
    public virtual Venue? Venue { get; set; }
}
