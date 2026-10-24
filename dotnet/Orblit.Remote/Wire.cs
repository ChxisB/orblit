using System.Globalization;

namespace Orblit.Remote;

/// <summary>
/// The JSON values the verbs read and write, in the shapes <see cref="Orblit.Json.JsonText"/> uses.
/// </summary>
internal static class Wire
{
    internal static OrderedDictionary<string, object?> Map(params ReadOnlySpan<(string Key, object? Value)> pairs)
    {
        var map = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    // IReadOnlyDictionary is not covariant, so the inspector's typed map is not a JSON map as it is.
    internal static OrderedDictionary<string, object?> Components(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> components)
    {
        var map = new OrderedDictionary<string, object?>();
        foreach (var (name, fields) in components)
        {
            map[name] = fields;
        }

        return map;
    }

    /// <summary>
    /// A copy of <paramref name="value"/> that JSON can write. A host that reuses its maps cannot
    /// change a kept copy. A float JSON has no word for becomes its name, as the inspector reports
    /// it, because rows from the renderer can hold the NaN that is being looked for. Anything
    /// else JSON cannot write is an internal error, because the host gave it.
    /// </summary>
    internal static object? Plain(object? value) => value switch
    {
        null or bool or string or long => value,
        int whole => (long)whole,
        double real => double.IsFinite(real) ? real : real.ToString(CultureInfo.InvariantCulture),
        IReadOnlyDictionary<string, object?> map => PlainMap(map),
        IReadOnlyList<object?> list => list.Select(Plain).ToList(),
        _ => throw new RemoteException(RemoteException.Internal, $"{value.GetType().Name} is not JSON"),
    };

    private static OrderedDictionary<string, object?> PlainMap(IReadOnlyDictionary<string, object?> map)
    {
        var copy = new OrderedDictionary<string, object?>();
        foreach (var (key, item) in map)
        {
            copy[key] = Plain(item);
        }

        return copy;
    }

    internal static long Whole(IReadOnlyDictionary<string, object?> args, string name) =>
        args.GetValueOrDefault(name) is long value ? value : throw Refuse(name, "a whole number");

    /// <summary>A whole number that fits 32 bits, because a count past that is as good as the limit.</summary>
    internal static int Count(IReadOnlyDictionary<string, object?> args, string name) =>
        (int)Math.Clamp(Whole(args, name), int.MinValue, int.MaxValue);

    internal static double Number(IReadOnlyDictionary<string, object?> args, string name) =>
        args.GetValueOrDefault(name) switch
        {
            long whole => whole,
            double real => real,
            _ => throw Refuse(name, "a number"),
        };

    internal static string Text(IReadOnlyDictionary<string, object?> args, string name) =>
        args.GetValueOrDefault(name) is string value ? value : throw Refuse(name, "text");

    internal static IReadOnlyList<string> Names(object? value) => value switch
    {
        null => [],
        IReadOnlyList<object?> list when list.All(name => name is string) => [.. list.Cast<string>()],
        _ => throw Refuse("filter", "a list of names"),
    };

    private static ArgumentException Refuse(string name, string kind) => new($"{name} must be {kind}");
}
