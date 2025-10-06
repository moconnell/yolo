using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using RobotWealth.Api.Config;
using RobotWealth.Api.Data;

namespace RobotWealth.Api;

public class RobotWealthApiService : IGetFactors
{
    private readonly HttpClient _httpClient;
    private readonly RobotWealthConfig _config;

    public RobotWealthApiService(HttpClient httpClient, RobotWealthConfig config)
    {
        _httpClient = httpClient;
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<IReadOnlyDictionary<string, Dictionary<FactorType, Factor>>> GetFactorsAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default)
    {
        var url = GetWeightsUrl();
        var weightsResponse =
            await _httpClient.GetAsync<RwApiResponse<RwWeight>, RwWeight>(
                url,
                cancellationToken: cancellationToken);
        var weightsFactors = weightsResponse.Data.ToDictionary(w => w.Ticker, ToFactors);

        url = GetVolatilitiesUrl();
        var volatilitiesResponse =
            await _httpClient.GetAsync<RwApiResponse<RwVolatility>, RwVolatility>(
                url,
                cancellationToken: cancellationToken);
        var volatilityFactors = volatilitiesResponse.Data.ToDictionary(v => v.Ticker, ToFactor);

        var mergedFactors = new Dictionary<string, Dictionary<FactorType, Factor>>();
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

    private Factor ToFactor(RwVolatility volatility) =>
        new(
            $"RobotWealth.{FactorType.Volatility}",
            FactorType.Volatility,
            volatility.Ticker,
            0,
            Convert.ToDecimal(volatility.EwVol),
            DateTime.ParseExact(volatility.Date, _config.DateFormat, CultureInfo.InvariantCulture)
        );

    private IEnumerable<Factor> ToFactors(RwWeight weight)
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