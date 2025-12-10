using Unravel.Api.Config;
using Unravel.Api.Data;
using Unravel.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Extensions;

namespace Unravel.Api;

public class UnravelApiService : IUnravelApiService
{
    private const string ApiKeyHeader = "X-API-KEY";
    private static readonly Dictionary<FactorType, string> FactorTypeToStringMap = new()
    {
        { FactorType.Carry, "carry_enhanced" },
        { FactorType.Momentum, "momentum_enhanced" },
        { FactorType.OpenInterestDivergence, "open_interest_divergence" },
        { FactorType.RelativeIlliquidity, "relative_illiquidity" },
        { FactorType.RetailFlow, "retail_flow" },
        { FactorType.SupplyVelocity, "supply_velocity" },
        { FactorType.TrendLongonlyAdaptive, "trend_longonly_adaptive" },
    };

    private readonly HttpClient _httpClient;
    private readonly UnravelConfig _config;
    private readonly Dictionary<string, string> _headers;

    public UnravelApiService(HttpClient httpClient, UnravelConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _headers = new Dictionary<string, string>
        {
            { ApiKeyHeader, _config.ApiKey }
        };
    }

    public async Task<FactorDataFrame> GetFactorsLiveAsync(
        IEnumerable<string> tickers,
        bool throwOnMissingValue = false,
        CancellationToken cancellationToken = default)
    {
        if (_config.Factors.Length == 0)
            return FactorDataFrame.Empty;

        ArgumentNullException.ThrowIfNull(tickers);
        var tickerList = tickers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        if (tickerList.Count == 0)
            throw new ArgumentException("Tickers cannot be empty.", nameof(tickers));

        var baseUrl = $"{_config.ApiBaseUrl}/{_config.UrlPathFactorsLive}";
        var tickersCsv = Uri.EscapeDataString(tickerList.ToCsv());
        var results = new List<FactorDataFrame>();

        foreach (var fac in _config.Factors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!FactorTypeToStringMap.TryGetValue(fac, out var factorTypeString))
                throw new InvalidOperationException($"Factor type {fac} is not supported.");
            var url = string.Format(baseUrl, factorTypeString, tickersCsv);
            var response = await _httpClient.GetAsync<FactorsLiveResponse, double?>(url, _headers, cancellationToken);

            if (response.Tickers.SequenceEqual(tickerList, StringComparer.OrdinalIgnoreCase))
            {
                var dataFrame = FactorDataFrame.NewFrom(response.Tickers, response.TimeStamp, (fac, response.Data.Select(d => d ?? double.NaN).ToArray()));
                results.Add(dataFrame);
                continue;
            }

            var missingTickers = tickerList.Except(response.Tickers).ToArray();
            if (missingTickers.Length > 0)
            {
                if (throwOnMissingValue)
                    throw new ApiException(
                        $"Not all requested tickers were returned for {fac}: {string.Join(", ", missingTickers)}");

                var values = response.Data.Select(d => d ?? double.NaN).ToList();
                foreach (var missingTicker in missingTickers)
                {
                    var insertIndex = tickerList.IndexOf(missingTicker);
                    values.Insert(insertIndex, double.NaN);
                }

                var dataFrame = FactorDataFrame.NewFrom(tickerList, response.TimeStamp, (fac, values));
                results.Add(dataFrame);
            }
            else
            {
                var unexpectedTickers = response.Tickers.Except(tickerList).ToArray();
                throw new ApiException(
                    $"Unravel API returned unexpected tickers for factor {fac}: {unexpectedTickers.ToCsv()}");
            }
        }

        return results.Aggregate((one, two) => one + two);
    }

    public async Task<IReadOnlyList<string>> GetUniverseAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = $"{_config.ApiBaseUrl}/{_config.UrlPathUniverse}";
        var exchange = _config.Exchange.ToString().ToLowerInvariant();
        var startDate = DateTime.Today.AddDays(-3).ToString(_config.DateFormat);
        var url = string.Format(baseUrl, _config.UniverseSize, exchange, startDate);
        var response = await _httpClient.GetAsync<UniverseResponse, byte?[]>(url, _headers, cancellationToken);
        if (response.Index.Count == 1 && response.Tickers.Count == _config.UniverseSize)
            return response.Tickers;

        var lastRow = response.Data[^1];
        if (lastRow.Length != response.Tickers.Count)
            throw new ApiException(
                $"Universe response malformed: lastRow length {lastRow.Length} != tickers count {response.Tickers.Count}.");

        var tickers = response.Tickers.Where((_, i) => lastRow[i] == 1).ToArray();
        return tickers;
    }
}