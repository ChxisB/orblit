namespace Orblit.Scene;

/// <summary>
/// Reading JSON that may be anything at all. A glTF comes from any tool on earth, so every value in
/// one may be missing or the wrong type, and none of that is a reason to stop reading.
/// </summary>
internal static class GltfJson
{
    /// <summary>The standard extension for a node that is switched off. Written and read here, so the name sits in one place.</summary>
    public const string NodeVisibility = "KHR_node_visibility";

    /// <summary>Where this package writes its own record, and where it looks for it.</summary>
    private const string Mark = "orblit";

    public static JsonList List(object? raw) => raw as JsonList ?? [];

    public static List<JsonMap> Maps(object? raw) => [.. List(raw).OfType<JsonMap>()];

    public static string? Text(object? raw) => raw as string;

    public static double? Number(object? raw) => Values.Num(raw);

    /// <summary>A whole number that is not negative, or null. Wide, because an index may be anything a file says.</summary>
    public static long? Index(object? raw) => raw is long and >= 0 ? (long)raw : null;

    /// <summary>An index as an <see cref="int"/>. One past the int range is past the end of any list too, so it stays out of range.</summary>
    public static int Saturate(long index) => (int)Math.Min(index, int.MaxValue);

    public static List<long> Ints(object? raw) => [.. List(raw).OfType<long>()];

    public static List<double> Doubles(object? raw) => [.. List(raw).Select(Values.Num).OfType<double>()];

    /// <summary>The record this package left on <paramref name="holder"/>, or null when there is none.</summary>
    public static JsonMap? OursOn(JsonMap holder) =>
        holder.GetValueOrDefault("extras") is JsonMap extras && extras.GetValueOrDefault(Mark) is JsonMap ours ? ours : null;

    /// <summary>
    /// Whether the standard extension shows or hides <paramref name="node"/>, or null when it says
    /// nothing. A value that is not a flag says nothing, because a file may hold anything.
    /// </summary>
    public static bool? ShownOn(JsonMap node) =>
        Values.Object(Values.Object(node.GetValueOrDefault("extensions")).GetValueOrDefault(NodeVisibility))
            .GetValueOrDefault("visible") as bool?;
}
