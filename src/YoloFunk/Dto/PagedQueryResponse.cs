namespace YoloFunk.Dto;

public sealed record PagedQueryResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string? OrderBy,
    string Direction,
    string? NextContinuationToken = null);
