using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;

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
            var (Started, InstanceId, Existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
                durableClient,
                request,
                cancellationToken);

            var response = req.CreateResponse(Started ? HttpStatusCode.Accepted : HttpStatusCode.OK);
            var payload = new RebalanceStartResponse(
                StrategyKey,
                InstanceId,
                Started,
                Started
                    ? "Scheduled"
                    : (Existing?.RuntimeStatus.ToString() ?? "Unknown"),
                DateTime.UtcNow);
            await response.WriteAsJsonAsync(payload, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual rebalance orchestration for {Strategy}", StrategyKey);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(
                new RebalanceErrorResponse(StrategyKey, "Failed to start rebalance", ex.Message),
                cancellationToken);
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
            await notFound.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "No orchestration found",
                    $"InstanceId: {instanceId}"),
                cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(
            new RebalanceStatusResponse(
                StrategyKey,
                instanceId,
                status.RuntimeStatus.ToString(),
                status.CreatedAt,
                status.LastUpdatedAt),
            cancellationToken);
        return response;
    }
}
