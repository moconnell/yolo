using System.Collections.Generic;
using System.Linq;
using CryptoExchange.Net.Objects;
using YoloAbstractions;
using static System.Environment;

namespace YoloBroker.Binance
{
    public class BinanceException : BrokerException
    {
        public BinanceException(string message, CallResult result)
            : base($"{message} - {result.Error}")
        {
        }

        public BinanceException(string message, WebCallResult result)
            : base($"{message} - {result.Error} ({result.ResponseStatusCode})")
        {
        }

        public BinanceException(
            string message,
            IEnumerable<(Trade trade, WebCallResultWrapper result)> tradeResults)
            : base($"{message}:{NewLine}{NewLine}{string.Join(NewLine, tradeResults.Select(FormatErrorInfo))}")
        {
        }

        private static string FormatErrorInfo(
            (Trade trade, WebCallResultWrapper result) tradeResult)
        {
            var (trade, result) = tradeResult;

            return $"({trade.AssetName}-{trade.AssetType}) {result.Error}";
        }
    }
}