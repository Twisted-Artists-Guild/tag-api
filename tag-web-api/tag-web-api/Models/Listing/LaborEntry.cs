// <copyright file="LaborEntry.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

/// <summary>
/// Labor cost entry. Solo artists use a single entry as their base pay.
/// Partnerships/corporations add multiple entries for different workers.
/// </summary>
public class LaborEntry
{
    [Key]
    public int LaborEntryID { get; set; }

    [Required]
    [ForeignKey("ListingCostBreakdown")]
    public int ListingCostBreakdownID { get; set; }

    [JsonIgnore]
    public ListingCostBreakdown ListingCostBreakdown { get; set; } = null!;

    [StringLength(150)]
    public string? WorkerName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal HoursWorked { get; set; }

    [StringLength(100)]
    public string? Role { get; set; }

    public int DisplayOrder { get; set; }

    [NotMapped]
    public decimal TotalLaborCost => HourlyRate * HoursWorked;
}