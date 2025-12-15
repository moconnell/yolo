# Hyperliquid Wallet Architecture

## Overview

Hyperliquid uses a two-tier wallet structure for security:

1. **Agent/API Wallet** - Signs transactions but holds no funds
2. **Vault Address** - Actual funded account (hardware wallet for production)

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Production Setup                                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Hardware Wallet (0xVault...)                                   │
│  └─ Holds all funds                                             │
│  └─ Distributed to sub-accounts via Hyperliquid web UI          │
│     └─ Sub-account 1 (Strategy: YOLO Daily).                    │
│     └─ Sub-account 2 (Strategy: Unravel Daily)                  │
│                                                                 │
│  Agent Wallet (0xAgent...)                                      │
│  └─ No funds deposited                                          │
│  └─ Used only for API authentication & transaction signing      │
│  └─ References vault address when trading                       │ 
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Configuration

### Azure Key Vault Secrets

For each environment (dev/prod), store:

**Agent Wallet (one per environment):**

```bash
# Development
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-agent-address" --value "0x..."
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-agent-privatekey" --value "0x..."

# Production
az keyvault secret set --vault-name YOLO --name "hyperliquid-prod-agent-address" --value "0x..."
az keyvault secret set --vault-name YOLO --name "hyperliquid-prod-agent-privatekey" --value "0x..."
```

**Vault Addresses (one per strategy per environment):**

```bash
# Development - Strategy: YOLO daily
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-vault-yolodaily" --value "0x..."

# Development - Strategy: Unravel daily
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-vault-unraveldaily" --value "0x..."

# Production - Strategy: YOLO daily
az keyvault secret set --vault-name YOLO --name "hyperliquid-prod-vault-yolodaily" --value "0x..."

# Production - Strategy: Unravel daily
az keyvault secret set --vault-name YOLO --name "hyperliquid-prod-vault-unraveldaily" --value "0x..."
```

### Azure Function App Settings

Reference these secrets in your Function App configuration:

```bash
az functionapp config appsettings set \
  --name yolo-funk-prod \
  --resource-group ResourceGroup1 \
  --settings \
    "Strategies__YoloDaily__Hyperliquid__Address=@Microsoft.KeyVault(VaultName=YOLO;SecretName=hyperliquid-prod-agent-address)" \
    "Strategies__YoloDaily__Hyperliquid__PrivateKey=@Microsoft.KeyVault(VaultName=YOLO;SecretName=hyperliquid-prod-agent-privatekey)" \
    "Strategies__YoloDaily__Hyperliquid__VaultAddress=@Microsoft.KeyVault(VaultName=YOLO;SecretName=hyperliquid-prod-vault-yolodaily)" \
    "Strategies__YoloDaily__RobotWealth__ApiKey=@Microsoft.KeyVault(VaultName=YOLO;SecretName=robotwealth-api-key)"
```

### Code Usage

The HyperliquidConfig needs to include the vault address:

```csharp
public class HyperliquidConfig
{
    public string Address { get; set; }        // Agent wallet address (for signing)
    public string PrivateKey { get; set; }     // Agent wallet private key (for signing)
    public string? VaultAddress { get; set; }  // Actual funded vault address
}
```

When placing orders, use the `vaultAddress` parameter:

```csharp
var client = new HyperLiquidRestClient(options => {
    options.ApiCredentials = new ApiCredentials(
        hyperliquidConfig.Address,      // Agent wallet (signs txs)
        hyperliquidConfig.PrivateKey    // Agent wallet key
    );
});

// When trading, reference the vault
var order = new OrderRequest {
    Asset = "BTC-PERP",
    IsBuy = true,
    Quantity = 0.1m,
    Price = 50000m,
    VaultAddress = hyperliquidConfig.VaultAddress  // The funded account
};
```

## Security Benefits

✅ **Agent wallet exposed to API** - Can be rotated without moving funds  
✅ **Vault wallet never exposed** - Funds stay safe in hardware wallet  
✅ **Sub-account isolation** - Different strategies can't affect each other's positions  
✅ **Easy credential rotation** - Generate new agent wallet, update Key Vault, restart Function App

## Setup Steps

### 1. Create Agent Wallet (Per Environment)

```bash
# Generate new agent wallet for dev
./generate-eth-keypair.py

# Store in Key Vault
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-agent-address" --value "0xGenerated..."
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-agent-privatekey" --value "0xGenerated..."
```

### 2. Create Hardware Wallet (Production Only)

- Use Ledger, Trezor, or similar hardware wallet
- **Never** expose the private key
- Fund this wallet with your trading capital

### 3. Create Sub-Accounts (Via Hyperliquid Web UI)

1. Connect hardware wallet to https://app.hyperliquid.xyz
2. Navigate to "Vaults" or "Sub-Accounts"
3. Create sub-account for each strategy (e.g., "Momentum Daily")
4. Allocate funds to each sub-account
5. Copy the sub-account address

### 4. Store Vault Addresses

```bash
# For each strategy
az keyvault secret set \
  --vault-name YOLO \
  --name "hyperliquid-prod-vault-yolodaily" \
  --value "0xSubAccountAddress..."
```

### 5. Authorize Agent Wallet

In Hyperliquid web UI:

1. Go to Settings → API
2. Add agent wallet address as authorized API key
3. Grant permissions: "Place Orders", "Cancel Orders", "View Positions"

## Development vs Production

|                  | Development (Testnet)               | Production (Mainnet)            |
| ---------------- | ----------------------------------- | ------------------------------- |
| **Agent Wallet** | Generated locally                   | Generated locally               |
| **Vault Wallet** | Test wallet (can be regular wallet) | Hardware wallet (Ledger/Trezor) |
| **Funds**        | Testnet tokens (free)               | Real money                      |
| **Sub-accounts** | Optional                            | Recommended per strategy        |

## Automated Setup

Run the setup script - it will prompt for all required credentials:

```bash
./setup-azure.sh
```

The script will:

- Create agent wallet secrets (one per environment)
- Prompt for vault addresses per strategy
- Store everything in Azure Key Vault
- Generate Azure CLI commands for Function App configuration

## Manual Commands

List all secrets:

```bash
az keyvault secret list --vault-name YOLO --query "[].name" -o table
```

View a secret value:

```bash
az keyvault secret show --vault-name YOLO --name "hyperliquid-prod-vault-yolodaily" --query "value" -o tsv
```

Update a secret:

```bash
az keyvault secret set --vault-name YOLO --name "secret-name" --value "new-value"
```
