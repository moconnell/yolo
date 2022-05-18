using System.Text.RegularExpressions;
using FTX.Net.Enums;
using FTX.Net.Objects;
using FTX.Net.Objects.Models;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    private static readonly Regex FutureMonthDayExpiryRegex =
        new(@"(?<=\w+-)(?<month>\d{2})(?<day>\d{2})$", RegexOptions.Compiled);

    private static readonly Regex FutureQuarterlyExpiryRegex =
        new(@"(?<=(\w+-)+)(?<year>\d{4})Q(?<quarter>\d)$", RegexOptions.Compiled);

    public static DateTime? GetExpiry(this FTXSymbol symbol)
    {
        if (symbol.Type != SymbolType.Future)
        {
            return null;
        }

        return symbol.Name switch
        {
            _ when FutureMonthDayExpiryRegex.IsMatch(symbol.Name) => GetDayMonthExpiry(
                symbol.Name),
            _ when FutureQuarterlyExpiryRegex.IsMatch(symbol.Name) => GetQuarterlyExpiry(
                symbol.Name),
            _ => null
        };
    }

    private static DateTime GetQuarterlyExpiry(string name)
    {
        DateTime LastFridayOf(int year, int month)
        {
            var firstOfNextMonth = new DateTime(year, month, 1).AddMonths(1);
            var deltaDays = -7 + (DayOfWeek.Friday - firstOfNextMonth.DayOfWeek);

            return firstOfNextMonth.AddDays(deltaDays);
        }

        var match = FutureQuarterlyExpiryRegex.Match(name);

        var year = Convert.ToInt32(match.Groups["year"]
            .Value);

        var q = Convert.ToInt32(match.Groups["quarter"]
            .Value);

        var month = q * 3;

        return LastFridayOf(year, month);
    }

    private static DateTime GetDayMonthExpiry(string name)
    {
        var match = FutureMonthDayExpiryRegex.Match(name);

        var month = Convert.ToInt32(match.Groups["month"]
            .Value);

        var day = Convert.ToInt32(match.Groups["day"]
            .Value);

        return new DateTime(DateTime.Now.Year, month, day);
    }
}