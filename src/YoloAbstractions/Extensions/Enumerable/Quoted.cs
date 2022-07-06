using System.Collections.Generic;
using System.Linq;

namespace YoloAbstractions.Extensions;

public static partial class EnumerableExtensions
{
    public static IEnumerable<string> Quoted<T>(this IEnumerable<T> enumerable) => enumerable.Select(x => $"'{x}'");
}