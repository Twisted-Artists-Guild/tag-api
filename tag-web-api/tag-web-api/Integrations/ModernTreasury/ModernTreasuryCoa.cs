namespace TAGWEBAPI.Integrations.ModernTreasury;

public static class ModernTreasuryCoa
{
    public const string StripeClearing = "1020";
    public const string AccountsPayable = "2000";
    public const string SalesTaxPayable = "2460";
    public const string ArtistPayable = "2470";
    public const string VendorPayable = "2475";
    public const string TransactionFeesRevenue = "4000";
    public const string ShippingFeesRevenue = "4100";
    public const string PaymentProcessingFees = "5030";
    public const string ShippingCosts = "5100";

    private static readonly IReadOnlyDictionary<string, string> AccountNames = new Dictionary<string, string>
    {
        [StripeClearing] = "Stripe Clearing",
        [AccountsPayable] = "Accounts Payable",
        [SalesTaxPayable] = "Sales Tax Payable",
        [ArtistPayable] = "Artist Payable",
        [VendorPayable] = "Vendor Payable",
        [TransactionFeesRevenue] = "Transaction Fees",
        [ShippingFeesRevenue] = "Shipping Fees Revenue",
        [PaymentProcessingFees] = "Payment Processing Fees",
        [ShippingCosts] = "Shipping Costs",
    };

    public static string GetAccountName(string accountNumber)
    {
        return AccountNames.TryGetValue(accountNumber, out var accountName)
            ? accountName
            : "Unknown CoA Account";
    }
}
