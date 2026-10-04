// <copyright file="Artist.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

public class Artist
{
    [Key]
    public int ArtistID { get; set; }

    [Required]
    public DateTime Applied { get; set; }

    public string? Biography { get; set; }

    public string? Biography_Plaintext { get; set; }

    public string? Country { get; set; }

    public string? StateOrProvince { get; set; }

    public string? BusinessEntityType { get; set; }

    public bool? IsFormallyIncorporated { get; set; }

    public string? Byline { get; set; }

    public string? Byline_Plaintext { get; set; }

    public int? IncorporatedYear { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    [Required]
    public string Path { get; set; }

    public string? SEOTags { get; set; }

    public DateTime? Since { get; set; }

    public string? Statement { get; set; }

    public string? Statement_Plaintext { get; set; }

    [Required]
    public string Title { get; set; }

    public string? Title_Plaintext { get; set; }

    [ForeignKey("Picture")]
    public int? CoverPicID { get; set; }

    public virtual Picture? CoverPic { get; set; }

    [ForeignKey("Picture")]
    public int? ProfilePicID { get; set; }

    public virtual Picture? ProfilePic { get; set; }

    [ForeignKey("Picture")]
    public int? LogoPicID { get; set; }

    public virtual Picture? LogoPic { get; set; }

    public int? GalleryID { get; set; }

    public virtual Gallery? Gallery { get; set; }

    public virtual ArtistPermissions ArtistPermissions { get; set; }

    public virtual ICollection<LinkerArtistToCategory>? ArtistCategoryLinks { get; set; } = new List<LinkerArtistToCategory>();

    [JsonIgnore]
    public virtual ICollection<Listing>? Listings { get; set; } = new List<Listing>();

    public void SetPath(string path)
    {
        Path = path.ToLower();
    }
}
