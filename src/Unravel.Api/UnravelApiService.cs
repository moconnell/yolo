using System.Runtime.CompilerServices;
using Unravel.Api.Config;
using Unravel.Api.Data;
using YoloAbstractions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;

namespace Unravel.Api;

public class UnravelApiService : IGetFactors
{
    private readonly UnravelConfig _config;

    public UnravelApiService(UnravelConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

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
            var factorResponse = await factorUrl
                .CallApiAsync<UrApiResponse<dynamic>, dynamic>(cancellationToken: cancellationToken);
            var factors = factorResponse.Data.Select(d => ToFactor(d)).Cast<Factor>();
            yield return new KeyValuePair<FactorType, IEnumerable<Factor>>(fc.Type, factors);
        }
    }

    private static Factor ToFactor(dynamic factor)
    {
        return new Factor(factor.Id, factor.Type, factor.Ticker, factor.RefPrice, factor.Value, factor.TimeStamp);
    }
}