using System.Collections.Generic;

namespace YoloAbstractions.Interfaces;

public interface IApiResponse
{
    bool Success { get; }
}

public interface IApiResponse<T> : IApiResponse
{
    IReadOnlyList<T>? Data { get; }
}