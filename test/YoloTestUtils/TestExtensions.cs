using Newtonsoft.Json;

namespace YoloTestUtils;

public static class TestExtensions
{
    public static Dictionary<TKey, IEnumerable<TValue>> ToEnumerableDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue[]> dictionary) where TKey : notnull
    {
        return dictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Cast<TValue>());
    }

    public static async Task<T> DeserializeAsync<T>(this string path)
    {
        await using var stream = File.OpenRead(path);
        using var streamReader = new StreamReader(stream);
        var json = await streamReader.ReadToEndAsync();

        return JsonConvert.DeserializeObject<T>(json);
    }
}