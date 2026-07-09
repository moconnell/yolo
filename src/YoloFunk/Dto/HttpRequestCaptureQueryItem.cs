namespace YoloFunk.Dto;

public sealed record HttpRequestCaptureQueryItem(
    string Host,
    string Endpoint,
    string Url,
    string Method,
    int StatusCode,
    string BlobContainer,
    string BlobName,
    string ContentHash,
    DateTimeOffset RequestTimeUtc,
    string QueryParametersJson);
