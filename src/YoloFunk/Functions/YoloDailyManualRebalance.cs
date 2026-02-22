using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public class YoloDailyManualRebalance(ILogger<YoloDailyManualRebalance> logger) : ManualRebalanceFunctionBase(logger)
{
    private const string StrategyKeyConstant = "yolodaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(YoloDailyManualRebalance))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = $"rebalance/{StrategyKeyConstant}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return await RunManualRebalanceAsync(req, durableClient, cancellationToken);
    }

    [Function("YoloDailyRebalanceStatus")]
    public async Task<HttpResponseData> Status(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"rebalance/{StrategyKeyConstant}/status")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return await GetStatusAsync(req, durableClient, cancellationToken);
    }
}
