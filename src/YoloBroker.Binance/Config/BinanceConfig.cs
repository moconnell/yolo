namespace YoloBroker.Binance
{
    public class BinanceConfig
    {
        public string ApiKey { get; init; }
        public string Secret { get; init; }
        public string BaseAddress { get; init; }
        public string BaseAddressCoinFutures { get; init; }
        public string BaseAddressUsdtFutures { get; init; }
        public string BaseAddressSocketClient { get; init; }
    }
}