using System.Collections.Concurrent;

namespace YoloRuntime;

public static class DictionaryExtensions
{
    public static void CopyDictTo<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> source,
        IDictionary<TKey, TValue> destination)
    {
        foreach (var (key, value) in source)
        {
            destination[key] = value;
        }
    }

    public static void CopyDictOfDictTo<TKey, TValue>(
        this IReadOnlyDictionary<TKey, IReadOnlyDictionary<TKey, TValue>> source,
        IDictionary<TKey, IDictionary<TKey, TValue>> destination,
        bool useConcurrentDictionary = true) where TKey : notnull
    {
        foreach (var (key, sourceDictionary) in source)
        {
            if (!destination.ContainsKey(key))
            {
                destination[key] = useConcurrentDictionary
                    ? new ConcurrentDictionary<TKey, TValue>()
                    : new Dictionary<TKey, TValue>();
            }

            var destinationDictionary = destination[key];

            foreach (var key2 in sourceDictionary.Keys)
            {
                destinationDictionary[key2] = sourceDictionary[key2];
            }
        }
    }
}