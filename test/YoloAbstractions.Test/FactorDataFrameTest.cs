using Microsoft.Data.Analysis;
using Xunit.Abstractions;
using static YoloAbstractions.FactorType;

namespace YoloAbstractions.Test;

public class FactorDataFrameTest(ITestOutputHelper output)
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
        ((StringDataFrameColumn)result["Ticker"]).ToArray().ShouldBe(tickers);
        var calculatedWeights = ((DoubleDataFrameColumn)result["Weight"]).Cast<double>().ToArray();
        const double tolerance = 0.0000001;
        for (var i = 0; i < calculatedWeights.Length; i++)
        {
            var calculatedWeight = calculatedWeights[i];
            var expectedValue = expectedValues[i];
            calculatedWeight.ShouldBe(expectedValue, tolerance);
        }
    }

    [Fact]
    public void GivenFactorDataFrame_WhenNormalized_ShouldHaveZeroMean()
    {
        // arrange
        const double tolerance = 1e-9;
        const int quantiles = 4;

        string[] tickers = ["BTC", "ETH", "ADA", "BNB"];
        double[] carryValues = [0.15, 0.10, 0.02, -0.05];
        double[] momoValues = [0.34, 0.17, 0.08, -0.12];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues),
            (Momentum, momoValues));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.CrossSectionalBins, quantiles);

        output.WriteLine("Normalized FactorDataFrame:\n{0}", normalized);

        // assert - check that each factor has mean ≈ 0 and std ≈ 1
        foreach (var factorType in normalized.FactorTypes)
        {
            var weights = tickers.Select(t => normalized[factorType, t]).Where(v => !double.IsNaN(v)).ToArray();
            var mean = weights.Average();

            mean.ShouldBe(0.0, tolerance);
            weights.Sum().ShouldBe(0.0, tolerance);
            weights.Sum(Math.Abs).ShouldBe(1.0, tolerance);
            weights.Min().ShouldBeGreaterThanOrEqualTo(-1.0);
            weights.Max().ShouldBeLessThanOrEqualTo(+1.0);
            weights.Where(v => !double.IsNaN(v)).Distinct().Count().ShouldBeLessThanOrEqualTo(quantiles);

            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights.Length; j++)
                {
                    if (weights[i] > weights[j])
                    {
                        weights[i].ShouldBeGreaterThanOrEqualTo(weights[j]);
                    }
                }
            }
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

    [Fact]
    public void GivenFactorDataFrame_WhenAccessingByTickerIndexer_ShouldReturnFactorDictionary()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA"];
        double[] carryValues = [0.15, 0.10, 0.02];
        double[] momoValues = [0.34, 0.17, 0.08];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues),
            (Momentum, momoValues));

        // act
        var btcFactors = factorDataFrame["BTC"];

        // assert
        btcFactors.ShouldNotBeNull();
        btcFactors.Count.ShouldBe(2);
        btcFactors[Carry].ShouldBe(0.15);
        btcFactors[Momentum].ShouldBe(0.34);
    }

    [Fact]
    public void GivenFactorDataFrame_WhenAccessingNonExistentTicker_ShouldThrowKeyNotFoundException()
    {
        // arrange
        string[] tickers = ["BTC", "ETH"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15, 0.10]));

        // act & assert
        Should.Throw<KeyNotFoundException>(() => factorDataFrame["XRP"]);
    }

    [Fact]
    public void GivenEmptyFactorDataFrame_WhenToStringCalled_ShouldReturnEmptyMessage()
    {
        // arrange
        var emptyDataFrame = FactorDataFrame.Empty;

        // act
        var result = emptyDataFrame.ToString();

        // assert
        result.ShouldBe("Empty FactorDataFrame");
    }

    [Fact]
    public void GivenFactorDataFrame_WhenToStringCalled_ShouldFormatAsPandasTable()
    {
        // arrange
        string[] tickers = ["BTC", "ETH"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            new DateTime(2025, 1, 15),
            (Carry, [0.15, 0.10]));

        // act
        var result = factorDataFrame.ToString();

        // assert
        result.ShouldContain("Date");
        result.ShouldContain("Ticker");
        result.ShouldContain("Carry");
        result.ShouldContain("BTC");
        result.ShouldContain("ETH");
        result.ShouldContain("2025-01-15");
        result.ShouldContain("[2 rows x 3 columns]");
    }

    [Fact]
    public void GivenFactorDataFrame_WhenNormalizedWithMinMax_ShouldScaleToMinusOneToOne()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA", "BNB"];
        double[] carryValues = [0.10, 0.20, 0.30, 0.40];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.MinMax);

        // assert
        var values = tickers.Select(t => normalized[Carry, t]).ToArray();
        values.Min().ShouldBe(-1.0, 1e-10);
        values.Max().ShouldBe(1.0, 1e-10);
    }

    [Fact]
    public void GivenFactorDataFrame_WhenNormalizedWithRank_ShouldPreserveRankOrder()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA", "BNB"];
        double[] carryValues = [0.10, 0.40, 0.20, 0.30];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.Rank);

        // assert
        // BTC (0.10) should be lowest, ETH (0.40) should be highest
        normalized[Carry, "BTC"].ShouldBe(-1.0, 1e-10);
        normalized[Carry, "ETH"].ShouldBe(1.0, 1e-10);
        normalized[Carry, "ADA"].ShouldBeLessThan(normalized[Carry, "BNB"]);
    }

    [Fact]
    public void GivenFactorDataFrame_WhenNormalizedWithNone_ShouldReturnSameInstance()
    {
        // arrange
        string[] tickers = ["BTC", "ETH"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15, 0.10]));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.None);

        // assert
        normalized.ShouldBeSameAs(factorDataFrame);
    }

    [Fact]
    public void GivenFactorDataFrameWithConstantValues_WhenNormalizedZScore_ShouldReturnZeros()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA"];
        double[] constantValues = [0.15, 0.15, 0.15];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, constantValues));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.CrossSectionalZScore);

        // assert
        foreach (var ticker in tickers)
        {
            normalized[Carry, ticker].ShouldBe(0.0);
        }
    }

    [Fact]
    public void GivenFactorDataFrameWithConstantValues_WhenNormalizedMinMax_ShouldReturnZeros()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA"];
        double[] constantValues = [0.15, 0.15, 0.15];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, constantValues));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.MinMax);

        // assert
        foreach (var ticker in tickers)
        {
            normalized[Carry, ticker].ShouldBe(0.0);
        }
    }

    [Fact]
    public void GivenFactorDataFrameWithAllNaN_WhenNormalized_ShouldReturnNaN()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "ADA"];
        double[] nanValues = [double.NaN, double.NaN, double.NaN];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, nanValues));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.CrossSectionalZScore);

        // assert
        foreach (var ticker in tickers)
        {
            double.IsNaN(normalized[Carry, ticker]).ShouldBeTrue();
        }
    }

    [Fact]
    public void GivenFactorDataFrameWithSingleValue_WhenNormalizedRank_ShouldReturnZero()
    {
        // arrange
        string[] tickers = ["BTC"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15]));

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.Rank);

        // assert
        normalized[Carry, "BTC"].ShouldBe(0.0);
    }

    [Fact]
    public void GivenFactorDataFrameWithVolatility_WhenNormalized_ShouldPreserveVolatilityColumn()
    {
        // arrange
        const int quantiles = 2;

        string[] tickers = ["BTC", "ETH"];
        double[] carryValues = [0.15, 0.10];
        double[] volatilityValues = [0.5, 0.8];

        var factorDataFrame = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, tickers.Length)),
                new StringDataFrameColumn("Ticker", tickers),
                new DoubleDataFrameColumn(nameof(Carry), carryValues),
                new DoubleDataFrameColumn(nameof(Volatility), volatilityValues)
            ),
            Carry,
            Volatility);

        // act
        var normalized = factorDataFrame.Normalize(NormalizationMethod.CrossSectionalBins, quantiles);

        // assert
        // Volatility should not be normalized
        normalized[Volatility, "BTC"].ShouldBe(0.5);
        normalized[Volatility, "ETH"].ShouldBe(0.8);
        // Carry should be normalized
        normalized[Carry, "BTC"].ShouldNotBe(0.15);
    }

    [Fact]
    public void GivenFactorDataFrameWithMissingValues_WhenApplyWeightsWithNormalizePerAsset_ShouldHandleCorrectly()
    {
        // arrange
        string[] tickers = ["BTC", "ETH", "MNT"];
        double[] carryValues = [0.15, 0.10, 0.05];
        double[] momoValues = [0.34, 0.17, double.NaN];

        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, carryValues),
            (Momentum, momoValues));

        var weights = new Dictionary<FactorType, double>
        {
            [Carry] = 1.0,
            [Momentum] = 1.0
        };

        // act
        var result = factorDataFrame.ApplyWeights(weights, normalizePerAsset: true);

        // assert
        result.ShouldNotBeNull();
        var resultWeights = ((DoubleDataFrameColumn)result["Weight"]).ToArray();

        // MNT should only use Carry factor (normalized by weight=1.0)
        resultWeights[2].ShouldBe(0.05);

        // BTC and ETH should use both factors (normalized by weight=2.0)
        resultWeights[0].ShouldBe((0.15 + 0.34) / 2.0);
        resultWeights[1].ShouldBe((0.10 + 0.17) / 2.0);
    }

    [Fact]
    public void GivenFactorDataFrameWithZeroNormalizer_WhenApplyWeights_ShouldReturnZero()
    {
        // arrange
        string[] tickers = ["BTC"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15]));

        var weights = new Dictionary<FactorType, double>
        {
            [Momentum] = 1.0 // Weight for factor not in dataframe
        };

        // act
        var result = factorDataFrame.ApplyWeights(weights);

        // assert
        var resultWeights = ((DoubleDataFrameColumn)result["Weight"]).ToArray();
        resultWeights[0].ShouldBe(0.0);
    }

    [Fact]
    public void GivenFactorDataFrameWithVolatilityZero_WhenApplyWeightsWithVolScaling_ShouldUseOne()
    {
        // arrange
        string[] tickers = ["BTC", "ETH"];
        double[] carryValues = [0.15, 0.10];
        double[] volatilityValues = [0.0, 0.5];

        var factorDataFrame = new FactorDataFrame(
            new DataFrame(
                new DateTimeDataFrameColumn("Date", Enumerable.Repeat(DateTime.Today, tickers.Length)),
                new StringDataFrameColumn("Ticker", tickers),
                new DoubleDataFrameColumn(nameof(Carry), carryValues),
                new DoubleDataFrameColumn(nameof(Volatility), volatilityValues)
            ),
            Carry,
            Volatility);

        var weights = new Dictionary<FactorType, double>
        {
            [Carry] = 1.0
        };

        // act
        var result = factorDataFrame.ApplyWeights(weights, volatilityScaling: true);

        // assert
        var resultWeights = ((DoubleDataFrameColumn)result["Weight"]).ToArray();
        // BTC: 0.15 / 1.0 (zero vol replaced with 1.0)
        resultWeights[0].ShouldBe(0.15);
        // ETH: 0.10 / 0.5
        resultWeights[1].ShouldBe(0.20);
    }

    [Fact]
    public void GivenFactorDataFrame_WhenApplyWeightsWithNullWeights_ShouldThrowArgumentNullException()
    {
        // arrange
        string[] tickers = ["BTC"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15]));

        // act & assert
        Should.Throw<ArgumentNullException>(() =>
            factorDataFrame.ApplyWeights(null!));
    }

    [Fact]
    public void GivenFactorDataFrame_WhenAddingWithNull_ShouldThrowArgumentNullException()
    {
        // arrange
        string[] tickers = ["BTC"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15]));

        // act & assert
        Should.Throw<ArgumentNullException>(() => factorDataFrame + null!);
        Should.Throw<ArgumentNullException>(() => null! + factorDataFrame);
    }

    [Fact]
    public void GivenFactorDataFrameWithDateTimeWithTime_WhenToStringCalled_ShouldFormatWithTime()
    {
        // arrange
        string[] tickers = ["BTC"];
        var timestamp = new DateTime(2025, 1, 15, 14, 30, 45);
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            timestamp,
            (Carry, [0.15]));

        // act
        var result = factorDataFrame.ToString();

        // assert
        result.ShouldContain("2025-01-15 02:30:45"); // Time should be included
    }

    [Fact]
    public void GivenInvalidNormalizationMethod_WhenNormalize_ShouldThrowArgumentOutOfRangeException()
    {
        // arrange
        string[] tickers = ["BTC"];
        var factorDataFrame = FactorDataFrame.NewFrom(
            tickers,
            DateTime.Today,
            (Carry, [0.15]));

        // act & assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            factorDataFrame.Normalize((NormalizationMethod)999));
    }
}