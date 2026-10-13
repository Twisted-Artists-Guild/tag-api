// <copyright file="ListingCostBreakdown.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class ListingCostBreakdown
{
    [Key]
    public int ListingCostBreakdownID { get; set; }

    [Required]
    [ForeignKey("Listing")]
    public int ListingID { get; set; }

    public Listing Listing { get; set; } = null!;

    // ── Packaging & Shipping ──
    [Column(TypeName = "decimal(18,2)")]
    public decimal PackagingCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingEstimate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InPersonPickupDiscount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InPersonVendingCost { get; set; }

    // ── Profit Controls ──
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ProfitMinAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ProfitMaxAmount { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ProfitMinPercent { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ProfitMaxPercent { get; set; }

    // ── Pricing ──
    [Column(TypeName = "decimal(18,2)")]
    public decimal ASMRP { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalPrice { get; set; }

    public bool ArtistPriceOverride { get; set; }

    public bool BelowASMRPConfirmed { get; set; }

    [StringLength(500)]
    public string? PriceOverrideReason { get; set; }

    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;

    // ── Navigation ──
    public ICollection<CostLineItem> CostLineItems { get; set; } = new List<CostLineItem>();

    public ICollection<LaborEntry> LaborEntries { get; set; } = new List<LaborEntry>();
}