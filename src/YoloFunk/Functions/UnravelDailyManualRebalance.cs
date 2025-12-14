using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Functions;

public class UnravelDailyManualRebalance
{
    private const string StrategyKey = "unraveldaily";

    private readonly ILogger<UnravelDailyManualRebalance> _logger;
    private readonly ICommand _rebalanceCommand;

    public UnravelDailyManualRebalance(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<UnravelDailyManualRebalance> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function(nameof(UnravelDailyManualRebalance))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "rebalance/unravel-daily")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Strategy} manual rebalance triggered at: {executionTime}",
            StrategyKey, DateTime.UtcNow);

        try
        {
            await _rebalanceCommand.ExecuteAsync(cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Rebalance completed for strategy: {StrategyKey}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing manual rebalance for {Strategy}", StrategyKey);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Rebalance failed: {ex.Message}");
            return errorResponse;
        }
    }
}
