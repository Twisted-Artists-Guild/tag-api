// <copyright file="LinkerTransactionLineItemController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class Linker_TransactionLineItemController : ControllerBase
{
    private readonly TAGDBContext context;
    private readonly ILogger<Linker_TransactionLineItemController> logger;

    public Linker_TransactionLineItemController(TAGDBContext context, ILogger<Linker_TransactionLineItemController> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Linker_TransactionLineItem>>> Get()
    {
        return await this.context.Set<Linker_TransactionLineItem>().ToListAsync().ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Linker_TransactionLineItem>> Get(int id)
    {
        var linker_TransactionLineItem = await this.context.Set<Linker_TransactionLineItem>().FindAsync(id).ConfigureAwait(false);
        if (linker_TransactionLineItem == null)
        {
            return this.NotFound();
        }

        return linker_TransactionLineItem;
    }

    [HttpPost]
    public async Task<ActionResult<Linker_TransactionLineItem>> Create(Linker_TransactionLineItem linker_TransactionLineItem)
    {
        this.context.Set<Linker_TransactionLineItem>().Add(linker_TransactionLineItem);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "Transaction line item created",
            tags: "scope=audit;entity=transaction_line_item;event=purchase;operation=create;result=success;channel=db",
            listingId: linker_TransactionLineItem.ListingID,
            loggedData: $"lineItemId={linker_TransactionLineItem.Linker_TransactionLineItemID};transactionId={linker_TransactionLineItem.TransactionID};finalSalesPrice={linker_TransactionLineItem.FinalSalesPrice}")
            .ConfigureAwait(false);

        return this.CreatedAtAction(nameof(this.Get), new { id = linker_TransactionLineItem.Linker_TransactionLineItemID }, linker_TransactionLineItem);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Linker_TransactionLineItem linker_TransactionLineItem)
    {
        if (id != linker_TransactionLineItem.Linker_TransactionLineItemID)
        {
            return this.BadRequest();
        }

        this.context.Entry(linker_TransactionLineItem).State = EntityState.Modified;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "Transaction line item updated",
                tags: "scope=audit;entity=transaction_line_item;event=line_item;operation=update;result=success;channel=db",
                listingId: linker_TransactionLineItem.ListingID,
                loggedData: $"lineItemId={linker_TransactionLineItem.Linker_TransactionLineItemID};transactionId={linker_TransactionLineItem.TransactionID};finalSalesPrice={linker_TransactionLineItem.FinalSalesPrice}")
                .ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.Linker_TransactionLineItemExists(id))
            {
                return this.NotFound();
            }
            else
            {
                throw;
            }
        }

        return this.NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var linker_TransactionLineItem = await this.context.Set<Linker_TransactionLineItem>().FindAsync(id).ConfigureAwait(false);
        if (linker_TransactionLineItem == null)
        {
            return this.NotFound();
        }

        this.context.Set<Linker_TransactionLineItem>().Remove(linker_TransactionLineItem);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "Transaction line item deleted",
            tags: "scope=audit;entity=transaction_line_item;event=line_item;operation=delete;result=success;channel=db",
            critical: true,
            listingId: linker_TransactionLineItem.ListingID,
            loggedData: $"lineItemId={id};transactionId={linker_TransactionLineItem.TransactionID};finalSalesPrice={linker_TransactionLineItem.FinalSalesPrice}")
            .ConfigureAwait(false);

        return this.NoContent();
    }

    private async Task TryWriteAuditLogAsync(
        string shortText,
        string tags,
        bool critical = false,
        string? longText = null,
        string? loggedData = null,
        int? userId = null,
        int? artistId = null,
        int? listingId = null)
    {
        try
        {
            this.context.Set<Log>().Add(new Log
            {
                ShortText = shortText,
                Tags = tags,
                Critical = critical,
                LongText = longText,
                LoggedData = loggedData,
                UserID = userId,
                ArtistID = artistId,
                ListingID = listingId,
                LogTimestamp = DateTime.UtcNow,
            });

            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to write transaction line-item audit log. Tags: {Tags}", tags);
        }
    }

    private bool Linker_TransactionLineItemExists(int id)
    {
        return this.context.Set<Linker_TransactionLineItem>().Any(e => e.Linker_TransactionLineItemID == id);
    }
}
