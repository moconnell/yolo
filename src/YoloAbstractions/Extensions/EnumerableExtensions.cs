using System.Collections.Generic;

namespace YoloAbstractions.Extensions
{
    public static class EnumerableExtensions
    {
        public static string ToCsv<T>(this IEnumerable<T> enumerable)
        {
            return string.Join(", ", enumerable);
        }
    }
}