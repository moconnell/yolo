using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public class YoloDailyEffectiveWeights(IServiceProvider serviceProvider, ILogger<YoloDailyEffectiveWeights> logger)
    : EffectiveWeightsFunctionBase(serviceProvider, logger)
{
    private const string StrategyKeyConstant = "yolodaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(YoloDailyEffectiveWeights))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"rebalance/{StrategyKeyConstant}/effective-weights")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        return await GetEffectiveWeightsAsync(req, cancellationToken);
    }
}