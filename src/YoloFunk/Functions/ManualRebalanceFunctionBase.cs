using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;

using static Microsoft.DurableTask.Client.OrchestrationRuntimeStatus;
using static System.Net.HttpStatusCode;

namespace YoloFunk.Functions;

public abstract class ManualRebalanceFunctionBase
{
    private readonly ILogger _logger;

    protected ManualRebalanceFunctionBase(ILogger logger)
    {
        _logger = logger;
    }

    protected abstract string StrategyKey { get; }

    protected async Task<HttpResponseData> RunManualRebalanceAsync(
        HttpRequestData req,
        DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Strategy} manual rebalance triggered at: {executionTime}",
            StrategyKey, DateTime.UtcNow);

        var request = new RebalanceRequest(
            StrategyKey,
            "manual",
            DateTime.UtcNow);

        try
        {
            var (started, instanceId, existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
                durableClient,
                request,
                cancellationToken);

            var orchestrationStatus = started
                ? "Scheduled"
                : (existing?.RuntimeStatus.ToString() ?? "Unknown");

            var statusCode = started
                ? Accepted
                : ResolveStatusCodeForExisting(existing?.RuntimeStatus);

            var response = req.CreateResponse(statusCode);
            var payload = new RebalanceStartResponse(
                StrategyKey,
                instanceId,
                started,
                orchestrationStatus,
                DateTime.UtcNow);
            await response.WriteAsJsonAsync(payload, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual rebalance orchestration for {Strategy}", StrategyKey);

            var errorResponse = req.CreateResponse(InternalServerError);
            await errorResponse.WriteAsJsonAsync(
                new RebalanceErrorResponse(StrategyKey, "Failed to start rebalance", "An internal error occurred. Check logs for details."),
                cancellationToken);
            return errorResponse;
        }
    }

    protected virtual HttpStatusCode ResolveStatusCodeForExisting(OrchestrationRuntimeStatus? runtimeStatus)
    {
        if (runtimeStatus is null)
            return NotFound;

        return runtimeStatus switch
        {
            Pending or Running or Completed => OK,
            Failed => InternalServerError,
            Terminated => Conflict,
            _ => OK
        };
    }

    protected async Task<HttpResponseData> GetStatusAsync(
        HttpRequestData req,
        DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var instanceId = RebalanceDurableWorkflow.GetInstanceId(StrategyKey);
        var status = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: false, cancellationToken);

        if (status is null)
        {
            var notFound = req.CreateResponse(NotFound);
            await notFound.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "No orchestration found",
                    $"InstanceId: {instanceId}"),
                cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(OK);
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