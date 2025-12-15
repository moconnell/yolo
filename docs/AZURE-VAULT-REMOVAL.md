# YoloBroker.AzureVault Removal

## Summary

The `YoloBroker.AzureVault` project and its test project have been removed from the solution. This custom Azure Key Vault signing implementation is no longer needed because:

1. **Azure Functions has native Key Vault integration** via the `@Microsoft.KeyVault(...)` reference syntax
2. **Per-strategy wallets** are now configured directly through application settings
3. **Simpler architecture** - no custom signing infrastructure needed

## What Was Removed

### Projects

- `src/YoloBroker.AzureVault/` - Custom Azure Key Vault signer implementation
- `test/YoloBroker.AzureVault.Test/` - Associated tests

### Code Changes

- **`Yolo.slnx`** - Removed project references
- **`src/YoloKonsole/YoloKonsole.csproj`** - Removed project reference
- **`src/YoloKonsole/Extensions/BrokerServiceCollectionExtensions.cs`**:
  - Removed `using YoloBroker.AzureVault.Extensions;`
  - Removed `using Nethereum.Util;`
  - Removed custom signer configuration: `hyperliquidBroker.ConfigureAzureKeyVaultSigner(config);`
  - Simplified broker registration to use standard HyperLiquid client

## Replacement Approach

### Before (Custom Azure Key Vault Signing)

```csharp
services.AddSingleton<IYoloBroker>(serviceProvider =>
{
    var broker = new HyperliquidBroker(...);
    broker.ConfigureAzureKeyVaultSigner(config);
    return broker;
});
```

Configuration required:

```json
{
  "AzureVault": {
    "VaultUri": "https://my-vault.vault.azure.net/",
    "KeyName": "my-signing-key"
  }
}
```

### After (Native Azure Functions Integration)

```csharp
services.AddHyperLiquid(options =>
{
    options.ApiCredentials = new ApiCredentials(
        hyperliquidConfig.Address,
        hyperliquidConfig.PrivateKey
    );
});
```

Configuration (secrets stored in Key Vault, referenced in App Settings):

```json
{
  "Strategies__YoloDaily__Hyperliquid__Address": "@Microsoft.KeyVault(VaultName=my-vault;SecretName=yolo-address)",
  "Strategies__YoloDaily__Hyperliquid__PrivateKey": "@Microsoft.KeyVault(VaultName=my-vault;SecretName=yolo-key)"
}
```

## Benefits

1. **Less code to maintain** - No custom signing implementation
2. **Native Azure integration** - Uses built-in Function App identity and Key Vault references
3. **Simpler configuration** - Direct secret references in app settings
4. **Better security** - Managed identities, no keys in config
5. **Per-strategy isolation** - Each strategy can have its own wallet credentials

## Migration Notes

If you were using `YoloBroker.AzureVault`:

1. Store your private keys as secrets in Azure Key Vault
2. Grant your Function App's managed identity access to the vault
3. Reference secrets in Application Settings using `@Microsoft.KeyVault(...)` syntax
4. Remove any `AzureVault` configuration sections

See [DEPLOYMENT.md](DEPLOYMENT.md) for full Azure Key Vault setup instructions.

## Test Results

All 307 tests pass after removal. No functionality was broken.
