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
            Weights: items);

        response.Strategy.ShouldBe("yolodaily");
        response.Address.ShouldBe("0xabc");
        response.VaultAddress.ShouldBe("0xdef");
        response.GeneratedAtUtc.ShouldBe(generatedAt);
        response.Nominal.ShouldBe(1000m);
        response.WeightConstraint.ShouldBe(0.8m);
        response.Weights.Count.ShouldBe(1);
        response.Weights[0].Token.ShouldBe("SOL");
        response.Weights[0].HasTradableMarket.ShouldBeTrue();
    }
}
