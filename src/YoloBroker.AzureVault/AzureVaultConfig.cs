namespace YoloBroker.AzureVault;

public record AzureVaultConfig
{
    public required string VaultUri { get; init; }
    public required string KeyName { get; init; }
    public string? ExpectedAddress { get; init; }
}