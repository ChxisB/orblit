using System.Text.Json;

namespace Orblit.Parity;

/// <summary>
/// Compares a JSON value the fixture holds with one the port produced.
/// Numbers use <see cref="Tolerance.Close"/>, so what C# gives has to agree to
/// 1e-9 and no closer. Objects match by key in any order, arrays by position.
/// </summary>
public static class JsonCompare
{
    /// <summary>
    /// Where the two first differ, as a path and both values, or null when
    /// they agree.
    /// </summary>
    public static string? FirstDifference(JsonElement expected, JsonElement actual, string path = "$") =>
        Kind(expected) != Kind(actual)
            ? Say(path, expected, actual)
            : expected.ValueKind switch
            {
                JsonValueKind.Object => Objects(expected, actual, path),
                JsonValueKind.Array => Arrays(expected, actual, path),
                JsonValueKind.Number => Tolerance.Close(expected.GetDouble(), actual.GetDouble()) ? null : Say(path, expected, actual),
                JsonValueKind.String => expected.GetString() == actual.GetString() ? null : Say(path, expected, actual),
                _ => expected.GetRawText() == actual.GetRawText() ? null : Say(path, expected, actual),
            };

    // True and false are two kinds in the reader and one here, so a flipped flag is reported as a value.
    private static JsonValueKind Kind(JsonElement value) =>
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? JsonValueKind.True : value.ValueKind;

    private static string? Objects(JsonElement expected, JsonElement actual, string path)
    {
        var names = expected.EnumerateObject().Select(p => p.Name).Order().ToList();
        if (!names.SequenceEqual(actual.EnumerateObject().Select(p => p.Name).Order()))
        {
            return $"{path}: expected keys {string.Join(",", names)}, got {actual}";
        }

        return names
            .Select(name => FirstDifference(expected.GetProperty(name), actual.GetProperty(name), $"{path}.{name}"))
            .FirstOrDefault(difference => difference is not null);
    }

    private static string? Arrays(JsonElement expected, JsonElement actual, string path)
    {
        if (expected.GetArrayLength() != actual.GetArrayLength())
        {
            return $"{path}: expected length {expected.GetArrayLength()}, got length {actual.GetArrayLength()}";
        }

        return expected.EnumerateArray()
            .Zip(actual.EnumerateArray())
            .Select((pair, index) => FirstDifference(pair.First, pair.Second, $"{path}[{index}]"))
            .FirstOrDefault(difference => difference is not null);
    }

    private static string Say(string path, JsonElement expected, JsonElement actual) =>
        $"{path}: expected {expected}, got {actual}";
}
