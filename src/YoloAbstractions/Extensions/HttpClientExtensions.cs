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
    public static async Task<TResponse> GetAsync<TResponse, TData>(
        this HttpClient httpClient,
        string url,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TResponse : IApiResponse<TData>
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
                $"Could not fetch from API ({url}): {response.ReasonPhrase} ({response.StatusCode})");
        }

        try
        {
            var apiResponse = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);

            if (apiResponse is null || apiResponse.Data is null || apiResponse.Data.Count == 0)
            {
                throw new ApiException($"No data returned from API ({url})");
            }

            return apiResponse;
        }
        catch (Exception ex) when (ex is not ApiException)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ApiException($"Error deserializing API response from ({url}): {ex.Message}\r\n{responseContent}", ex);
        }
    }
}