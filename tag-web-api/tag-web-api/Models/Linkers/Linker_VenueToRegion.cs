// <copyright file="Linker_VenueToRegion.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Links a venue to a region. A venue can belong to several regions.
/// </summary>
public class Linker_VenueToRegion
{
    public int Linker_VenueToRegionID { get; set; }

    [Required]
    public int VenueID { get; set; }

    [Required]
    public int RegionID { get; set; }

    public virtual Venue? Venue { get; set; }

    public virtual Region? Region { get; set; }
}

/// <summary>
/// Links an artist to a region. Artists use the same regions as venues; choosing regions on the artist screens is a separate story.
/// </summary>
public class Linker_ArtistToRegion
{
    public int Linker_ArtistToRegionID { get; set; }

    [Required]
    public int ArtistID { get; set; }

    [Required]
    public int RegionID { get; set; }

    public virtual Region? Region { get; set; }
}
