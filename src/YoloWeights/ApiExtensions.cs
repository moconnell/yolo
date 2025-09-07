using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using YoloAbstractions.Config;
using YoloWeights.Data;
using YoloWeights.Exceptions;

namespace YoloWeights;

public static class ApiExtensions
{
    public static async Task<IReadOnlyList<YoloAbstractions.Weight>> GetWeights(
        this YoloConfig config)
    {
        var weightsResponse = await CallApi<Weight>(config.GetWeightsUrl());
        var volatilitiesResponse = await CallApi<Volatility>(config.GetVolatilitiesUrl());

        return [.. GetWeights(weightsResponse.Data, volatilitiesResponse.Data, config.DateFormat)];
    }

    private static string GetVolatilitiesUrl(this YoloConfig config) => config.GetUrl(config.VolatilitiesUrlPath);

    private static string GetWeightsUrl(this YoloConfig config) => config.GetUrl(config.WeightsUrlPath);

    private static string GetUrl(this YoloConfig config, string path) => $"{config.ApiBaseUrl}/{path}?api_key={Uri.EscapeDataString(config.ApiKey)}";

    private static IEnumerable<YoloAbstractions.Weight> GetWeights(
        IReadOnlyList<Weight> weightsData,
        IReadOnlyList<Volatility> volatilitiesData,
        string dateFormat = "yyyy-MM-dd")
    {
        foreach (var weight in weightsData)
        {
            var volatility = volatilitiesData.FirstOrDefault(v => v.Ticker == weight.Ticker);
            yield return MapWeight(weight, volatility?.EwVol ?? 1);
        }

        YoloAbstractions.Weight MapWeight(Weight arg, double ewVol)
        {
            return new(
                Convert.ToDecimal(arg.ArrivalPrice),
                Convert.ToDecimal(arg.CarryMegafactor),
                DateTime.ParseExact(arg.Date, dateFormat, CultureInfo.InvariantCulture),
                Convert.ToDecimal(arg.MomentumMegafactor),
                arg.Ticker,
                Convert.ToDecimal(arg.TrendMegafactor),
                Convert.ToDecimal(ewVol));
        }
    }

    private static async Task<ApiResponse<T>> CallApi<T>(string url)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(
                $"Could not fetch from API: {await response.Content.ReadAsStringAsync()} ({response.StatusCode}: {response.ReasonPhrase})");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();

        if (apiResponse is null || apiResponse.Success != "true" || apiResponse.Data is null || !apiResponse.Data.Any())
        {
            throw new ApiException("Invalid response");
        }

        return apiResponse;
    }
}