using CryptoExchange.Net.Objects;
using YoloBroker.Exceptions;

namespace YoloBroker.Hyperliquid.Exceptions;

public class HyperliquidException : BrokerException
{
    public HyperliquidException(string message, int errorCode)
        : base($"{message} ({errorCode})")
    {   
    }

    public HyperliquidException(string message, CallResult result)
        : base($"{message} - {result.Error}")
    {
    }

    public HyperliquidException(string message, HttpResult result)
        : base($"{message} - {result.Error} ({result.ResponseStatusCode})")
    {
    }

    public HyperliquidException(string message, WebSocketResult result)
        : base($"{message} - {result.Error}")
    {
    }

    // public HyperliquidException(
    //     string message,
    //     IEnumerable<(Trade trade, HttpCallResultWrapper result)> tradeResults)
    //     : base($"{message}:{NewLine}{NewLine}{string.Join(NewLine, tradeResults.Select(FormatErrorInfo))}")
    // {
    // }

    // private static string FormatErrorInfo(
    //     (Trade trade, HttpCallResultWrapper result) tradeResult)
    // {
    //     var (trade, result) = tradeResult;

    //     return $"({trade.AssetName}-{trade.AssetType}) {result.Error}";
    // }
}
