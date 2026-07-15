@description('Globally unique Key Vault name for this environment')
param keyVaultName string

@description('Environment name (dev or prod)')
@allowed([
  'dev'
  'prod'
])
param environmentName string

@description('Location for the Key Vault')
param location string = resourceGroup().location

@description('Tags to apply to the Key Vault')
param tags object = {}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: union(tags, {
    Environment: environmentName
    ManagedBy: 'GitHub'
  })
  properties: {
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    // GitHub-hosted runners and operator workstations do not have stable egress IPs.
    // Keep the data-plane endpoint public; Entra authentication and vault-scoped RBAC
    // remain mandatory for both deployments and manual secret maintenance.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Allow'
      ipRules: []
      virtualNetworkRules: []
    }
    sku: {
      family: 'A'
      name: 'standard'
    }
  }
}

output keyVaultId string = keyVault.id
output keyVaultUri string = keyVault.properties.vaultUri
