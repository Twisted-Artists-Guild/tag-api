// <copyright file="Linker_VenueToTag.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Links a venue to one of its tags. A venue can have several tags.
/// </summary>
public class Linker_VenueToTag
{
    public int Linker_VenueToTagID { get; set; }

    [Required]
    public int VenueID { get; set; }

    [Required]
    public int VenueTagID { get; set; }

    public virtual Venue? Venue { get; set; }

    public virtual VenueTag? Tag { get; set; }
}
