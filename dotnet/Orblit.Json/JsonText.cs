namespace Orblit.Json;

/// <summary>
/// JSON as Orblit files hold it. System.Text.Json differs in places
/// that change what a file means: it reads <c>1e999</c> as an error where this reads infinity,
/// reads a whole number past 64 bits as an error where this reads a double, and escapes
/// <c>&gt;</c> and every non-ASCII character where this writes them as they are.
/// </summary>
/// <remarks>
/// A value is null, a bool, a <c>long</c>, a <c>double</c>, a string, a
/// <c>List&lt;object?&gt;</c> or an <c>OrderedDictionary&lt;string, object?&gt;</c>.
/// </remarks>
public static class JsonText
{
    /// <summary>The deepest nesting <see cref="Decode"/> reads.</summary>
    public const int MaxDepth = 512;

    /// <summary>Reads <paramref name="text"/>. Throws <see cref="FormatException"/> when it is not JSON.</summary>
    public static object? Decode(string text) => new JsonReader(text).ReadAll();

    /// <summary>
    /// Writes <paramref name="value"/>. With <paramref name="indent"/> every list item and map
    /// entry takes its own line, as <c>JsonEncoder.withIndent</c> does.
    /// </summary>
    public static string Encode(object? value, string? indent = null) => JsonWriter.Write(value, indent);
}
