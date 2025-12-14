# Multi-Environment Azure Functions Setup

## What Was Created

I've set up a complete multi-environment CI/CD pipeline with Infrastructure as Code:

### 📁 Infrastructure (Bicep Templates)

- **[.azure/function-app.bicep](.azure/function-app.bicep)** - Provisions Function App, Storage, and App Service Plan
- **[.azure/app-insights.bicep](.azure/app-insights.bicep)** - Shared Application Insights (cost optimization)

### 🚀 CI/CD Workflows

- **[.github/workflows/deploy-azure-functions.yml](.github/workflows/deploy-azure-functions.yml)** - Main deployment workflow
- **[.github/workflows/cleanup-azure-functions.yml](.github/workflows/cleanup-azure-functions.yml)** - Auto-cleanup ephemeral environments

### 📖 Documentation

- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Complete deployment guide
- **[setup-azure.sh](setup-azure.sh)** - Automated setup script

### 🗑️ Removed

- **[.github/workflows/azure-functions-deploy.yml]** ← Old single-environment workflow (can delete)

## Architecture

### Environment Strategy

```
┌─────────────────┬──────────────┬────────────────────────┬──────────┬─────────────┐
│ Branch          │ Environment  │ Function App           │ Network  │ Cleanup     │
├─────────────────┼──────────────┼────────────────────────┼──────────┼─────────────┤
│ feature/*       │ feat-{name}  │ yolo-funk-feat-{name}  │ testnet  │ Auto on PR  │
│ Pull Request    │ pr-{number}  │ yolo-funk-pr-{number}  │ testnet  │ Auto on PR  │
│ develop         │ dev          │ yolo-funk-dev          │ testnet  │ Manual only │
│ master          │ prod         │ yolo-funk-prod         │ mainnet  │ Manual only │
└─────────────────┴──────────────┴────────────────────────┴──────────┴─────────────┘
```

### Cost Optimization Features

✅ **Consumption Plan** - Pay only for executions (~$0.20 per million)  
✅ **Shared Application Insights** - Single instance across all environments  
✅ **Auto-cleanup** - Ephemeral PR/feature environments deleted when PR closes  
✅ **On-demand infrastructure** - Resources created only when needed

### Deployment Flow

```
┌──────────────┐
│ Push Code    │
└──────┬───────┘
       │
       ▼
┌──────────────────────────┐
│ Determine Environment    │ ◄── Based on branch/PR
│ - master → prod          │
│ - develop → dev          │
│ - PR → pr-{number}       │
│ - feature/* → feat-*     │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────┐
│ Provision Infrastructure │ ◄── Bicep templates
│ - Function App           │
│ - Storage Account        │
│ - App Service Plan       │
│ - Grant Key Vault access │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────┐
│ Build & Deploy           │
│ - dotnet restore/build   │
│ - dotnet publish         │
│ - Deploy to Azure        │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────┐
│ Smoke Test               │ ◄── Health check
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────┐
│ Comment on PR (if PR)    │ ◄── Deployment URL
└──────────────────────────┘
```

## Next Steps

### 1. Run Setup Script

This automates everything:

```bash
./setup-azure.sh
```

The script will:

- ✅ Create Azure Resource Group
- ✅ Deploy shared Application Insights
- ✅ Create Azure Key Vault
- ✅ Add your Hyperliquid credentials (testnet & mainnet)
- ✅ Create Service Principal for GitHub Actions
- ✅ Configure GitHub secrets (if GitHub CLI installed)
- ✅ Create `develop` branch (optional)

### 2. Manual GitHub Setup

After running the script:

1. **Create GitHub Environments** (for production approvals):

   - Go to: `Settings` → `Environments` → `New environment`
   - Name: `production`
   - Enable "Required reviewers" → Add yourself
   - (Optional) Create `development` environment without restrictions

2. **Verify GitHub Secrets** (if not auto-configured):
   - Go to: `Settings` → `Secrets and variables` → `Actions`
   - Should have: `AZURE_CREDENTIALS`
   - Should have variable: `AZURE_KEYVAULT_NAME`

### 3. Configure Function App Settings

After first deployment, each environment needs strategy configuration:

```bash
# Development
az functionapp config appsettings set \
  --name yolo-funk-dev \
  --resource-group rg-yolo-funk \
  --settings \
    "Strategies__MomentumDaily__Yolo__MaxLeverage=1.0" \
    "Strategies__MomentumDaily__Yolo__MaxNumAssets=10" \
    "Strategies__MomentumDaily__Hyperliquid__Address=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-dev-hyperliquid-address)" \
    "Strategies__MomentumDaily__Hyperliquid__PrivateKey=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-dev-hyperliquid-privatekey)"

# Production (similar but with prod secrets)
az functionapp config appsettings set \
  --name yolo-funk-prod \
  --resource-group rg-yolo-funk \
  --settings \
    "Strategies__MomentumDaily__Yolo__MaxLeverage=3.0" \
    "Strategies__MomentumDaily__Hyperliquid__Address=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-prod-hyperliquid-address)" \
    "Strategies__MomentumDaily__Hyperliquid__PrivateKey=@Microsoft.KeyVault(VaultName=yolo-vault;SecretName=yolo-prod-hyperliquid-privatekey)"
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for full configuration examples.

### 4. Trigger First Deployment

```bash
# Option 1: Push to develop (auto-deploy to dev)
git checkout -b develop  # if not exists
git push -u origin develop

# Option 2: Create a test PR (creates ephemeral environment)
git checkout -b feature/test-deployment
git push -u origin feature/test-deployment
# Create PR on GitHub

# Option 3: Merge to master (deploys to prod with approval)
# Merge via GitHub PR → requires manual approval
```

## Testing the Setup

### Test Feature Branch Deployment

1. Create feature branch:

   ```bash
   git checkout -b feature/test-azure-deploy
   echo "// test" >> src/YoloFunk/Program.cs
   git add .
   git commit -m "test: Azure deployment"
   git push -u origin feature/test-azure-deploy
   ```

2. Create PR on GitHub

3. Watch GitHub Actions:

   - Should provision `yolo-funk-feat-test-azure-deploy`
   - Should deploy code
   - Should comment on PR with URL

4. Test the deployment:

   ```bash
   # URL will be in PR comment, e.g.:
   curl https://yolo-funk-feat-test-azure-deploy.azurewebsites.net/api/health
   ```

5. Close PR → Automatic cleanup deletes all resources

### Test Development Deployment

```bash
git checkout develop
git merge feature/test-azure-deploy
git push origin develop
# Watch GitHub Actions → deploys to yolo-funk-dev
```

## Workflow Features

### Smart Environment Detection

The deployment workflow automatically determines where to deploy based on:

- **`master` branch** → Production (requires approval)
- **`develop` branch** → Development
- **`feature/*` branches** → Ephemeral feature environment
- **Pull Requests** → Ephemeral PR environment (`pr-{number}`)
- **Manual trigger** → Choose environment via dropdown

### Production Protection

- ✅ Requires manual approval via GitHub Environment
- ✅ Cannot be auto-deleted (safety check in cleanup workflow)
- ✅ Separate secrets from dev/test environments

### Cost Control

- Auto-cleanup when PR closes/merged
- Consumption plan (pay per execution)
- Shared Application Insights
- Can manually cleanup any non-prod environment

## Troubleshooting

If deployment fails:

1. **Check GitHub Actions logs**: Detailed error messages
2. **Verify Azure credentials**: `az login` and test
3. **Check service principal permissions**: Should have Contributor on Resource Group
4. **Verify Key Vault access**: Managed identity needs `get list` permissions

See [DEPLOYMENT.md](DEPLOYMENT.md) for complete troubleshooting guide.

## Migration from Manual Deployment

Currently you're:

- Manually merging PRs to master
- Copy-deploying YoloKonsole.exe to VPS folders
- Manually editing appsettings.json

**New workflow:**

- Merge PR → Auto-deploys to Azure Functions
- Configuration stored in Azure (Key Vault references)
- No manual file management
- Built-in monitoring via Application Insights

**Advantages:**

- ✅ Test features in isolation before merging
- ✅ Automatic rollback (redeploy previous commit)
- ✅ No VPS management
- ✅ Automatic scaling
- ✅ Pay only for actual executions
- ✅ Full audit trail via GitHub + Azure logs

## Resources

- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Full deployment documentation
- **[.azure/](.)** - Infrastructure as Code (Bicep templates)
- **Azure Resource Group**: `rg-yolo-funk`
- **Shared Monitoring**: `yolo-funk-insights`
