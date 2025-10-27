namespace YoloBroker.Interface;

public interface IGetTickerAlias
{
    bool TryGetAlias(string ticker, out string? tickerAlias);
    
    bool TryGetTicker(string tickerAlias, out string? ticker);
}