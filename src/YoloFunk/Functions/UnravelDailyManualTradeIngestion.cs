using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
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
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return RunManualTradeIngestionAsync(req, durableClient, cancellationToken);
    }

    [Function("UnravelDailyTradeIngestionStatus")]
    public Task<HttpResponseData> Status(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"trade-ingestion/{StrategyKeyConstant}/status")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return GetStatusAsync(req, durableClient, cancellationToken);
    }
}
