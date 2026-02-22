using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public class UnravelDailyManualRebalance(ILogger<UnravelDailyManualRebalance> logger) : ManualRebalanceFunctionBase(logger)
{
    private const string StrategyKeyConstant = "unraveldaily";
    protected override string StrategyKey => StrategyKeyConstant;

    [Function(nameof(UnravelDailyManualRebalance))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = $"rebalance/{StrategyKeyConstant}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return await RunManualRebalanceAsync(req, durableClient, cancellationToken);
    }

    [Function("UnravelDailyRebalanceStatus")]
    public async Task<HttpResponseData> Status(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"rebalance/{StrategyKeyConstant}/status")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return await GetStatusAsync(req, durableClient, cancellationToken);
    }
}
