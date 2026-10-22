using System.Collections.Frozen;
using System.Globalization;
using Orblit.Light;

namespace Orblit.Scene;

/// <summary>
/// Reading values out of decoded JSON, and writing them back the same way.
/// </summary>
/// <remarks>
/// Every field in a scene file passes through here. A file is a thing people hand-edit, merge and
/// generate, so any value in it may be missing, may be the wrong type, or may be a number where a
/// string was meant. None of that should lose the other nine hundred fields that were fine. So
/// these read what they can and fall back rather than throwing, and the places where a bad value
/// genuinely costs something say so by keeping a note instead.
/// <para>
/// The writing half exists so that the two stay together: a reader and a writer that live in
/// different files drift, and the day they do is the day a scene stops surviving its own round trip.
/// </para>
/// </remarks>
public static class Values
{
    private static readonly FrozenDictionary<string, object?> NoFields = FrozenDictionary<string, object?>.Empty;

    private const double Int64Limit = 9223372036854775808.0;

    /// <summary>A number, or <paramref name="fallback"/> when the file has something else there.</summary>
    public static double Number(JsonMap json, string key, double fallback) => Num(At(json, key)) ?? fallback;

    /// <summary>
    /// A number that may legitimately be absent, which is not the same as one that defaults: a spot
    /// angle on a thing that is not a spot has no value, and writing one would invent a fact.
    /// </summary>
    public static double? MaybeNumber(JsonMap json, string key) => Num(At(json, key));

    public static bool Flag(JsonMap json, string key, bool fallback) => At(json, key) is bool value ? value : fallback;

    /// <summary>
    /// A whole number that is a set of bits, a layer mask, or <paramref name="fallback"/>. Kept to
    /// thirty-two bits, because that is how many layers there are, and a mask written as <c>-1</c>
    /// by hand means all of them rather than an error.
    /// </summary>
    public static long Bits(JsonMap json, string key, long fallback) => At(json, key) switch
    {
        long whole => whole & 0xFFFFFFFFL,
        double fraction when double.IsFinite(fraction) => (long)fraction & 0xFFFFFFFFL,
        _ => fallback,
    };

    public static string? Text(JsonMap json, string key) => At(json, key) as string;

    /// <summary>
    /// One of an enum's values by its name, or null when the name is not one. Null rather than a
    /// default, so a caller can tell "this file named something I do not know" apart from "this file
    /// said nothing". The first is worth a note, the second is not.
    /// </summary>
    public static T? Named<T>(object? name)
        where T : struct, Enum
    {
        if (name is not string text)
        {
            return null;
        }

        foreach (var value in Enum.GetValues<T>())
        {
            if (EnumNames.Of(value) == text)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Three numbers, or <paramref name="fallback"/> in all three places. A short or ragged list
    /// falls back per component rather than wholesale: a scale written <c>[2, 2]</c> by hand means
    /// two of the three, and reading it as <c>[1, 1, 1]</c> would move the object further from what
    /// was meant than reading it as <c>[2, 2, 1]</c> does.
    /// </summary>
    public static Vector3 Vector(object? raw, double fallback = 0)
    {
        if (raw is not JsonList list)
        {
            return Vector3.All(fallback);
        }

        double At(int index) => index < list.Count ? Num(list[index]) ?? fallback : fallback;
        return new Vector3(At(0), At(1), At(2));
    }

    public static List<object?> VectorToJson(Vector3 value) => [value.X, value.Y, value.Z];

    /// <summary>Numbers as a JSON list. A list of doubles is not a list of objects, so it is boxed here once.</summary>
    public static List<object?> NumbersToJson(IEnumerable<double> values) => [.. values.Select(static value => (object?)value)];

    /// <summary>A colour written <c>#RRGGBB</c>, as everywhere else in Orblit writes one.</summary>
    public static Tint ReadTint(object? raw, Tint? fallback = null)
    {
        var otherwise = fallback ?? Tint.Hex(0xD9634F);
        if (raw is not string text)
        {
            return otherwise;
        }

        var digits = text.StartsWith('#') ? text[1..] : text;
        return digits.Length == 6 && TryParseHex(digits, out var value) ? Tint.Hex(value) : otherwise;
    }

    public static string TintToJson(Tint value)
    {
        static string Pair(double channel) =>
            ((int)Math.Clamp(Doubles.Round(channel * 255), 0, 255)).ToString("x2", CultureInfo.InvariantCulture);

        return $"#{Pair(value.Red)}{Pair(value.Green)}{Pair(value.Blue)}".ToUpperInvariant();
    }

    /// <summary>
    /// A nested object, or an empty one when there is nothing there. Empty rather than null so the
    /// caller reads its fields the same way whether the block was written or not, and every one of
    /// them falls back.
    /// </summary>
    public static JsonMap Object(object? raw) => raw as JsonMap ?? NoFields;

    /// <summary>
    /// Every number in a list, or nothing when it is not a list of numbers. All or nothing, unlike
    /// <see cref="Texts"/>. These are points written three to a corner, and a skipped entry would
    /// move every number after it into the wrong place.
    /// </summary>
    public static IReadOnlyList<double> Numbers(object? raw)
    {
        if (raw is not JsonList list)
        {
            return [];
        }

        var read = new List<double>(list.Count);
        foreach (var value in list)
        {
            if (Num(value) is not { } number)
            {
                return [];
            }

            read.Add(number);
        }

        return read;
    }

    /// <summary>The strings out of a list, skipping anything that is not one.</summary>
    public static IReadOnlyList<string> Texts(object? raw) =>
        raw is JsonList list ? [.. list.OfType<string>()] : [];

    /// <summary>
    /// Whether two decoded JSON values are the same value. What a diff is built on. Comparing the
    /// JSON rather than the components means one comparison covers every component there is,
    /// including the ones this version has never heard of, and a component that grows a field cannot
    /// forget to grow its equality with it, which is the bug that makes an edit quietly un-undoable.
    /// <para>
    /// Numbers compare across <c>long</c> and <c>double</c> on purpose: a file may say <c>1</c> where
    /// we write <c>1.0</c>, and treating a scene as changed because of that would mark every old file
    /// dirty the moment it was opened.
    /// </para>
    /// </summary>
    public static bool Same(object? a, object? b) => (a, b) switch
    {
        (long x, long y) => x == y,
        (double x, double y) => x == y,
        (long x, double y) => Exactly(x, y),
        (double x, long y) => Exactly(y, x),
        (JsonMap x, JsonMap y) => SameMaps(x, y),
        (JsonList x, JsonList y) => SameLists(x, y),
        _ => Equals(a, b),
    };

    /// <summary>
    /// Drops the keys whose value is null, so an optional field that is not set leaves nothing behind
    /// in the file. Written as a pass over the finished map rather than as a conditional entry on
    /// each line, because the components have a lot of optional fields and the conditional form makes
    /// the shape of a component hard to read.
    /// </summary>
    public static OrderedDictionary<string, object?> Pruned(IEnumerable<KeyValuePair<string, object?>> json)
    {
        var kept = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in json)
        {
            if (value is not null)
            {
                kept[key] = value;
            }
        }

        return kept;
    }

    internal static object? At(JsonMap json, string key) => json.TryGetValue(key, out var value) ? value : null;

    /// <summary>A JSON number as a double, or null for anything else.</summary>
    internal static double? Num(object? value) => value switch
    {
        long whole => whole,
        double fraction => fraction,
        _ => null,
    };

    private static bool Exactly(long whole, double fraction) =>
        fraction >= -Int64Limit && fraction < Int64Limit && fraction == Math.Floor(fraction) && (long)fraction == whole;

    private static bool SameMaps(JsonMap a, JsonMap b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !Same(value, other))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameLists(JsonList a, JsonList b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!Same(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    // A hex whole number takes a leading sign and nothing else but hex digits.
    private static bool TryParseHex(string digits, out int value)
    {
        value = 0;
        var negative = digits[0] == '-';
        var body = negative || digits[0] == '+' ? digits[1..] : digits;
        if (body.Length == 0 || !int.TryParse(body, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            return false;
        }

        value = negative ? -parsed : parsed;
        return true;
    }
}
