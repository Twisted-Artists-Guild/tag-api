// <copyright file="Event.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class Event
{
    [Key]
    public int EventID { get; set; }

    public int ArtistID { get; set; }

    public decimal Cost { get; set; }

    [Required]
    public string Description { get; set; }

    public string? Description_Plaintext { get; set; }

    public DateTime Doors { get; set; }

    public DateTime EndTime { get; set; }

    public int MaxOccupancy { get; set; }

    public int MinimumAge { get; set; }

    public string? Note { get; set; }

    public string? Note_Plaintext { get; set; }

    [Required]
    public string PointOfContact { get; set; }

    public DateTime StartTime { get; set; }

    [Required]
    public string Title { get; set; }

    public string? Title_Plaintext { get; set; }

    public int VenueID { get; set; }

    [Required]
    public string Path { get; set; }

    [ForeignKey("EventCategory")]
    public int? EventCategoryID { get; set; }

    public int? GalleryID { get; set; }

    public int? CoverPicID { get; set; }

    public int? ProfilePicID { get; set; }

    public int? LogoPicID { get; set; }

    public EventStatus StatusID { get; set; } = EventStatus.Draft;

    // Navigation properties
    public Artist Artist { get; set; }

    public EventCategory? EventCategory { get; set; }

    public Venue Venue { get; set; }

    public Gallery? Gallery { get; set; }

    public Picture? CoverPic { get; set; }

    public Picture? ProfilePic { get; set; }

    public Picture? LogoPic { get; set; }
}
