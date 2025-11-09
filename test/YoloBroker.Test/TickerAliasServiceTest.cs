using Shouldly;

namespace YoloBroker.Test;

public class TickerAliasServiceTest
{
    [Fact]
    public void Constructor_WhenGivenAliases_ShouldCreateBidirectionalMapping()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" }
        };

        // act
        var service = new TickerAliasService(aliases);

        // assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void TryGetAlias_WhenTickerExists_ShouldReturnTrueAndAlias()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act
        var result = service.TryGetAlias("BTC", out var alias);

        // assert
        result.ShouldBeTrue();
        alias.ShouldBe("BTCUSDT");
    }

    [Fact]
    public void TryGetAlias_WhenTickerDoesNotExist_ShouldReturnFalse()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act
        var result = service.TryGetAlias("ETH", out var alias);

        // assert
        result.ShouldBeFalse();
        alias.ShouldBeNull();
    }

    [Fact]
    public void TryGetAlias_WhenEmptyDictionary_ShouldReturnFalse()
    {
        // arrange
        var aliases = new Dictionary<string, string>();
        var service = new TickerAliasService(aliases);

        // act
        var result = service.TryGetAlias("BTC", out var alias);

        // assert
        result.ShouldBeFalse();
        alias.ShouldBeNull();
    }

    [Fact]
    public void TryGetAlias_WhenMultipleAliases_ShouldReturnCorrectAlias()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" },
            { "SOL", "SOLUSDT" },
            { "ADA", "ADAUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert
        service.TryGetAlias("BTC", out var btcAlias).ShouldBeTrue();
        btcAlias.ShouldBe("BTCUSDT");

        service.TryGetAlias("ETH", out var ethAlias).ShouldBeTrue();
        ethAlias.ShouldBe("ETHUSDT");

        service.TryGetAlias("SOL", out var solAlias).ShouldBeTrue();
        solAlias.ShouldBe("SOLUSDT");

        service.TryGetAlias("ADA", out var adaAlias).ShouldBeTrue();
        adaAlias.ShouldBe("ADAUSDT");
    }

    [Fact]
    public void TryGetTicker_WhenAliasExists_ShouldReturnTrueAndTicker()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act
        var result = service.TryGetTicker("BTCUSDT", out var ticker);

        // assert
        result.ShouldBeTrue();
        ticker.ShouldBe("BTC");
    }

    [Fact]
    public void TryGetTicker_WhenAliasDoesNotExist_ShouldReturnFalse()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act
        var result = service.TryGetTicker("ETHUSDT", out var ticker);

        // assert
        result.ShouldBeFalse();
        ticker.ShouldBeNull();
    }

    [Fact]
    public void TryGetTicker_WhenEmptyDictionary_ShouldReturnFalse()
    {
        // arrange
        var aliases = new Dictionary<string, string>();
        var service = new TickerAliasService(aliases);

        // act
        var result = service.TryGetTicker("BTCUSDT", out var ticker);

        // assert
        result.ShouldBeFalse();
        ticker.ShouldBeNull();
    }

    [Fact]
    public void TryGetTicker_WhenMultipleAliases_ShouldReturnCorrectTicker()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" },
            { "SOL", "SOLUSDT" },
            { "ADA", "ADAUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert
        service.TryGetTicker("BTCUSDT", out var btcTicker).ShouldBeTrue();
        btcTicker.ShouldBe("BTC");

        service.TryGetTicker("ETHUSDT", out var ethTicker).ShouldBeTrue();
        ethTicker.ShouldBe("ETH");

        service.TryGetTicker("SOLUSDT", out var solTicker).ShouldBeTrue();
        solTicker.ShouldBe("SOL");

        service.TryGetTicker("ADAUSDT", out var adaTicker).ShouldBeTrue();
        adaTicker.ShouldBe("ADA");
    }

    [Fact]
    public void BidirectionalMapping_ShouldWorkBothWays()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert - ticker to alias
        service.TryGetAlias("BTC", out var alias).ShouldBeTrue();
        alias.ShouldBe("BTCUSDT");

        // act & assert - alias back to ticker
        service.TryGetTicker("BTCUSDT", out var ticker).ShouldBeTrue();
        ticker.ShouldBe("BTC");
    }

    [Fact]
    public void BidirectionalMapping_WithMultipleEntries_ShouldMaintainConsistency()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "ETH", "ETHUSDT" },
            { "SOL", "SOLUSDT" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert - round trip for each ticker
        foreach (var kvp in aliases)
        {
            service.TryGetAlias(kvp.Key, out var alias).ShouldBeTrue();
            alias.ShouldBe(kvp.Value);

            service.TryGetTicker(kvp.Value, out var ticker).ShouldBeTrue();
            ticker.ShouldBe(kvp.Key);
        }
    }

    [Fact]
    public void TryGetAlias_WithCaseSensitiveTicker_ShouldRespectCase()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "btc", "btcusdt" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert
        service.TryGetAlias("BTC", out var upperAlias).ShouldBeTrue();
        upperAlias.ShouldBe("BTCUSDT");

        service.TryGetAlias("btc", out var lowerAlias).ShouldBeTrue();
        lowerAlias.ShouldBe("btcusdt");

        service.TryGetAlias("Btc", out var mixedAlias).ShouldBeFalse();
        mixedAlias.ShouldBeNull();
    }

    [Fact]
    public void TryGetTicker_WithCaseSensitiveAlias_ShouldRespectCase()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" },
            { "btc", "btcusdt" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert
        service.TryGetTicker("BTCUSDT", out var upperTicker).ShouldBeTrue();
        upperTicker.ShouldBe("BTC");

        service.TryGetTicker("btcusdt", out var lowerTicker).ShouldBeTrue();
        lowerTicker.ShouldBe("btc");

        service.TryGetTicker("BtcUsdt", out var mixedTicker).ShouldBeFalse();
        mixedTicker.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithSingleEntry_ShouldCreateValidMapping()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC", "BTCUSDT" }
        };

        // act
        var service = new TickerAliasService(aliases);

        // assert
        service.TryGetAlias("BTC", out var alias).ShouldBeTrue();
        alias.ShouldBe("BTCUSDT");

        service.TryGetTicker("BTCUSDT", out var ticker).ShouldBeTrue();
        ticker.ShouldBe("BTC");
    }

    [Fact]
    public void TryGetAlias_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC-USD", "BTC/USDT" },
            { "ETH_USDT", "ETH:USDT" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert
        service.TryGetAlias("BTC-USD", out var btcAlias).ShouldBeTrue();
        btcAlias.ShouldBe("BTC/USDT");

        service.TryGetAlias("ETH_USDT", out var ethAlias).ShouldBeTrue();
        ethAlias.ShouldBe("ETH:USDT");
    }

    [Fact]
    public void TryGetTicker_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // arrange
        var aliases = new Dictionary<string, string>
        {
            { "BTC-USD", "BTC/USDT" },
            { "ETH_USDT", "ETH:USDT" }
        };
        var service = new TickerAliasService(aliases);

        // act & assert
        service.TryGetTicker("BTC/USDT", out var btcTicker).ShouldBeTrue();
        btcTicker.ShouldBe("BTC-USD");

        service.TryGetTicker("ETH:USDT", out var ethTicker).ShouldBeTrue();
        ethTicker.ShouldBe("ETH_USDT");
    }
}
