using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Functions;

public class YoloDailyManualRebalance
{
    private const string StrategyKey = "yolodaily";

    private readonly ILogger<YoloDailyManualRebalance> _logger;
    private readonly ICommand _rebalanceCommand;

    public YoloDailyManualRebalance(
        [FromKeyedServices(StrategyKey)] ICommand rebalanceCommand,
        ILogger<YoloDailyManualRebalance> logger)
    {
        _rebalanceCommand = rebalanceCommand;
        _logger = logger;
    }

    [Function(nameof(YoloDailyManualRebalance))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "rebalance/yolo-daily")]
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
