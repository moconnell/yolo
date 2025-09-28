using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;

namespace YoloAbstractions.Extensions;

public static class ApiExtensions
{
    public static async Task<T> CallApiAsync<T, TData>(this string url, CancellationToken cancellationToken = default)
        where T : IApiResponse<TData>
    {
        ArgumentException.ThrowIfNullOrEmpty(url, nameof(url));

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(
                $"Could not fetch from API: {await response.Content.ReadAsStringAsync(cancellationToken)} ({response.StatusCode}: {response.ReasonPhrase})");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);

        if (apiResponse is null || !apiResponse.Success || apiResponse.Data is null || apiResponse.Data.Count == 0)
        {
            throw new ApiException("Invalid response");
        }

        return apiResponse;
    }
}