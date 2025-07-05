using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions.Config;

namespace YoloWeights;

public class YoloWeightsService : IYoloWeightsService
{
    private readonly YoloConfig _config;

    public YoloWeightsService(YoloConfig config) => _config = config;

    public async Task<IReadOnlyDictionary<string, YoloAbstractions.Weight>> GetWeightsAsync(CancellationToken cancellationToken) =>
        await GetWeights(_config.WeightsUrl, _config.DateFormat, cancellationToken);

    private static async Task<IReadOnlyDictionary<string, YoloAbstractions.Weight>> GetWeights(
        string weightsUrl,
        string dateFormat = "yyyy-MM-dd",
        CancellationToken cancellationToken = default)
    {
        YoloAbstractions.Weight MapWeight(Weight arg) =>
            new(
                Convert.ToDecimal(arg.ArrivalPrice),
                Convert.ToDecimal(arg.ComboWeight),
                DateTime.ParseExact(arg.Date, dateFormat, CultureInfo.InvariantCulture),
                Convert.ToDecimal(arg.MomentumMegafactor),
                arg.Ticker,
                Convert.ToDecimal(arg.TrendMegafactor));

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(weightsUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new WeightsException(
                $"Could not fetch weights: {response.ReasonPhrase} ({response.StatusCode})");
        }

        var weightsResponse =
            await response.Content.ReadFromJsonAsync<WeightsResponse>(cancellationToken: cancellationToken);

        if (weightsResponse is null)
        {
            throw new WeightsException("No weights returned - response was empty");
        }

        return weightsResponse.Data
            .Select(MapWeight)
            .ToDictionary(w => w.BaseAsset);
    }
}