using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Infrastructure;

namespace YoloFunk.Functions;

public sealed class StorageQueryFunctions(
    IServiceProvider serviceProvider,
    ILogger<StorageQueryFunctions> logger)
{
    private const string TradeExecutionsTableName = "tradeexecutions";
    private const string HttpRequestsTableName = "httprequestsindex";
    private const string HttpRequestsContainerName = "http-requests";

    [Function(nameof(GetTradeExecutions))]
    public async Task<HttpResponseData> GetTradeExecutions(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "storage/trade-executions")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!TryGetStorageClients(req, out var tableServiceClient, out _))
            return await ServiceUnavailableAsync(req, cancellationToken);

        var query = HttpQueryParameters.Parse(req.Url);
        var page = query.GetInt32("page", 1, 1, 10_000);
        var pageSize = query.GetInt32("pageSize", 100, 1, 500);
        var orderBy = query.GetString("orderBy") ?? "submittedAt";
        var direction = NormalizeDirection(query.GetString("direction"));

        try
        {
            var items = await QueryTradeExecutionsAsync(tableServiceClient, query, cancellationToken);
            items = ApplyTradeExecutionSort(items, orderBy, direction);

            return await WritePagedResponseAsync(req, items, page, pageSize, orderBy, direction, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return await WritePagedResponseAsync(
                req,
                Array.Empty<TradeExecutionQueryItem>(),
                page,
                pageSize,
                orderBy,
                direction,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query trade executions");
            return await ErrorAsync(req, "Failed to query trade executions", cancellationToken);
        }
    }

    [Function(nameof(GetHttpRequestCaptures))]
    public async Task<HttpResponseData> GetHttpRequestCaptures(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "storage/http-requests")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!TryGetStorageClients(req, out var tableServiceClient, out _))
            return await ServiceUnavailableAsync(req, cancellationToken);

        var query = HttpQueryParameters.Parse(req.Url);
        var page = query.GetInt32("page", 1, 1, 10_000);
        var pageSize = query.GetInt32("pageSize", 100, 1, 500);
        var orderBy = query.GetString("orderBy") ?? "requestTimeUtc";
        var direction = NormalizeDirection(query.GetString("direction"));

        try
        {
            var items = await QueryHttpRequestCapturesAsync(tableServiceClient, query, cancellationToken);
            items = ApplyHttpRequestCaptureSort(items, orderBy, direction);

            return await WritePagedResponseAsync(req, items, page, pageSize, orderBy, direction, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return await WritePagedResponseAsync(
                req,
                Array.Empty<HttpRequestCaptureQueryItem>(),
                page,
                pageSize,
                orderBy,
                direction,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query HTTP request captures");
            return await ErrorAsync(req, "Failed to query HTTP request captures", cancellationToken);
        }
    }

    [Function(nameof(GetHttpRequestPayload))]
    public async Task<HttpResponseData> GetHttpRequestPayload(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "storage/http-requests/payload")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!TryGetStorageClients(req, out _, out var blobServiceClient))
            return await ServiceUnavailableAsync(req, cancellationToken);

        var blobName = HttpQueryParameters.Parse(req.Url).GetString("blobName");
        if (string.IsNullOrWhiteSpace(blobName))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(
                new { Error = "Missing required query parameter: blobName" },
                cancellationToken);
            return badRequest;
        }

        try
        {
            var blobClient = blobServiceClient
                .GetBlobContainerClient(HttpRequestsContainerName)
                .GetBlobClient(blobName);
            var download = await blobClient.DownloadContentAsync(cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(download.Value.Content.ToString(), cancellationToken);
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(
                new { Error = "HTTP request payload not found", BlobName = blobName },
                cancellationToken);
            return notFound;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get HTTP request payload {BlobName}", blobName);
            return await ErrorAsync(req, "Failed to get HTTP request payload", cancellationToken);
        }
    }

    private bool TryGetStorageClients(
        HttpRequestData req,
        out TableServiceClient tableServiceClient,
        out BlobServiceClient blobServiceClient)
    {
        tableServiceClient = serviceProvider.GetService<TableServiceClient>() ??
                             req.FunctionContext.InstanceServices.GetService<TableServiceClient>()!;
        blobServiceClient = serviceProvider.GetService<BlobServiceClient>() ??
                            req.FunctionContext.InstanceServices.GetService<BlobServiceClient>()!;

        return tableServiceClient is not null && blobServiceClient is not null;
    }

    private static async Task<IReadOnlyList<TradeExecutionQueryItem>> QueryTradeExecutionsAsync(
        TableServiceClient tableServiceClient,
        HttpQueryParameters query,
        CancellationToken cancellationToken)
    {
        var strategy = query.GetString("strategy");
        var coin = query.GetString("coin");
        var runId = query.GetString("runId");
        var status = query.GetString("status");
        var from = query.GetDateTimeOffset("from");
        var to = query.GetDateTimeOffset("to");

        var tableClient = tableServiceClient.GetTableClient(TradeExecutionsTableName);
        var entities = await QueryEntitiesAsync(tableClient, BuildPartitionFilter(strategy), cancellationToken);

        return [.. entities
            .Select(ToTradeExecutionQueryItem)
            .Where(item => Matches(item.StrategyName, strategy))
            .Where(item => Matches(item.Coin, coin))
            .Where(item => Matches(item.RunId, runId))
            .Where(item => Matches(item.Status, status))
            .Where(item => !from.HasValue || (item.SubmittedAt ?? item.RecordedAt) >= from.Value)
            .Where(item => !to.HasValue || (item.SubmittedAt ?? item.RecordedAt) <= to.Value)];
    }

    private static async Task<IReadOnlyList<HttpRequestCaptureQueryItem>> QueryHttpRequestCapturesAsync(
        TableServiceClient tableServiceClient,
        HttpQueryParameters query,
        CancellationToken cancellationToken)
    {
        var host = query.GetString("host");
        var endpoint = query.GetString("endpoint");
        var method = query.GetString("method");
        var statusCode = query.GetString("statusCode");
        var contentHash = query.GetString("contentHash");
        var from = query.GetDateTimeOffset("from");
        var to = query.GetDateTimeOffset("to");

        var tableClient = tableServiceClient.GetTableClient(HttpRequestsTableName);
        var entities = await QueryEntitiesAsync(tableClient, BuildPartitionFilter(host), cancellationToken);

        return [.. entities
            .Select(ToHttpRequestCaptureQueryItem)
            .Where(item => Matches(item.Host, host))
            .Where(item => Contains(item.Endpoint, endpoint))
            .Where(item => Matches(item.Method, method))
            .Where(item => !int.TryParse(statusCode, out var expectedStatusCode) || item.StatusCode == expectedStatusCode)
            .Where(item => Matches(item.ContentHash, contentHash))
            .Where(item => !from.HasValue || item.RequestTimeUtc >= from.Value)
            .Where(item => !to.HasValue || item.RequestTimeUtc <= to.Value)];
    }

    private static async Task<IReadOnlyList<TableEntity>> QueryEntitiesAsync(
        TableClient tableClient,
        string? filter,
        CancellationToken cancellationToken)
    {
        var entities = new List<TableEntity>();
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(
                           filter,
                           maxPerPage: 1_000,
                           cancellationToken: cancellationToken))
        {
            entities.Add(entity);
        }

        return entities;
    }

    private static string? BuildPartitionFilter(string? partitionKey)
    {
        return string.IsNullOrWhiteSpace(partitionKey)
            ? null
            : $"PartitionKey eq '{SanitizeTableKey(partitionKey).Replace("'", "''")}'";
    }

    private static IReadOnlyList<TradeExecutionQueryItem> ApplyTradeExecutionSort(
        IReadOnlyList<TradeExecutionQueryItem> items,
        string orderBy,
        string direction)
    {
        Func<TradeExecutionQueryItem, object?> key = orderBy.ToLowerInvariant() switch
        {
            "coin" => item => item.Coin,
            "completedat" => item => item.CompletedAt,
            "recordedat" => item => item.RecordedAt,
            "runid" => item => item.RunId,
            "status" => item => item.Status,
            "strategy" or "strategyname" => item => item.StrategyName,
            _ => item => item.SubmittedAt
        };

        return Sort(items, key, direction);
    }

    private static IReadOnlyList<HttpRequestCaptureQueryItem> ApplyHttpRequestCaptureSort(
        IReadOnlyList<HttpRequestCaptureQueryItem> items,
        string orderBy,
        string direction)
    {
        Func<HttpRequestCaptureQueryItem, object?> key = orderBy.ToLowerInvariant() switch
        {
            "endpoint" => item => item.Endpoint,
            "host" => item => item.Host,
            "method" => item => item.Method,
            "statuscode" => item => item.StatusCode,
            _ => item => item.RequestTimeUtc
        };

        return Sort(items, key, direction);
    }

    private static IReadOnlyList<T> Sort<T>(
        IReadOnlyList<T> items,
        Func<T, object?> key,
        string direction)
    {
        return direction == "asc"
            ? [.. items.OrderBy(key)]
            : [.. items.OrderByDescending(key)];
    }

    private static async Task<HttpResponseData> WritePagedResponseAsync<T>(
        HttpRequestData req,
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        string? orderBy,
        string direction,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        await response.WriteAsJsonAsync(
            new PagedQueryResponse<T>(pageItems, page, pageSize, items.Count, orderBy, direction),
            cancellationToken);
        return response;
    }

    private static TradeExecutionQueryItem ToTradeExecutionQueryItem(TableEntity entity)
    {
        return new TradeExecutionQueryItem(
            GetString(entity, "ExecutionId"),
            GetString(entity, "RunId"),
            GetString(entity, "StrategyName"),
            GetNullableString(entity, "WalletAddress"),
            GetNullableString(entity, "VaultAddress"),
            GetString(entity, "Coin"),
            GetString(entity, "Side"),
            GetNullableString(entity, "TargetPosition"),
            GetNullableString(entity, "CurrentPosition"),
            GetNullableString(entity, "IntendedDelta"),
            GetNullableString(entity, "ArrivalMid"),
            GetNullableString(entity, "ArrivalBid"),
            GetNullableString(entity, "ArrivalAsk"),
            GetNullableString(entity, "SpreadBps"),
            GetNullableString(entity, "OrderId"),
            GetString(entity, "OrderType"),
            GetNullableBool(entity, "PostOnly"),
            GetNullableBool(entity, "ReduceOnly"),
            GetNullableString(entity, "LimitPrice"),
            GetNullableDateTimeOffset(entity, "SubmittedAt"),
            GetNullableString(entity, "FilledQty"),
            GetNullableString(entity, "AvgFillPrice"),
            GetNullableString(entity, "Fees"),
            GetNullableString(entity, "MakerQty"),
            GetNullableString(entity, "MakerAvgFillPrice"),
            GetNullableString(entity, "MakerFees"),
            GetNullableString(entity, "TakerQty"),
            GetNullableString(entity, "TakerAvgFillPrice"),
            GetNullableString(entity, "TakerFees"),
            GetNullableString(entity, "CancelledQty"),
            GetNullableDateTimeOffset(entity, "CompletedAt"),
            GetNullableString(entity, "Status"),
            GetNullableString(entity, "Error"),
            GetNullableDateTimeOffset(entity, "RecordedAt"));
    }

    private static HttpRequestCaptureQueryItem ToHttpRequestCaptureQueryItem(TableEntity entity)
    {
        return new HttpRequestCaptureQueryItem(
            GetString(entity, "Host"),
            GetString(entity, "Endpoint"),
            GetString(entity, "Url"),
            GetString(entity, "Method"),
            GetInt32(entity, "StatusCode"),
            GetString(entity, "BlobContainer"),
            GetString(entity, "BlobName"),
            GetString(entity, "ContentHash"),
            GetDateTimeOffset(entity, "RequestTimeUtc"),
            GetString(entity, "QueryParametersJson"));
    }

    private static bool Matches(string? actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) ||
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) ||
        actual.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDirection(string? direction) =>
        string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

    private static string SanitizeTableKey(string value)
    {
        return value
            .Replace("/", "|")
            .Replace("\\", "|")
            .Replace("#", "_")
            .Replace("?", "_");
    }

    private static string GetString(TableEntity entity, string property) =>
        entity.TryGetValue(property, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static string? GetNullableString(TableEntity entity, string property) =>
        entity.TryGetValue(property, out var value) ? value?.ToString() : null;

    private static int GetInt32(TableEntity entity, string property) =>
        entity.TryGetValue(property, out var value) && value is int parsed ? parsed : 0;

    private static bool? GetNullableBool(TableEntity entity, string property) =>
        entity.TryGetValue(property, out var value) && value is bool parsed ? parsed : null;

    private static DateTimeOffset GetDateTimeOffset(TableEntity entity, string property) =>
        GetNullableDateTimeOffset(entity, property) ?? DateTimeOffset.MinValue;

    private static DateTimeOffset? GetNullableDateTimeOffset(TableEntity entity, string property) =>
        entity.TryGetValue(property, out var value) && value is DateTimeOffset parsed ? parsed : null;

    private static async Task<HttpResponseData> ServiceUnavailableAsync(
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
        await response.WriteAsJsonAsync(
            new { Error = "Azure storage is not configured. Set AzureWebJobsStorage to enable storage query endpoints." },
            cancellationToken);
        return response;
    }

    private static async Task<HttpResponseData> ErrorAsync(
        HttpRequestData req,
        string message,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteAsJsonAsync(new { Error = message }, cancellationToken);
        return response;
    }
}
