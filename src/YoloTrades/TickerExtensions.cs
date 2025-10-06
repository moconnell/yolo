using System;
using System.Text.RegularExpressions;

namespace YoloTrades;

public static class TickerExtensions
{
    private static readonly Regex TickerRegex = new(
        @"^(?<base>[\w\d]{3,5})([/-]?(?<quote>USD(\w)?))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static (string BaseAsset, string QuoteAsset) GetBaseAndQuoteAssets(this string ticker)
    {
        var match = TickerRegex.Match(ticker);
        if (!match.Success)
            throw new ArgumentException($"Invalid ticker format: {ticker}", nameof(ticker));

        var baseAsset = match.Groups["base"].Value.ToUpperInvariant();
        var quoteAsset = match.Groups["quote"].Success ? match.Groups["quote"].Value.ToUpperInvariant() : string.Empty;

        return (baseAsset, quoteAsset);
    }
}