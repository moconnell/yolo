using Unravel.Api.Config;
using Unravel.Api.Data;
using Unravel.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Extensions;

namespace Unravel.Api;

public class UnravelApiService : IUnravelApiService
{
    private const string ApiKeyHeader = "X-API-KEY";

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
        CancellationToken cancellationToken = default)
    {
        if (_config.Factors.Length == 0)
            return FactorDataFrame.Empty;

        ArgumentNullException.ThrowIfNull(tickers);
        var tickersArray = tickers.Select(x => x.ToUpperInvariant()).Distinct().ToArray();
        if (tickersArray.Length == 0)
            throw new ArgumentException("Tickers cannot be empty.", nameof(tickers));

        var baseUrl = $"{_config.ApiBaseUrl}/{_config.UrlPathFactorsLive}";
        var tickersCsv = tickersArray.ToCsv().ToUpperInvariant();
        var results = new List<FactorDataFrame>();

        foreach (var fc in _config.Factors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = string.Format(baseUrl, fc.Id, tickersCsv);
            var response = await _httpClient.GetAsync<FactorsLiveResponse, double>(url, _headers, cancellationToken);
            var dataFrame = FactorDataFrame.NewFrom(response.Tickers, response.TimeStamp, (fc.Type, response.Data));

            results.Add(dataFrame);
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
        var tickers = response.Tickers.Where((_, i) => lastRow[i] == 1).ToArray();
        return tickers;
    }
}