namespace YoloTrades;

public static class TickerExtensions
{
    public static (string, string) SplitConstituents(
        this string ticker,
        string separator = "/")
    {
        var sides = ticker.Split(separator);
        return (sides[0], sides[1]);
    }
}