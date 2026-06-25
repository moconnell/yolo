using System.Text.RegularExpressions;

namespace YoloAbstractions.Extensions;

public static class TickerExtensions
{
    private static readonly Regex TickerRegex = new(
        @"^(?<base>[\w\d&]{1,12}?)([/-]?(?<quote>USD(\w)?))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static (string BaseAsset, string QuoteAsset) GetBaseAndQuoteAssets(this string ticker)
    {
        var match = TickerRegex.Match(ticker);
        if (!match.Success)
            throw new ArgumentException($"Invalid ticker format: {ticker}", nameof(ticker));

        var baseAsset = NormalizeBaseAsset(match.Groups["base"].Value);
        var quoteAsset = match.Groups["quote"].Success ? match.Groups["quote"].Value.ToUpperInvariant() : string.Empty;

        return (baseAsset, quoteAsset);
    }
    private static string NormalizeBaseAsset(string baseAsset)
    {
        return baseAsset.Length > 1 &&
               char.IsLower(baseAsset[0]) &&
               baseAsset.Skip(1).All(c => char.IsUpper(c) || char.IsDigit(c))
            ? baseAsset
            : baseAsset.ToUpperInvariant();
    }
}
