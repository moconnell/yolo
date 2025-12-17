# YoloFunk Azure Functions Deployment Guide

## Local Development Setup

### 1. Create local.settings.json

```bash
cd src/YoloFunk
cp local.settings.json.example local.settings.json
```

Edit `local.settings.json` and fill in your actual values (this file is git-ignored).

### 2. Run Locally

```bash
func start
```

Or use VS Code debugger (F5).

### 3. Test Locally

**Scheduled functions** run on their timer schedules.

**Manual HTTP triggers:**

```bash
curl -X POST http://localhost:7071/api/rebalance/yolodaily
```

---

## Azure Deployment

### Option 1: GitHub Actions (Recommended)

#### Setup Steps

**1. Create Azure Function App**

```bash
# Login to Azure
az login

# Create resource group
az group create --name yolo-rg --location eastus

# Create storage account
az storage account create \
  --name yolofunkstorage \
  --resource-group yolo-rg \
  --location eastus \
  --sku Standard_LRS

# Create Linux Function App with .NET 10 Isolated
az functionapp create \
  --name yolo-funk \
  --resource-group yolo-rg \
  --storage-account yolofunkstorage \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 10 \
  --functions-version 4 \
  --os-type Linux
```

**2. Configure GitHub Secrets**

Get the publish profile:

```bash
az functionapp deployment list-publishing-profiles \
  --name yolo-funk \
  --resource-group yolo-rg \
  --xml
```

Add to GitHub Repository Secrets:

- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` - paste the XML output

**3. Configure Application Settings**

Instead of using `local.settings.json`, set Application Settings in Azure:

```bash
# Example: Configure strategy settings
az functionapp config appsettings set \
  --name yolo-funk-prod \
  --resource-group yolo-rg \
  --settings \
    "Strategies__YoloDaily__Yolo__MaxLeverage=1.5" \
    "Strategies__YoloDaily__Yolo__NotionalCash=25000" \
    "Strategies__YoloDaily__Schedule=0 15 9 * * *" \
    # ... add all other settings
```

**4. Setup Azure Key Vault**

```bash
# Create Key Vault
az keyvault create \
  --name my-vault \
  --resource-group yolo-rg \
  --location eastus

# Enable managed identity for Function App
az functionapp identity assign \
  --name yolo-funk \
  --resource-group yolo-rg

# Grant Function App access to Key Vault
FUNCTION_IDENTITY=$(az functionapp identity show \
  --name yolo-funk \
  --resource-group yolo-rg \
  --query principalId \
  --output tsv)

az keyvault set-policy \
  --name my-vault \
  --object-id $FUNCTION_IDENTITY \
  --secret-permissions get list

# Store secrets
az keyvault secret set \
  --vault-name my-vault \
  --name yolo-daily-address \
  --value "0x..."

az keyvault secret set \
  --vault-name my-vault \
  --name yolo-daily-key \
  --value "0x..."
```

**5. Push to GitHub**

The workflow in `.github/workflows/azure-functions-deploy.yml` will automatically deploy on push to master.

---

### Option 2: Manual Deployment via Azure CLI

```bash
cd src/YoloFunk

# Build and publish
dotnet publish --configuration Release --output ./publish

# Deploy
func azure functionapp publish yolo-funk
```

---

### Option 3: VS Code Azure Functions Extension

1. Install "Azure Functions" extension
2. Sign in to Azure
3. Right-click on `src/YoloFunk` → "Deploy to Function App"
4. Follow prompts

---

## Configuration in Azure

### Application Settings Structure

Azure uses flat key-value pairs with double underscores for hierarchy:

```
Strategies__YoloDaily__Hyperliquid__Address = "0x..."
Strategies__YoloDaily__Yolo__MaxLeverage = "2"
```

This maps to:

```json
{
  "Strategies": {
    "YoloDaily": {
      "Hyperliquid": {
        "Address": "0x..."
      },
      "Yolo": {
        "MaxLeverage": 2
      }
    }
  }
}
```

### Using Azure Key Vault References

Format: `@Microsoft.KeyVault(VaultName=<vault-name>;SecretName=<secret-name>)`

Example:

```
Strategies__YoloDaily__Hyperliquid__PrivateKey = "@Microsoft.KeyVault(VaultName=my-vault;SecretName=yolo-daily-key)"
```

---

## Infrastructure as Code (IaC)

### Bicep Template Example

```bicep
param functionAppName string = 'yolo-funk'
param location string = resourceGroup().location
param keyVaultName string = 'my-vault'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${functionAppName}storage'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${functionAppName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        // Add your strategy configurations here
      ]
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: functionApp.identity.principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
  }
}
```

Deploy:

```bash
az deployment group create \
  --resource-group yolo-rg \
  --template-file infrastructure/main.bicep
```

---

## Monitoring

### View Logs

**Azure Portal:**

- Navigate to Function App → Functions → Select function → Monitor

**Azure CLI:**

```bash
az webapp log tail \
  --name yolo-funk \
  --resource-group yolo-rg
```

**Application Insights:**

- Auto-configured via `host.json`
- View in Azure Portal → Application Insights

---

## CI/CD Best Practices

1. **Use GitHub Environments** for prod/staging separation
2. **Store secrets in GitHub Secrets** or Azure Key Vault
3. **Test before deploy** - run tests in workflow before deployment
4. **Use deployment slots** for blue/green deployments
5. **Monitor deployments** - check Application Insights after deploy

### Complete CI/CD Workflow

See `.github/workflows/azure-functions-deploy.yml` for the automated deployment pipeline that:

- ✅ Builds on every push to master
- ✅ Runs tests before deploy
- ✅ Publishes to Azure Functions
- ✅ Can be manually triggered via `workflow_dispatch`
