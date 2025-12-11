namespace YoloBroker.AzureVault.Test;

public record AzureVaultConfig
{
    public required string VaultUri { get; init; }
    public required string KeyName { get; init; }
    public required string ExpectedAddress { get; init; }
}