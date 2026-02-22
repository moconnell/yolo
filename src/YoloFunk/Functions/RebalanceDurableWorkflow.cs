using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Functions;

public class RebalanceDurableWorkflow
{
    public const string OrchestratorName = nameof(RunRebalanceOrchestrator);
    public const string ActivityName = nameof(RunRebalanceActivity);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RebalanceDurableWorkflow> _logger;

    public RebalanceDurableWorkflow(IServiceProvider serviceProvider, ILogger<RebalanceDurableWorkflow> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [Function(OrchestratorName)]
    public static async Task RunRebalanceOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<RebalanceRequest>()
                ?? throw new InvalidOperationException("Missing orchestration input.");

        await context.CallActivityAsync(ActivityName, input);
    }

    [Function(ActivityName)]
    public async Task<RebalanceResult> RunRebalanceActivity([ActivityTrigger] RebalanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var rebalanceCommand = _serviceProvider.GetRequiredKeyedService<ICommand>(request.StrategyKey);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Executing durable rebalance activity for strategy {Strategy} (trigger: {Trigger}, requestedAtUtc: {RequestedAtUtc})",
                    request.StrategyKey,
                    request.Trigger,
                    request.RequestedAtUtc);
            }

            await rebalanceCommand.ExecuteAsync(cancellationToken);
            return new RebalanceResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Durable rebalance activity failed for strategy {Strategy} (trigger: {Trigger})",
                request.StrategyKey,
                request.Trigger);
            return new RebalanceResult(false, ex.Message);
        }
    }

    public sealed record RebalanceResult(bool Success, string? ErrorMessage);

    public sealed record RebalanceRequest(string StrategyKey, string Trigger, DateTime RequestedAtUtc);

    public static string GetInstanceId(string strategyKey) => $"rebalance-{strategyKey}";

    public static async Task<(bool Started, string InstanceId, OrchestrationMetadata? Existing)> StartIfNotRunningAsync(
        DurableTaskClient durableClient,
        RebalanceRequest request,
        CancellationToken cancellationToken)
    {
        var instanceId = GetInstanceId(request.StrategyKey);
        var existing = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: false, cancellationToken);

        if (existing?.IsRunning == true)
            return (false, instanceId, existing);

        var options = new StartOrchestrationOptions { InstanceId = instanceId };
        var startedInstanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            OrchestratorName,
            request,
            options,
            cancellationToken);

        return (true, startedInstanceId, existing);
    }
}
