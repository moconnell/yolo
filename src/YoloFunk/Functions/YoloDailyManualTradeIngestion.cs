using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public sealed class YoloDailyManualTradeIngestion(
    IServiceProvider serviceProvider,
    ILogger<YoloDailyManualTradeIngestion> logger)
    : ManualTradeIngestionFunctionBase(serviceProvider, logger)
{
    private const string StrategyKeyConstant = "yolodaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(YoloDailyManualTradeIngestion))]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = $"trade-ingestion/{StrategyKeyConstant}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return RunManualTradeIngestionAsync(req, durableClient, cancellationToken);
    }

    [Function("YoloDailyTradeIngestionStatus")]
    public Task<HttpResponseData> Status(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"trade-ingestion/{StrategyKeyConstant}/status")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return GetStatusAsync(req, durableClient, cancellationToken);
    }
}
