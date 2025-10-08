using YoloAbstractions;

namespace Unravel.Api.Config;

public record FactorConfig
{
    public string Id { get; init; } = "";

    public FactorType Type { get; init; }

    public int Window { get; init; }
}