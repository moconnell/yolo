using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public sealed class UnravelDailyManualTradeIngestion(
    IServiceProvider serviceProvider,
    ILogger<UnravelDailyManualTradeIngestion> logger)
    : ManualTradeIngestionFunctionBase(serviceProvider, logger)
{
    private const string StrategyKeyConstant = "unraveldaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(UnravelDailyManualTradeIngestion))]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = $"trade-ingestion/{StrategyKeyConstant}")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        return RunManualTradeIngestionAsync(req, cancellationToken);
    }
}
