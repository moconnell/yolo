namespace YoloBroker.Ftx.Config;

public class FtxConfig
{
    public string ApiKey { get; init; }
    public string Secret { get; init; }
    public string RestApi { get; init; }
    public string WebSocketApi { get; init; }
    public bool? PostOnly { get; init; }
    public string? SubAccount { get; init; }
}