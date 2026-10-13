// <copyright file="Listing.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

public class Listing
{
    [Key]
    public int ListingID { get; set; }

    public string? Title { get; set; }

    public string? Title_Plaintext { get; set; }

    public string? Description { get; set; }

    public string? Description_Plaintext { get; set; }

    public string? CatalogueID { get; set; }

    public bool CommissionInquiryWelcome { get; set; }

    public DateTime? Created { get; set; }

    public string? Credits { get; set; }

    public string? Culture { get; set; }

    public string? Date { get; set; }

    public string? Department { get; set; }

    public string? Locale { get; set; }

    public string? Locus { get; set; }

    public string? Medium { get; set; }

    public string? Period { get; set; }

    public decimal? Price { get; set; }

    public string? Repository { get; set; }

    public string? Rights { get; set; }

    public string? TaxJurisdiction { get; set; }

    public DateTime? Work_BeginDate { get; set; }

    public DateTime? Work_CompletionDate { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    [Required]
    public string Path { get; set; } = string.Empty;

    [ForeignKey("ArtCategory")]
    public int ArtCategoryID { get; set; }

    public ArtCategory ArtCategory { get; set; } = null!;

    [Required]
    [ForeignKey("Artist")]
    public int ArtistID { get; set; }

    public Artist Artist { get; set; } = null!;

    public int? GalleryID { get; set; }

    [ForeignKey("Picture")]
    public int? CoverPicID { get; set; }

    public Picture? CoverPic { get; set; }

    public Gallery? Gallery { get; set; }

    public ICollection<ListingSalesItem> ListingSalesItems { get; set; } = new List<ListingSalesItem>();

    public ListingCostBreakdown? CostBreakdown { get; set; }
}
