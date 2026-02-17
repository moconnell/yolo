using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace YoloFunk.Functions;

public class YoloDailyManualRebalance
{
    private const string StrategyKey = "yolodaily";

    private readonly ILogger<YoloDailyManualRebalance> _logger;

    public YoloDailyManualRebalance(ILogger<YoloDailyManualRebalance> logger)
    {
        _logger = logger;
    }

    [Function(nameof(YoloDailyManualRebalance))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = $"rebalance/{StrategyKey}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Strategy} manual rebalance triggered at: {executionTime}",
            StrategyKey, DateTime.UtcNow);

        var request = new RebalanceDurableWorkflow.RebalanceRequest(
            StrategyKey,
            "manual",
            DateTime.UtcNow);

        try
        {
            var startResult = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
                durableClient,
                request,
                cancellationToken);

            var response = req.CreateResponse(startResult.Started ? HttpStatusCode.Accepted : HttpStatusCode.OK);
            await response.WriteStringAsync(startResult.Started
                ? $"Rebalance started for strategy: {StrategyKey}. InstanceId: {startResult.InstanceId}"
                : $"Rebalance already running for strategy: {StrategyKey}. InstanceId: {startResult.InstanceId}, Status: {startResult.Existing?.RuntimeStatus}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual rebalance orchestration for {Strategy}", StrategyKey);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Failed to start rebalance: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("YoloDailyRebalanceStatus")]
    public async Task<HttpResponseData> Status(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"rebalance/{StrategyKey}/status")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var instanceId = RebalanceDurableWorkflow.GetInstanceId(StrategyKey);
        var status = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: false, cancellationToken);

        if (status is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"No orchestration found for strategy: {StrategyKey}. InstanceId: {instanceId}");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(
            $"Strategy: {StrategyKey}\nInstanceId: {instanceId}\nRuntimeStatus: {status.RuntimeStatus}\nCreatedAt: {status.CreatedAt:O}\nLastUpdatedAt: {status.LastUpdatedAt:O}");
        return response;
    }
}
