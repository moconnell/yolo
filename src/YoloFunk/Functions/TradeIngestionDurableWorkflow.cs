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
    public const string PlanActivityName = nameof(PlanTradeIngestionActivity);
    public const string WindowActivityName = nameof(RunTradeIngestionWindowActivity);
    public const string CompleteActivityName = nameof(CompleteTradeIngestionActivity);

    [Function(OrchestratorName)]
    public static async Task RunTradeIngestionOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<TradeIngestionRequest>() ?? throw new InvalidOperationException("Missing orchestration input.");
        var retryOptions = CreateActivityRetryOptions();
        var plan = await context.CallActivityAsync<UserTradeIngestionPlan>(
            PlanActivityName,
            new TradeIngestionPlanActivityRequest(
                input.StrategyKey,
                input.Trigger,
                input.RequestedAtUtc,
                context.CurrentUtcDateTime),
            retryOptions);

        var tradeCount = 0;
        foreach (var window in plan.Windows)
        {
            var windowResult = await context.CallActivityAsync<UserTradeIngestionWindowResult>(
                WindowActivityName,
                new TradeIngestionWindowActivityRequest(
                    input.StrategyKey,
                    window.StartUtc,
                    window.EndUtc),
                retryOptions);

            tradeCount += windowResult.TradeCount;
        }

        _ = await context.CallActivityAsync<UserTradeIngestionResult>(
            CompleteActivityName,
            new TradeIngestionCompleteActivityRequest(
                input.StrategyKey,
                plan.StartUtc,
                plan.EndUtc,
                plan.Windows.Count,
                tradeCount),
            retryOptions);
    }

    private static TaskOptions CreateActivityRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2,
            maxRetryInterval: TimeSpan.FromMinutes(1),
            retryTimeout: TimeSpan.FromMinutes(5)));
    }

    [Function(PlanActivityName)]
    public async Task<UserTradeIngestionPlan> PlanTradeIngestionActivity(
        [ActivityTrigger] TradeIngestionPlanActivityRequest request,
        CancellationToken cancellationToken)
    {
        var ingestionService = serviceProvider.GetRequiredKeyedService<IUserTradeIngestionService>(request.StrategyKey);

        logger.LogInformation(
            "Planning durable trade ingestion for strategy {Strategy} (trigger: {Trigger}, requestedAtUtc: {RequestedAtUtc}, endUtc: {EndUtc})",
            request.StrategyKey,
            request.Trigger,
            request.RequestedAtUtc,
            request.EndUtc);

        var plan = await ingestionService.PlanAsync(request.EndUtc, cancellationToken);
        logger.LogInformation(
            "Planned durable trade ingestion for strategy {Strategy}: {WindowCount} window(s), {StartUtc} to {EndUtc}",
            request.StrategyKey,
            plan.Windows.Count,
            plan.StartUtc,
            plan.EndUtc);

        return plan;
    }

    [Function(WindowActivityName)]
    public async Task<UserTradeIngestionWindowResult> RunTradeIngestionWindowActivity(
        [ActivityTrigger] TradeIngestionWindowActivityRequest request,
        CancellationToken cancellationToken)
    {
        var ingestionService = serviceProvider.GetRequiredKeyedService<IUserTradeIngestionService>(request.StrategyKey);

        logger.LogInformation(
            "Executing durable trade ingestion window for strategy {Strategy} from {StartUtc} to {EndUtc}",
            request.StrategyKey,
            request.StartUtc,
            request.EndUtc);

        var result = await ingestionService.IngestWindowAsync(
            new UserTradeIngestionWindow(request.StartUtc, request.EndUtc),
            cancellationToken);

        logger.LogInformation(
            "Completed durable trade ingestion window for strategy {Strategy}: {TradeCount} trade(s), {StartUtc} to {EndUtc}",
            request.StrategyKey,
            result.TradeCount,
            result.StartUtc,
            result.EndUtc);

        return result;
    }

    [Function(CompleteActivityName)]
    public async Task<UserTradeIngestionResult> CompleteTradeIngestionActivity(
        [ActivityTrigger] TradeIngestionCompleteActivityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ingestionService = serviceProvider.GetRequiredKeyedService<IUserTradeIngestionService>(request.StrategyKey);

            logger.LogInformation(
                "Completing durable trade ingestion for strategy {Strategy}: {TradeCount} trade(s) across {WindowCount} window(s), {StartUtc} to {EndUtc}",
                request.StrategyKey,
                request.TradeCount,
                request.WindowCount,
                request.StartUtc,
                request.EndUtc);

            var result = await ingestionService.CompleteAsync(
                request.StartUtc,
                request.EndUtc,
                request.WindowCount,
                request.TradeCount,
                cancellationToken);

            logger.LogInformation(
                "Completed durable trade ingestion for strategy {Strategy}: {TradeCount} trade(s) across {WindowCount} window(s), {StartUtc} to {EndUtc}",
                request.StrategyKey,
                result.TradeCount,
                result.WindowCount,
                result.StartUtc,
                result.EndUtc);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Durable trade ingestion completion failed for strategy {Strategy}",
                request.StrategyKey);

            throw;
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
