// <copyright file="Linker_VenueToCategory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Links a venue to one of its categories. A venue can have several categories.
/// </summary>
public class Linker_VenueToCategory
{
    public int Linker_VenueToCategoryID { get; set; }

    [Required]
    public int VenueID { get; set; }

    [Required]
    public int VenueCategoryID { get; set; }

    public virtual Venue? Venue { get; set; }

    public virtual VenueCategory? Category { get; set; }
}
