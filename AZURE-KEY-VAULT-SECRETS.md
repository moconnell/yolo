# Azure Key Vault Secrets Setup

## Understanding Azure Key Vault: Keys vs Secrets

Azure Key Vault has two main types of objects:

| Type        | Purpose                                                        | Use Case                                                    |
| ----------- | -------------------------------------------------------------- | ----------------------------------------------------------- |
| **Keys**    | Cryptographic keys for signing/encryption **within the vault** | Never expose private key - vault does signing               |
| **Secrets** | Stored values that can be retrieved                            | Store passwords, connection strings, API keys, private keys |

## Your Current Setup

You have an **EC Key** (Elliptic Curve key) in your vault. This is for vault-side cryptographic operations where the private key never leaves the vault.

## What You Need for Azure Functions

For HyperLiquid.Net integration, you need **Secrets** (not Keys) because:

- The HyperLiquid client needs the actual private key value to sign transactions
- Azure Functions retrieves secret values at runtime using Managed Identity
- No code changes needed - Azure handles authentication automatically

## The Idiomatic Pattern

### 1. Store Credentials as Secrets

```bash
# Production (mainnet)
az keyvault secret set \
  --vault-name YOLO \
  --name "hyperliquid-prod-address" \
  --value "0xYourMainnetWalletAddress"

az keyvault secret set \
  --vault-name YOLO \
  --name "hyperliquid-prod-privatekey" \
  --value "YourMainnetPrivateKey"

# Development (testnet)
az keyvault secret set \
  --vault-name YOLO \
  --name "hyperliquid-dev-address" \
  --value "0xYourTestnetWalletAddress"

az keyvault secret set \
  --vault-name YOLO \
  --name "hyperliquid-dev-privatekey" \
  --value "YourTestnetPrivateKey"

# RobotWealth API
az keyvault secret set \
  --vault-name YOLO \
  --name "robotwealth-api-key" \
  --value "YourRobotWealthApiKey"
```

### 2. Function App Configuration References Vault

When configuring your Function App, use the special `@Microsoft.KeyVault(...)` syntax:

```bash
az functionapp config appsettings set \
  --name yolo-funk-prod \
  --resource-group ResourceGroup1 \
  --settings \
    "Strategies__MomentumDaily__Hyperliquid__Address=@Microsoft.KeyVault(VaultName=YOLO;SecretName=hyperliquid-prod-address)" \
    "Strategies__MomentumDaily__Hyperliquid__PrivateKey=@Microsoft.KeyVault(VaultName=YOLO;SecretName=hyperliquid-prod-privatekey)" \
    "Strategies__MomentumDaily__RobotWealth__ApiKey=@Microsoft.KeyVault(VaultName=YOLO;SecretName=robotwealth-api-key)"
```

### 3. How It Works at Runtime

```
┌─────────────────────────────────────────────────────────────┐
│ Azure Functions Startup                                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Load configuration from Application Settings            │
│     Hyperliquid__PrivateKey = "@Microsoft.KeyVault(...)"    │
│                                                             │
│  2. Azure detects Key Vault reference syntax                │
│                                                             │
│  3. Function App Managed Identity authenticates to vault    │
│     (no code, no credentials needed - automatic!)           │
│                                                             │
│  4. Azure retrieves actual secret value from vault          │
│                                                             │
│  5. Configuration system replaces reference with value      │
│     Hyperliquid__PrivateKey = "actual-private-key-here"     │
│                                                             │
│  6. Your code sees the actual value                         │
│     var key = config["Hyperliquid:PrivateKey"];             │
│     // Returns: "actual-private-key-here"                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Your code doesn't change at all!**

```csharp
// In AddStrategyServices.cs - this just works
var hyperliquidConfig = strategySection
    .GetSection("Hyperliquid")
    .Get<HyperliquidConfig>();

// Azure has already resolved the Key Vault reference
// hyperliquidConfig.PrivateKey contains the actual private key
var client = new HyperLiquidRestClient(options => {
    options.ApiCredentials = new ApiCredentials(
        hyperliquidConfig.Address,      // From Key Vault
        hyperliquidConfig.PrivateKey    // From Key Vault
    );
});
```

## Security Benefits

✅ **Private keys never in source code**  
✅ **Private keys never in config files**  
✅ **Private keys never in environment variables**  
✅ **No authentication code needed** - Managed Identity handles it  
✅ **Automatic credential rotation** - update vault, restart function app  
✅ **Audit trail** - Key Vault logs every access  
✅ **Access control** - Only specific Function Apps can access specific secrets

## Migration from EC Key

If you want to use your existing EC key for signing (more secure - key never leaves vault):

1. **Would require custom implementation** (the deleted `YoloBroker.AzureVault` project)
2. **More complex** - need Azure Key Vault SDK integration
3. **HyperLiquid.Net compatibility** - would need custom signer implementation

**Recommendation**: Use Secrets for simplicity. The security benefit of vault-side signing is minimal for your use case since:

- Function App has Managed Identity (already secure)
- Key Vault access is logged and auditable
- Secrets are encrypted at rest and in transit
- Only your Function App can access the secrets

## Quick Start

Run the setup script - it will check existing secrets and only add missing ones:

```bash
./setup-azure.sh
```

Or manually add secrets:

```bash
# List existing secrets
az keyvault secret list --vault-name YOLO --query "[].name"

# Add new secret
az keyvault secret set --vault-name YOLO --name "secret-name" --value "secret-value"

# View secret (requires appropriate permissions)
az keyvault secret show --vault-name YOLO --name "secret-name" --query "value"
```
