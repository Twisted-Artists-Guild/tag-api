using Microsoft.AspNetCore.Mvc;
using TAGWEBAPI.Integrations.ModernTreasury;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Microsoft.AspNetCore.Authorization;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/treasury")]
[Authorize]
public sealed class TreasuryController : ControllerBase
{
    private readonly IModernTreasuryLedgerService modernTreasuryLedgerService;
    private readonly TAGDBContext context;
    private readonly ILogger<TreasuryController> logger;

    public TreasuryController(
        IModernTreasuryLedgerService modernTreasuryLedgerService,
        TAGDBContext context,
        ILogger<TreasuryController> logger)
    {
        this.modernTreasuryLedgerService = modernTreasuryLedgerService;
        this.context = context;
        this.logger = logger;
    }

    [HttpPost("stripe-ledger")]
    public async Task<ActionResult<ModernTreasuryLedgerResponse>> PostStripeLedger(
        [FromBody] StripeLedgerPrototypeRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid)
        {
            return this.ValidationProblem(this.ModelState);
        }

        try
        {
            var response = await this.modernTreasuryLedgerService
                .PostStripeLedgerAsync(request, cancellationToken)
                .ConfigureAwait(false);

            var actionTag = request.IsReversal || string.Equals(request.PrototypeAction, "refund", StringComparison.OrdinalIgnoreCase)
                ? "refund"
                : "purchase";

            await this.TryWriteAuditLogAsync(
                shortText: $"Treasury stripe-ledger {actionTag} posted",
                tags: $"scope=audit;entity=treasury_transaction;event=stripe_ledger_{actionTag};operation=post;result=success;channel=db",
                critical: false,
                loggedData: $"stripeEventId={request.StripeEventId};orderId={request.OrderId};amount={request.GrossAmount};currency={request.Currency};posted={response.Posted};dryRun={response.DryRun}")
                .ConfigureAwait(false);

            return this.Ok(response);
        }
        catch (ArgumentException argumentException)
        {
            await this.TryWriteAuditLogAsync(
                shortText: "Treasury stripe-ledger validation failed",
                tags: "scope=audit;entity=treasury_transaction;event=stripe_ledger;operation=post;result=validation_error;channel=db",
                critical: true,
                longText: argumentException.Message,
                loggedData: $"stripeEventId={request.StripeEventId};orderId={request.OrderId}")
                .ConfigureAwait(false);

            return this.BadRequest(argumentException.Message);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            await this.TryWriteAuditLogAsync(
                shortText: "Treasury stripe-ledger failed",
                tags: "scope=audit;entity=treasury_transaction;event=stripe_ledger;operation=post;result=error;channel=db",
                critical: true,
                longText: invalidOperationException.Message,
                loggedData: $"stripeEventId={request.StripeEventId};orderId={request.OrderId}")
                .ConfigureAwait(false);

            return this.Problem(detail: invalidOperationException.Message, statusCode: 500, title: "Modern Treasury integration error");
        }
    }

    [HttpPost("stripe-event")]
    public async Task<ActionResult<ModernTreasuryLedgerResponse>> PostStripeEvent(
        [FromBody] StripeEventAccountingRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid)
        {
            return this.ValidationProblem(this.ModelState);
        }

        try
        {
            var response = await this.modernTreasuryLedgerService
                .PostStripeEventAsync(request, cancellationToken)
                .ConfigureAwait(false);

            var isRefundLike = request.StripeEventType.Equals("charge.refunded", StringComparison.OrdinalIgnoreCase)
                || request.StripeEventType.Equals("charge.dispute.created", StringComparison.OrdinalIgnoreCase)
                || request.StripeEventType.Equals("payment_intent.payment_failed", StringComparison.OrdinalIgnoreCase);

            var actionTag = isRefundLike ? "refund" : "purchase";

            await this.TryWriteAuditLogAsync(
                shortText: $"Treasury stripe-event {actionTag} posted",
                tags: $"scope=audit;entity=treasury_transaction;event=stripe_event_{actionTag};operation=post;result=success;channel=db",
                critical: false,
                loggedData: $"stripeEventId={request.StripeEventId};stripeEventType={request.StripeEventType};orderId={request.OrderId};amountCents={request.AmountTotalCents};currency={request.Currency};posted={response.Posted};dryRun={response.DryRun}")
                .ConfigureAwait(false);

            return this.Ok(response);
        }
        catch (ArgumentException argumentException)
        {
            await this.TryWriteAuditLogAsync(
                shortText: "Treasury stripe-event validation failed",
                tags: "scope=audit;entity=treasury_transaction;event=stripe_event;operation=post;result=validation_error;channel=db",
                critical: true,
                longText: argumentException.Message,
                loggedData: $"stripeEventId={request.StripeEventId};stripeEventType={request.StripeEventType};orderId={request.OrderId}")
                .ConfigureAwait(false);

            return this.BadRequest(argumentException.Message);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            await this.TryWriteAuditLogAsync(
                shortText: "Treasury stripe-event failed",
                tags: "scope=audit;entity=treasury_transaction;event=stripe_event;operation=post;result=error;channel=db",
                critical: true,
                longText: invalidOperationException.Message,
                loggedData: $"stripeEventId={request.StripeEventId};stripeEventType={request.StripeEventType};orderId={request.OrderId}")
                .ConfigureAwait(false);

            return this.Problem(detail: invalidOperationException.Message, statusCode: 500, title: "Modern Treasury integration error");
        }
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
            this.logger.LogError(ex, "Failed to write treasury audit log. Tags: {Tags}", tags);
        }
    }
}
