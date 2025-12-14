# Deployment Guide

This document describes how to deploy the YOLO trading application to Azure Functions using automated CI/CD pipelines.

## Table of Contents

1. [Overview](#overview)
2. [Environments](#environments)
3. [Prerequisites](#prerequisites)
4. [Initial Setup](#initial-setup)
5. [Configuration](#configuration)
6. [Deployment Process](#deployment-process)
7. [Monitoring](#monitoring)

## Overview

The YOLO trading application uses a GitOps approach with automated deployments:

- **Feature branches / PRs** → Ephemeral test environments (auto-created, auto-deleted)
- **`develop` branch** → Development environment (Hyperliquid testnet)
- **`master` branch** → Production environment (Hyperliquid mainnet, requires approval)

All infrastructure is provisioned automatically using Azure Bicep templates.

## Environments

| Environment  | Branch            | Azure Function App      | Hyperliquid Network | Auto-Deploy            | Auto-Cleanup   |
| ------------ | ----------------- | ----------------------- | ------------------- | ---------------------- | -------------- |
| PR / Feature | `feature/*` or PR | `yolo-funk-pr-{number}` | testnet             | ✅                     | ✅ on PR close |
| Development  | `develop`         | `yolo-funk-dev`         | testnet             | ✅                     | ❌             |
| Production   | `master`          | `yolo-funk-prod`        | mainnet             | ✅ (approval required) | ❌             |

## Prerequisites

- Azure subscription
- Azure CLI installed locally
- GitHub repository with Actions enabled
- .NET 10.0 SDK

## Initial Setup

### 1. Create Azure Service Principal

Create a service principal for GitHub Actions to authenticate with Azure:

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "Your Subscription Name"

# Create service principal with contributor role
az ad sp create-for-rbac \
  --name "github-yolo-funk" \
  --role contributor \
  --scopes /subscriptions/{subscription-id} \
  --sdk-auth

# Output will be JSON - copy this entire output
```

### 2. Configure GitHub Secrets

Add the following secrets to your GitHub repository (`Settings` → `Secrets and variables` → `Actions`):

| Secret Name         | Value                       | Description                                      |
| ------------------- | --------------------------- | ------------------------------------------------ |
| `AZURE_CREDENTIALS` | JSON from service principal | Full JSON output from `az ad sp create-for-rbac` |

### 3. Configure GitHub Variables (optional)

Add these variables if using Azure Key Vault:

| Variable Name         | Value              | Description               |
| --------------------- | ------------------ | ------------------------- |
| `AZURE_KEYVAULT_NAME` | e.g., `yolo-vault` | Your Azure Key Vault name |

### 4. Create Azure Key Vault (optional but recommended)

```bash
# Create Key Vault
az keyvault create \
  --name yolo-vault \
  --resource-group rg-yolo-funk \
  --location australiaeast

# Add secrets for development environment
az keyvault secret set \
  --vault-name yolo-vault \
  --name "yolo-dev-hyperliquid-address" \
  --value "0xYourTestnetAddress"

az keyvault secret set \
  --vault-name yolo-vault \
  --name "yolo-dev-hyperliquid-privatekey" \
  --value "YourTestnetPrivateKey"

# Add secrets for production environment
az keyvault secret set \
  --vault-name yolo-vault \
  --name "yolo-prod-hyperliquid-address" \
  --value "0xYourMainnetAddress"

az keyvault secret set \
  --vault-name yolo-vault \
  --name "yolo-prod-hyperliquid-privatekey" \
  --value "YourMainnetPrivateKey"
```

### 5. Create GitHub Environments (for manual approvals)

1. Go to `Settings` → `Environments` → `New environment`
2. Create environment named `production`
3. Enable "Required reviewers" and add yourself
4. (Optional) Create `development` environment without restrictions

### 6. Create `develop` Branch

```bash
# Create and push develop branch
git checkout -b develop
git push -u origin develop
```

## Configuration

### Application Settings

After first deployment, configure each Function App with application settings. Settings can be added via:

1. **Azure Portal**: Function App → Configuration → Application settings
2. **Azure CLI**: See examples below

#### Development Environment Settings

```bash
FUNCTION_APP="yolo-funk-dev"

# Strategy configurations
az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group rg-yolo-funk \
  --settings \
    "Strategies__MomentumDaily__Yolo__MaxLeverage=1.0" \
    "Strategies__MomentumDaily__Yolo__MaxNumAssets=10" \
    "Strategies__MomentumDaily__Hyperliquid__Address=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-dev-hyperliquid-address)" \
    "Strategies__MomentumDaily__Hyperliquid__PrivateKey=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-dev-hyperliquid-privatekey)" \
    "Strategies__MomentumDaily__RobotWealth__ApiKey=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=robotwealth-api-key)" \
    "Strategies__MomentumDaily__Schedule=0 0 2 * * *"
```

#### Production Environment Settings

```bash
FUNCTION_APP="yolo-funk-prod"

az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group rg-yolo-funk \
  --settings \
    "Strategies__MomentumDaily__Yolo__MaxLeverage=3.0" \
    "Strategies__MomentumDaily__Yolo__MaxNumAssets=20" \
    "Strategies__MomentumDaily__Hyperliquid__Address=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-prod-hyperliquid-address)" \
    "Strategies__MomentumDaily__Hyperliquid__PrivateKey=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-prod-hyperliquid-privatekey)" \
    "Strategies__MomentumDaily__RobotWealth__ApiKey=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=robotwealth-api-key)" \
    "Strategies__MomentumDaily__Schedule=0 0 1 * * *"
```

### Local Development

Create `src/YoloFunk/local.settings.json` (git-ignored):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Strategies__MomentumDaily__Yolo__MaxLeverage": "1.0",
    "Strategies__MomentumDaily__Yolo__MaxNumAssets": "5",
    "Strategies__MomentumDaily__Hyperliquid__Address": "0xYourTestnetAddress",
    "Strategies__MomentumDaily__Hyperliquid__PrivateKey": "YourTestnetPrivateKey",
    "Strategies__MomentumDaily__RobotWealth__ApiKey": "YourApiKey",
    "Strategies__MomentumDaily__Schedule": "0 */5 * * * *"
  }
}
```

## Deployment Process

### Automatic Deployments

#### Feature Branch / Pull Request

1. Create feature branch: `git checkout -b feature/my-new-feature`
2. Make changes and push: `git push -u origin feature/my-new-feature`
3. Create pull request on GitHub
4. **Automatic**: Infrastructure provisioned and code deployed to `yolo-funk-pr-{number}`
5. PR receives comment with deployment URL
6. Test your changes in isolated environment
7. When PR is closed/merged: **Automatic cleanup** deletes all resources

#### Development Environment

1. Merge to `develop` branch
2. **Automatic**: Deploys to `yolo-funk-dev` (testnet)
3. No approval required

#### Production Environment

1. Merge to `master` branch
2. **Automatic**: Starts deployment workflow
3. **Manual approval required** (via GitHub Environment protection)
4. After approval: Deploys to `yolo-funk-prod` (mainnet)

### Manual Deployments

Trigger deployment manually via GitHub Actions:

1. Go to `Actions` → `Deploy to Azure Functions` → `Run workflow`
2. Select environment: `dev` or `prod`
3. Click `Run workflow`

### Manual Cleanup

To cleanup a specific environment:

1. Go to `Actions` → `Cleanup Azure Functions` → `Run workflow`
2. Enter environment name (e.g., `pr-123`, `feat-my-feature`)
3. Click `Run workflow`

Note: Production (`prod`) cannot be cleaned up via automation for safety.

## Monitoring

### View Logs

#### Azure Portal

1. Navigate to Function App
2. Go to `Log stream` or `Monitoring` → `Logs`
3. Query Application Insights

#### Azure CLI

```bash
# Get recent logs
az monitor app-insights query \
  --app yolo-funk-insights \
  --resource-group rg-yolo-funk \
  --analytics-query "traces | where timestamp > ago(1h) | order by timestamp desc | take 100"

# Get errors
az monitor app-insights query \
  --app yolo-funk-insights \
  --resource-group rg-yolo-funk \
  --analytics-query "exceptions | where timestamp > ago(24h) | order by timestamp desc"
```

### Metrics

View metrics in Azure Portal → Application Insights → `yolo-funk-insights`:

- Request rates
- Failure rates
- Response times
- Custom events from your trading strategies

### Costs

Monitor costs via Azure Portal → Cost Management:

- Consumption Plan: Pay per execution (~$0.20 per million executions)
- Storage: ~$0.02 per GB per month
- Application Insights: First 5GB/month free

Ephemeral PR environments are automatically cleaned up to minimize costs.

## Troubleshooting

### Deployment Fails

1. Check GitHub Actions logs for detailed error messages
2. Verify Azure credentials are valid: `az login` and test commands
3. Ensure Resource Group exists: `az group show --name rg-yolo-funk`

### Function App Not Starting

1. Check Application Insights logs for errors
2. Verify configuration settings (especially Key Vault references)
3. Check that managed identity has Key Vault access:
   ```bash
   az functionapp identity show --name yolo-funk-dev --resource-group rg-yolo-funk
   az keyvault show --name yolo-vault --query properties.accessPolicies
   ```

### Key Vault Access Denied

Grant Function App managed identity access:

```bash
# Get Function App principal ID
PRINCIPAL_ID=$(az functionapp identity show \
  --name yolo-funk-dev \
  --resource-group rg-yolo-funk \
  --query principalId \
  --output tsv)

# Grant access
az keyvault set-policy \
  --name yolo-vault \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

## Additional Resources

- [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/)
- [Azure Bicep Documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)
- [GitHub Actions Documentation](https://docs.github.com/actions)
