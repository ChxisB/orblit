using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Orblit.VectorMath;

namespace Orblit.Terrain.Tests;

/// <summary>Writes what the port computed in the shape the Dart exporter wrote.</summary>
internal static class Dump
{
    private const long ExactLimit = 9007199254740992;

    /// <summary>An int JSON holds exactly. Past 2^53 a reader gives a double, so those travel as text.</summary>
    public static JsonNode Big(long value) =>
        value > ExactLimit || value < -ExactLimit
            ? JsonValue.Create(value.ToString(CultureInfo.InvariantCulture))!
            : JsonValue.Create(value);

    /// <summary>A double, with NaN and the infinities as the strings the fixture uses.</summary>
    public static JsonNode Num(double value) =>
        double.IsFinite(value)
            ? JsonValue.Create(value)
            : JsonValue.Create(value.ToString(CultureInfo.InvariantCulture))!;

    public static JsonNode? Num(double? value) => value is { } number ? Num(number) : null;

    public static JsonArray Arr<T>(IEnumerable<T> items, Func<T, JsonNode?> convert) =>
        new([.. items.Select(convert)]);

    /// <summary>The bytes of a typed array, as the exporter's dataB64 wrote them.</summary>
    public static string Base64<T>(T[] items)
        where T : unmanaged =>
        Convert.ToBase64String(MemoryMarshal.AsBytes(items.AsSpan()));

    /// <summary>What a call gave, or that it threw. The kind of exception is not compared.</summary>
    public static JsonObject Attempt(Func<JsonNode?> body)
    {
        try
        {
            return new JsonObject { ["value"] = body() };
        }
        catch (Exception)
        {
            return new JsonObject { ["throws"] = true };
        }
    }

    public static JsonArray KeyJson(RegionKey key) => new(Big(key.X), Big(key.Z));

    public static JsonNode? Vec(Vector3? v) =>
        v is { } vector ? new JsonArray(Num(vector.X), Num(vector.Y), Num(vector.Z)) : null;

    /// <summary>A decoded JSON value, as the exporter's tree function wrote it.</summary>
    public static JsonNode? Tree(object? value) => value switch
    {
        null => null,
        bool flag => flag,
        int number => Big(number),
        long number => Big(number),
        double number => Num(number),
        string text => JsonValue.Create(text),
        IReadOnlyDictionary<string, object?> map => new JsonObject(
            map.Select(pair => KeyValuePair.Create(pair.Key, Tree(pair.Value)))),
        IReadOnlyList<object?> list => Arr(list, Tree),
        _ => throw new InvalidOperationException($"no JSON for {value.GetType()}"),
    };
}
