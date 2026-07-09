# yolo

Automation for the [RobotWealth](https://robotwealth.com) YOLO and [Unravel](https://unravel.finance) cryptocurrency factor strategies

[![Coverage Status](https://coveralls.io/repos/github/moconnell/yolo/badge.svg?branch=master)](https://coveralls.io/github/moconnell/yolo?branch=master)

## YoloFunk - Azure Function App

YoloFunk is an Azure Function App project and now the primary means of invoking rebalance functionality.

### Endpoints

- `POST /api/rebalance/yolodaily` - Execute YOLO strategy rebalance
- `POST /api/rebalance/unraveldaily` - Execute Unravel strategy rebalance
- `GET /api/rebalance/yolodaily/status` - Get YOLO rebalance orchestration status
- `GET /api/rebalance/unraveldaily/status` - Get Unravel rebalance orchestration status
- `GET /api/rebalance/yolodaily/effective-weights` - Calculate and return effective YOLO rebalance weights
- `GET /api/rebalance/unraveldaily/effective-weights` - Calculate and return effective Unravel rebalance weights
- `GET /api/storage/rebalance-events` - Query persisted rebalance telemetry events; supports `strategy`, `runId`, `eventType`, `level`, `coin`, `clientOrderId`, `from`, `to`, `pageSize`, and `continuationToken`
- `GET /api/storage/http-requests` - Query captured upstream HTTP request metadata; supports `host`, `endpoint`, `method`, `statusCode`, `contentHash`, `from`, `to`, `pageSize`, and `continuationToken`
- `GET /api/storage/http-requests/payload?blobName=...` - Fetch the raw persisted HTTP response/request payload for a captured HTTP request

## YOLO Console - Windows x64 Deployment (DEPRECATED)

This project is no longer actively maintained.

There are a selection of past pre-built win64 console apps in the releases section, which can be scheduled to run daily.

### Quick Start

- download `YoloKonsole.exe`, `appsettings.json` and `./setup-secrets.ps1` and save to a new folder on your computer

1. **Setup Secrets** (First time only):

   ```powershell
   .\setup-secrets.ps1
   ```

2. **Edit appsettings.json**:

   Adjust additional settings as desired

3. **Run Application**:

   ```powershell
   ./YoloKonsole.exe
   ```

### Files Included

- `YoloKonsole.exe` - Main application
- `setup-secrets.ps1` - Configure your API keys and addresses securely
- `appsettings.json` - Application configuration settings
- `README.md` - This file

### Security Notes

- Secrets are stored in `.\secrets` under the install directory with restricted permissions
- Only your Windows user account can access the secret files

### Requirements

- Windows 10/11 x64
- .NET 10 installation required
- PowerShell 5.1+ (included with Windows)

### Troubleshooting

If you get execution policy errors:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## appsettings.json

There is an example of this file included in the build. You will need to add config as follows to either run the RobotWealth YOLO strategy or a combination of single factors from Unravel.

### Example for RobotWealth YOLO

As per the original intention, the app supports running the [RobotWealth YOLO](https://robotwealth.com/yolo-strategy-cheat-sheet/) crypto strategy.

The relevant config elements to include in this case are as follows:

```JSON
{
  "RobotWealth": {
    "ApiBaseUrl": "https://api.robotwealth.com/v1/yolo",
    "ApiKey": ""
  },
  "Yolo": {
    "FactorWeights": {
      "Carry": 1,
      "Momentum": 1,
      "Trend": 1
    },
  }
}
```

### Unravel Example

The app also supports combining multiple single factors from [Unravel](https://unravel.finance/home).

The relevant config elements to include in this case are as follows (note the required `Aliases` config for SHIB - there may be other tickers in future that require similar treatment):

```JSON
{
  "Hyperliquid": {
    "Aliases": {
      "SHIB": "kSHIB"
    }
  },
  "Unravel": {
    "ApiBaseUrl": "https://unravel.finance/api/v1",
    "ApiKey": "",
    "Factors": [
      "Carry",
      "Momentum",
      "OpenInterestDivergence",
      "RelativeIlliquidity",
      "RetailFlow",
      "SupplyVelocity",
      "TrendLongonlyAdaptive"
    ]
  },
  "Yolo": {
    "FactorWeights": {
      "Carry": 1,
      "Momentum": 1,
      "OpenInterestDivergence": 1,
      "RelativeIlliquidity": 1,
      "RetailFlow": 1,
      "SupplyVelocity": 1,
      "TrendLongonlyAdaptive": 1
    },
    "FactorNormalizationMethod": "CrossSectionalZScore",
  }
}
```

Factor normalisation is currently cross-sectional rank / quantile normalisation (TODO: time-series normalisation).

## Hyperliquid

The app currently only supports [Hyperliquid](https://hyperliquid.xyz/) - even if the architecture would easily allow the implementation of further brokers.

`Address` and `PrivateKey` can be configure directly in `appsettings.json` for e.g. testing - or via single-file secrets configured by the script `setup-secrets.ps1`

```JSON
  "Hyperliquid": {
    "Address": "",
    "PrivateKey": "",
    "UseTestnet": false,
    "BuilderFeePercentage": 0.0
  },
```

`BuilderFeePercentage` is passed through to HyperLiquid.Net's builder-code setting. The repo default is `0.0`, which disables the optional library builder fee; set it explicitly to `0.01` to opt in to the 1 bps fee.

### Logging/PathFormat

This determines where the application will write logs to. Windows paths using `\` must be escaped as `\\` as below. The substitution token `{Date}` included in the path means that a new file will be written each day. You can omit this if you would prefer to have a single file.

```json
{
  "Logging": {
    "PathFormat": "C:\\logs\\yolo-{Date}.log"
  }
}
```

### Yolo/BaseAsset

This is the token that the application will trade in and out of. It defaults to `USDC` as this is what Hyperliquid supports.

### Yolo/AssetPermissions

In case your account does not have margin trading or futures enabled, it is possible to configure accordingly via the `AssetPermissions` setting - possible settings of which are currently:

```C#
    None
    LongSpot
    ShortSpot
    Spot
    PerpetualFutures
    LongSpotAndPerp
    SpotAndPerp
    ExpiringFutures
    All
```

N.B. only `PerpetualFutures` has been fully implemented and tested for use with Hyperliquid.

### Yolo/RebalanceMode

This setting determines the target weight when rebalancing positions that are outside the tolerance band (TradeBuffer).

The default setting of `Center` will rebalance to the ideal calculated asset weighting (the center of the tolerance band).

Setting this to `Edge` will rebalance to the nearest edge of the tolerance band instead. This can be optimal depending on broker fee structure, as price is theoretically nearly as likely to reverse away from the ideal weight as it is to continue.

For example, with a target weight of 10% and a TradeBuffer of 4%:

- The tolerance band is [6%, 14%]
- If current weight is 2% (below the band):
  - `Center` mode: rebalance to 10% (ideal weight)
  - `Edge` mode: rebalance to 6% (lower edge)
- If current weight is 16% (above the band):
  - `Center` mode: rebalance to 10% (ideal weight)
  - `Edge` mode: rebalance to 14% (upper edge)

Possible values:

```JSON
Center  // (default)
Edge
```

### Yolo/SpreadSplit

This setting determines the placement of the limit price within the bid-ask price spread and can take any value between 0 and 1 (values greater than 1 will be treated as 1).

The default setting of 0.5 ensures that the limit price will always be placed exactly in the middle of the spread.

e.g. a setting of 0.618 would place the limit price for a buy order at the best bid price + 61.8% of the current bid-ask spread; conversely for a sell it would be the lowest ask price - 61.8% of the current bid-ask spread.

Limit price must be divisible by the intrument price step i.e. in the case where the spread equals the minimum price increment, an order will be submitted that matches the current best bid or ask price.

## Order Management

The application will submit an initial limit order at best bid/ask and then after the timeout period, cancel that and submit a market order.
