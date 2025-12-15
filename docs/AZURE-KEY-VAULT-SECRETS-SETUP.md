# Azure Key Vault Secrets Setup

The Azure Functions deployment requires secrets to be stored in Azure Key Vault. The secrets are environment-specific (dev vs prod) and are automatically referenced by the function app based on the deployment environment.

## Required Secrets

### Hyperliquid Agent Wallet Secrets (Development/Testnet)

Used for dev, PR, and feature branch deployments. The agent wallet provides API credentials for signing transactions:

```bash
# Development agent wallet address
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "hyperliquid-dev-agent-address" \
  --value "<dev-agent-wallet-address>"

# Development agent private key
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "hyperliquid-dev-agent-privatekey" \
  --value "<dev-agent-private-key>"
```

### Hyperliquid Agent Wallet Secrets (Production/Mainnet)

Used for production deployments. The agent wallet provides API credentials for signing transactions:

```bash
# Production agent wallet address
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "hyperliquid-prod-agent-address" \
  --value "<prod-agent-wallet-address>"

# Production agent private key
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "hyperliquid-prod-agent-privatekey" \
  --value "<prod-agent-private-key>"
```

### Hyperliquid Vault Addresses (Strategy-Specific)

Each strategy requires a vault address (the funded account where trades are executed). Replace `{strategy}` with your strategy name in lowercase (e.g., `yolodaily`, `unraveldaily`):

```bash
# Development vault address for a specific strategy
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "hyperliquid-dev-vault-{strategy}" \
  --value "<dev-vault-address>"

# Production vault address for a specific strategy
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "hyperliquid-prod-vault-{strategy}" \
  --value "<prod-vault-address>"
```

Example for specific strategies:

```bash
# YoloDaily strategy vaults
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-vault-yolodaily" --value "<address>"
az keyvault secret set --vault-name YOLO --name "hyperliquid-prod-vault-yolodaily" --value "<address>"

# UnravelDaily strategy vaults
az keyvault secret set --vault-name YOLO --name "hyperliquid-dev-vault-unraveldaily" --value "<address>"
az keyvault secret set --vault-name YOLO --name "hyperliquid-prod-vault-unraveldaily" --value "<address>"
```

### API Keys

These are shared across all environments:

```bash
# RobotWealth API Key
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "robotwealth-api-key" \
  --value "<robotwealth-api-key>"

# Unravel API Key
az keyvault secret set \
  --vault-name <your-keyvault-name> \
  --name "unravel-api-key" \
  --value "<unravel-api-key>"
```

## Secret Naming Convention

Secrets use lowercase with hyphens as separators:

- `hyperliquid-dev-agent-address` → Development agent wallet address
- `hyperliquid-prod-agent-address` → Production agent wallet address
- `hyperliquid-dev-vault-{strategy}` → Development vault for specific strategy
- `hyperliquid-prod-vault-{strategy}` → Production vault for specific strategy
- `robotwealth-api-key` → RobotWealth API key (shared)
- `unravel-api-key` → Unravel API key (shared)

## Hyperliquid Wallet Architecture

Hyperliquid uses a two-wallet system:

1. **Agent Wallet**: API credentials (address + private key) used for signing transactions
   - Stored as: `hyperliquid-{env}-agent-address` and `hyperliquid-{env}-agent-privatekey`
2. **Vault Address**: The actual funded account where your capital is deposited
   - Stored as: `hyperliquid-{env}-vault-{strategy}`
   - Strategy-specific (e.g., different vaults for yolodaily vs unraveldaily)

Both are required for each environment.

## Environment-Based Secret Selection

The deployment automatically selects the correct secrets based on the `hyperliquidNetwork` parameter:

- **Development/Testnet**: `dev`, `pr-*`, `feat-*` → uses `hyperliquid-dev-*` secrets
- **Production/Mainnet**: `prod`, `master` branch → uses `hyperliquid-prod-*` secrets

The Bicep template maps `hyperliquidNetwork` to secret environment:

- `testnet` → `dev` secrets
- `mainnet` → `prod` secrets

## Automated Setup

The easiest way to configure all secrets is to use the setup script:

```bash
./scripts/setup-azure.sh
```

This script will:

- Check which secrets already exist
- Prompt for missing secrets only
- Support multiple strategies per environment
- Save credentials securely to Key Vault

## Granting Access

The GitHub Actions workflow automatically grants the function app's managed identity access to the Key Vault. If this fails, you can manually grant access:

```bash
# Get the function app's managed identity principal ID
PRINCIPAL_ID=$(az functionapp show \
  --resource-group ResourceGroup1 \
  --name <function-app-name> \
  --query identity.principalId -o tsv)

# Grant Key Vault Secrets User role
az role assignment create \
  --assignee-object-id $PRINCIPAL_ID \
  --assignee-principal-type ServicePrincipal \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/<subscription-id>/resourceGroups/ResourceGroup1/providers/Microsoft.KeyVault/vaults/<keyvault-name>
```

## Verification

After deploying, verify the secrets are accessible:

1. Check the function app configuration in Azure Portal
2. Look for app settings starting with `Strategies__` that have values like `@Microsoft.KeyVault(...)`
3. Check the function app logs for any Key Vault access errors

## Troubleshooting

**Function app fails to start with "No script host available":**

- Verify all required secrets exist in Key Vault
- Check the function app's managed identity has "Key Vault Secrets User" role
- Review Application Insights or function app logs for detailed error messages

**404 when calling function endpoints:**

- Function app may be failing to start due to missing secrets
- Check `LogFiles/Application/Functions/Host/*.log` in the Kudu console

**Secret reference not resolving:**

- Verify secret name matches exactly (case-sensitive, use hyphens not underscores)
- Ensure Key Vault URI format: `@Microsoft.KeyVault(SecretUri=https://{vault}.vault.azure.net/secrets/{secret}/)`
- Check managed identity has appropriate permissions
