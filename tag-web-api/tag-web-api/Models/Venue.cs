// <copyright file="Venue.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TAGWEBAPI.Models;
public class Venue
{
    public const int EventSourceUrlMaxLength = 2048;

    [Key]
    public int VenueID { get; set; }

    public string Name { get; set; }

    public string? Name_Plaintext { get; set; }

    [ForeignKey("Address")]
    public int AddressID { get; set; }

    // Optional so a venue can exist with only a name and address (for example staff-seeded, unowned venues).
    [ForeignKey("ExternalLink")]
    public int? ExternalLinkID { get; set; }

    [ForeignKey("PhoneContact")]
    public int? PhoneContactID { get; set; }

    [ForeignKey("Picture")]
    public int? LogoPicID { get; set; }

    // Rich text; Description_Plaintext is maintained by the database trigger.
    public string? Description { get; set; }

    public string? Description_Plaintext { get; set; }

    // Page a future crawler can scan for events. Absolute http/https URL only.
    public string? EventSourceUrl { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    // Navigation properties
    public virtual Address Address { get; set; }
    public virtual ExternalLink? ExternalLink { get; set; }
    public virtual PhoneContact? PhoneContact { get; set; }
    public virtual Picture? LogoPic { get; set; }
}
