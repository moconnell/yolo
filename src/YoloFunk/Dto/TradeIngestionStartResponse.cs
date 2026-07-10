namespace YoloFunk.Dto;

public sealed record TradeIngestionStartResponse(
    string Strategy,
    string InstanceId,
    bool Started,
    string RuntimeStatus,
    DateTime RequestedAtUtc);
