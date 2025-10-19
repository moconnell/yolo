using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using YoloAbstractions.Extensions;

namespace YoloAbstractions.Test;

public class VectorExtensionsTest(ITestOutputHelper output)
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(5, false)]
    [InlineData(10, true)]
    [InlineData(10, false)]
    public void GivenVector_WhenMarketNeutral_ShouldSumToZero(int bins, bool marketNeutral)
    {
        // setup
        var v = Vector<double>.Build.DenseOfEnumerable(
            Enumerable.Range(0, bins * 2).Select(i => i * Math.Pow(0.8, i)));

        // act
        var result = v.QTiledRowToWeights(bins, marketNeutral);

        output.WriteLine(result.ToString());

        // assert
        result.Count.ShouldBe(v.Count);

        if (marketNeutral)
        {
            result.Min().ShouldBeGreaterThanOrEqualTo(-1);
            result.Sum().ShouldBeGreaterThanOrEqualTo(-0.5);
            result.Sum().ShouldBeLessThanOrEqualTo(0.5);
        }
        else
        {
            result.Min().ShouldBeGreaterThanOrEqualTo(0);
        }

        result.Max().ShouldBeLessThanOrEqualTo(1);
    }
}