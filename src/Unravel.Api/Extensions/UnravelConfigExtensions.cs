using Microsoft.Extensions.Configuration;
using Unravel.Api.Config;
using YoloAbstractions.Extensions;

namespace Unravel.Api.Extensions;

public static class UnravelConfigExtensions
{
    public static bool HasUnravelConfig(this IConfiguration configuration)
    {
        try
        {
            return configuration.GetUnravelConfig() is not null;
        }
        catch
        {
            return false;
        }
    }

    public static UnravelConfig? GetUnravelConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(nameof(Unravel))
            .Get<UnravelConfig>()
            ?.Ensure(c => c.ApiBaseUrl)
            .Ensure(c => c.ApiKey)
            .Ensure(c => c.Factors);
    }
}