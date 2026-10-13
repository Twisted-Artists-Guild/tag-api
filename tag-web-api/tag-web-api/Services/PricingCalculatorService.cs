// <copyright file="PricingCalculatorService.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Services;

using TAGWEBAPI.Models;

public class PricingCalculatorService
{
    /// <summary>
    /// Recalculates ASMRP and suggested FinalPrice from the full cost breakdown.
    /// </summary>
    public PricingResult Calculate(ListingCostBreakdown breakdown)
    {
        decimal materialsCost = breakdown.CostLineItems
            .Where(c => c.Category == "Materials")
            .Sum(c => c.Amount);

        decimal businessCost = breakdown.CostLineItems
            .Where(c => c.Category == "Business")
            .Sum(c => c.Amount);

        decimal consumablesCost = breakdown.CostLineItems
            .Where(c => c.Category == "Consumables")
            .Sum(c => c.Amount);

        decimal laborCost = breakdown.LaborEntries
            .Sum(l => l.HourlyRate * l.HoursWorked);

        decimal totalCost = materialsCost
            + businessCost
            + consumablesCost
            + laborCost
            + breakdown.PackagingCost
            + breakdown.ShippingEstimate;

        // Apply minimum profit to derive ASMRP
        decimal minProfit = 0m;
        if (breakdown.ProfitMinAmount.HasValue)
        {
            minProfit = breakdown.ProfitMinAmount.Value;
        }
        else if (breakdown.ProfitMinPercent.HasValue)
        {
            minProfit = totalCost * (breakdown.ProfitMinPercent.Value / 100m);
        }

        decimal asmrp = totalCost + minProfit;

        // Suggested final price uses max profit when available
        decimal maxProfit = minProfit;
        if (breakdown.ProfitMaxAmount.HasValue)
        {
            maxProfit = breakdown.ProfitMaxAmount.Value;
        }
        else if (breakdown.ProfitMaxPercent.HasValue)
        {
            maxProfit = totalCost * (breakdown.ProfitMaxPercent.Value / 100m);
        }

        decimal suggestedPrice = totalCost + maxProfit;

        // In-person pickup price
        decimal pickupPrice = suggestedPrice
            - breakdown.InPersonPickupDiscount
            + breakdown.InPersonVendingCost;

        return new PricingResult
        {
            MaterialsCost = materialsCost,
            BusinessCost = businessCost,
            ConsumablesCost = consumablesCost,
            LaborCost = laborCost,
            PackagingCost = breakdown.PackagingCost,
            ShippingEstimate = breakdown.ShippingEstimate,
            TotalCost = totalCost,
            MinProfit = minProfit,
            MaxProfit = maxProfit,
            ASMRP = asmrp,
            SuggestedFinalPrice = suggestedPrice,
            InPersonPickupPrice = pickupPrice,
        };
    }
}

public class PricingResult
{
    public decimal MaterialsCost { get; set; }

    public decimal BusinessCost { get; set; }

    public decimal ConsumablesCost { get; set; }

    public decimal LaborCost { get; set; }

    public decimal PackagingCost { get; set; }

    public decimal ShippingEstimate { get; set; }

    public decimal TotalCost { get; set; }

    public decimal MinProfit { get; set; }

    public decimal MaxProfit { get; set; }

    public decimal ASMRP { get; set; }

    public decimal SuggestedFinalPrice { get; set; }

    public decimal InPersonPickupPrice { get; set; }
}