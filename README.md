# yolo
Automation for the RobotWealth cryptocurrency YOLO strategies

## YoloKonsole.exe

There is a pre-built win64 console app in the releases section, which can be scheduled to run daily.

### HOWTO (win-x64):
- download YoloKonsole.exe and appsettings.json and save to a new folder on your computer
- ensure you have .NET 6.0 runtime installed
- edit appsettings.json with the correct settings
- run YoloKonsole.exe from the command line is best initially, so that you can see the output easily (Windows+R, cmd) although file logging is also provided
- by default, the application will issue a `Proceed? (y/n)` challenge before issuing any trades to the broker
- this can be overridden by using the command-line switch `-s`

### appsettings.json

Sample configuration below. 
   
```
{
  "Logging": {
    "PathFormat": "C:\\logs\\yolo-{Date}.json",
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Yolo": {
    "WeightsUrl": "https://api.robotwealth.com/v1/yolo/weights?api_key=<API-KEY>",
    "NominalCash": 10000,
    "MaxLeverage": 1,
    "TradeBuffer": 0.02,
    "BaseAsset": "USD",
    "AssetPermissions": "SpotAndPerp",
    "SpreadSplit": 0.5
  },
  "FTX": {
    "ApiKey": "",
    "Secret": "",
    "BaseAddress": "https://ftx.com/api",
    "SubAccount": ""
  }
}
```

#### Logging/PathFormat

This determines where the application will write logs to. Windows paths using \ must be escaped as \\ as below. The substitution token `{Date}` included in the path means that a new file will be written each day. You can omit this if you would prefer to have a single file.

```
{
  "Logging": {
    "PathFormat": "C:\\logs\\yolo-{Date}.json",
```

#### BaseAsset

This is the token that the application will trade in and out of. It defaults to `USD` but you could equally change to `USDT` etc. if preferred.

#### AssetPermissions

In case your account does not have margin trading or futures enabled, it is possible to configure accordingly via the `AssetPermissions` setting - possible settings of which are currently:

```
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

#### SpreadSplit

This setting determines the placement of the limit price within the bid-ask price spread and can take any value between 0 and 1 (values greater than 1 will be treated as 1).

The default setting of 0.5 ensures that the limit price will always be placed exactly in the middle of the spread.

e.g. a setting of 0.618 would place the limit price for a sell order at the bid price + 61.8% of the current bid-ask spread; conversely for a sell it would be the ask price - 61.8% of the current bid-ask spread.
