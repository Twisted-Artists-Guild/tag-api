// <copyright file="ArtistCategory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class ArtistCategory
{
    [Key]
    public int ArtistCategoryID { get; set; }

    [Required]
    public string Category { get; set; }

    [Required]
    public string CategoryKey { get; set; }

    [Required]
    public string Description { get; set; }

    [Required]
    public string Tags { get; set; }

    [ForeignKey("ParentCategory")]
    public int? ParentArtistCategoryID { get; set; }

    public virtual ArtistCategory? ParentCategory { get; set; }

    public virtual ICollection<ArtistCategory>? SubCategories { get; set; } = new List<ArtistCategory>();

    public virtual ICollection<LinkerArtistToCategory>? ArtistLinks { get; set; } = new List<LinkerArtistToCategory>();
}
