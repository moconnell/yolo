namespace YoloFunk.Dto;

public sealed record EffectiveWeightsResponse(
    string Strategy,
    string Address,
    string? VaultAddress,
    DateTime GeneratedAtUtc,
    decimal Nominal,
    decimal WeightConstraint,
    decimal? CurrentGrossExposure,
    decimal? CurrentNetExposure,
    decimal? EffectiveGrossExposure,
    decimal? EffectiveNetExposure,
    decimal? BufferAdjustedGrossExposure,
    decimal? BufferAdjustedNetExposure,
    IReadOnlyList<EffectiveWeightItem> Weights);

public sealed record EffectiveWeightItem(
    string Token,
    decimal RawTargetWeight,
    decimal ConstrainedTargetWeight,
    decimal? CurrentWeight,
    decimal? EffectiveWeight,
    decimal? DeltaWeight,
    bool IsInUniverse,
    bool WithinTradeBuffer,
    bool HasTradableMarket);
