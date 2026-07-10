namespace YoloFunk.Dto;

public sealed record TradeIngestionStatusResponse(
    string Strategy,
    string InstanceId,
    string RuntimeStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt);
