using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using Shouldly;
using Xunit;
using static YoloAbstractions.FactorType;

namespace YoloAbstractions.Test;

public class FactorDataFrameTest
{
    [Fact]
    public void GivenDuplicateFactorTypes_WhenNewFromCalled_ShouldThrow()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ETC"];

        // act
        var func = () => FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15, 0.10, 0.02]),
            (Carry, [0.15, 0.10, 0.02]));

        // assert
        Assert.Throws<ArgumentException>(func);
    }

    [Fact]
    public void GivenDifferingValueLengths_WhenNewFromCalled_ShouldThrow()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ETC"];

        // act
        var func = () => FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15, 0.10, 0.02]),
            (Momentum, [0.15, 0.10, 0.02, 0.01]));

        // assert
        Assert.Throws<ArgumentException>(func);
    }

    [Fact]
    public void GivenDuplicateTickers_WhenNewFromCalled_ShouldThrow()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "Eth"];

        // act
        var func = () => FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15, 0.10, 0.02]),
            (Momentum, [0.15, 0.10, 0.02]));

        // assert
        Assert.Throws<ArgumentException>(func);
    }

    [Theory]
    [InlineData("BTC", Carry, 0.15)]
    [InlineData("BTC", Momentum, 0.17)]
    [InlineData("ETH", Carry, 0.10)]
    [InlineData("ETH", Momentum, 0.11)]
    [InlineData("BNB", Carry, 0.02)]
    [InlineData("BNB", Momentum, 0.03)]
    [InlineData("BNB", Trend, double.NaN)]
    [InlineData("XRP", Carry, double.NaN)]
    [InlineData("XRP", Momentum, double.NaN)]
    public void GivenGoodSetup_WhenValueCalledForNonExistentTicker_ShouldReturnNan(
        string ticker,
        FactorType factorType,
        double expected)
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "BNB"];

        // act
        var fdf = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15, 0.10, 0.02]),
            (Momentum, [0.17, 0.11, 0.03]));

        // assert
        fdf[factorType, ticker].ShouldBe(expected);
    }

    [Theory]
    [InlineData("BTC,ETH,ETC", "BTC,ETH,ETC", false)]
    [InlineData("BTC,ETH,ETC", "BTC,ETH,Etc", false)]
    [InlineData("BTC,ETH,ETC", "BTC,ETC,ETH", false)]
    [InlineData("BTC,ETH,ETC", "ETC,ETH,BTC", false)]
    [InlineData("BTC,ETH,ETC", "BTC,ETH,BNB", true)]
    [InlineData("BTC,ETH,ETC", "BTC,ETH,bnb", true)]
    public void GivenTwoFactorDataFrames_WhenTickersNotEqual_ShouldThrow(
        string tickers1,
        string tickers2,
        bool shouldThrow)
    {
        // arrange
        var one = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, 3)),
                new StringDataFrameColumn("Ticker", tickers1.Split(',')),
                new DoubleDataFrameColumn(nameof(Carry), [0.15, 0.10, 0.02])),
            Carry);

        var two = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, 3)),
                new StringDataFrameColumn("Ticker", tickers2.Split(',')),
                new DoubleDataFrameColumn(nameof(Momentum), [0.15, 0.10, 0.02])),
            Momentum);

        // act
        var func = () => one + two;

        // assert
        if (shouldThrow)
        {
            Assert.Throws<ArgumentException>(func);
        }
        else
        {
            var res = func();
            res.ShouldNotBeNull();
        }
    }

    [Theory]
    [InlineData("Carry", "Momentum,Trend", false)]
    [InlineData("Carry", "Carry", true)]
    [InlineData("Carry,Momentum", "Momentum,Trend", true)]
    public void GivenTwoFactorDataFrames_WhenFactorsOverlap_ShouldThrow(
        string factorTypes1,
        string factorTypes2,
        bool shouldThrow)
    {
        // arrange
        string[] tickers = ["BTC"];
        var one = NewFactorDataFrame(factorTypes1);
        var two = NewFactorDataFrame(factorTypes2);

        // act
        var func = () => one + two;

        // assert
        if (shouldThrow)
        {
            Assert.Throws<ArgumentException>(func);
        }
        else
        {
            var res = func();
            res.ShouldNotBeNull();
        }

        return;

        FactorDataFrame NewFactorDataFrame(string s)
        {
            return FactorDataFrame.NewFrom(
                tickers,
                DateTime.Today,
                [
                    ..s.Split(',')
                        .Select(Enum.Parse<FactorType>)
                        .Select(x => (x, (IReadOnlyList<double>) [0.1]))
                ]);
        }
    }


    [Fact]
    public void GivenTwoFactorDataFrames_WhenTickersEqual_ShouldAdd()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ETC"];
        double[] carryValues = [0.15, 0.10, 0.02];
        double[] momoValues = [0.34, 0.17, 0.08];
        double[] trendValues = [0.22, 0.51, 0.05];

        var one = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, tickers.Length)),
                new StringDataFrameColumn("Ticker", tickers),
                new DoubleDataFrameColumn(nameof(Carry), carryValues),
                new DoubleDataFrameColumn(nameof(Trend), trendValues)
            ),
            Carry,
            Trend);

        var two = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, tickers.Length)),
                new StringDataFrameColumn("Ticker", tickers),
                new DoubleDataFrameColumn(nameof(Momentum), momoValues)),
            Momentum);

        // act
        var result = one + two;

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(tickers);
        result.FactorTypes.ShouldBe([Carry, Trend, Momentum]);
        var i = 0;
        foreach (var ticker in tickers)
        {
            result[Carry, ticker].ShouldBe(carryValues[i]);
            result[Momentum, ticker].ShouldBe(momoValues[i]);
            result[Trend, ticker].ShouldBe(trendValues[i++]);
        }
    }

    [Theory]
    [InlineData(null, false, 0.254, 0.292, 0.056)]
    [InlineData(0.25, false, 0.25, 0.25, 0.056)]
    [InlineData(null, true, 0.79375d, 0.3792207792207792d, 0.060869565217391314d)]
    [InlineData(0.5, true, 0.5, 0.3792207792207792d, 0.060869565217391314d)]
    public void GivenFactorDataFrameAndWeightsDict_WhenTickersEqual_ShouldMultiply(
        double? maxWeightAbs,
        bool volScaling,
        params double[] expectedValues)
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ETC"];
        double[] carryValues = [0.15, 0.10, 0.02];
        double[] momoValues = [0.34, 0.17, 0.08];
        double[] trendValues = [0.22, 0.51, 0.05];
        double[] volatilityValues = [0.32, 0.77, 0.92];

        var factorDataFrame = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, tickers.Length)),
                new StringDataFrameColumn("Ticker", tickers),
                new DoubleDataFrameColumn(nameof(Carry), carryValues),
                new DoubleDataFrameColumn(nameof(Momentum), momoValues),
                new DoubleDataFrameColumn(nameof(Trend), trendValues),
                new DoubleDataFrameColumn(nameof(Volatility), volatilityValues)
            ),
            Carry,
            Momentum,
            Trend,
            Volatility);

        var weights = new Dictionary<FactorType, double>
        {
            [Carry] = 0.5,
            [Momentum] = 1.0,
            [Trend] = 1.0
        };

        // act
        var result = factorDataFrame.ApplyWeights(weights, maxWeightAbs, volScaling);

        // assert
        result.ShouldNotBeNull();
        ((StringDataFrameColumn) result["Ticker"]).ToArray().ShouldBe(tickers);
        var calculatedWeights = ((DoubleDataFrameColumn) result["Weight"]).Cast<double>().ToArray();
        const double tolerance = 0.0000001;
        for (var i = 0; i < calculatedWeights.Length; i++)
        {
            var calculatedWeight = calculatedWeights[i];
            var expectedValue = expectedValues[i];
            calculatedWeight.ShouldBe(expectedValue, tolerance);
        }
    }

    [Fact]
    public void GivenFactorDataFrame_WhenNormalized_ShouldHaveZeroMeanUnitStd()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA", "BNB"];
        double[] carryValues = [0.15, 0.10, 0.02, -0.05];
        double[] momoValues = [0.34, 0.17, 0.08, -0.12];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues),
            (Momentum, momoValues));

        // act
        var normalized = factorDataFrame.Normalize();

        // assert - check that each factor has mean ≈ 0 and std ≈ 1
        foreach (var factorType in normalized.FactorTypes)
        {
            var values = tickers.Select(t => normalized[factorType, t]).Where(v => !double.IsNaN(v)).ToArray();
            var mean = values.Average();
            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Length;
            var stdDev = Math.Sqrt(variance);

            mean.ShouldBe(0.0, 1e-10);
            stdDev.ShouldBe(1.0, 1e-10);
        }
    }

    [Fact]
    public void GivenFactorDataFrameWithMissingValue_WhenNormalized_ShouldPreserveNaN()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "MNT"];
        double[] carryValues = [0.15, 0.10, 0.02];
        double[] momoValues = [0.34, 0.17, double.NaN];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues),
            (Momentum, momoValues));

        // act
        var normalized = factorDataFrame.Normalize();

        // assert
        normalized[Momentum, "MNT"].ShouldBe(double.NaN);
        double.IsNaN(normalized[Momentum, "BTC"]).ShouldBeFalse();
        double.IsNaN(normalized[Momentum, "ETH"]).ShouldBeFalse();
    }
}