using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk;

public class YoloManualRebalance
{
    private const string StrategyKey = "momentumdaily";

    private readonly ILogger<YoloManualRebalance> _logger;
    private readonly ICommand _rebalanceCommand;

    public YoloManualRebalance(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<YoloManualRebalance> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function("MomentumDailyManual")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "rebalance/momentum-daily")]
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
