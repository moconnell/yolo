using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using YoloFunk.Extensions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var config = builder.Configuration;

var openTelemetry = builder.Services.AddOpenTelemetry();
openTelemetry.UseFunctionsWorkerDefaults();
if (!string.IsNullOrWhiteSpace(config["APPLICATIONINSIGHTS_CONNECTION_STRING"]) ||
    !string.IsNullOrWhiteSpace(config["ApplicationInsights:ConnectionString"]))
{
    openTelemetry.UseAzureMonitor();
}

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
