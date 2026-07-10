using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Infrastructure;

namespace YoloFunk.Functions;

public sealed class TradeIngestionDurableWorkflow(
    IServiceProvider serviceProvider,
    ILogger<TradeIngestionDurableWorkflow> logger)
{
    public const string OrchestratorName = nameof(RunTradeIngestionOrchestrator);
    public const string ActivityName = nameof(RunTradeIngestionActivity);

    [Function(OrchestratorName)]
    public static async Task RunTradeIngestionOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<TradeIngestionRequest>() ?? throw new InvalidOperationException("Missing orchestration input.");
        _ = await context.CallActivityAsync<TradeIngestionActivityResult>(ActivityName, input);
    }

    [Function(ActivityName)]
    public async Task<TradeIngestionActivityResult> RunTradeIngestionActivity(
        [ActivityTrigger] TradeIngestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ingestionService = serviceProvider.GetRequiredKeyedService<IUserTradeIngestionService>(request.StrategyKey);

            logger.LogInformation(
                "Executing durable trade ingestion activity for strategy {Strategy} (trigger: {Trigger}, requestedAtUtc: {RequestedAtUtc})",
                request.StrategyKey,
                request.Trigger,
                request.RequestedAtUtc);

            var result = await ingestionService.IngestAsync(cancellationToken);
            return new TradeIngestionActivityResult(true, result, null);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Durable trade ingestion activity failed for strategy {Strategy} (trigger: {Trigger})",
                request.StrategyKey,
                request.Trigger);

            return new TradeIngestionActivityResult(false, null, ex.Message);
        }
    }

    public static string GetInstanceId(string strategyKey) => $"trade-ingestion-{strategyKey}";

    public static async Task<(bool Started, string InstanceId, OrchestrationMetadata? Existing)> StartIfNotRunningAsync(
        DurableTaskClient durableClient,
        TradeIngestionRequest request,
        CancellationToken cancellationToken)
    {
        var instanceId = GetInstanceId(request.StrategyKey);
        var existing = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: false, cancellationToken);

        if (existing is not null &&
            (existing.IsRunning ||
             (existing.RuntimeStatus != OrchestrationRuntimeStatus.Completed &&
              existing.RuntimeStatus != OrchestrationRuntimeStatus.Failed &&
              existing.RuntimeStatus != OrchestrationRuntimeStatus.Terminated)))
        {
            return (false, instanceId, existing);
        }

        var options = new StartOrchestrationOptions { InstanceId = instanceId };
        var startedInstanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            OrchestratorName,
            request,
            options,
            cancellationToken);

        return (true, startedInstanceId, existing);
    }
}
