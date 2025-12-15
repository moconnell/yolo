# Configuration Architecture

## Overview

The project uses standard ASP.NET Core configuration hierarchy:

1. **appsettings.json** - Base configuration (committed to repo)
2. **appsettings.Development.json** - Local dev overrides (committed to repo)
3. **local.settings.json** - Local secrets for Azure Functions CLI (git-ignored)
4. **Environment Variables / Azure App Settings** - Production secrets (Azure only)

Environment variables override JSON files, allowing secrets to be stored securely in Azure Key Vault while keeping readable configuration in source control.

## File Structure

```
src/YoloFunk/
├── appsettings.json                    # Base config (COMMIT)
├── appsettings.Development.json        # Dev overrides (COMMIT)
├── local.settings.json                 # Local secrets (GIT-IGNORE)
└── local.settings.json.example         # Template (COMMIT)
```

## Configuration Files

### appsettings.json (Committed)

Contains all non-sensitive configuration with empty/placeholder values for secrets:

```json
{
  "Strategies": {
    "YoloDaily": {
      "Hyperliquid": {
        "Address": "", // ← Override in local.settings.json or Azure
        "PrivateKey": "", // ← Override in local.settings.json or Azure
        "VaultAddress": "", // ← Override in local.settings.json or Azure
        "UseTestnet": false
      },
      "Yolo": {
        "MaxLeverage": 2.0, // ← Actual config values here
        "NominalCash": 1000
      }
    }
  }
}
```

**Advantages:**

- ✅ Hierarchical, readable structure
- ✅ Committed to source control
- ✅ Clear defaults and structure
- ✅ Easy to diff and review changes
- ✅ IntelliSense support in editors

### appsettings.Development.json (Committed)

Environment-specific overrides for local development:

```json
{
  "Strategies": {
    "YoloDaily": {
      "Hyperliquid": {
        "UseTestnet": true // ← Use testnet for local dev
      },
      "Yolo": {
        "NominalCash": 100 // ← Smaller amounts for testing
      }
    }
  }
}
```

### local.settings.json (Git-Ignored)

Only secrets for local Azure Functions development:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    "Strategies__YoloDaily__Hyperliquid__Address": "0x...",
    "Strategies__YoloDaily__Hyperliquid__PrivateKey": "0x...",
    "Strategies__YoloDaily__Hyperliquid__VaultAddress": "0x..."
  }
}
```

**Note:** Double-underscore `__` syntax is required for Azure Functions local CLI.

### Azure App Settings (Production)

Only override secrets - all other config comes from appsettings.json in deployment:

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

## Configuration Merge Order

```
1. appsettings.json                    (base defaults)
2. appsettings.{Environment}.json      (environment overrides)
3. Environment Variables               (secrets, highest priority)
```

Example:

```json
// appsettings.json
{
  "Strategies": {
    "YoloDaily": {
      "Yolo": { "MaxLeverage": 2.0 },
      "Hyperliquid": { "Address": "" }
    }
  }
}

// appsettings.Development.json
{
  "Strategies": {
    "YoloDaily": {
      "Yolo": { "MaxLeverage": 1.0 }  // ← Overrides base
    }
  }
}

// Environment Variable
Strategies__YoloDaily__Hyperliquid__Address=0x123  // ← Overrides both

// Result in Development:
{
  "Strategies": {
    "YoloDaily": {
      "Yolo": { "MaxLeverage": 1.0 },              // From Development.json
      "Hyperliquid": { "Address": "0x123" }        // From env var
    }
  }
}
```

## Local Development Setup

1. **Create local.settings.json** from example:

   ```bash
   cp local.settings.json.example local.settings.json
   ```

2. **Add your secrets** to local.settings.json:

   ```json
   {
     "Values": {
       "Strategies__YoloDaily__Hyperliquid__Address": "0xYourAddress",
       "Strategies__YoloDaily__Hyperliquid__PrivateKey": "0xYourKey"
     }
   }
   ```

3. **Run locally:**
   ```bash
   cd src/YoloFunk
   func start
   ```

## Azure Deployment

The CI/CD pipeline publishes appsettings.json to Azure (configured in YoloFunk.csproj).

**What gets deployed:**

- ✅ appsettings.json (base config)
- ✅ appsettings.Development.json (for staging slots if needed)
- ❌ local.settings.json (excluded from publish)

**Configuration at runtime:**

- Reads appsettings.json for defaults
- Overrides with Azure App Settings (from Key Vault)

## Best Practices

### What Goes Where

| Type                      | File                         | Committed | Example                     |
| ------------------------- | ---------------------------- | --------- | --------------------------- |
| **Defaults**              | appsettings.json             | ✅ Yes    | MaxLeverage, timeouts, URLs |
| **Environment overrides** | appsettings.Development.json | ✅ Yes    | UseTestnet: true            |
| **Local secrets**         | local.settings.json          | ❌ No     | Private keys, API keys      |
| **Production secrets**    | Azure App Settings           | ❌ No     | Key Vault references        |

### Adding New Configuration

1. **Add to appsettings.json** with appropriate default:

   ```json
   {
     "Strategies": {
       "YoloDaily": {
         "Yolo": {
           "NewSetting": "default-value"
         }
       }
     }
   }
   ```

2. **If environment-specific**, add override to appsettings.Development.json

3. **If secret**, add to local.settings.json.example as placeholder:

   ```json
   {
     "Values": {
       "Strategies__YoloDaily__SomeSecret": "placeholder"
     }
   }
   ```

4. **For production**, add to Azure App Settings via CLI or portal

### Migrating from local.settings.json Approach

The old approach (everything in local.settings.json) is now **only for secrets**.

**Before:**

```json
// local.settings.json (flat structure, hard to read)
{
  "Values": {
    "Strategies__YoloDaily__Yolo__MaxLeverage": "2",
    "Strategies__YoloDaily__Yolo__NominalCash": "1000",
    "Strategies__YoloDaily__Hyperliquid__Address": "0x...",
    "Strategies__YoloDaily__Hyperliquid__PrivateKey": "0x..."
  }
}
```

**After:**

```json
// appsettings.json (readable, committed)
{
  "Strategies": {
    "YoloDaily": {
      "Yolo": {
        "MaxLeverage": 2,
        "NominalCash": 1000
      },
      "Hyperliquid": {
        "Address": "",
        "PrivateKey": ""
      }
    }
  }
}

// local.settings.json (only secrets, git-ignored)
{
  "Values": {
    "Strategies__YoloDaily__Hyperliquid__Address": "0x...",
    "Strategies__YoloDaily__Hyperliquid__PrivateKey": "0x..."
  }
}
```

## Benefits

✅ **Readable config** - Hierarchical JSON, not flat key-value pairs  
✅ **Source control** - Config structure versioned, secrets excluded  
✅ **Code review** - Easier to review config changes  
✅ **IntelliSense** - Editor support for JSON schema  
✅ **Separation of concerns** - Config vs secrets clearly separated  
✅ **Standard ASP.NET Core patterns** - Familiar to .NET developers  
✅ **Minimal Azure overrides** - Only set secrets in App Settings

## References

- [ASP.NET Core Configuration](https://docs.microsoft.com/aspnet/core/fundamentals/configuration/)
- [Azure Functions Configuration](https://docs.microsoft.com/azure/azure-functions/functions-how-to-use-azure-function-app-settings)
- [Azure Key Vault References](https://docs.microsoft.com/azure/app-service/app-service-key-vault-references)
