using YoloAbstractions.Config;

namespace Unravel.Api.Config;

public record UnravelConfig : ApiConfig
{
    public FactorConfig[] Factors { get; init; } = [];
}