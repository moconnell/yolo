# Azure Monitoring and Alerts Setup

## Why Environment Variables Disappeared

**Problem:** Manually added environment variables in Azure Portal get deleted during deployments.

**Root Cause:** Bicep/ARM template deployments **replace** all app settings with only those defined in the template. Any settings added manually in the portal but not in the Bicep template will be removed.

**Solution:** Always add environment variables to `.azure/function-app.bicep` to persist across deployments.

## Schedule Settings Now in Bicep

The timer trigger schedules are now defined in the Bicep template:

```bicep
{
  name: 'Strategies__YoloDaily__Schedule'
  value: '0 30 9 * * *'  // 09:30 UTC daily
}
{
  name: 'Strategies__UnravelDaily__Schedule'
  value: '0 30 0 * * *'  // 00:30 UTC daily
}
```

These will now persist across all deployments and won't be accidentally deleted.

## Email Alerts Setup

### Step 1: Add Email Variable to GitHub

1. Go to: **Settings** → **Secrets and variables** → **Actions** → **Variables** tab
2. Click **New repository variable**
3. Name: `ALERT_EMAIL`
4. Value: Your email address (e.g., `matthew.oconnell@gmail.com`)
5. Click **Add variable**

### Step 2: Deploy Alert Rules

The alert rules are automatically deployed to **production only** when:

- Environment is `prod`
- `ALERT_EMAIL` variable is set
- Deployment workflow runs

To trigger deployment:

```bash
git add .azure/alert-rules.bicep .azure/function-app.bicep .github/workflows/deploy-azure-functions.yml
git commit -m "Add schedule settings to Bicep and configure email alerts"
git push origin master
```

### What Alerts Are Configured

1. **Function Execution Failures**

   - Triggers when any function execution fails
   - Evaluates every 5 minutes over 15-minute window
   - Severity: Warning

2. **Application Exceptions**

   - Triggers when exceptions are logged to Application Insights
   - Queries Application Insights logs for exceptions
   - Evaluates every 5 minutes over 15-minute window
   - Severity: Warning

3. **Timer Trigger Failures**
   - Triggers when scheduled functions fail to execute
   - Looks for timer trigger errors in logs
   - Evaluates every 15 minutes over 30-minute window
   - Severity: Warning

### Email Notification Format

You'll receive emails with:

- Alert name and severity
- Time the alert was triggered
- Query results or metric values
- Link to Azure Portal for investigation
- Automatically resolves when condition clears

### Manual Setup (Alternative)

If you prefer to set up alerts manually in Azure Portal:

1. Navigate to your Function App in Azure Portal
2. Click **Alerts** in left menu
3. Click **+ Create** → **Alert rule**
4. Configure:
   - **Scope**: Select your function app
   - **Condition**: Choose metric (e.g., "Function Execution Count" with SuccessStatus=False)
   - **Actions**: Create Action Group with email receiver
   - **Details**: Set alert name and severity

### Testing Alerts

To test that alerts are working:

1. **Trigger a test failure**: Temporarily break a function or throw an exception
2. **Check email**: Should receive alert within 5-15 minutes
3. **Fix the issue**: Alert should auto-resolve when condition clears

### Viewing Alert History

In Azure Portal:

1. Go to **Monitor** → **Alerts**
2. View all fired alerts across all resources
3. Filter by severity, time range, or resource

## Best Practices

1. **Always use Bicep for configuration**: Never rely on manual Portal changes
2. **Test in dev first**: Make changes to dev environment before prod
3. **Monitor alert noise**: Adjust thresholds if getting too many alerts
4. **Set up alert suppression**: For planned maintenance windows
5. **Add multiple email recipients**: Use distribution lists for team alerts

## Troubleshooting

**Alert not firing:**

- Check alert is enabled in Azure Portal
- Verify email address in Action Group
- Check spam/junk folder
- Review alert evaluation logs in Azure Monitor

**Too many alerts:**

- Increase threshold values
- Increase evaluation frequency
- Add suppression rules for known issues

**Alerts for wrong environment:**

- Alert rules are deployed to production only
- Check environment tags on resources
