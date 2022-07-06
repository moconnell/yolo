using System.Collections.Generic;

namespace YoloAbstractions.Extensions;

public static partial class EnumerableExtensions
{
    public static string ToCsv<T>(this IEnumerable<T> enumerable) => string.Join(", ", enumerable);
}
