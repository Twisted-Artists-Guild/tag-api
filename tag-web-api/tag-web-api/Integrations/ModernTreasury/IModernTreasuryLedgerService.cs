namespace TAGWEBAPI.Integrations.ModernTreasury;

public interface IModernTreasuryLedgerService
{
    Task<ModernTreasuryLedgerResponse> PostStripeLedgerAsync(StripeLedgerPrototypeRequest request, CancellationToken cancellationToken);
    
    Task<ModernTreasuryLedgerResponse> PostStripeEventAsync(StripeEventAccountingRequest request, CancellationToken cancellationToken);
    
    Task<bool> ExecuteArtistACHPayoutAsync(int orderId);
}
