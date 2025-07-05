using System.Collections.Generic;

namespace YoloKonsole.Comparers;

public class ReadOnlyDictionaryComparer<TKey, TValue> : IComparer<IReadOnlyDictionary<TKey, TValue>>
{
    private readonly IComparer<TValue> _valueComparer;

    public ReadOnlyDictionaryComparer(IComparer<TValue> valueComparer) => _valueComparer = valueComparer;

    public int Compare(IReadOnlyDictionary<TKey, TValue>? x, IReadOnlyDictionary<TKey, TValue>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (ReferenceEquals(null, y))
        {
            return 1;
        }

        if (ReferenceEquals(null, x))
        {
            return -1;
        }

        if (x.Count < y.Count)
            return 1;
        if (x.Count > y.Count)
            return -1;

        foreach (var key in x.Keys)
        {
            if (y.TryGetValue(key, out var yValue))
            {
                var xValue = x[key];
                var valueComparison = _valueComparer.Compare(xValue, yValue);
                if (valueComparison != 0)
                {
                    return valueComparison;
                }
            }
            else
            {
                return -1;
            }
        }

        return 0;
    }
}