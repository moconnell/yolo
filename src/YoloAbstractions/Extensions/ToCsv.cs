namespace YoloAbstractions.Extensions;

public static partial class EnumerableExtensions
{
    public static string ToCsv<T>(this IEnumerable<T> enumerable, string? separator = ",") => string.Join(separator, enumerable);
}