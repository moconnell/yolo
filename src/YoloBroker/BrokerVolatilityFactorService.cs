using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloBroker.Interface;

namespace YoloBroker;

public class BrokerVolatilityFactorService(IYoloBroker broker) : IGetFactors
{
    private const int Periods = 30;

    public bool IsFixedUniverse => false;

    public int Order => 1000;

    public async Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        ISet<FactorType>? factors = null,
        CancellationToken cancellationToken = default)
    {
        if (tickers is null)
            return FactorDataFrame.Empty;
        var tickerArray = tickers.ToArray();
        if (tickerArray.Length == 0 || factors?.Contains(FactorType.Volatility) == true)
            return FactorDataFrame.Empty;

        var tasks = tickerArray.Select(t => broker.GetDailyClosePricesAsync(t, Periods, cancellationToken));
        var priceArrays = await Task.WhenAll(tasks);
        var volatilities = priceArrays.Select(prices => prices.AnnualizedVolatility()).ToArray();
        var df = FactorDataFrame.NewFrom(tickerArray, DateTime.Today, (FactorType.Volatility, volatilities));

        return df;
    }
}