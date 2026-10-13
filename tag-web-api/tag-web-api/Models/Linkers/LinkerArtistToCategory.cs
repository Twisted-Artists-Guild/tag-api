// <copyright file="LinkerArtistToCategory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class LinkerArtistToCategory
{
    [Key]
    public int Linker_ArtistToCategoryID { get; set; }

    [Required]
    [ForeignKey("Artist")]
    public int ArtistID { get; set; }

    [Required]
    [ForeignKey("ArtistCategory")]
    public int ArtistCategoryID { get; set; }

    [Required]
    public string Genre { get; set; }

    [Required]
    public string ExpertiseLevel { get; set; }

    [Required]
    public bool IsProfessional { get; set; }

    public virtual Artist? Artist { get; set; }

    public virtual ArtistCategory? ArtistCategory { get; set; }
}