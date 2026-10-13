// <copyright file="Venue.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TAGWEBAPI.Models;
public class Venue
{
    [Key]
    public int VenueID { get; set; }

    public string Name { get; set; }

    public string? Name_Plaintext { get; set; }

    [ForeignKey("Address")]
    public int AddressID { get; set; }

    [ForeignKey("ExternalLink")]
    public int ExternalLinkID { get; set; }

    [ForeignKey("PhoneContact")]
    public int PhoneContactID { get; set; }

    [ForeignKey("Picture")]
    public int? LogoPicID { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    // Navigation properties
    public virtual Address Address { get; set; }
    public virtual ExternalLink ExternalLink { get; set; }
    public virtual PhoneContact PhoneContact { get; set; }
    public virtual Picture? LogoPic { get; set; }
}
