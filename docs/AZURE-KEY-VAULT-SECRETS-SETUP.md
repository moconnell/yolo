# Azure Key Vault Secrets Setup

Development and production have separate Key Vaults. Both vaults use the same secret names; the selected GitHub environment determines which vault is referenced.

The vault data-plane endpoint intentionally remains publicly reachable because GitHub-hosted runners and operator workstations do not have stable egress IP addresses. Public reachability does not grant access: Entra authentication and vault-scoped Azure RBAC are required. If private endpoints or static-egress runners are introduced later, change `publicNetworkAccess` and `networkAcls` together and provide operators an approved private access path before disabling public access.

## Required secrets

Populate each vault with:

| Secret | Purpose |
| --- | --- |
| `hyperliquid-agent-address` | Hyperliquid signing-agent address |
| `hyperliquid-agent-privatekey` | Hyperliquid signing-agent private key |
| `hyperliquid-vault-yolodaily` | Funded account for YoloDaily |
| `hyperliquid-vault-unraveldaily` | Funded account for UnravelDaily |
| `robotwealth-api-key` | RobotWealth API key used by that environment |
| `unravel-api-key` | Unravel API key used by that environment |

The development vault must contain testnet credentials only. Never copy a mainnet private key or funded mainnet vault address into development.

Example, repeated once for each environment-specific vault:

```bash
VAULT_NAME="<environment-key-vault>"

az keyvault secret set --vault-name "$VAULT_NAME" --name hyperliquid-agent-address --value "<address>"
az keyvault secret set --vault-name "$VAULT_NAME" --name hyperliquid-agent-privatekey --value "<private-key>"
az keyvault secret set --vault-name "$VAULT_NAME" --name hyperliquid-vault-yolodaily --value "<vault-address>"
az keyvault secret set --vault-name "$VAULT_NAME" --name hyperliquid-vault-unraveldaily --value "<vault-address>"
az keyvault secret set --vault-name "$VAULT_NAME" --name robotwealth-api-key --value "<api-key>"
az keyvault secret set --vault-name "$VAULT_NAME" --name unravel-api-key --value "<api-key>"
```

Avoid placing secret values in shell history where practical; the commands above show the required names, not a preferred secret-entry mechanism.

## Access model

Each Function App has a system-assigned managed identity. The deployment workflow grants that identity `Key Vault Secrets User` on its own vault only. No cross-environment role assignments should exist.

Verify isolation after both apps exist:

```bash
DEV_PRINCIPAL=$(az functionapp identity show --resource-group "<dev-rg>" --name yolo-funk-dev --query principalId -o tsv)
PROD_PRINCIPAL=$(az functionapp identity show --resource-group "<prod-rg>" --name yolo-funk-prod --query principalId -o tsv)

az role assignment list --assignee "$DEV_PRINCIPAL" --all -o table
az role assignment list --assignee "$PROD_PRINCIPAL" --all -o table
```

The development principal should list only the development vault assignment, and the production principal only the production vault assignment.

## Migrating the production vault

1. Create or select the dedicated production vault in the production resource group.
2. Copy values from the legacy environment-qualified secrets into the new neutral names.
3. Set the production GitHub environment variable `AZURE_KEYVAULT_NAME` to the dedicated vault.
4. Run the protected production deployment and confirm every Function App Key Vault reference reports a resolved status.
5. Verify application startup and strategy configuration before disabling or deleting legacy secrets.

Do not delete the old vault or remove its role assignments as part of the first migration deployment. Retain it until production has run successfully with the new references.

## Troubleshooting

- A Key Vault reference error usually means a missing secret name or missing `Key Vault Secrets User` assignment.
- A deployment failure while granting access means the deployment identity lacks User Access Administrator or Owner at the vault/resource-group scope.
- A development app configured for mainnet indicates a branch/environment mismatch; the workflow should reject this before Azure login.
