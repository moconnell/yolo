using YoloBroker.Interface;

namespace YoloBroker;

public class TickerAliasService : ITickerAliasService
{
    private readonly IReadOnlyDictionary<string, string> _aliases;
    private readonly IReadOnlyDictionary<string, string> _tickers;

    public TickerAliasService(IReadOnlyDictionary<string, string> aliases)
    {
        _aliases = aliases;
        _tickers = aliases.Select(kvp => new KeyValuePair<string, string>(kvp.Value, kvp.Key)).ToDictionary();
    }

    public bool TryGetAlias(string ticker, out string? tickerAlias) => _aliases.TryGetValue(ticker, out tickerAlias);
    
    public bool TryGetTicker(string tickerAlias, out string? ticker) => _tickers.TryGetValue(tickerAlias, out ticker);
}