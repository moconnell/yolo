# YoloFunk - Azure Functions Strategy-Based Trading

## Overview

YoloFunk provides Azure Functions for automated and manual rebalancing of multiple trading strategies. Each strategy can have its own:

- Configuration (leverage, trade buffer, rebalance mode, etc.)
- Factor data provider (RobotWealth, Unravel, or both)
- Execution schedule (timer trigger)
- Manual trigger endpoint (HTTP trigger)

## Architecture

### Strategy-Based Design

Each trading strategy is independently configured and can be triggered via:

1. **Scheduled Functions** - Timer-based execution (e.g., daily at midnight)
2. **Manual Functions** - HTTP endpoints for ad-hoc execution

### Key Components

- **`AddStrategyServices`** - Registers all strategy-specific services using keyed DI
- **Strategy Configuration** - Each strategy defined under `Strategies` section in config
- **Keyed Services** - .NET 8+ keyed dependency injection differentiates strategy instances

## Configuration

### Structure

```json
{
  "Strategies": {
    "StrategyName": {
      "Yolo": {
        /* YoloConfig settings */
      },
      "RobotWealth": {
        /* Optional: RobotWealth factor provider */
      },
      "Unravel": {
        /* Optional: Unravel factor provider */
      }
    }
  }
}
```

### Example: Multiple Strategies

See [appsettings.example.json](appsettings.example.json) for a complete example.

## Adding a New Strategy

### 1. Add Configuration

Add a new section under `Strategies` in your `appsettings.json`.

### 2. Create Function Classes

Create scheduled and/or manual function classes following the pattern in [YoloDailyScheduledRebalance.cs](YoloDailyScheduledRebalance.cs) and [YoloDailyManualRebalance.cs](YoloDailyManualRebalance.cs).

The key is to use `[FromKeyedServices("strategykey")]` to inject the correct command instance.

## How It Works

Strategies are auto-registered from configuration in [Program.cs](Program.cs). Each strategy gets its own isolated instances of all dependencies via keyed dependency injection.

## Benefits

✅ **Independent Strategies** - Each runs with its own configuration and schedule  
✅ **Shared Infrastructure** - Common broker, trade factory, and core logic  
✅ **Type-Safe** - Compile-time verification via keyed DI  
✅ **Scalable** - Add strategies without modifying core code  
✅ **Azure-Native** - Leverages Functions' serverless scaling and monitoring
