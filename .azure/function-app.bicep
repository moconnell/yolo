@description('Environment name (dev, prod, or pr-{number})')
param environmentName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Hyperliquid network (mainnet or testnet)')
@allowed([
  'mainnet'
  'testnet'
])
param hyperliquidNetwork string = 'testnet'

@description('Key Vault name for secrets (optional)')
param keyVaultName string = ''

@description('Tags to apply to all resources')
param tags object = {}

var functionAppName = 'yolo-funk-${environmentName}'
var storageAccountName = 'yolofunk${uniqueString(resourceGroup().id, environmentName)}'
var appInsightsName = 'yolo-funk-insights'

// Determine secret suffix based on network (testnet or mainnet)
var secretEnv = hyperliquidNetwork == 'mainnet' ? 'prod' : 'dev'
var useTestnet = hyperliquidNetwork == 'mainnet' ? 'false' : 'true'

// Key Vault reference helper
var keyVaultId = !empty(keyVaultName) ? resourceId('Microsoft.KeyVault/vaults', keyVaultName) : ''
var keyVaultUri = !empty(keyVaultName) ? 'https://${keyVaultName}.vault.azure.net' : ''

// Storage Account (required for Azure Functions)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Application Insights (shared across environments for cost savings)
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// Consumption Plan (pay-per-execution)
resource hostingPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${functionAppName}-plan'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'Environment'
          value: environmentName
        }
        {
          name: 'HyperliquidNetwork'
          value: hyperliquidNetwork
        }
        // Hyperliquid configuration from Key Vault (for both strategies)
        {
          name: 'Strategies__YoloDaily__Hyperliquid__Address'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/hyperliquid-${secretEnv}-agent-address/)'
            : ''
        }
        {
          name: 'Strategies__YoloDaily__Hyperliquid__PrivateKey'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/hyperliquid-${secretEnv}-privatekey/)'
            : ''
        }
        {
          name: 'Strategies__YoloDaily__Hyperliquid__VaultAddress'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/hyperliquid-${secretEnv}-vault-yolodaily/)'
            : ''
        }
        {
          name: 'Strategies__YoloDaily__Hyperliquid__UseTestnet'
          value: useTestnet
        }
        {
          name: 'Strategies__UnravelDaily__Hyperliquid__Address'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/hyperliquid-${secretEnv}-agent-address/)'
            : ''
        }
        {
          name: 'Strategies__UnravelDaily__Hyperliquid__PrivateKey'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/hyperliquid-${secretEnv}-privatekey/)'
            : ''
        }
        {
          name: 'Strategies__UnravelDaily__Hyperliquid__VaultAddress'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/hyperliquid-${secretEnv}-vault-unraveldaily/)'
            : ''
        }
        {
          name: 'Strategies__UnravelDaily__Hyperliquid__UseTestnet'
          value: useTestnet
        }
        // API Keys from Key Vault
        {
          name: 'Strategies__YoloDaily__RobotWealth__ApiKey'
          value: !empty(keyVaultName)
            ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/robotwealth-api-key/)'
            : ''
        }
        {
          name: 'Strategies__UnravelDaily__Unravel__ApiKey'
          value: !empty(keyVaultName) ? '@Microsoft.KeyVault(SecretUri=${keyVaultUri}/secrets/unravel-api-key/)' : ''
        }
      ]
      netFrameworkVersion: 'v10.0'
      use32BitWorkerProcess: false
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
    }
    httpsOnly: true
  }
}

output functionAppName string = functionApp.name
output functionAppId string = functionApp.id
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output principalId string = functionApp.identity.principalId
