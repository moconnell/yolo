# Quick Log Troubleshooting

## Problem: Only Seeing Framework Logs, Not Application Logs

### Immediate Fixes

**1. Check host.json sampling:**

```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": false // ← Should be false
      }
    }
  }
}
```

**2. Redeploy after changing host.json:**

```bash
git add src/YoloFunk/host.json
git commit -m "Disable log sampling"
git push origin master
```

**3. Wait 2-3 minutes after deployment** for logs to flow

**4. Try this query in Application Insights:**

```kusto
traces
| where timestamp > ago(30m)
| where cloud_RoleName contains "yolo-funk"
| where message !contains "Host."  // Exclude host framework logs
| order by timestamp desc
| take 100
```

### If Still No Logs

**Check Application Insights connection:**

```bash
# Verify environment variable is set
az functionapp config appsettings list \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod \
  --query "[?name=='APPLICATIONINSIGHTS_CONNECTION_STRING'].value"
```

**Check function is actually running:**

```kusto
requests
| where cloud_RoleName contains "yolo-funk"
| where timestamp > ago(1h)
| order by timestamp desc
```

**Manually trigger function to generate logs:**

```bash
# In Azure Portal: Function App → Functions → [Function Name] → Test/Run
# Or use Azure CLI:
az functionapp function invoke \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod \
  --function-name UnravelDailyManualRebalance
```

### Alternative: Check File System Logs

If Application Insights still not working, check file system:

```bash
# Stream logs directly from function app
az webapp log tail \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod

# Or download
az webapp log download \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod \
  --log-file logs.zip
```

### Expected Log Messages

Once fixed, you should see:

- `"UnravelDaily scheduled rebalance executed at: ..."`
- `"YoloDaily manual rebalance triggered at: ..."`
- `"Error executing manual rebalance for ..."` (on errors)

## Quick Reference: Where Are My Logs?

| Location     | Latency   | Retention | Query | Best For            |
| ------------ | --------- | --------- | ----- | ------------------- |
| Log Stream   | Real-time | None      | N/A   | Active debugging    |
| App Insights | 2-3 min   | 90 days   | KQL   | Historical analysis |
| File System  | Real-time | Ephemeral | N/A   | Emergency fallback  |

## Most Common Issues

1. **Sampling enabled** → Logs randomly dropped (50%+ loss)
2. **Wrong log level** → Information logs filtered out
3. **Deployment pending** → Old code still running
4. **Cold start** → Function not warm, no executions yet
5. **Wrong cloud_RoleName** → Querying wrong function app
