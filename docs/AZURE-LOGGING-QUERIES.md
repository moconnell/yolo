# Azure Functions Logging Guide

## Where Your Logs Are

### Application Insights (Primary)

All `ILogger` calls automatically flow to Application Insights:

- **Location**: Azure Portal → Application Insights → Logs
- **Retention**: 90 days by default (configurable)
- **Query Language**: KQL (Kusto Query Language)

### File System (Secondary)

Limited ephemeral storage:

- **Location**: `/home/LogFiles/` (consumption plan)
- **Access**: Via Kudu console or Azure CLI
- **Retention**: Temporary, clears on cold start
- **Not recommended** for production log analysis

## Accessing Logs

### 1. Azure Portal - Live Stream

```
Function App → Monitoring → Log stream
```

Shows real-time logs as they're written. Good for debugging active issues.

### 2. Azure Portal - Application Insights

```
Function App → Application Insights → Logs
```

Run KQL queries for historical analysis.

### 3. Azure CLI - Streaming

```bash
# Stream live logs
az webapp log tail \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod

# Stream specific log types
az webapp log tail \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod \
  --filter Error
```

### 4. Azure CLI - Download

```bash
# Download recent logs
az webapp log download \
  --resource-group ResourceGroup1 \
  --name yolo-funk-prod \
  --log-file logs.zip
```

### 5. VS Code Extension

Install "Azure Functions" extension:

- View → Explorer → Azure
- Right-click function app → Start Streaming Logs

## Useful KQL Queries

### ⚠️ TROUBLESHOOTING: If You Don't See Application Logs

If you only see framework logs and not your application code logs:

```kusto
// Check if sampling is dropping your logs
traces
| where timestamp > ago(1h)
| summarize count() by cloud_RoleName, severityLevel
```

**Fix:** Ensure `host.json` has `"isEnabled": false` in `samplingSettings`

### All Application Logs (Most Useful)

```kusto
traces
| where cloud_RoleName startswith "yolo-funk"
| where severityLevel >= 1  // 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical
| order by timestamp desc
| take 500
| project timestamp, severityLevel, message, operation_Name
```

### Recent Executions

```kusto
traces
| where cloud_RoleName == "yolo-funk-prod"
| where message contains "rebalance"
| order by timestamp desc
| take 100
| project timestamp, message, severityLevel
```

### Timer Trigger Status

```kusto
traces
| where cloud_RoleName == "yolo-funk-prod"
| where message contains "Next timer schedule"
| order by timestamp desc
| project timestamp, message
```

### Error Analysis

```kusto
exceptions
| where cloud_RoleName == "yolo-funk-prod"
| order by timestamp desc
| project timestamp, type, outerMessage, problemId
```

### Function Execution Times

```kusto
requests
| where cloud_RoleName == "yolo-funk-prod"
| summarize
    count=count(),
    avg_duration=avg(duration),
    p95_duration=percentile(duration, 95),
    p99_duration=percentile(duration, 99)
  by name
| order by count desc
```

### Execution Success Rate

```kusto
requests
| where cloud_RoleName == "yolo-funk-prod"
| summarize
    total=count(),
    successful=countif(success == true),
    failed=countif(success == false)
  by bin(timestamp, 1h), name
| extend success_rate = (successful * 100.0 / total)
| order by timestamp desc
```

### Find Specific Strategy Logs

```kusto
traces
| where cloud_RoleName == "yolo-funk-prod"
| where message contains "UnravelDaily" or message contains "YoloDaily"
| order by timestamp desc
| project timestamp, message, severityLevel
```

### Last 24 Hours - All Events

```kusto
union traces, exceptions, requests
| where cloud_RoleName == "yolo-funk-prod"
| where timestamp > ago(24h)
| order by timestamp desc
| take 500
```

### Custom Dimensions Analysis

```kusto
traces
| where cloud_RoleName == "yolo-funk-prod"
| extend Strategy = tostring(customDimensions.Strategy)
| where isnotempty(Strategy)
| summarize count() by Strategy, bin(timestamp, 1d)
| render timechart
```

## Log Levels

Your functions use these log levels (from lowest to highest severity):

| Level       | Method             | When to Use                                  |
| ----------- | ------------------ | -------------------------------------------- |
| Trace       | `LogTrace()`       | Very detailed debugging info                 |
| Debug       | `LogDebug()`       | Debugging information                        |
| Information | `LogInformation()` | General informational messages               |
| Warning     | `LogWarning()`     | Potential issues that don't stop execution   |
| Error       | `LogError()`       | Errors that should be investigated           |
| Critical    | `LogCritical()`    | Critical failures requiring immediate action |

## Improving Your Logging

### 1. Add Structured Logging

Instead of:

```csharp
_logger.LogInformation($"Rebalanced {symbol} with weight {weight}");
```

Use:

```csharp
_logger.LogInformation(
    "Symbol rebalanced: {Symbol}, Weight: {Weight}, Leverage: {Leverage}",
    symbol, weight, leverage);
```

This makes logs queryable by individual fields in Application Insights.

### 2. Add Custom Dimensions

```csharp
using var scope = _logger.BeginScope(new Dictionary<string, object>
{
    ["Strategy"] = "UnravelDaily",
    ["ExecutionId"] = Guid.NewGuid(),
    ["Environment"] = Environment.GetEnvironmentVariable("Environment")
});

_logger.LogInformation("Rebalance started");
// All logs within this scope will include the custom dimensions
```

### 3. Log Trade Details

```csharp
_logger.LogInformation(
    "Trade executed: {Symbol} {Side} {Quantity} @ {Price}",
    order.Symbol, order.Side, order.Quantity, order.Price);
```

### 4. Log Performance Metrics

```csharp
var sw = Stopwatch.StartNew();
// ... operation ...
_logger.LogInformation(
    "Operation completed in {DurationMs}ms",
    sw.ElapsedMilliseconds);
```

## Retention and Costs

**Application Insights:**

- **Free tier**: 5 GB/month ingestion
- **Default retention**: 90 days
- **Extended retention**: Up to 730 days (additional cost)

**To check usage:**

```
Application Insights → Usage and estimated costs
```

## Best Practices

1. **Use structured logging** with named parameters, not string interpolation
2. **Log meaningful events**: rebalances, trades, errors, configuration changes
3. **Don't log secrets**: Ensure API keys, private keys are never logged
4. **Use appropriate log levels**: Info for normal ops, Warning for concerns, Error for failures
5. **Add correlation IDs**: Use `BeginScope` to group related logs
6. **Monitor your alerts**: Set up alerts for critical errors (already configured in your setup)

## Troubleshooting

**Logs not appearing:**

- Check Application Insights connection string is configured
- Verify `APPINSIGHTS_INSTRUMENTATIONKEY` in function app settings
- Wait 2-3 minutes for logs to appear in Application Insights
- Check if sampling is enabled (might drop logs in high-volume scenarios)

**Too many logs:**

- Adjust log levels in `appsettings.json`
- Enable sampling in Application Insights
- Filter verbose Microsoft framework logs

**Logs delayed:**

- Application Insights has ~2-3 minute latency
- Use Log Stream for real-time viewing
- Kudu console for immediate file system logs

## Related Documentation

- [Application Insights Overview](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
- [KQL Quick Reference](https://learn.microsoft.com/azure/data-explorer/kql-quick-reference)
- [Azure Functions Monitoring](https://learn.microsoft.com/azure/azure-functions/functions-monitoring)
