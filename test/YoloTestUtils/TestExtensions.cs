using System.Net;
using CryptoExchange.Net.Objects;
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

    public static Dictionary<TKey, IDictionary<TKey, TValue>> ToDictionaryOfDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue[]> dictionary,
        Func<TValue, TKey> keySelector) where TKey : notnull
    {
        return dictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDictionary(keySelector) as IDictionary<TKey, TValue>);
    }

    public static async Task<T?> DeserializeAsync<T>(this string path)
    {
        await using var stream = File.OpenRead(path);
        using var streamReader = new StreamReader(stream);
        var json = await streamReader.ReadToEndAsync();
        var deserializedObject = JsonConvert.DeserializeObject<T>(json);

        return deserializedObject;
    }

    public static WebCallResult<T> ToWebCallResult<T>(this T data) => new(
        HttpStatusCode.OK,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        data,
        null);
}