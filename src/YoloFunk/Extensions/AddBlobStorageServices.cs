using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoloFunk.Infrastructure;

namespace YoloFunk.Extensions;

public static class AddAzureStorageServices
{
    /// <summary>
    /// Adds Azure Blob Storage and Table Storage clients for raw API payload persistence. If the AzureWebJobsStorage connection string config setting is not provided, this will be a no-op.
    /// </summary>
    /// <param name="services">Services collection</param>
    /// <param name="config">Configuration instance</param>
    /// <returns>Updated services collection</returns>
    public static IServiceCollection AddAzureStorage(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetValue<string>("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services; // No connection string, skip blob storage setup
        }

        services.AddSingleton(sp => new BlobServiceClient(connectionString));
        services.AddSingleton(sp => new TableServiceClient(connectionString));
        services.AddSingleton<RawJsonPersistenceHandler>();

        return services;
    }
}
