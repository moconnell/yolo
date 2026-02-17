namespace YoloFunk.Dto;

public sealed record RebalanceStartResponse(
    string Strategy,
    string InstanceId,
    bool Started,
    string RuntimeStatus,
    DateTime RequestedAtUtc);
