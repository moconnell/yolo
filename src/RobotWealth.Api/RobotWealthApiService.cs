using YoloAbstractions.Extensions;
using RobotWealth.Api.Config;
using RobotWealth.Api.Data;
using RobotWealth.Api.Interfaces;

namespace RobotWealth.Api;

public class RobotWealthApiService : IRobotWealthApiService
{
    private readonly HttpClient _httpClient;
    private readonly RobotWealthConfig _config;

    public RobotWealthApiService(HttpClient httpClient, RobotWealthConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<IReadOnlyList<RwWeight>> GetWeightsAsync(CancellationToken cancellationToken = default)
    {
        var url = GetWeightsUrl();
        var response =
            await _httpClient.GetAsync<RwApiResponse<RwWeight>, RwWeight>(
                url,
                cancellationToken: cancellationToken);

        return response.Data;
    }

    public async Task<IReadOnlyList<RwVolatility>> GetVolatilitiesAsync(CancellationToken cancellationToken = default)
    {
        var url = GetVolatilitiesUrl();
        var response =
            await _httpClient.GetAsync<RwApiResponse<RwVolatility>, RwVolatility>(
                url,
                cancellationToken: cancellationToken);
        return response.Data;
    }

    private string GetVolatilitiesUrl() => GetUrl(_config.VolatilitiesUrlPath);

    private string GetWeightsUrl() => GetUrl(_config.WeightsUrlPath);

    private string GetUrl(string path) => $"{_config.ApiBaseUrl}/{path}?api_key={Uri.EscapeDataString(_config.ApiKey)}";
}