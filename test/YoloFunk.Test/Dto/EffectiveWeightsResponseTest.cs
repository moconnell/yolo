using YoloFunk.Dto;

namespace YoloFunk.Test.Dto;

public class EffectiveWeightsResponseTest
{
    [Fact]
    public void Constructor_ShouldPopulateProperties()
    {
        var generatedAt = DateTime.UtcNow;
        var items = new[]
        {
            new EffectiveWeightItem(
                Token: "SOL",
                RawTargetWeight: 0.4m,
                ConstrainedTargetWeight: 0.3m,
                CurrentWeight: 0.2m,
                EffectiveWeight: 0.25m,
                DeltaWeight: 0.05m,
                IsInUniverse: true,
                WithinTradeBuffer: false,
                HasTradableMarket: true)
        };

        var response = new EffectiveWeightsResponse(
            Strategy: "yolodaily",
            Address: "0xabc",
            VaultAddress: "0xdef",
            GeneratedAtUtc: generatedAt,
            Nominal: 1000m,
            WeightConstraint: 0.8m,
            CurrentGrossExposure: 0.2m,
            CurrentNetExposure: 0.2m,
            EffectiveGrossExposure: 0.25m,
            EffectiveNetExposure: 0.25m,
            BufferAdjustedGrossExposure: 0.24m,
            BufferAdjustedNetExposure: 0.24m,
            Weights: items);

        response.Strategy.ShouldBe("yolodaily");
        response.Address.ShouldBe("0xabc");
        response.VaultAddress.ShouldBe("0xdef");
        response.GeneratedAtUtc.ShouldBe(generatedAt);
        response.Nominal.ShouldBe(1000m);
        response.WeightConstraint.ShouldBe(0.8m);
        response.CurrentGrossExposure.ShouldBe(0.2m);
        response.CurrentNetExposure.ShouldBe(0.2m);
        response.EffectiveGrossExposure.ShouldBe(0.25m);
        response.EffectiveNetExposure.ShouldBe(0.25m);
        response.BufferAdjustedGrossExposure.ShouldBe(0.24m);
        response.BufferAdjustedNetExposure.ShouldBe(0.24m);
        response.Weights.Count.ShouldBe(1);
        response.Weights[0].Token.ShouldBe("SOL");
        response.Weights[0].HasTradableMarket.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_ShouldAllowNullablesAndFalseFlags()
    {
        var items = new[]
        {
            new EffectiveWeightItem(
                Token: "BTC",
                RawTargetWeight: 0m,
                ConstrainedTargetWeight: 0m,
                CurrentWeight: null,
                EffectiveWeight: null,
                DeltaWeight: null,
                IsInUniverse: false,
                WithinTradeBuffer: false,
                HasTradableMarket: false)
        };

        var response = new EffectiveWeightsResponse(
            Strategy: "unraveldaily",
            Address: "0xabc",
            VaultAddress: null,
            GeneratedAtUtc: DateTime.UtcNow,
            Nominal: 0m,
            WeightConstraint: 1m,
            CurrentGrossExposure: null,
            CurrentNetExposure: null,
            EffectiveGrossExposure: null,
            EffectiveNetExposure: null,
            BufferAdjustedGrossExposure: null,
            BufferAdjustedNetExposure: null,
            Weights: items);

        response.VaultAddress.ShouldBeNull();
        response.CurrentGrossExposure.ShouldBeNull();
        response.CurrentNetExposure.ShouldBeNull();
        response.EffectiveGrossExposure.ShouldBeNull();
        response.EffectiveNetExposure.ShouldBeNull();
        response.BufferAdjustedGrossExposure.ShouldBeNull();
        response.BufferAdjustedNetExposure.ShouldBeNull();
        response.Weights.Single().CurrentWeight.ShouldBeNull();
        response.Weights.Single().HasTradableMarket.ShouldBeFalse();
    }
}
