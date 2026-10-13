using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Integrations.ModernTreasury;

public sealed class StripeLedgerPrototypeRequest
{
    [Required]
    public string StripeEventId { get; set; } = string.Empty;

    public string? StripePaymentIntentId { get; set; }

    public string? OrderId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal GrossAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PlatformFeeAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ShippingRevenueAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxesWithheldAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal StripeFeeAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ShippingCostAmount { get; set; }

    [Required]
    public string SellerType { get; set; } = "artist";

    [Required]
    public string PrototypeAction { get; set; } = "purchase";

    public string EcosystemScenario { get; set; } = "user-buys-art";

    public string BuyerUserId { get; set; } = "user_demo_001";

    public string SellerAccountId { get; set; } = "artist_demo_001";

    public string VendorAccountId { get; set; } = "vendor_demo_001";

    [Required]
    public string Currency { get; set; } = "USD";

    public string? Description { get; set; }

    public bool? DryRun { get; set; }

    public bool IsReversal { get; set; }
}

public sealed class StripeEventAccountingRequest
{
    [Required]
    public string StripeEventId { get; set; } = string.Empty;

    [Required]
    public string StripeEventType { get; set; } = string.Empty;

    public string? StripePaymentIntentId { get; set; }

    public string? OrderId { get; set; }

    [Range(0, long.MaxValue)]
    public long AmountTotalCents { get; set; }

    [Range(0, long.MaxValue)]
    public long AmountTaxCents { get; set; }

    [Range(0, long.MaxValue)]
    public long AmountShippingCents { get; set; }

    [Range(0, long.MaxValue)]
    public long PlatformFeeCents { get; set; }

    [Range(0, long.MaxValue)]
    public long StripeFeeCents { get; set; }

    [Range(0, long.MaxValue)]
    public long ShippingCostCents { get; set; }

    [Required]
    public string SellerType { get; set; } = "artist";

    [Required]
    public string PrototypeAction { get; set; } = "purchase";

    public string EcosystemScenario { get; set; } = "user-buys-art";

    public string BuyerUserId { get; set; } = "user_demo_001";

    public string SellerAccountId { get; set; } = "artist_demo_001";

    public string VendorAccountId { get; set; } = "vendor_demo_001";

    [Required]
    public string Currency { get; set; } = "USD";

    public string? Description { get; set; }

    public bool? DryRun { get; set; }
}

public sealed class ModernTreasuryLedgerResponse
{
    public bool Posted { get; set; }

    public bool DryRun { get; set; }

    public string RequestPath { get; set; } = string.Empty;

    public object Payload { get; set; } = new();

    public string? ProviderResponse { get; set; }
}

public sealed class LedgerLine
{
    public string AccountNumber { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Memo { get; set; }
}
