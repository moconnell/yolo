using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace YoloFunk.Infrastructure;

public class RawJsonPersistenceHandler(
    BlobServiceClient blobServiceClient,
    TableServiceClient tableServiceClient) : DelegatingHandler
{
    private const string ContainerName = "http-requests";
    private const string TableName = "httprequestsindex";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestTime = DateTimeOffset.UtcNow;
        var timestamp = requestTime.ToString("yyyy-MM-dd-HH-mm-ss-fff");
        var requestUri = request.RequestUri;
        var blobName = BuildBlobName(requestUri, timestamp);
        var requestBody = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;

        var response = await base.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        var payload = new HttpExchangePayload(
            requestTime,
            requestUri?.ToString() ?? string.Empty,
            GetQueryParameters(requestUri),
            request.Method.Method,
            requestBody,
            (int)response.StatusCode,
            responseBody);

        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        var tableClient = tableServiceClient.GetTableClient(TableName);

        await Task.WhenAll(
            containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken),
            tableClient.CreateIfNotExistsAsync(cancellationToken));

        var responseBlobClient = containerClient.GetBlobClient(blobName);
        var blobUploadTask = responseBlobClient.UploadAsync(
            BinaryData.FromString(JsonSerializer.Serialize(payload, SerializerOptions)),
            overwrite: true,
            cancellationToken: cancellationToken);

        var indexEntity = BuildIndexEntity(requestTime, requestUri, request.Method.Method, response.StatusCode, blobName);
        var indexUpsertTask = tableClient.UpsertEntityAsync(
            indexEntity,
            TableUpdateMode.Replace,
            cancellationToken);

        await Task.WhenAll(blobUploadTask, indexUpsertTask);

        return response;
    }

    private static string BuildBlobName(Uri? requestUri, string timestamp)
    {
        if (requestUri == null)
            return $"unknown/unknown/{timestamp}.json";

        var host = string.IsNullOrWhiteSpace(requestUri.Host) ? "unknown" : requestUri.Host;
        var path = requestUri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
            path = "root";

        return $"{host}/{path}/{timestamp}.json";
    }

    private static Dictionary<string, string[]> GetQueryParameters(Uri? requestUri)
    {
        if (requestUri == null || string.IsNullOrWhiteSpace(requestUri.Query))
            return [];

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var query = requestUri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            var key = Uri.UnescapeDataString(parts[0]);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (!result.TryGetValue(key, out var values))
            {
                values = [];
                result[key] = values;
            }

            values.Add(value);
        }

        return result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private sealed record HttpExchangePayload(
        DateTimeOffset RequestTimeUtc,
        string Url,
        IReadOnlyDictionary<string, string[]> QueryParameters,
        string Method,
        string RequestBody,
        int StatusCode,
        string ResponseBody);

    private static HttpExchangeIndexEntity BuildIndexEntity(
        DateTimeOffset requestTime,
        Uri? requestUri,
        string method,
        System.Net.HttpStatusCode statusCode,
        string blobName)
    {
        var host = requestUri?.Host ?? "unknown";
        var endpoint = requestUri?.AbsolutePath ?? "/";
        var endpointKey = SanitizeTableKey(endpoint.Trim('/'));
        if (string.IsNullOrWhiteSpace(endpointKey))
            endpointKey = "root";

        var rowKey = $"{endpointKey}|{requestTime:yyyyMMddHHmmssfff}|{Guid.NewGuid():N}";

        return new HttpExchangeIndexEntity
        {
            PartitionKey = SanitizeTableKey(host),
            RowKey = rowKey,
            RequestTimeUtc = requestTime,
            Url = requestUri?.ToString() ?? string.Empty,
            Method = method,
            StatusCode = (int)statusCode,
            BlobContainer = ContainerName,
            BlobName = blobName,
            QueryParametersJson = requestUri == null
                ? "{}"
                : JsonSerializer.Serialize(GetQueryParameters(requestUri), SerializerOptions),
            Endpoint = endpoint,
            Host = host
        };
    }

    private static string SanitizeTableKey(string value)
    {
        return value
            .Replace("/", "|")
            .Replace("\\", "|")
            .Replace("#", "_")
            .Replace("?", "_");
    }

    private sealed class HttpExchangeIndexEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset RequestTimeUtc { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string BlobContainer { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public string QueryParametersJson { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
    }
}