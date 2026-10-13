namespace TAGWEBAPI.Integrations.ModernTreasury;

public sealed class ModernTreasuryOptions
{
    public const string SectionName = "ModernTreasury";

    public bool Enabled { get; set; }

    public bool DryRun { get; set; } = true;

    public string BaseUrl { get; set; } = "https://app.moderntreasury.com";

    public string OrganizationId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string LedgerId { get; set; } = string.Empty;

    public Dictionary<string, string> AccountMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
