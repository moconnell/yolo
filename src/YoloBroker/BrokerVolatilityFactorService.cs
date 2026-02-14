using YoloAbstractions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloBroker.Interface;

namespace YoloBroker;

public class BrokerVolatilityFactorService(IYoloBroker broker, bool throwOnMissingData = true) : IGetFactors
{
    private const int Periods = 120;

    public bool IsFixedUniverse => false;

    public int Order => 1000;

    public async Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        ISet<FactorType>? existingFactors = null,
        CancellationToken cancellationToken = default)
    {
        if (tickers is null)
            return FactorDataFrame.Empty;
        var tickerArray = tickers.ToArray();
        if (tickerArray.Length == 0 || existingFactors?.Contains(FactorType.Volatility) == true)
            return FactorDataFrame.Empty;

        var priceArrays = await Task.WhenAll(tickerArray.Select(async t =>
        {
            try
            {
                return await broker.GetDailyClosePricesAsync(t, Periods, ct: cancellationToken);
            }
            catch (Exception) when (!throwOnMissingData)
            {
                return [];
            }
        }));

        var volatilities = priceArrays.Select(prices => prices.AnnualizedVolatility(throwOnMissingData: throwOnMissingData)).ToArray();
        var df = FactorDataFrame.NewFrom(tickerArray, DateTime.Today, (FactorType.Volatility, volatilities));

        return df;
    }
}