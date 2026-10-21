using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// A JSON object being read, whose every complaint says where it came from. The entries, the index and the
/// release lists are files somebody else publishes, so each word a reader echoes goes through
/// <see cref="KitText.Show"/>.
/// </summary>
internal sealed class KitJsonObject(IReadOnlyDictionary<string, object?> map, string where)
{
    public IReadOnlyDictionary<string, object?> Map { get; } = map;

    /// <summary>What the file or part is called, such as <c>The entry "brass-lamp/crates"</c>.</summary>
    public string Where { get; } = where;

    /// <summary>
    /// Throws a <see cref="FormatException"/> unless <paramref name="bytes"/> are UTF-8 JSON, no longer than
    /// <paramref name="limit"/>, holding one object.
    /// </summary>
    public static KitJsonObject Parse(byte[] bytes, string where, int limit)
    {
        if (bytes.Length > limit)
        {
            throw new FormatException($"{where} is over {limit} bytes.");
        }

        object? parsed;
        try
        {
            parsed = DartJson.Decode(DartText.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new FormatException($"{where} is not JSON: {KitText.Show(error.Message)}");
        }

        return parsed is IReadOnlyDictionary<string, object?> map
            ? new KitJsonObject(map, where)
            : throw new FormatException($"{where} is not a JSON object.");
    }

    /// <summary>The bytes of <paramref name="json"/>, refused when the reader would refuse them.</summary>
    public static byte[] Encode(string name, int limit, IReadOnlyDictionary<string, object?> json)
    {
        var bytes = Encoding.UTF8.GetBytes(DartJson.Encode(json));
        return bytes.Length > limit ? throw new FormatException($"{name} would be {bytes.Length} bytes, over {limit}.") : bytes;
    }

    /// <summary>A complaint about this object. <paramref name="what"/> may already end in a full stop.</summary>
    public FormatException Problem(string what) =>
        new(what.EndsWith('.') ? $"{Where} {what}" : $"{Where} {what}.");

    /// <summary>Runs <paramref name="make"/>, and says where this object is in a complaint that does not already say so.</summary>
    public T Read<T>(Func<T> make)
    {
        try
        {
            return make();
        }
        catch (FormatException error) when (!error.Message.StartsWith(Where, StringComparison.Ordinal))
        {
            throw Problem($"is wrong: {error.Message}");
        }
    }

    public string Text(string key) =>
        Map.GetValueOrDefault(key) as string ?? throw Problem($"has no \"{key}\" text");

    public long Integer(string key) =>
        Map.GetValueOrDefault(key) is long value ? value : throw Problem($"has no \"{key}\" whole number");

    public bool Flag(string key) =>
        Map.GetValueOrDefault(key) is bool value ? value : throw Problem($"has no \"{key}\" true or false");

    /// <summary>The text at <paramref name="key"/>, or null when it is not there.</summary>
    public string? OptionalText(string key) => Map.ContainsKey(key) ? Text(key) : null;

    public KitJsonObject Section(string key) =>
        Map.GetValueOrDefault(key) is IReadOnlyDictionary<string, object?> value
            ? new KitJsonObject(value, $"{Where}, {key}")
            : throw Problem($"has no \"{key}\" object");

    public List<KitJsonObject> Objects(string key)
    {
        var values = Items(key);
        var objects = new List<KitJsonObject>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            objects.Add(values[i] is IReadOnlyDictionary<string, object?> value
                ? new KitJsonObject(value, $"{Where}, {key} {i + 1}")
                : throw Problem($"holds something other than an object in \"{key}\""));
        }

        return objects;
    }

    public List<string> Strings(string key) =>
    [
        .. Items(key).Select(value =>
            value as string ?? throw Problem($"holds {KitText.Show(DartText.Print(value))}, not text, in \"{key}\"")),
    ];

    /// <summary>Throws unless every key is one of <paramref name="keys"/>, so a typo is caught.</summary>
    public void Only(IReadOnlySet<string> keys)
    {
        foreach (var key in Map.Keys)
        {
            if (!keys.Contains(key))
            {
                throw Problem($"has \"{KitText.Show(key)}\", which is not a key of this file. Check the spelling");
            }
        }
    }

    /// <summary>
    /// Throws unless <c>formatVersion</c> is <paramref name="newest"/>, saying so when the file is from a
    /// newer Orblit. Keys this version does not know are left alone.
    /// </summary>
    public void CheckFormat(int newest)
    {
        var format = Integer("formatVersion");
        if (format > newest)
        {
            throw new FormatException(
                $"{Where} was written by a newer Orblit (format {format}; this one reads up to {newest}).");
        }

        if (format != newest)
        {
            throw Problem($"says format {format}");
        }
    }

    private IReadOnlyList<object?> Items(string key) =>
        Map.GetValueOrDefault(key) as IReadOnlyList<object?> ?? throw Problem($"has no \"{key}\" list");
}
