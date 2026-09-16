#!/usr/bin/env bash

set -euo pipefail

usage() {
  echo "Usage: $0 <dev|prod> <resource-group> <key-vault-name> [location]"
  echo "Creates the non-secret Azure foundation for exactly one environment."
}

if [[ $# -lt 3 || $# -gt 4 ]]; then
  usage
  exit 1
fi

ENVIRONMENT="$1"
RESOURCE_GROUP="$2"
KEYVAULT_NAME="$3"
LOCATION="${4:-switzerlandnorth}"

if [[ "$ENVIRONMENT" != "dev" && "$ENVIRONMENT" != "prod" ]]; then
  usage
  exit 1
fi

command -v az >/dev/null 2>&1 || {
  echo "Azure CLI is required: https://aka.ms/InstallAzureCLI"
  exit 1
}

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/.." && pwd)

az account show >/dev/null 2>&1 || az login

echo "Provisioning $ENVIRONMENT foundation in $RESOURCE_GROUP ($LOCATION)"
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --tags Environment="$ENVIRONMENT" ManagedBy=GitHub

az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/.azure/key-vault.bicep" \
  --parameters \
    keyVaultName="$KEYVAULT_NAME" \
    environmentName="$ENVIRONMENT" \
    location="$LOCATION"

az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/.azure/app-insights.bicep" \
  --parameters environmentName="$ENVIRONMENT" location="$LOCATION"

echo
echo "Foundation created. Populate $KEYVAULT_NAME using:"
echo "  docs/AZURE-KEY-VAULT-SECRETS-SETUP.md"
echo
echo "Configure the GitHub $([[ "$ENVIRONMENT" == "prod" ]] && echo production || echo development) environment with:"
echo "  AZURE_RESOURCE_GROUP=$RESOURCE_GROUP"
echo "  AZURE_LOCATION=$LOCATION"
echo "  AZURE_KEYVAULT_NAME=$KEYVAULT_NAME"
echo "and an environment-scoped AZURE_CREDENTIALS secret."
