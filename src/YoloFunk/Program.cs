using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YoloFunk.Extensions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var config = builder.Configuration;

builder.Services.AddLogging()
                .AddHttpClient()
                .AddSingleton<IConfiguration>(config);

// Raw API payload persistence (Blob/Table Storage)
builder.Services.AddAzureStorage(config);

// Register each strategy from configuration
var strategiesSection = config.GetSection("Strategies");
foreach (var strategySection in strategiesSection.GetChildren())
{
    var strategyKey = strategySection.Key.ToLowerInvariant();
    builder.Services.AddStrategy(config, strategyKey, $"Strategies:{strategySection.Key}");
}

builder.Build().Run();
