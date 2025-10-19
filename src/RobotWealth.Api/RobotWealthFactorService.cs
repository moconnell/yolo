using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RobotWealth.Api.Data;
using RobotWealth.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Exceptions;
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
        if (!weights.Any())
            throw new ApiException("No weights returned");
        
        var volatilities = await _apiService.GetVolatilitiesAsync(cancellationToken);
        if (!volatilities.Any())
            throw new ApiException("No volatilities returned");
        
        var factorDataFrame = ToFactorDataFrame(weights, volatilities);

        return factorDataFrame;
    }

    private static FactorDataFrame ToFactorDataFrame(
        IReadOnlyList<RwWeight> weights,
        IReadOnlyList<RwVolatility> volatilities)
    {
        var orderedWeights = weights.OrderBy(x => x.Ticker).ToArray();
        var weightsTickers = orderedWeights.Select(w => w.Ticker).ToArray();
        var carryFrame = orderedWeights.Select(w => w.CarryMegafactor).ToArray();
        var momentumFrame = orderedWeights.Select(w => w.MomentumMegafactor).ToArray();
        var trendFrame = orderedWeights.Select(w => w.TrendMegafactor).ToArray();
        var orderedVolatilities = volatilities.OrderBy(x => x.Ticker).ToArray();
        var volTickers = orderedVolatilities.Select(x => x.Ticker).ToArray();
        if (weightsTickers.Length != volTickers.Length ||
            weightsTickers.Except(volTickers).Any() ||
            volTickers.Except(weightsTickers).Any())
            throw new ApiException("Weights/volatilities tickers mismatch");
        var volFrame = orderedVolatilities.Select(w => w.EwVol).ToArray();

        return FactorDataFrame.NewFrom(
            weightsTickers,
            weights[0].Date,
            (FactorType.Carry, carryFrame),
            (FactorType.Momentum, momentumFrame),
            (FactorType.Trend, trendFrame),
            (FactorType.Volatility, volFrame)
        );
    }
}