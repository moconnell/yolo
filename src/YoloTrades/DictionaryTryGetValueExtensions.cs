using System.Collections.Generic;

namespace YoloTrades;

internal static class DictionaryTryGetValueExtensions
{
    public static TValue GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key, TValue defaultValue) =>
        dictionary.TryGetValue(key, out var value)
            ? value
            : defaultValue;
}