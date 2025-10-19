using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RobotWealth.Api.Data;
using RobotWealth.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Interfaces;

namespace RobotWealth.Api;

public class RobotWealthFactorService : IGetFactors
{
    private readonly IRobotWealthApiService _apiService;

    public RobotWealthFactorService(IRobotWealthApiService apiService) => _apiService = apiService;

    public bool IsFixedUniverse => true;

    public async Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        CancellationToken cancellationToken = default)
    {
        var weights = await _apiService.GetWeightsAsync(cancellationToken);
        var volatilities = await _apiService.GetVolatilitiesAsync(cancellationToken);
        var factorDataFrame = ToFactorDataFrame(weights, volatilities);

        return factorDataFrame;
    }

    private static FactorDataFrame ToFactorDataFrame(
        IReadOnlyList<RwWeight> weights,
        IReadOnlyList<RwVolatility> volatilities)
    {
        var orderedWeights = weights.OrderBy(x => x.Ticker).ToArray();
        var tickers = orderedWeights.Select(w => w.Ticker).ToArray();
        var carryFrame = orderedWeights.Select(w => w.CarryMegafactor).ToArray();
        var momentumFrame = orderedWeights.Select(w => w.MomentumMegafactor).ToArray();
        var trendFrame = orderedWeights.Select(w => w.TrendMegafactor).ToArray();
        var volFrame = volatilities.OrderBy(x => x.Ticker).Select(w => w.EwVol).ToArray();

        return FactorDataFrame.NewFrom(
            tickers,
            weights[0].Date,
            (FactorType.Carry, carryFrame),
            (FactorType.Momentum, momentumFrame),
            (FactorType.Trend, trendFrame),
            (FactorType.Volatility, volFrame)
        );
    }
}