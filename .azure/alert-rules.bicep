@description('Function App name to monitor')
param functionAppName string

@description('Email address for alerts')
param alertEmail string

@description('Resource location')
param location string = resourceGroup().location

// Get existing function app
resource functionApp 'Microsoft.Web/sites@2023-01-01' existing = {
  name: functionAppName
}

// Get existing Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: 'yolo-funk-insights'
}

// Action Group for email notifications
resource emailActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${functionAppName}-email-alerts'
  location: 'global'
  properties: {
    groupShortName: 'YoloAlerts'
    enabled: true
    emailReceivers: [
      {
        name: 'EmailAdmin'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// Alert: Function Execution Failures
resource functionFailureAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${functionAppName}-function-failures'
  location: 'global'
  properties: {
    description: 'Alert when function executions fail'
    severity: 2
    enabled: true
    scopes: [
      functionApp.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'FunctionExecutionFailures'
          metricName: 'FunctionExecutionCount'
          dimensions: [
            {
              name: 'SuccessStatus'
              operator: 'Include'
              values: [
                'False'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Count'
        }
      ]
    }
    actions: [
      {
        actionGroupId: emailActionGroup.id
      }
    ]
  }
}

// Alert: Application Exceptions (via Application Insights)
resource exceptionAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${functionAppName}-exceptions'
  location: location
  properties: {
    displayName: '${functionAppName} - Exceptions Detected'
    description: 'Alert when exceptions are logged in Application Insights'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    scopes: [
      appInsights.id
    ]
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: '''
exceptions
| where cloud_RoleName == "${functionAppName}"
| summarize exceptionCount = count() by bin(timestamp, 5m)
'''
          timeAggregation: 'Count'
          dimensions: []
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        emailActionGroup.id
      ]
    }
  }
}

// Alert: Timer Trigger Failures (functions not running on schedule)
resource timerTriggerAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${functionAppName}-timer-failures'
  location: location
  properties: {
    displayName: '${functionAppName} - Timer Trigger Failures'
    description: 'Alert when scheduled functions fail to execute'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT15M'
    scopes: [
      appInsights.id
    ]
    windowSize: 'PT30M'
    criteria: {
      allOf: [
        {
          query: '''
traces
| where cloud_RoleName == "${functionAppName}"
| where message contains "Timer trigger" and (message contains "error" or message contains "failed")
| summarize failureCount = count() by bin(timestamp, 15m)
'''
          timeAggregation: 'Count'
          dimensions: []
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        emailActionGroup.id
      ]
    }
  }
}

output actionGroupId string = emailActionGroup.id
output functionFailureAlertId string = functionFailureAlert.id
output exceptionAlertId string = exceptionAlert.id
output timerTriggerAlertId string = timerTriggerAlert.id
