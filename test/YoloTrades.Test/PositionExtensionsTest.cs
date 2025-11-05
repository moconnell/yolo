using System.Collections.Generic;
using Moq;
using Xunit;
using YoloAbstractions;

namespace YoloTrades.Test;

public class PositionExtensionsTest
{
    [Fact]
    public void GetTotalValue_WithBaseCurrencyPosition_ReturnsSum()
    {
        // Arrange
        const string baseCurrency = "USDC";
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            [baseCurrency] = new List<Position>
            {
                new("account1", baseCurrency, AssetType.Spot, 1000m),
                new("account1", baseCurrency, AssetType.Spot, 500m)
            }
        };
        var markets = Mock.Of<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>();

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(1500m, totalValue);
    }

    [Fact]
    public void GetTotalValue_WithLongPosition_UsesMaxBidPrice()
    {
        // Arrange
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["BTC"] = new List<Position>
            {
                new("account1", "BTC", AssetType.Spot, 2m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] = new List<MarketInfo>
            {
                new("BTC-USDC", "BTC", "USDC", AssetType.Spot, System.DateTime.UtcNow, Bid: 50000m, Ask: 50100m),
                new("BTC", "BTC", "USDC", AssetType.Future, System.DateTime.UtcNow, Bid: 49900m, Ask: 50000m)
            }
        };
        var baseCurrency = "USDC";

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(100000m, totalValue); // 2 * 50000 (max bid)
    }

    [Fact]
    public void GetTotalValue_WithShortPosition_UsesMinAskPrice()
    {
        // Arrange
        const string baseCurrency = "USDC";
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["ETH"] = new List<Position>
            {
                new("account1", "ETH", AssetType.Spot, -5m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["ETH"] = new List<MarketInfo>
            {
                new("ETH-USDC", "ETH", "USDC", AssetType.Spot, System.DateTime.UtcNow, Bid: 3000m, Ask: 3100m),
                new("ETH", "ETH", "USDC", AssetType.Future, System.DateTime.UtcNow, Bid: 2900m, Ask: 3050m)
            }
        };

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(-15250m, totalValue); // -5 * 3050 (min ask)
    }

    [Fact]
    public void GetTotalValue_WithMultipleAssets_ReturnsTotalSum()
    {
        // Arrange
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["USDC"] = new List<Position>
            {
                new("account1", "USDC", AssetType.Spot, 10000m)
            },
            ["BTC"] = new List<Position>
            {
                new("account1", "BTC", AssetType.Spot, 1m)
            },
            ["ETH"] = new List<Position>
            {
                new("account1", "ETH", AssetType.Spot, 5m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] = new List<MarketInfo>
            {
                new("BTC-USDC", "BTC", "USDC", AssetType.Spot, System.DateTime.UtcNow, Bid: 50000m, Ask: 50100m)
            },
            ["ETH"] = new List<MarketInfo>
            {
                new("ETH-USDC", "ETH", "USDC", AssetType.Spot, System.DateTime.UtcNow, Bid: 3000m, Ask: 3100m)
            }
        };
        var baseCurrency = "USDC";

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(75000m, totalValue); // 10000 + (1 * 50000) + (5 * 3000)
    }

    [Fact]
    public void GetTotalValue_WithNoMatchingMarket_UsesZeroPrice()
    {
        // Arrange
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["SOL"] = new List<Position>
            {
                new("account1", "SOL", AssetType.Spot, 10m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["SOL"] = new List<MarketInfo>
            {
                new("SOL-USDT", "SOL", "USDT", AssetType.Spot, System.DateTime.UtcNow, Bid: 100m, Ask: 101m)
            }
        };
        var baseCurrency = "USDC";

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(0m, totalValue);
    }

    [Fact]
    public void GetTotalValue_WithNullPrices_IgnoresThoseMarkets()
    {
        // Arrange
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["BTC"] = new List<Position>
            {
                new("account1", "BTC", AssetType.Spot, 1m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] = new List<MarketInfo>
            {
                new("BTC-USDC-1", "BTC", "USDC", AssetType.Spot, System.DateTime.UtcNow, Bid: null, Ask: null),
                new("BTC-USDC-2", "BTC", "USDC", AssetType.Spot, System.DateTime.UtcNow, Bid: 50000m, Ask: 50100m)
            }
        };
        var baseCurrency = "USDC";

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(50000m, totalValue);
    }

    [Fact]
    public void GetTotalValue_WithEmptyPositions_ReturnsZero()
    {
        // Arrange
        const string baseCurrency = "USDC";
        var positions = new Dictionary<string, IReadOnlyList<Position>>();
        var markets = Mock.Of<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>();

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(0m, totalValue);
    }

    [Fact]
    public void GetTotalValue_WithZeroAmount_ReturnsZero()
    {
        // Arrange
        const string baseCurrency = "USDC";
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["BTC"] = new List<Position>
            {
                new("account1", "BTC", AssetType.Spot, 0m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] = new List<MarketInfo>
            {
                new("BTC-USDC", "BTC", baseCurrency, AssetType.Spot, System.DateTime.UtcNow, Bid: 50000m, Ask: 50100m)
            }
        };

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(0m, totalValue);
    }

    [Fact]
    public void GetTotalValue_WithCaseInsensitiveQuoteAsset_MatchesMarket()
    {
        // Arrange
        const string baseCurrency = "USDC";
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["BTC"] = new List<Position>
            {
                new("account1", "BTC", AssetType.Spot, 1m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] = new List<MarketInfo>
            {
                new("BTC-USDC", "BTC", "usdc", AssetType.Spot, System.DateTime.UtcNow, Bid: 50000m, Ask: 50100m)
            }
        };

        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(50000m, totalValue);
    }

    [Fact]
    public void GetTotalValue_WithMixedLongAndShortPositions_CalculatesCorrectly()
    {
        // Arrange
        const string baseCurrency = "USDC";
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["BTC"] = new List<Position>
            {
                new("account1", "BTC", AssetType.Spot, 2m)
            },
            ["ETH"] = new List<Position>
            {
                new("account1", "ETH", AssetType.Spot, -10m)
            }
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["BTC"] = new List<MarketInfo>
            {
                new("BTC-USDC", "BTC", baseCurrency, AssetType.Spot, System.DateTime.UtcNow, Bid: 50000m, Ask: 50100m)
            },
            ["ETH"] = new List<MarketInfo>
            {
                new("ETH-USDC", "ETH", baseCurrency, AssetType.Spot, System.DateTime.UtcNow, Bid: 3000m, Ask: 3100m)
            }
        };


        // Act
        var totalValue = positions.GetTotalValue(markets, baseCurrency);

        // Assert
        Assert.Equal(69000m, totalValue); // (2 * 50000) + (-10 * 3100)
    }
}