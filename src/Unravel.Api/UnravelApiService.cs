using System.Runtime.CompilerServices;
using MathNet.Numerics.LinearAlgebra;
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
        ArgumentNullException.ThrowIfNull(tickers);
        var tickersArray = tickers as string[] ?? tickers.ToArray();
        if (tickersArray.Length == 0)
        {
            throw new ArgumentException("No tickers provided", nameof(tickers));
        }

        var values = new Dictionary<string, Dictionary<FactorType, Factor>>();
        await foreach (var factorKvp in GetFactorsImplAsync(tickersArray, cancellationToken))
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
        IReadOnlyList<string> tickers,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var baseUrl = $"{_config.ApiBaseUrl}/{_config.FactorsUrlPath}";

        foreach (var fc in _config.Factors)
        {
            var factors = await GetTickerZScoreFactorsAsync(tickers, baseUrl, fc, cancellationToken);
            yield return new KeyValuePair<FactorType, IEnumerable<Factor>>(fc.Type, factors);
        }
    }

    private async Task<IEnumerable<Factor>> GetTickerZScoreFactorsAsync(
        IReadOnlyList<string> tickers,
        string baseUrl,
        FactorConfig fc,
        CancellationToken cancellationToken)
    {
        var from = DateTime.Today.AddDays(-fc.Window);
        var url = string.Format(baseUrl, fc.Id, tickers.ToCsv().ToUpperInvariant(), from.ToString(_config.DateFormat));
        var response = await _httpClient
            .GetAsync<MultiTickerResponse, double[]>(url, _headers, cancellationToken);
        var factors = ToZScoreFactors(response, fc.Type);
        return factors;
    }

    private static IEnumerable<Factor> ToZScoreFactors(MultiTickerResponse response, FactorType factorType)
    {
        var matrix = Matrix<double>.Build.DenseOfRowArrays(response.Data);
        var zScores = matrix.ZScoreColumns();
        var lastRow = response.Data.Count - 1;
        for (var i = 0; i < matrix.ColumnCount; i++)
        {
            var timeStamp = response.Index[^1];
            var ticker = response.Tickers[i];
            var value = Convert.ToDecimal(zScores[lastRow, i]);
            yield return new Factor($"{Ur}.{factorType}", factorType, ticker, null, value, timeStamp);
        }
    }
}