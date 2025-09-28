using Microsoft.Extensions.Configuration;

namespace Unravel.Weights.Extensions;

public static class UnravelConfigExtensions
{
    public static UnravelConfig? GetUnravelConfig(this IConfiguration configuration)
    {
        return configuration
            .GetSection(nameof(Unravel))
            .Get<UnravelConfig>()
            ?.Ensure(c => c.ApiBaseUrl)
            ?.Ensure(c => c.ApiKey)
            ?.Ensure(c => c.Factor);
    }
}