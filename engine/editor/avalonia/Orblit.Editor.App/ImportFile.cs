using System.Globalization;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>One import option: its key in the file, such as meshes/ensure_tangents, and the row the Import tab shows for it.</summary>
internal sealed record ImportOption(string Key, EntityProperty Property);

/// <summary>
/// The text of a file's .import file. The engine writes it beside the file it imports. Its [params] section holds
/// the options the importer reads, one key=value line each. The text is kept whole so a change rewrites one line.
/// </summary>
internal sealed class ImportFile(string text)
{
    // Variant types, as the engine numbers them. Nil marks a value the tab shows read only.
    const int Nil = 0;
    const int Bool = 1;
    const int Int = 2;
    const int Float = 3;
    const int TextType = 4;

    const string Params = "[params]";

    /// <summary>What the engine adds to a file's name to name its .import file.</summary>
    internal const string Suffix = ".import";

    readonly string[] _lines = text.Split('\n');

    public string Text => string.Join('\n', _lines);

    /// <summary>The importer's name, such as Scene or Texture, or an empty string when the file names none.</summary>
    public string Importer => Words(_lines.Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("importer=", StringComparison.Ordinal))?["importer=".Length..].Trim('"') ?? "");

    /// <summary>
    /// The options in file order. A key's first part is its group. A key that starts with an underscore is the
    /// importer's own and is left out.
    /// </summary>
    // shortcut: an enum option shows as a number, because its choice names live in the engine's importer. Ask the engine for them when the tab gains dropdowns.
    public IReadOnlyList<ImportOption> Options() =>
        [.. ParamLines().Select(at => Option(_lines[at])).OfType<ImportOption>()];

    /// <summary>A copy with one option's line set to the value. Every other line stays as it was.</summary>
    public ImportFile With(string key, object value)
    {
        var lines = (string[])_lines.Clone();
        var at = ParamLines().First(index => KeyOf(_lines[index]) == key);
        lines[at] = $"{key}={TextOf(value)}";
        return new ImportFile(string.Join('\n', lines));
    }

    IEnumerable<int> ParamLines()
    {
        var inside = false;
        for (var at = 0; at < _lines.Length; at++)
        {
            var line = _lines[at].Trim();
            if (line.StartsWith('['))
            {
                inside = line == Params;
            }
            // A line of a value that spans lines starts with a quote or a bracket, so it has no key.
            else if (inside && KeyOf(line) is { Length: > 0 })
            {
                yield return at;
            }
        }
    }

    static string? KeyOf(string line)
    {
        var equals = line.IndexOf('=');
        return equals > 0 && line[..equals].All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '/') ? line[..equals] : null;
    }

    static ImportOption? Option(string line)
    {
        var key = KeyOf(line)!;
        if (key.StartsWith('_'))
        {
            return null;
        }

        var slash = key.IndexOf('/');
        var group = slash > 0 ? Words(key[..slash]) : "General";
        var name = key[(slash + 1)..].Replace('/', '_');
        var (type, value) = ValueOf(line[(key.Length + 1)..].Trim());
        return new ImportOption(key, new EntityProperty("", group, name, type, value));
    }

    // Whole numbers parse before reals, because the engine writes a real with a point.
    static (int Type, object Value) ValueOf(string text) => text switch
    {
        "true" or "false" => (Bool, text == "true"),
        _ when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) => (Int, whole),
        _ when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) => (Float, real),
        // shortcut: text with an escaped quote shows its backslash. Unescape when an importer takes such text.
        ['"', .. var inner, '"'] when !inner.Contains('"') => (TextType, inner),
        _ => (Nil, text),
    };

    static string TextOf(object value) => value switch
    {
        bool on => on ? "true" : "false",
        long whole => whole.ToString(CultureInfo.InvariantCulture),
        double real when real == Math.Floor(real) && Math.Abs(real) < 1e15 => real.ToString("0.0", CultureInfo.InvariantCulture),
        double real => real.ToString("R", CultureInfo.InvariantCulture),
        string words => $"\"{words.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        _ => throw new ArgumentException($"An import option cannot hold a {value.GetType().Name}.", nameof(value)),
    };

    static string Words(string key) => string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
