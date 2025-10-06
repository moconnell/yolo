using System.Runtime.CompilerServices;
using Unravel.Api.Config;
using Unravel.Api.Data;
using YoloAbstractions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;

namespace Unravel.Api;

public class UnravelApiService : IGetFactors
{
    private const string Ur = nameof(Ur);
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

    public bool RequireTickers => true;

    public async Task<IReadOnlyDictionary<string, Dictionary<FactorType, Factor>>> GetFactorsAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, Dictionary<FactorType, Factor>>();
        await foreach (var factorKvp in GetFactorsImplAsync(tickers, cancellationToken))
        {
            foreach (var factor in factorKvp.Value)
            {
                if (values.TryGetValue(factor.Ticker, out var factors))
                {
                    factors[factor.Type] = factor;
                }
                else
                {
                    values.Add(factor.Ticker, new Dictionary<FactorType, Factor> { { factor.Type, factor } });
                }
            }
        }

        return values;
    }

    private async IAsyncEnumerable<KeyValuePair<FactorType, IEnumerable<Factor>>> GetFactorsImplAsync(
        IEnumerable<string> tickers,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var baseUrl = $"{_config.ApiBaseUrl}{_config.FactorsUrlPath}";
        var tickersCsv = tickers.ToCsv();

        foreach (var fc in _config.Factors)
        {
            var factorUrl = string.Format(baseUrl, fc.Id, tickersCsv);
            var factorResponse = await _httpClient
                .GetAsync<FactorResponse, decimal>(factorUrl, _headers, cancellationToken);
            var factors = ToFactors(factorResponse, fc.Type);

            yield return new KeyValuePair<FactorType, IEnumerable<Factor>>(fc.Type, factors);
        }
    }

    private static IEnumerable<Factor> ToFactors(FactorResponse response, FactorType factorType)
    {
        for (var i = 0; i < response.Data.Count; i++)
        {
            var ticker = response.Tickers[i];
            var value = response.Data[i];
            yield return new Factor($"{Ur}.{factorType}", factorType, ticker, null, value, response.TimeStamp);
        }
    }
}