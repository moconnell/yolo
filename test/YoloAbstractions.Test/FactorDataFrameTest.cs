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

    [Fact]
    public void GivenTwoFactorDataFrames_WhenTickersNotEqual_ShouldThrow()
    {
        // arrange
        var one = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, 3)),
                new StringDataFrameColumn("Ticker", ["BTC", "ETH", "ETC"]),
                new DoubleDataFrameColumn(nameof(Carry), [0.15, 0.10, 0.02])),
            Carry);

        var two = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, 3)),
                new StringDataFrameColumn("Ticker", ["BTC", "ETH", "BNB"]),
                new DoubleDataFrameColumn(nameof(Momentum), [0.15, 0.10, 0.02])),
            Momentum);

        // act
        var func = () => one + two;

        // assert
        Assert.Throws<ArgumentOutOfRangeException>(func);
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
            result.Value(Carry, ticker).ShouldBe(carryValues[i]);
            result.Value(Momentum, ticker).ShouldBe(momoValues[i]);
            result.Value(Trend, ticker).ShouldBe(trendValues[i++]);
        }
    }

    [Theory]
    [InlineData(null, false, 0.21166667, 0.24333333, 0.04666667)]
    [InlineData(0.25, false, 0.21166667, 0.24333333, 0.04666667)]
    [InlineData(null, true, 0.66145833, 0.31601732, 0.05072464)]
    [InlineData(0.5, true, 0.5, 0.31601732, 0.05072464)]
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
            var delta = Math.Abs(calculatedWeight - expectedValue);
            delta.ShouldBeLessThan(
                tolerance,
                $"difference between expected value {expectedValue} and calculated weight {calculatedWeight} exceeded tolerance"
            );
        }
    }
}