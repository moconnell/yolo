namespace YoloRuntime;

public static class DictionaryExtensions
{
    public static void CopyTo<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        IDictionary<TKey, TValue> destination)
    {
        foreach (var (key, value) in source)
        {
            destination[key] = value;
        }
    }
}