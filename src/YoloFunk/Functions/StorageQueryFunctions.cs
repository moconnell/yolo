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
    private const string RebalanceEventsTableName = "rebalanceevents";
    private const string HttpRequestsTableName = "httprequestsindex";
    private const string HttpRequestsContainerName = "http-requests";

    [Function(nameof(GetRebalanceEvents))]
    public async Task<HttpResponseData> GetRebalanceEvents(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "storage/rebalance-events")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!TryGetTableServiceClient(req, out var tableServiceClient))
            return await ServiceUnavailableAsync(req, cancellationToken);

        var query = HttpQueryParameters.Parse(req.Url);
        var page = query.GetInt32("page", 1, 1, 10_000);
        var pageSize = query.GetInt32("pageSize", 100, 1, 500);
        var orderBy = NormalizeTableOrderBy(query.GetString("orderBy"));
        var direction = NormalizeTableDirection(query.GetString("direction"));
        var continuationToken = query.GetString("continuationToken");
        if (orderBy is null || direction is null)
            return await InvalidPagedTableSortAsync(req, cancellationToken);

        try
        {
            var pageResult = await QueryRebalanceEventsAsync(
                tableServiceClient,
                query,
                pageSize,
                continuationToken,
                cancellationToken);

            return await WritePagedResponseAsync(
                req,
                pageResult.Items,
                page,
                pageSize,
                orderBy,
                direction,
                cancellationToken,
                pageResult.NextContinuationToken,
                skipItems: false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return await WritePagedResponseAsync(
                req,
                Array.Empty<RebalanceEventQueryItem>(),
                page,
                pageSize,
                orderBy,
                direction,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query rebalance events");
            return await ErrorAsync(req, "Failed to query rebalance events", cancellationToken);
        }
    }

    [Function(nameof(GetHttpRequestCaptures))]
    public async Task<HttpResponseData> GetHttpRequestCaptures(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "storage/http-requests")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!TryGetTableServiceClient(req, out var tableServiceClient))
            return await ServiceUnavailableAsync(req, cancellationToken);

        var query = HttpQueryParameters.Parse(req.Url);
        var page = query.GetInt32("page", 1, 1, 10_000);
        var pageSize = query.GetInt32("pageSize", 100, 1, 500);
        var orderBy = NormalizeTableOrderBy(query.GetString("orderBy"));
        var direction = NormalizeTableDirection(query.GetString("direction"));
        var continuationToken = query.GetString("continuationToken");
        if (orderBy is null || direction is null)
            return await InvalidPagedTableSortAsync(req, cancellationToken);

        try
        {
            var pageResult = await QueryHttpRequestCapturesAsync(
                tableServiceClient,
                query,
                pageSize,
                continuationToken,
                cancellationToken);

            return await WritePagedResponseAsync(
                req,
                pageResult.Items,
                page,
                pageSize,
                orderBy,
                direction,
                cancellationToken,
                pageResult.NextContinuationToken,
                skipItems: false);
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
        if (!TryGetBlobServiceClient(req, out var blobServiceClient))
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

    private bool TryGetTableServiceClient(
        HttpRequestData req,
        out TableServiceClient tableServiceClient)
    {
        tableServiceClient = serviceProvider.GetService<TableServiceClient>() ??
                             req.FunctionContext.InstanceServices.GetService<TableServiceClient>()!;

        return tableServiceClient is not null;
    }

    private bool TryGetBlobServiceClient(
        HttpRequestData req,
        out BlobServiceClient blobServiceClient)
    {
        blobServiceClient = serviceProvider.GetService<BlobServiceClient>() ??
                            req.FunctionContext.InstanceServices.GetService<BlobServiceClient>()!;

        return blobServiceClient is not null;
    }

    private static async Task<QueryPageResult<HttpRequestCaptureQueryItem>> QueryHttpRequestCapturesAsync(
        TableServiceClient tableServiceClient,
        HttpQueryParameters query,
        int pageSize,
        string? continuationToken,
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
        return await QueryFilteredEntitiesPageAsync(
            tableClient,
            BuildPartitionFilter(host),
            pageSize,
            continuationToken,
            ToHttpRequestCaptureQueryItem,
            item => Matches(item.Host, host) &&
                    Contains(item.Endpoint, endpoint) &&
                    Matches(item.Method, method) &&
                    (!int.TryParse(statusCode, out var expectedStatusCode) || item.StatusCode == expectedStatusCode) &&
                    Matches(item.ContentHash, contentHash) &&
                    (!from.HasValue || item.RequestTimeUtc >= from.Value) &&
                    (!to.HasValue || item.RequestTimeUtc <= to.Value),
            cancellationToken);
    }

    private static async Task<QueryPageResult<RebalanceEventQueryItem>> QueryRebalanceEventsAsync(
        TableServiceClient tableServiceClient,
        HttpQueryParameters query,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var strategy = query.GetString("strategy");
        var runId = query.GetString("runId");
        var eventType = query.GetString("eventType");
        var level = query.GetString("level");
        var coin = query.GetString("coin");
        var clientOrderId = query.GetString("clientOrderId");
        var from = query.GetDateTimeOffset("from");
        var to = query.GetDateTimeOffset("to");

        var tableClient = tableServiceClient.GetTableClient(RebalanceEventsTableName);
        var partitionFilter = !string.IsNullOrWhiteSpace(strategy) && !string.IsNullOrWhiteSpace(runId)
            ? BuildPartitionFilter($"{strategy}|{runId}")
            : null;
        return await QueryFilteredEntitiesPageAsync(
            tableClient,
            partitionFilter,
            pageSize,
            continuationToken,
            ToRebalanceEventQueryItem,
            item => Matches(item.StrategyName, strategy) &&
                    Matches(item.RunId, runId) &&
                    Matches(item.EventType, eventType) &&
                    Matches(item.Level, level) &&
                    Matches(item.Coin, coin) &&
                    Matches(item.ClientOrderId, clientOrderId) &&
                    (!from.HasValue || item.TimestampUtc >= from.Value) &&
                    (!to.HasValue || item.TimestampUtc <= to.Value),
            cancellationToken);
    }

    private static async Task<QueryPageResult<T>> QueryFilteredEntitiesPageAsync<T>(
        TableClient tableClient,
        string? filter,
        int pageSize,
        string? continuationToken,
        Func<TableEntity, T> map,
        Func<T, bool> predicate,
        CancellationToken cancellationToken)
    {
        var items = new List<T>(pageSize);

        await foreach (var page in tableClient
                           .QueryAsync<TableEntity>(
                               filter,
                               maxPerPage: 1,
                               cancellationToken: cancellationToken)
                           .AsPages(continuationToken, 1))
        {
            foreach (var entity in page.Values)
            {
                var item = map(entity);
                if (!predicate(item))
                {
                    continue;
                }

                items.Add(item);
                if (items.Count == pageSize)
                {
                    return new QueryPageResult<T>(items, page.ContinuationToken);
                }
            }
        }

        return new QueryPageResult<T>(items, null);
    }

    private static string? BuildPartitionFilter(string? partitionKey)
    {
        return string.IsNullOrWhiteSpace(partitionKey)
            ? null
            : $"PartitionKey eq '{SanitizeTableKey(partitionKey).Replace("'", "''")}'";
    }

    private static async Task<HttpResponseData> WritePagedResponseAsync<T>(
        HttpRequestData req,
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        string? orderBy,
        string direction,
        CancellationToken cancellationToken,
        string? nextContinuationToken = null,
        bool skipItems = true)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var pageItems = skipItems
            ? items
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray()
            : [.. items.Take(pageSize)];

        await response.WriteAsJsonAsync(
            new PagedQueryResponse<T>(pageItems, page, pageSize, items.Count, orderBy, direction, nextContinuationToken),
            cancellationToken);
        return response;
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

    private static RebalanceEventQueryItem ToRebalanceEventQueryItem(TableEntity entity)
    {
        return new RebalanceEventQueryItem(
            GetString(entity, "RunId"),
            GetString(entity, "StrategyName"),
            GetDateTimeOffset(entity, "TimestampUtc"),
            GetInt32(entity, "Sequence"),
            GetString(entity, "EventType"),
            GetString(entity, "Level"),
            GetString(entity, "Summary"),
            GetNullableString(entity, "WalletAddress"),
            GetNullableString(entity, "VaultAddress"),
            GetNullableString(entity, "Coin"),
            GetNullableString(entity, "ClientOrderId"),
            GetNullableString(entity, "OrderId"),
            GetString(entity, "PayloadJson"));
    }

    private static bool Matches(string? actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) ||
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) ||
        actual.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeTableOrderBy(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return "table";
        }

        return orderBy.Equals("table", StringComparison.OrdinalIgnoreCase) ||
               orderBy.Equals("partitionKey", StringComparison.OrdinalIgnoreCase) ||
               orderBy.Equals("rowKey", StringComparison.OrdinalIgnoreCase)
            ? "table"
            : null;
    }

    private static string? NormalizeTableDirection(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction) ||
            direction.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        return null;
    }

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

    private static async Task<HttpResponseData> InvalidPagedTableSortAsync(
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(
            new { Error = "Continuation-token paging supports only table order. Use orderBy=table and direction=asc." },
            cancellationToken);
        return response;
    }

    private sealed record QueryPageResult<T>(IReadOnlyList<T> Items, string? NextContinuationToken);
}
