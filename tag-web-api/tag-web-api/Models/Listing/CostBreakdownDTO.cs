// <copyright file="CostBreakdownDTO.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public class CostBreakdownDTO
{
    public int? ListingCostBreakdownID { get; set; }

    public int ListingID { get; set; }

    public decimal PackagingCost { get; set; }

    public decimal ShippingEstimate { get; set; }

    public decimal InPersonPickupDiscount { get; set; }

    public decimal InPersonVendingCost { get; set; }

    public decimal? ProfitMinAmount { get; set; }

    public decimal? ProfitMaxAmount { get; set; }

    public decimal? ProfitMinPercent { get; set; }

    public decimal? ProfitMaxPercent { get; set; }

    public decimal ASMRP { get; set; }

    public decimal FinalPrice { get; set; }

    public bool ArtistPriceOverride { get; set; }

    public bool BelowASMRPConfirmed { get; set; }

    public string? PriceOverrideReason { get; set; }

    public List<CostLineItemDTO> CostLineItems { get; set; } = new();

    public List<LaborEntryDTO> LaborEntries { get; set; } = new();
}

public class CostLineItemDTO
{
    public int? CostLineItemID { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int DisplayOrder { get; set; }
}

public class LaborEntryDTO
{
    public int? LaborEntryID { get; set; }

    public string? WorkerName { get; set; }

    public decimal HourlyRate { get; set; }

    public decimal HoursWorked { get; set; }

    public string? Role { get; set; }

    public int DisplayOrder { get; set; }
}