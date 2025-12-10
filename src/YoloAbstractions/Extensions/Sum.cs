namespace YoloAbstractions.Extensions;

public static partial class EnumerableExtensions
{
    public static Trade Sum(this IEnumerable<Trade> trades) => trades.Aggregate((t1, t2) => t1 + t2);
}