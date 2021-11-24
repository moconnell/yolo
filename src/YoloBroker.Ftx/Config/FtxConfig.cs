namespace YoloBroker.Ftx.Config
{
    public class FtxConfig
    {
        public string ApiKey { get; init; }
        public string Secret { get; init; }
        public string BaseAddress { get; init; }
        public string? SubAccount { get; init; }
    }
}