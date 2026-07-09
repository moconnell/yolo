#!/bin/bash

# Setup script for Azure Functions deployment
# This script helps you configure all necessary Azure resources and GitHub secrets

set -e

echo "🚀 YOLO Azure Functions Deployment Setup"
echo "========================================"
echo ""

# Check prerequisites
command -v az >/dev/null 2>&1 || { echo "❌ Azure CLI is required but not installed. Visit https://aka.ms/InstallAzureCLI"; exit 1; }
command -v gh >/dev/null 2>&1 || { echo "⚠️  GitHub CLI not installed. You'll need to manually add secrets to GitHub."; GH_INSTALLED=false; }

# Configuration (matching your existing Azure resources)
RESOURCE_GROUP="ResourceGroup1"
LOCATION="switzerlandnorth"  # Azure uses lowercase, no spaces
KEYVAULT_NAME="YOLO"
SP_NAME="github-yolo-funk"

echo "Configuration:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Key Vault: $KEYVAULT_NAME"
echo ""
echo "NOTE: This script will use your existing Resource Group and Key Vault."
echo "      It will only create what doesn't already exist."
echo ""

read -p "Continue with this configuration? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 1
fi

# Login to Azure
echo ""
echo "Step 1: Azure Login"
echo "-------------------"
az login

# Get subscription ID
SUBSCRIPTION_ID=$(az account show --query id --output tsv)
echo "✅ Using subscription: $SUBSCRIPTION_ID"

# Create Resource Group
echo ""
echo "Step 2: Create Resource Group"
echo "-----------------------------"
if az group show --name $RESOURCE_GROUP >/dev/null 2>&1; then
    echo "✅ Resource group $RESOURCE_GROUP already exists"
else
    az group create --name $RESOURCE_GROUP --location $LOCATION
    echo "✅ Created resource group: $RESOURCE_GROUP"
fi

# Create shared Application Insights
echo ""
echo "Step 3: Create Shared Application Insights"
echo "------------------------------------------"
cd "$(dirname "$0")"
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file ../.azure/app-insights.bicep \
  --parameters location=$LOCATION
echo "✅ Application Insights created"

# Create Key Vault
echo ""
echo "Step 4: Create Azure Key Vault"
echo "------------------------------"
if az keyvault show --name $KEYVAULT_NAME >/dev/null 2>&1; then
    echo "✅ Key Vault $KEYVAULT_NAME already exists"
else
    az keyvault create \
      --name $KEYVAULT_NAME \
      --resource-group $RESOURCE_GROUP \
      --location $LOCATION \
      --enable-rbac-authorization false
    echo "✅ Created Key Vault: $KEYVAULT_NAME"
fi

# Add secrets to Key Vault
echo ""
echo "Step 5: Add Secrets to Key Vault"
echo "--------------------------------"
echo "IMPORTANT: Hyperliquid Wallet Architecture:"
echo "  - Agent Wallet: API credentials (address + private key) for signing transactions"
echo "  - Vault Address: The actual funded account where your funds are deposited"
echo ""
echo "You need BOTH for each environment. We'll store these as SECRETS (not Keys)."
echo "We'll check which secrets already exist and only add missing ones."
echo ""

# Helper function to check if secret exists
secret_exists() {
    az keyvault secret show --vault-name $KEYVAULT_NAME --name "$1" >/dev/null 2>&1
}

# Development (testnet) secrets
echo "Development Environment (Testnet):"
echo "-----------------------------------"
if secret_exists "hyperliquid-dev-agent-address" && secret_exists "hyperliquid-dev-agent-privatekey"; then
    echo "✅ Development agent wallet secrets already exist (skipping)"
else
    echo "Agent/API Wallet (for signing transactions):"
    read -p "  Enter agent wallet address: " DEV_AGENT_ADDRESS
    read -sp "  Enter agent wallet private key: " DEV_AGENT_KEY
    echo ""
    
    az keyvault secret set --vault-name $KEYVAULT_NAME --name "hyperliquid-dev-agent-address" --value "$DEV_AGENT_ADDRESS" >/dev/null
    az keyvault secret set --vault-name $KEYVAULT_NAME --name "hyperliquid-dev-agent-privatekey" --value "$DEV_AGENT_KEY" >/dev/null
    echo "✅ Development agent wallet secrets added"
fi

# Strategy-specific vault addresses (dev)
echo ""
echo "Strategy Vault Addresses (the funded accounts to trade with):"
echo "  You can add multiple strategies. Press Enter with empty name when done."
echo ""

STRATEGY_NUM=1
while true; do
    read -p "  Strategy $STRATEGY_NUM name (e.g., 'momentumdaily', or Enter to skip): " STRATEGY_NAME
    
    if [ -z "$STRATEGY_NAME" ]; then
        break
    fi
    
    SECRET_NAME="hyperliquid-dev-vault-${STRATEGY_NAME}"
    
    if secret_exists "$SECRET_NAME"; then
        echo "  ✅ Vault address for $STRATEGY_NAME already exists (skipping)"
    else
        read -p "  Enter vault address for $STRATEGY_NAME: " VAULT_ADDRESS
        az keyvault secret set --vault-name $KEYVAULT_NAME --name "$SECRET_NAME" --value "$VAULT_ADDRESS" >/dev/null
        echo "  ✅ Vault address for $STRATEGY_NAME added"
    fi
    
    STRATEGY_NUM=$((STRATEGY_NUM + 1))
    echo ""
done

# Production (mainnet) secrets
echo ""
echo "Production Environment (Mainnet):"
echo "----------------------------------"
if secret_exists "hyperliquid-prod-agent-address" && secret_exists "hyperliquid-prod-agent-privatekey"; then
    echo "✅ Production agent wallet secrets already exist (skipping)"
else
    echo "Agent/API Wallet (for signing transactions):"
    read -p "  Enter agent wallet address: " PROD_AGENT_ADDRESS
    read -sp "  Enter agent wallet private key: " PROD_AGENT_KEY
    echo ""
    
    az keyvault secret set --vault-name $KEYVAULT_NAME --name "hyperliquid-prod-agent-address" --value "$PROD_AGENT_ADDRESS" >/dev/null
    az keyvault secret set --vault-name $KEYVAULT_NAME --name "hyperliquid-prod-agent-privatekey" --value "$PROD_AGENT_KEY" >/dev/null
    echo "✅ Production agent wallet secrets added"
fi

# Strategy-specific vault addresses (prod)
echo ""
echo "Strategy Vault Addresses (the funded accounts to trade with):"
echo "  You can add multiple strategies. Press Enter with empty name when done."
echo ""

STRATEGY_NUM=1
while true; do
    read -p "  Strategy $STRATEGY_NUM name (e.g., 'momentumdaily', or Enter to skip): " STRATEGY_NAME
    
    if [ -z "$STRATEGY_NAME" ]; then
        break
    fi
    
    SECRET_NAME="hyperliquid-prod-vault-${STRATEGY_NAME}"
    
    if secret_exists "$SECRET_NAME"; then
        echo "  ✅ Vault address for $STRATEGY_NAME already exists (skipping)"
    else
        read -p "  Enter vault address for $STRATEGY_NAME: " VAULT_ADDRESS
        az keyvault secret set --vault-name $KEYVAULT_NAME --name "$SECRET_NAME" --value "$VAULT_ADDRESS" >/dev/null
        echo "  ✅ Vault address for $STRATEGY_NAME added"
    fi
    
    STRATEGY_NUM=$((STRATEGY_NUM + 1))
    echo ""
done

# RobotWealth API key
echo ""
if secret_exists "robotwealth-api-key"; then
    echo "✅ RobotWealth API key already exists (skipping)"
else
    read -sp "Enter RobotWealth API key: " ROBOTWEALTH_KEY
    echo ""
    az keyvault secret set --vault-name $KEYVAULT_NAME --name "robotwealth-api-key" --value "$ROBOTWEALTH_KEY" >/dev/null
    echo "✅ RobotWealth API key added"
fi

# Create Service Principal
echo ""
echo "Step 6: Create Service Principal for GitHub"
echo "-------------------------------------------"
SP_JSON=$(az ad sp create-for-rbac \
  --name $SP_NAME \
  --role contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth)

echo "✅ Service Principal created"

# Allow GitHub Actions to grant and remove Key Vault RBAC assignments for Function App managed identities.
# The workflow itself grants only Key Vault Secrets User to each Function App identity.
KEYVAULT_ID=$(az keyvault show --name $KEYVAULT_NAME --query id --output tsv)
SP_OBJECT_ID=$(az ad sp list --display-name "$SP_NAME" --query "[0].id" --output tsv)

if [ -z "$SP_OBJECT_ID" ]; then
    echo "❌ Could not resolve service principal object id for $SP_NAME"
    exit 1
fi

EXISTING_UAA_ASSIGNMENT=$(az role assignment list \
  --assignee "$SP_OBJECT_ID" \
  --role "User Access Administrator" \
  --scope "$KEYVAULT_ID" \
  --query "[0].id" \
  --output tsv)

if [ -z "$EXISTING_UAA_ASSIGNMENT" ]; then
    az role assignment create \
      --assignee-object-id "$SP_OBJECT_ID" \
      --assignee-principal-type ServicePrincipal \
      --role "User Access Administrator" \
      --scope "$KEYVAULT_ID" \
      >/dev/null
fi

echo "✅ Service Principal can manage Key Vault role assignments"

# Save to file
mkdir -p .github/secrets
echo "$SP_JSON" > .github/secrets/AZURE_CREDENTIALS.json
echo "✅ Saved credentials to .github/secrets/AZURE_CREDENTIALS.json"

# Add to GitHub
echo ""
echo "Step 7: Configure GitHub Secrets"
echo "--------------------------------"

if [ "$GH_INSTALLED" != false ]; then
    echo "Adding secrets to GitHub repository..."
    
    # Check if we're in a GitHub repo
    if gh repo view >/dev/null 2>&1; then
        gh secret set AZURE_CREDENTIALS < .github/secrets/AZURE_CREDENTIALS.json
        echo "✅ AZURE_CREDENTIALS added to GitHub"
        
        gh variable set AZURE_KEYVAULT_NAME --body "$KEYVAULT_NAME"
        echo "✅ AZURE_KEYVAULT_NAME variable added to GitHub"
    else
        echo "⚠️  Not in a GitHub repository. Skipping GitHub secret configuration."
        echo "   You'll need to manually add AZURE_CREDENTIALS to GitHub secrets."
    fi
else
    echo "⚠️  GitHub CLI not installed."
    echo "   Manually add the following secret to your GitHub repository:"
    echo "   Name: AZURE_CREDENTIALS"
    echo "   Value: (contents of .github/secrets/AZURE_CREDENTIALS.json)"
    echo ""
    echo "   Also add this variable:"
    echo "   Name: AZURE_KEYVAULT_NAME"
    echo "   Value: $KEYVAULT_NAME"
fi

# Create GitHub Environments
echo ""
echo "Step 8: Create GitHub Environments"
echo "----------------------------------"
echo "⚠️  Manual step required:"
echo "   1. Go to your GitHub repository → Settings → Environments"
echo "   2. Create 'production' environment"
echo "   3. Enable 'Required reviewers' and add yourself"
echo "   4. (Optional) Create 'development' environment"

# Create develop branch
echo ""
echo "Step 9: Create develop branch"
echo "-----------------------------"
if git rev-parse --verify develop >/dev/null 2>&1; then
    echo "✅ develop branch already exists"
else
    read -p "Create and push develop branch? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        git checkout -b develop
        git push -u origin develop
        git checkout -
        echo "✅ develop branch created and pushed"
    else
        echo "⚠️  Skipped creating develop branch"
    fi
fi

# Summary
echo ""
echo "========================================"
echo "✅ Setup Complete!"
echo "========================================"
echo ""
echo "Next steps:"
echo "  1. Complete GitHub Environment setup (see Step 8 above)"
echo "  2. Push code to trigger first deployment:"
echo "     - Push to 'develop' → deploys to yolo-funk-dev"
echo "     - Push to 'master' → deploys to yolo-funk-prod (requires approval)"
echo "     - Create PR → deploys to ephemeral environment"
echo ""
echo "  3. Configure Function App settings after first deployment:"
echo "     See DEPLOYMENT.md for configuration examples"
echo ""
echo "Resources created:"
echo "  - Resource Group: $RESOURCE_GROUP"
echo "  - Application Insights: yolo-funk-insights"
echo "  - Key Vault: $KEYVAULT_NAME"
echo "  - Service Principal: $SP_NAME"
echo ""
echo "Security note:"
echo "  - AZURE_CREDENTIALS saved to .github/secrets/"
echo "  - This directory is in .gitignore - do NOT commit!"
echo "  - Delete after confirming GitHub secrets are configured"
echo ""
