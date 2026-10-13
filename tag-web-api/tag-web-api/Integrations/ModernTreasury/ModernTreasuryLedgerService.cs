using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TAGWEBAPI.Integrations.ModernTreasury;

public sealed class ModernTreasuryLedgerService : IModernTreasuryLedgerService
{
    private static readonly HashSet<string> AllowedSellerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "artist",
        "vendor",
    };

    private static readonly HashSet<string> AllowedPrototypeActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "purchase",
        "register_user",
        "register_artist",
        "register_vendor",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly HttpClient httpClient;
    private readonly ModernTreasuryOptions options;
    private readonly ILogger<ModernTreasuryLedgerService> logger;

    public ModernTreasuryLedgerService(
        HttpClient httpClient,
        IOptions<ModernTreasuryOptions> options,
        ILogger<ModernTreasuryLedgerService> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<ModernTreasuryLedgerResponse> PostStripeLedgerAsync(StripeLedgerPrototypeRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var lines = BuildLines(request);
        EnsureBalanced(lines);

        var payload = BuildPayload(request, lines);
        var requestPath = "/api/ledger_transactions";

        var response = new ModernTreasuryLedgerResponse
        {
            DryRun = this.options.DryRun || !this.options.Enabled,
            Posted = false,
            RequestPath = requestPath,
            Payload = payload,
        };

        if (IsRegistrationAction(request.PrototypeAction) && !response.DryRun)
        {
            throw new InvalidOperationException("Registration prototype actions are dry-run only.");
        }

        if (response.DryRun)
        {
            var sanitizedStripeEventId = (request.StripeEventId ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);
            this.logger.LogInformation("Modern Treasury dry-run enabled. Ledger payload built for StripeEventId {StripeEventId}", sanitizedStripeEventId);
            return response;
        }

        if (string.IsNullOrWhiteSpace(this.options.ApiKey))
        {
            throw new InvalidOperationException("ModernTreasury.ApiKey is required when dry-run is disabled.");
        }

        if (string.IsNullOrWhiteSpace(this.options.OrganizationId))
        {
            throw new InvalidOperationException("ModernTreasury.OrganizationId is required when dry-run is disabled.");
        }

        if (string.IsNullOrWhiteSpace(this.options.BaseUrl))
        {
            throw new InvalidOperationException("ModernTreasury.BaseUrl is required when dry-run is disabled.");
        }

        this.httpClient.BaseAddress = new Uri(this.options.BaseUrl);
        this.httpClient.DefaultRequestHeaders.Authorization = BuildBasicAuth(this.options.OrganizationId, this.options.ApiKey);

        using var message = new HttpRequestMessage(HttpMethod.Post, requestPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("Idempotency-Key", request.StripeEventId);

        using var providerResponse = await this.httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var providerBody = await providerResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!providerResponse.IsSuccessStatusCode)
        {
            this.logger.LogError(
                "Modern Treasury request failed. StatusCode: {StatusCode}. Body: {Body}",
                providerResponse.StatusCode,
                providerBody);

            throw new InvalidOperationException($"Modern Treasury request failed: {(int)providerResponse.StatusCode} {providerResponse.ReasonPhrase}");
        }

        response.Posted = true;
        response.ProviderResponse = providerBody;

        return response;
    }

    public Task<ModernTreasuryLedgerResponse> PostStripeEventAsync(StripeEventAccountingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = BuildRequestFromStripeEvent(request);
        return this.PostStripeLedgerAsync(normalized, cancellationToken);
    }

    public async Task<bool> ExecuteArtistACHPayoutAsync(int orderId)
    {
        // 1. Normally you fetch Order details here from DB using DI to get the exact Amount & Artist ID routing numbers.
        // For demonstration, we assume we want to push exactly $50.00 (5000 cents) out as a simulated payout.

        long payoutAmountCents = 5000;
        string artistMTAccountId = "ext_account_simulated_xyz"; // This is their Modern Treasury internal Account ID stored in DB during onboarding

        if (this.options.DryRun || !this.options.Enabled)
        {
            this.logger.LogInformation("Modern Treasury Payout DryRun. Would have pushed {Amount} to Artist for Order {OrderId}", payoutAmountCents, orderId);
            return true;
        }

        var payload = new
        {
            type = "ach",
            amount = payoutAmountCents,
            direction = "credit",
            currency = "USD",
            originating_account_id = "INTERNAL_TAG_MASTER_ACCOUNT_ID", // Your operating account inside MT
            receiving_account_id = artistMTAccountId,
            subtype = "standard",
            description = $"TAG Order #{orderId} Payout",
            metadata = new Dictionary<string, string>
            {
                ["order_id"] = orderId.ToString()
            }
        };

        var requestPath = "/api/payment_orders";
        this.httpClient.BaseAddress = new Uri(this.options.BaseUrl);
        this.httpClient.DefaultRequestHeaders.Authorization = BuildBasicAuth(this.options.OrganizationId, this.options.ApiKey);

        using var message = new HttpRequestMessage(HttpMethod.Post, requestPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("Idempotency-Key", $"payout_order_{orderId}");

        using var providerResponse = await this.httpClient.SendAsync(message, CancellationToken.None).ConfigureAwait(false);
        var providerBody = await providerResponse.Content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false);

        if (!providerResponse.IsSuccessStatusCode)
        {
            this.logger.LogError("Modern Treasury ACH request failed. StatusCode: {StatusCode}. Body: {Body}",
                providerResponse.StatusCode, providerBody);
            return false;
        }

        this.logger.LogInformation("Successfully initiated ACH transfer for Order {OrderId}", orderId);
        return true;
    }

    private static void ValidateRequest(StripeLedgerPrototypeRequest request)
    {
        if (!AllowedPrototypeActions.Contains(request.PrototypeAction))
        {
            throw new ArgumentException("PrototypeAction must be one of: purchase, register_user, register_artist, register_vendor.");
        }

        if (!AllowedSellerTypes.Contains(request.SellerType))
        {
            throw new ArgumentException("SellerType must be either 'artist' or 'vendor'.");
        }

        if (request.PrototypeAction.Equals("purchase", StringComparison.OrdinalIgnoreCase)
            && request.GrossAmount <= 0)
        {
            throw new ArgumentException("GrossAmount must be greater than 0 for purchase flows.");
        }

        if (request.PrototypeAction.Equals("purchase", StringComparison.OrdinalIgnoreCase)
            && request.PlatformFeeAmount + request.ShippingRevenueAmount + request.TaxesWithheldAmount > request.GrossAmount)
        {
            throw new ArgumentException("PlatformFeeAmount + ShippingRevenueAmount + TaxesWithheldAmount cannot exceed GrossAmount.");
        }
    }

    private static List<LedgerLine> BuildLines(StripeLedgerPrototypeRequest request)
    {
        if (IsRegistrationAction(request.PrototypeAction))
        {
            return new List<LedgerLine>();
        }

        var lines = new List<LedgerLine>();

        var payableAccount = request.SellerType.Equals("vendor", StringComparison.OrdinalIgnoreCase)
            ? ModernTreasuryCoa.VendorPayable
            : ModernTreasuryCoa.ArtistPayable;

        var netSellerPayable = request.GrossAmount
            - request.PlatformFeeAmount
            - request.ShippingRevenueAmount
            - request.TaxesWithheldAmount;

        lines.Add(new LedgerLine
        {
            AccountNumber = ModernTreasuryCoa.StripeClearing,
            Direction = "debit",
            Amount = request.GrossAmount,
            Memo = "Gross Stripe cash collection",
        });

        if (request.PlatformFeeAmount > 0)
        {
            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.TransactionFeesRevenue,
                Direction = "credit",
                Amount = request.PlatformFeeAmount,
                Memo = "Platform transaction fee revenue",
            });
        }

        if (request.ShippingRevenueAmount > 0)
        {
            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.ShippingFeesRevenue,
                Direction = "credit",
                Amount = request.ShippingRevenueAmount,
                Memo = "Shipping fees billed to customer",
            });
        }

        if (request.TaxesWithheldAmount > 0)
        {
            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.SalesTaxPayable,
                Direction = "credit",
                Amount = request.TaxesWithheldAmount,
                Memo = "Sales taxes withheld",
            });
        }

        if (netSellerPayable > 0)
        {
            lines.Add(new LedgerLine
            {
                AccountNumber = payableAccount,
                Direction = "credit",
                Amount = netSellerPayable,
                Memo = "Net amount owed to seller",
            });
        }

        if (request.StripeFeeAmount > 0)
        {
            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.PaymentProcessingFees,
                Direction = "debit",
                Amount = request.StripeFeeAmount,
                Memo = "Stripe processing fee expense",
            });

            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.StripeClearing,
                Direction = "credit",
                Amount = request.StripeFeeAmount,
                Memo = "Stripe fee offset from clearing",
            });
        }

        if (request.ShippingCostAmount > 0)
        {
            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.ShippingCosts,
                Direction = "debit",
                Amount = request.ShippingCostAmount,
                Memo = "Shippo shipping label cost",
            });

            lines.Add(new LedgerLine
            {
                AccountNumber = ModernTreasuryCoa.AccountsPayable,
                Direction = "credit",
                Amount = request.ShippingCostAmount,
                Memo = "Amount owed to shipping vendor",
            });
        }

        if (!request.IsReversal)
        {
            return lines;
        }

        // Refund/dispute accounting mirrors the original entry with reversed directions.
        return lines
            .Select(line => new LedgerLine
            {
                AccountNumber = line.AccountNumber,
                Direction = line.Direction == "debit" ? "credit" : "debit",
                Amount = line.Amount,
                Memo = line.Memo,
            })
            .ToList();
    }

    private static StripeLedgerPrototypeRequest BuildRequestFromStripeEvent(StripeEventAccountingRequest request)
    {
        var isReversal = IsReversalEvent(request.StripeEventType);

        return new StripeLedgerPrototypeRequest
        {
            StripeEventId = request.StripeEventId,
            StripePaymentIntentId = request.StripePaymentIntentId,
            OrderId = request.OrderId,
            GrossAmount = ToMajorUnits(request.AmountTotalCents),
            PlatformFeeAmount = ToMajorUnits(request.PlatformFeeCents),
            ShippingRevenueAmount = ToMajorUnits(request.AmountShippingCents),
            TaxesWithheldAmount = ToMajorUnits(request.AmountTaxCents),
            StripeFeeAmount = ToMajorUnits(request.StripeFeeCents),
            ShippingCostAmount = ToMajorUnits(request.ShippingCostCents),
            SellerType = request.SellerType,
            PrototypeAction = request.PrototypeAction,
            EcosystemScenario = request.EcosystemScenario,
            BuyerUserId = request.BuyerUserId,
            SellerAccountId = request.SellerAccountId,
            VendorAccountId = request.VendorAccountId,
            Currency = request.Currency,
            Description = request.Description ?? $"Stripe event {request.StripeEventType}",
            DryRun = request.DryRun,
            IsReversal = isReversal,
        };
    }

    private static bool IsReversalEvent(string stripeEventType)
    {
        return stripeEventType.Equals("charge.refunded", StringComparison.OrdinalIgnoreCase)
            || stripeEventType.Equals("payment_intent.payment_failed", StringComparison.OrdinalIgnoreCase)
            || stripeEventType.Equals("charge.dispute.created", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ToMajorUnits(long amountInCents)
    {
        return decimal.Round(amountInCents / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static void EnsureBalanced(IEnumerable<LedgerLine> lines)
    {
        var totalDebits = lines.Where(line => line.Direction == "debit").Sum(line => line.Amount);
        var totalCredits = lines.Where(line => line.Direction == "credit").Sum(line => line.Amount);

        if (Math.Abs(totalDebits - totalCredits) > 0.01m)
        {
            throw new InvalidOperationException($"Ledger payload is unbalanced. Debits={totalDebits}, Credits={totalCredits}");
        }
    }

    private object BuildPayload(StripeLedgerPrototypeRequest request, IReadOnlyCollection<LedgerLine> lines)
    {
        if (string.IsNullOrWhiteSpace(this.options.LedgerId))
        {
            throw new InvalidOperationException("ModernTreasury.LedgerId is required.");
        }

        return new
        {
            ledger_id = this.options.LedgerId,
            external_id = request.StripeEventId,
            status = "posted",
            description = request.Description ?? "Stripe settlement journal",
            metadata = new Dictionary<string, string>
            {
                ["stripe_event_id"] = request.StripeEventId,
                ["stripe_payment_intent_id"] = request.StripePaymentIntentId ?? string.Empty,
                ["order_id"] = request.OrderId ?? string.Empty,
                ["seller_type"] = request.SellerType.ToLowerInvariant(),
                ["prototype_action"] = request.PrototypeAction.ToLowerInvariant(),
                ["ecosystem_scenario"] = request.EcosystemScenario,
                ["buyer_user_id"] = request.BuyerUserId,
                ["seller_account_id"] = request.SellerAccountId,
                ["vendor_account_id"] = request.VendorAccountId,
                ["currency"] = request.Currency.ToUpperInvariant(),
            },
            ledger_entries = lines.Select(line => new
            {
                ledger_account_id = this.GetLedgerAccountId(line.AccountNumber),
                direction = line.Direction,
                amount = ToMinorUnits(line.Amount),
                metadata = new Dictionary<string, string>
                {
                    ["coa_account_number"] = line.AccountNumber,
                    ["coa_account_name"] = ModernTreasuryCoa.GetAccountName(line.AccountNumber),
                    ["memo"] = line.Memo ?? string.Empty,
                },
            }),
        };
    }

    private string GetLedgerAccountId(string accountNumber)
    {
        if (this.options.AccountMappings.TryGetValue(accountNumber, out var ledgerAccountId)
            && !string.IsNullOrWhiteSpace(ledgerAccountId))
        {
            return ledgerAccountId;
        }

        throw new InvalidOperationException($"Modern Treasury account mapping is missing for CoA account {accountNumber}.");
    }

    private static long ToMinorUnits(decimal amount)
    {
        return decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static AuthenticationHeaderValue BuildBasicAuth(string organizationId, string apiKey)
    {
        var credentialBytes = Encoding.ASCII.GetBytes($"{organizationId}:{apiKey}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
    }

    private static bool IsRegistrationAction(string prototypeAction)
    {
        return prototypeAction.Equals("register_user", StringComparison.OrdinalIgnoreCase)
            || prototypeAction.Equals("register_artist", StringComparison.OrdinalIgnoreCase)
            || prototypeAction.Equals("register_vendor", StringComparison.OrdinalIgnoreCase);
    }
}
