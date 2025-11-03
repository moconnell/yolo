namespace YoloBroker.Interface;

public interface ITickerAliasService
{
    bool TryGetAlias(string ticker, out string? tickerAlias);
    
    bool TryGetTicker(string tickerAlias, out string? ticker);
}