using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Infrastructure;

using static Microsoft.DurableTask.Client.OrchestrationRuntimeStatus;
using static System.Net.HttpStatusCode;

namespace YoloFunk.Functions;

public abstract class ManualTradeIngestionFunctionBase(
    IServiceProvider serviceProvider,
    ILogger logger)
{
    protected abstract string StrategyKey { get; }

    protected async Task<HttpResponseData> RunManualTradeIngestionAsync(
        HttpRequestData req,
        DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "{Strategy} manual trade ingestion triggered at: {ExecutionTime}",
            StrategyKey,
            DateTime.UtcNow);

        var ingestionService = serviceProvider.GetKeyedService<IUserTradeIngestionService>(StrategyKey);
        if (ingestionService is null)
        {
            var unavailable = req.CreateResponse(ServiceUnavailable);
            await unavailable.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "Trade ingestion is not configured",
                    "AzureWebJobsStorage must be configured before trade ingestion services are registered."),
                cancellationToken);
            return unavailable;
        }

        var request = new TradeIngestionRequest(
            StrategyKey,
            "manual",
            DateTime.UtcNow);

        try
        {
            var (started, instanceId, existing) = await TradeIngestionDurableWorkflow.StartIfNotRunningAsync(
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
            await response.WriteAsJsonAsync(
                new TradeIngestionStartResponse(
                    StrategyKey,
                    instanceId,
                    started,
                    orchestrationStatus,
                    request.RequestedAtUtc),
                cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting manual trade ingestion orchestration for {Strategy}", StrategyKey);

            var errorResponse = req.CreateResponse(InternalServerError);
            await errorResponse.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "Failed to start trade ingestion",
                    "An internal error occurred. Check logs for details."),
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
        var instanceId = TradeIngestionDurableWorkflow.GetInstanceId(StrategyKey);
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
            new TradeIngestionStatusResponse(
                StrategyKey,
                instanceId,
                status.RuntimeStatus.ToString(),
                status.CreatedAt,
                status.LastUpdatedAt),
            cancellationToken);
        return response;
    }
}
