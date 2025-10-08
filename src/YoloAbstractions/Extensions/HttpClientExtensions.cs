using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;

namespace YoloAbstractions.Extensions;

public static class HttpClientExtensions
{
    public static async Task<T> GetAsync<T, TData>(
        this HttpClient httpClient,
        string url,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where T : IApiResponse<TData>
    {
        ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));
        ArgumentException.ThrowIfNullOrEmpty(url, nameof(url));

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(
                $"Could not fetch from API: {await response.Content.ReadAsStringAsync(cancellationToken)} ({response.StatusCode}: {response.ReasonPhrase})");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);

        if (apiResponse is null || apiResponse.Data is null || apiResponse.Data.Count == 0)
        {
            throw new ApiException("Response contained no data");
        }

        return apiResponse;
    }
}