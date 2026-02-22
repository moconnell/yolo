namespace YoloFunk.Dto;

public sealed record RebalanceStatusResponse(
    string Strategy,
    string InstanceId,
    string RuntimeStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt);
