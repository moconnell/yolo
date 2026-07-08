using System.Globalization;

namespace YoloFunk.Infrastructure;

internal sealed class HttpQueryParameters
{
    private readonly Dictionary<string, string[]> _values;

    private HttpQueryParameters(Dictionary<string, string[]> values)
    {
        _values = values;
    }

    public static HttpQueryParameters Parse(Uri url)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var query = url.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace("+", " "));
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = parts.Length > 1
                ? Uri.UnescapeDataString(parts[1].Replace("+", " "))
                : string.Empty;

            if (!values.TryGetValue(key, out var list))
            {
                list = [];
                values[key] = list;
            }

            list.Add(value);
        }

        return new HttpQueryParameters(
            values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
    }

    public string? GetString(string name)
    {
        return _values.TryGetValue(name, out var values)
            ? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            : null;
    }

    public int GetInt32(string name, int defaultValue, int min, int max)
    {
        var value = GetString(name);
        if (!int.TryParse(value, out var parsed))
            return defaultValue;

        return Math.Clamp(parsed, min, max);
    }

    public DateTimeOffset? GetDateTimeOffset(string name)
    {
        var value = GetString(name);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            var dateTime = string.Equals(name, "to", StringComparison.OrdinalIgnoreCase)
                ? date.ToDateTime(TimeOnly.MaxValue)
                : date.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(dateTime, TimeSpan.Zero);
        }

        string[] formats =
        [
            "O",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
            "yyyy-MM-dd'T'HH:mm:sszzz"
        ];

        return DateTimeOffset.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
