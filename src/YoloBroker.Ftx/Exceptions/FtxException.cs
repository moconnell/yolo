using CryptoExchange.Net.Objects;
using FTX.Net.Objects;
using YoloAbstractions;
using static System.Environment;

namespace YoloBroker.Ftx.Exceptions;

public class FtxException : BrokerException
{
    public FtxException(string message, CallResult result)
        : base($"{message} - {result.Error}")
    {
    }

    public FtxException(string message, WebCallResult result)
        : base($"{message} - {result.Error} ({result.ResponseStatusCode})")
    {
    }

    public FtxException(
        string message,
        IEnumerable<(Trade trade, WebCallResult<FTXOrder> result)> failedResults)
        : base(
            $"{message}:{NewLine}{NewLine}{string.Join(NewLine, failedResults.Select(FormatErrorInfo))}")
    {
    }

    private static string FormatErrorInfo(
        (Trade trade, WebCallResult<FTXOrder> result) tradeResult)
    {
        var (trade, result) = tradeResult;

        return $"({trade.AssetName}-{trade.AssetType}) {result.Error}";
    }
}