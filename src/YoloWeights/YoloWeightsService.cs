using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloWeights.Data;

namespace YoloWeights;

public class YoloWeightsService : IGetFactors
{
    private readonly RobotWealthConfig _config;

    public YoloWeightsService(RobotWealthConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<FactorType, Factor>>> GetFactorsAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default)
    {
        var weightsResponse = await GetWeightsUrl().CallApiAsync<ApiResponse<Weight>, Weight>(cancellationToken);
        var weightsFactors = weightsResponse.Data.ToDictionary(w => w.Ticker, ToFactors);

        var volatilitiesResponse = await GetVolatilitiesUrl().CallApiAsync<ApiResponse<Volatility>, Volatility>(cancellationToken);
        var volatilityFactors = volatilitiesResponse.Data.ToDictionary(v => v.Ticker, ToFactor);

        var mergedFactors = new Dictionary<string, IReadOnlyDictionary<FactorType, Factor>>();
        foreach (var kvp in weightsFactors)
        {
            var factorsList = kvp.Value.ToDictionary(f => f.Type, f => f);
            if (volatilityFactors.TryGetValue(kvp.Key, out Factor? value))
            {
                factorsList[FactorType.Volatility] = value;
            }
            mergedFactors[kvp.Key] = factorsList;
        }

        return mergedFactors;
    }

    private Factor ToFactor(Volatility volatility)
    {
        return new Factor(
            $"RobotWealth.{FactorType.Volatility}",
            FactorType.Volatility,
            volatility.Ticker,
            0,
            Convert.ToDecimal(volatility.EwVol),
            DateTime.ParseExact(volatility.Date, _config.DateFormat, CultureInfo.InvariantCulture)
        );
    }

    private IEnumerable<Factor> ToFactors(Weight weight)
    {
        var refPrice = Convert.ToDecimal(weight.ArrivalPrice);
        var timeStamp = DateTime.ParseExact(weight.Date, _config.DateFormat, CultureInfo.InvariantCulture);

        yield return new Factor(
            $"RobotWealth.{FactorType.Carry}",
            FactorType.Carry,
            weight.Ticker,
            refPrice,
            Convert.ToDecimal(weight.CarryMegafactor),
            timeStamp
        );

        yield return new Factor(
            $"RobotWealth.{FactorType.Momentum}",
            FactorType.Momentum,
            weight.Ticker,
            refPrice,
            Convert.ToDecimal(weight.MomentumMegafactor),
            timeStamp
        );

        yield return new Factor(
            $"RobotWealth.{FactorType.Trend}",
            FactorType.Trend,
            weight.Ticker,
            refPrice,
            Convert.ToDecimal(weight.TrendMegafactor),
            timeStamp
        );
    }

    private string GetVolatilitiesUrl() => GetUrl(_config.VolatilitiesUrlPath);

    private string GetWeightsUrl() => GetUrl(_config.WeightsUrlPath);

    private string GetUrl(string path) => $"{_config.ApiBaseUrl}/{path}?api_key={Uri.EscapeDataString(_config.ApiKey)}";
}