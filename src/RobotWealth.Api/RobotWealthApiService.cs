using System;
using System.Collections.Generic;
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
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool RequireTickers => false;

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
            if (volatilityFactors.TryGetValue(kvp.Key, out var factor))
            {
                factorsList[FactorType.Volatility] = factor;
            }

            mergedFactors[kvp.Key] = factorsList;
        }

        return mergedFactors;
    }

    private static Factor ToFactor(RwVolatility volatility) =>
        new(
            $"{Rw}.{FactorType.Volatility}",
            FactorType.Volatility,
            volatility.Ticker,
            null,
            volatility.EwVol,
            volatility.Date
        );

    private IEnumerable<Factor> ToFactors(RwWeight weight)
    {
        yield return new Factor(
            $"{Rw}.{FactorType.Carry}",
            FactorType.Carry,
            weight.Ticker,
            weight.ArrivalPrice,
            weight.CarryMegafactor,
            weight.Date
        );

        yield return new Factor(
            $"{Rw}.{FactorType.Momentum}",
            FactorType.Momentum,
            weight.Ticker,
            weight.ArrivalPrice,
            weight.MomentumMegafactor,
            weight.Date
        );

        yield return new Factor(
            $"{Rw}.{FactorType.Trend}",
            FactorType.Trend,
            weight.Ticker,
            weight.ArrivalPrice,
            weight.TrendMegafactor,
            weight.Date
        );
    }

    private const string Rw = nameof(Rw);

    private string GetVolatilitiesUrl() => GetUrl(_config.VolatilitiesUrlPath);

    private string GetWeightsUrl() => GetUrl(_config.WeightsUrlPath);

    private string GetUrl(string path) => $"{_config.ApiBaseUrl}/{path}?api_key={Uri.EscapeDataString(_config.ApiKey)}";
}