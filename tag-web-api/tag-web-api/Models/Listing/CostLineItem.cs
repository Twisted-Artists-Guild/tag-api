// <copyright file="CostLineItem.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

/// <summary>
/// Individual cost entry for materials, business overhead, or consumables.
/// </summary>
public class CostLineItem
{
    [Key]
    public int CostLineItemID { get; set; }

    [Required]
    [ForeignKey("ListingCostBreakdown")]
    public int ListingCostBreakdownID { get; set; }

    [JsonIgnore]
    public ListingCostBreakdown ListingCostBreakdown { get; set; } = null!;

    /// <summary>
    /// "Materials", "Business", or "Consumables".
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public int DisplayOrder { get; set; }
}