using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Everything that decided what a cooked asset is, written down.
/// </summary>
/// <remarks>
/// A cache that skips work has to answer one question and get it right every time: would running
/// this again produce the same bytes? A key that leaves something out answers yes when the truth is
/// no, and the project then builds against a stale texture no clean build will reproduce. So the key
/// is the whole recipe: the file, the files it reads, the importer and its version, the settings,
/// and the device the result is for.
/// <para>
/// The recipe is kept as readable JSON rather than only hashed, because a cache miss nobody expected
/// is otherwise unexplainable. Two recipes can be printed side by side, and the line that differs is
/// the answer.
/// </para>
/// <para>
/// <c>importerVersion</c> is the part that gets forgotten. Bytes, settings and target can all be
/// unchanged while the code that reads them is fixed, and every entry that importer wrote is then
/// wrong. Raising the version is how a fix reaches caches that already exist.
/// </para>
/// </remarks>
public sealed class CookKey : IEquatable<CookKey>
{
    /// <summary>
    /// Bumped when the shape of a recipe changes. Every key changes with it, so every entry an older
    /// build wrote is missed rather than matched.
    /// </summary>
    public const int KeyFormatVersion = 1;

    private string? _recipe;
    private ContentHash? _hash;

    // F2: six parameters, all named at each call, as in the Dart constructor.
    public CookKey(
        string importer,
        int importerVersion,
        ContentHash source,
        IReadOnlyDictionary<AssetId, ContentHash>? dependencies = null,
        IReadOnlyDictionary<string, object?>? settings = null,
        IReadOnlyDictionary<string, object?>? target = null)
    {
        if (importer.Length == 0)
        {
            throw new ArgumentException("A cook key has to say which importer made it.", nameof(importer));
        }

        if (importerVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(importerVersion), importerVersion, "An importer version starts at 1 and only goes up.");
        }

        Importer = importer;
        ImporterVersion = importerVersion;
        Source = source;
        Dependencies = new ReadOnlyDictionary<AssetId, ContentHash>(
            new Dictionary<AssetId, ContentHash>(dependencies ?? new Dictionary<AssetId, ContentHash>()));
        Settings = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(settings ?? new Dictionary<string, object?>()));
        Target = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(target ?? new Dictionary<string, object?>()));
    }

    /// <summary>Which importer ran: <c>gltf</c>, <c>texture</c>, <c>splat</c>.</summary>
    public string Importer { get; }

    /// <summary>Which version of that importer ran.</summary>
    public int ImporterVersion { get; }

    /// <summary>What the source file is, byte for byte.</summary>
    public ContentHash Source { get; }

    /// <summary>
    /// The other files the importer read, by the id each was read as. Named as well as hashed,
    /// because moving a texture from one id to another changes what the result refers to even when
    /// every byte in the project is the same.
    /// </summary>
    public IReadOnlyDictionary<AssetId, ContentHash> Dependencies { get; }

    /// <summary>
    /// The settings the importer ran with, already resolved: what the importer actually used, with
    /// its defaults filled in and its values read as the types it reads them as.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Settings { get; }

    /// <summary>
    /// What the result is for: the target platform, and whatever about the device changes the
    /// output. Plain data rather than a device profile, because this package knows nothing about a
    /// renderer.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Target { get; }

    /// <summary>
    /// The recipe as canonical JSON: the same text for the same inputs, on every machine and in every
    /// run. Object keys are sorted, inside settings and target too, so a map built in a different
    /// order is not a different key.
    /// </summary>
    public string Recipe => _recipe ??= Encode(new Dictionary<string, object?>
    {
        ["keyFormatVersion"] = (long)KeyFormatVersion,
        ["importer"] = Importer,
        ["importerVersion"] = (long)ImporterVersion,
        ["source"] = Source.Hex,
        ["dependencies"] = Dependencies.ToDictionary(pair => pair.Key.ToString(), pair => (object?)pair.Value.Hex),
        ["settings"] = Settings,
        ["target"] = Target,
    });

    /// <summary>The key itself: the SHA-256 of <see cref="Recipe"/>.</summary>
    public ContentHash Hash => _hash ??= ContentHash.Of(Encoding.UTF8.GetBytes(Recipe));

    public bool Equals(CookKey? other) => other is not null && other.Hash == Hash;

    public override bool Equals(object? obj) => Equals(obj as CookKey);

    public override int GetHashCode() => Hash.GetHashCode();

    /// <summary>The recipe, which is what anybody holding a key wants to see.</summary>
    public override string ToString() => Recipe;

    private static string Encode(object? value)
    {
        var buffer = new StringBuilder();
        Write(value, buffer, "the recipe");
        return buffer.ToString();
    }

    // Anything that is not JSON is refused here, where the field holding it can still be named.
    private static void Write(object? value, StringBuilder output, string where)
    {
        switch (value)
        {
            case null or bool or string:
                output.Append(DartJson.Encode(value));
                break;
            case int or long:
                output.Append(DartJson.Encode(Convert.ToInt64(value, CultureInfo.InvariantCulture)));
                break;
            case double number:
                WriteDouble(number, output, where);
                break;
            case IDictionary map:
                WriteMap(map.Keys.Cast<object>().Select(key => (key, map[key])), output, where);
                break;
            case IReadOnlyDictionary<string, object?> map:
                WriteMap(map.Select(pair => ((object)pair.Key, pair.Value)), output, where);
                break;
            case IList items:
                WriteList(items, output, where);
                break;
            default:
                throw new ArgumentException(
                    $"A cook key holds only JSON — null, a number, a string, a boolean, a list or a map. This is a {value.GetType().Name}.",
                    where);
        }
    }

    private static void WriteDouble(double number, StringBuilder output, string where)
    {
        if (!double.IsFinite(number))
        {
            throw new ArgumentException(
                $"A cook key cannot hold {number.ToString(CultureInfo.InvariantCulture)}, because JSON has no way to write it and a key that cannot be written cannot be compared.",
                where);
        }

        output.Append(DartJson.Encode(number));
    }

    private static void WriteList(IList items, StringBuilder output, string where)
    {
        output.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                output.Append(',');
            }

            Write(items[i], output, $"{where}[{i}]");
        }

        output.Append(']');
    }

    private static void WriteMap(IEnumerable<(object Key, object? Value)> pairs, StringBuilder output, string where)
    {
        var map = new Dictionary<string, object?>();
        foreach (var (key, value) in pairs)
        {
            map[key as string ?? throw new ArgumentException(
                $"A cook key holds JSON, and a JSON object's keys are strings. This one is a {key.GetType().Name}.",
                where)] = value;
        }

        var keys = map.Keys.Order(StringComparer.Ordinal).ToList();
        output.Append('{');
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
            {
                output.Append(',');
            }

            output.Append(DartJson.Encode(keys[i])).Append(':');
            Write(map[keys[i]], output, $"{where}.{keys[i]}");
        }

        output.Append('}');
    }
}
