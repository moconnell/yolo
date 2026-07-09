using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Infrastructure;

namespace YoloFunk.Functions;

public abstract class ManualTradeIngestionFunctionBase(
    IServiceProvider serviceProvider,
    ILogger logger)
{
    protected abstract string StrategyKey { get; }

    protected async Task<HttpResponseData> RunManualTradeIngestionAsync(
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "{Strategy} manual trade ingestion triggered at: {ExecutionTime}",
            StrategyKey,
            DateTime.UtcNow);

        var ingestionService = serviceProvider.GetKeyedService<IUserTradeIngestionService>(StrategyKey);
        if (ingestionService is null)
        {
            var unavailable = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await unavailable.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "Trade ingestion is not configured",
                    "AzureWebJobsStorage must be configured before trade ingestion services are registered."),
                cancellationToken);
            return unavailable;
        }

        try
        {
            var result = await ingestionService.IngestAsync(cancellationToken);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running manual trade ingestion for {Strategy}", StrategyKey);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "Failed to run trade ingestion",
                    "An internal error occurred. Check logs for details."),
                cancellationToken);
            return errorResponse;
        }
    }
}
