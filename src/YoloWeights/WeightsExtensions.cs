using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using YoloAbstractions.Config;

namespace YoloWeights
{
    public static class WeightsExtensions
    {
        public static async Task<IEnumerable<YoloAbstractions.Weight>> GetWeights(
            this YoloConfig config)
        {
            return await GetWeights(config.WeightsUrl, config.DateFormat);
        }

        public static async Task<IEnumerable<YoloAbstractions.Weight>> GetWeights(
            this string weightsUrl,
            string dateFormat = "yyyy-MM-dd")
        {
            YoloAbstractions.Weight MapWeight(Weight arg)
            {
                return new YoloAbstractions.Weight(
                    Convert.ToDecimal(arg.ArrivalPrice),
                    Convert.ToDecimal(arg.ComboWeight),
                    DateTime.ParseExact(arg.Date, dateFormat, CultureInfo.InvariantCulture),
                    Convert.ToDecimal(arg.MomentumMegafactor),
                    arg.Ticker,
                    Convert.ToDecimal(arg.TrendMegafactor));
            }

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(weightsUrl);

            if (!response.IsSuccessStatusCode)
            {
                throw new WeightsException(
                    $"Could not fetch weights: {response.ReasonPhrase} ({response.StatusCode})");
            }

            var weightsResponse = await response.Content.ReadFromJsonAsync<WeightsResponse>();

            if (weightsResponse is null)
            {
                throw new WeightsException("No weights returned - response was empty");
            }

            return weightsResponse.Data.Select(MapWeight);
        }
    }
}