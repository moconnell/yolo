namespace YoloFunk.Dto;

public sealed record RebalanceErrorResponse(
    string Strategy,
    string Error,
    string? Details = null);