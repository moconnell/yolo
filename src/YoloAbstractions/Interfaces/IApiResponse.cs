using System.Collections.Generic;

namespace YoloAbstractions.Interfaces;

public interface IApiResponse<out T> 
{
    IReadOnlyList<T>? Data { get; }
}