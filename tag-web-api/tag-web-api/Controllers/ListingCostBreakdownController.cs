// <copyright file="ListingCostBreakdownController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using TAGWEBAPI.Services;

namespace TAGWEBAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ListingCostBreakdownController : ControllerBase
{
    private readonly TAGDBContext _context;
    private readonly PricingCalculatorService _calculator;

    public ListingCostBreakdownController(TAGDBContext context, PricingCalculatorService calculator)
    {
        _context = context;
        _calculator = calculator;
    }

    // GET: api/ListingCostBreakdown/by-listing/{listingId}
    [HttpGet("by-listing/{listingId}")]
    public async Task<ActionResult<CostBreakdownDTO>> GetByListing(int listingId)
    {
        var breakdown = await _context.ListingCostBreakdowns
            .Include(b => b.CostLineItems.OrderBy(c => c.DisplayOrder))
            .Include(b => b.LaborEntries.OrderBy(l => l.DisplayOrder))
            .FirstOrDefaultAsync(b => b.ListingID == listingId);

        if (breakdown == null)
        {
            return NotFound();
        }

        return MapToDTO(breakdown);
    }

    // POST: api/ListingCostBreakdown/calculate (preview only, no save)
    [HttpPost("calculate")]
    public ActionResult<PricingResult> Calculate(CostBreakdownDTO dto)
    {
        var breakdown = MapToEntity(dto);
        return _calculator.Calculate(breakdown);
    }

    // POST: api/ListingCostBreakdown
    [HttpPost]
    public async Task<ActionResult<CostBreakdownDTO>> Create(CostBreakdownDTO dto)
    {
        var existing = await _context.ListingCostBreakdowns
            .AnyAsync(b => b.ListingID == dto.ListingID);

        if (existing)
        {
            return Conflict("A cost breakdown already exists for this listing. Use PUT to update.");
        }

        var breakdown = MapToEntity(dto);
        var result = _calculator.Calculate(breakdown);
        breakdown.ASMRP = result.ASMRP;
        breakdown.FinalPrice = breakdown.ArtistPriceOverride ? breakdown.FinalPrice : result.SuggestedFinalPrice;
        breakdown.LastCalculated = DateTime.UtcNow;

        _context.ListingCostBreakdowns.Add(breakdown);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByListing), new { listingId = breakdown.ListingID }, MapToDTO(breakdown));
    }

    // PUT: api/ListingCostBreakdown/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CostBreakdownDTO dto)
    {
        if (id != dto.ListingCostBreakdownID)
        {
            return BadRequest();
        }

        var breakdown = MapToEntity(dto);
        breakdown.ListingCostBreakdownID = id;

        var result = _calculator.Calculate(breakdown);
        breakdown.ASMRP = result.ASMRP;
        breakdown.LastCalculated = DateTime.UtcNow;

        // Enforce double-confirm if artist overrides below ASMRP
        if (breakdown.ArtistPriceOverride && breakdown.FinalPrice < result.ASMRP)
        {
            if (!breakdown.BelowASMRPConfirmed)
            {
                return BadRequest(new
                {
                    error = "below_asmrp",
                    message = "Price is below the Artist Suggested Minimum Retail Price. Set belowASMRPConfirmed to true to proceed.",
                    asmrp = result.ASMRP,
                    suggestedPrice = result.SuggestedFinalPrice,
                });
            }
        }
        else
        {
            breakdown.FinalPrice = result.SuggestedFinalPrice;
            breakdown.BelowASMRPConfirmed = false;
        }

        _context.Entry(breakdown).State = EntityState.Modified;

        // Replace child collections
        var existingItems = await _context.CostLineItems
            .Where(c => c.ListingCostBreakdownID == id).ToListAsync();
        _context.CostLineItems.RemoveRange(existingItems);
        foreach (var item in breakdown.CostLineItems)
        {
            item.ListingCostBreakdownID = id;
            _context.CostLineItems.Add(item);
        }

        var existingLabor = await _context.LaborEntries
            .Where(l => l.ListingCostBreakdownID == id).ToListAsync();
        _context.LaborEntries.RemoveRange(existingLabor);
        foreach (var entry in breakdown.LaborEntries)
        {
            entry.ListingCostBreakdownID = id;
            _context.LaborEntries.Add(entry);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/ListingCostBreakdown/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var breakdown = await _context.ListingCostBreakdowns.FindAsync(id);
        if (breakdown == null)
        {
            return NotFound();
        }

        _context.ListingCostBreakdowns.Remove(breakdown);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Mapping helpers ──────────────────────────────────────────────

    private static ListingCostBreakdown MapToEntity(CostBreakdownDTO dto)
    {
        return new ListingCostBreakdown
        {
            ListingCostBreakdownID = dto.ListingCostBreakdownID ?? 0,
            ListingID = dto.ListingID,
            PackagingCost = dto.PackagingCost,
            ShippingEstimate = dto.ShippingEstimate,
            InPersonPickupDiscount = dto.InPersonPickupDiscount,
            InPersonVendingCost = dto.InPersonVendingCost,
            ProfitMinAmount = dto.ProfitMinAmount,
            ProfitMaxAmount = dto.ProfitMaxAmount,
            ProfitMinPercent = dto.ProfitMinPercent,
            ProfitMaxPercent = dto.ProfitMaxPercent,
            ASMRP = dto.ASMRP,
            FinalPrice = dto.FinalPrice,
            ArtistPriceOverride = dto.ArtistPriceOverride,
            BelowASMRPConfirmed = dto.BelowASMRPConfirmed,
            PriceOverrideReason = dto.PriceOverrideReason,
            CostLineItems = dto.CostLineItems.Select(c => new CostLineItem
            {
                CostLineItemID = c.CostLineItemID ?? 0,
                Category = c.Category,
                Description = c.Description,
                Amount = c.Amount,
                DisplayOrder = c.DisplayOrder,
            }).ToList(),
            LaborEntries = dto.LaborEntries.Select(l => new LaborEntry
            {
                LaborEntryID = l.LaborEntryID ?? 0,
                WorkerName = l.WorkerName,
                HourlyRate = l.HourlyRate,
                HoursWorked = l.HoursWorked,
                Role = l.Role,
                DisplayOrder = l.DisplayOrder,
            }).ToList(),
        };
    }

    private static CostBreakdownDTO MapToDTO(ListingCostBreakdown entity)
    {
        return new CostBreakdownDTO
        {
            ListingCostBreakdownID = entity.ListingCostBreakdownID,
            ListingID = entity.ListingID,
            PackagingCost = entity.PackagingCost,
            ShippingEstimate = entity.ShippingEstimate,
            InPersonPickupDiscount = entity.InPersonPickupDiscount,
            InPersonVendingCost = entity.InPersonVendingCost,
            ProfitMinAmount = entity.ProfitMinAmount,
            ProfitMaxAmount = entity.ProfitMaxAmount,
            ProfitMinPercent = entity.ProfitMinPercent,
            ProfitMaxPercent = entity.ProfitMaxPercent,
            ASMRP = entity.ASMRP,
            FinalPrice = entity.FinalPrice,
            ArtistPriceOverride = entity.ArtistPriceOverride,
            BelowASMRPConfirmed = entity.BelowASMRPConfirmed,
            PriceOverrideReason = entity.PriceOverrideReason,
            CostLineItems = entity.CostLineItems.Select(c => new CostLineItemDTO
            {
                CostLineItemID = c.CostLineItemID,
                Category = c.Category,
                Description = c.Description,
                Amount = c.Amount,
                DisplayOrder = c.DisplayOrder,
            }).ToList(),
            LaborEntries = entity.LaborEntries.Select(l => new LaborEntryDTO
            {
                LaborEntryID = l.LaborEntryID,
                WorkerName = l.WorkerName,
                HourlyRate = l.HourlyRate,
                HoursWorked = l.HoursWorked,
                Role = l.Role,
                DisplayOrder = l.DisplayOrder,
            }).ToList(),
        };
    }
}