[![Coverage Status](https://coveralls.io/repos/github/moconnell/yolo/badge.svg?branch=master)](https://coveralls.io/github/moconnell/yolo?branch=master)

# yolo

Automation for the [RobotWealth](https://robotwealth.com) cryptocurrency YOLO strategies

## YOLO Console - Windows x64 Deployment

There is a pre-built win64 console app in the releases section, which can be scheduled to run daily.

## Quick Start

- download `YoloKonsole.exe`, `appsettings.json` and save to a new folder on your computer
- edit `appsettings.json` with the correct settings
- run `YoloKonsole.exe` - from the command line is recommended initially, so that you can see the output easily (Windows+R, cmd) although file logging is also provided
- by default, the application will issue a `Proceed? (y/n)` challenge before issuing any trades to the broker
- this can be overridden by using the command-line switch `-s`

1. **Setup Secrets** (First time only):

   ```powershell
   .\setup-secrets.ps1
   ```

2. **Run Application**:

   ```powershell
   .\run-yolo.ps1
   ```

## Files Included

- `YoloKonsole.exe` - Main application (self-contained)
- `setup-secrets.ps1` - Configure your API keys and addresses securely
- `run-yolo.ps1` - Launch the application with proper environment
- `appsettings.prod.json` - Production configuration
- `README.md` - This file

## Security Notes

- Secrets are stored in `C:\secrets\yolo\` with restricted permissions
- Only your Windows user account can access the secret files
- Never commit or share the contents of `C:\secrets\yolo\`

## Requirements

- Windows 10/11 x64
- No .NET installation required (self-contained)
- PowerShell 5.1+ (included with Windows)

## Troubleshooting

If you get execution policy errors:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Logging/PathFormat

This determines where the application will write logs to. Windows paths using `\` must be escaped as `\\` as below. The substitution token `{Date}` included in the path means that a new file will be written each day. You can omit this if you would prefer to have a single file.

```JSON
{
  "Logging": {
    "PathFormat": "C:\\logs\\yolo-{Date}.json",
```

## Yolo/BaseAsset

This is the token that the application will trade in and out of. It defaults to `USD` but you could equally change to `USDT` etc. if preferred.

## Yolo/AssetPermissions

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

## Yolo/SpreadSplit

This setting determines the placement of the limit price within the bid-ask price spread and can take any value between 0 and 1 (values greater than 1 will be treated as 1).

The default setting of 0.5 ensures that the limit price will always be placed exactly in the middle of the spread.

e.g. a setting of 0.618 would place the limit price for a buy order at the best bid price + 61.8% of the current bid-ask spread; conversely for a sell it would be the lowest ask price - 61.8% of the current bid-ask spread.

Limit price must be divisible by the intrument price step i.e. in the case where the spread equals the minimum price increment, an order will be submitted that matches the current best bid or ask price.

## Order Management

The application will submit an initial limit order at best bid/ask and then after the timeout period, cancel that and submit a market order.
