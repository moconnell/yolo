using Microsoft.Data.Analysis;
using YoloAbstractions.Extensions;
using static YoloAbstractions.NormalizationMethod;

namespace YoloAbstractions.Test;

public class DataFrameExtensionsTest
{
    [Fact]
    public void GivenNoneNormalization_WhenNormalizeCalled_ShouldReturnSameInstance()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, 2.0]));

        var result = df.Normalize(None);

        ReferenceEquals(df, result).ShouldBeTrue();
    }

    [Fact]
    public void GivenInvalidQuantiles_WhenNormalizeCalled_ShouldThrow()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, 2.0]));

        Should.Throw<ArgumentOutOfRangeException>(() => df.Normalize(CrossSectionalBins, 0));
    }

    [Fact]
    public void GivenQuantilesOneWithMissingValues_WhenNormalized_ShouldPreserveNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, double.NaN, 3.0]));

        var result = df.Normalize(CrossSectionalBins, 1);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values[0].ShouldBe(0.0);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(0.0);
    }

    [Fact]
    public void GivenRankNormalizationWithMissingValues_WhenNormalized_ShouldPreserveNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [3.0, double.NaN, 1.0]));

        var result = df.Normalize(Rank);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values[0].ShouldBe(1.0);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(-1.0);
    }
}
