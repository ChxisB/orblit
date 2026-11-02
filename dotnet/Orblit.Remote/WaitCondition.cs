using System.Globalization;
using Args = System.Collections.Generic.IReadOnlyDictionary<string, object?>;

namespace Orblit.Remote;

/// <summary>
/// What <c>world.wait</c> waits for: one number field compared with a number, or an entity that
/// is no longer alive. It reads the world and says what it saw. The verb owns the clock.
/// </summary>
internal sealed class WaitCondition
{
    // One call must finish inside the client's deadline of thirty seconds.
    internal const int MaxTimeoutMs = 20_000;

    private const int DefaultTimeoutMs = 5_000;

    private static readonly Dictionary<string, Func<double, double, bool>> Tests = new()
    {
        ["=="] = (seen, wanted) => seen == wanted,
        ["!="] = (seen, wanted) => seen != wanted,
        ["<"] = (seen, wanted) => seen < wanted,
        ["<="] = (seen, wanted) => seen <= wanted,
        [">"] = (seen, wanted) => seen > wanted,
        [">="] = (seen, wanted) => seen >= wanted,
    };

    private Func<double, double, bool> _test = Tests["=="];

    internal static IReadOnlyList<string> Operators { get; } = [.. Tests.Keys];

    internal required long Id { get; init; }

    internal int TimeoutMs { get; private init; } = DefaultTimeoutMs;

    private bool Gone { get; init; }

    private string Component { get; init; } = "";

    private string Field { get; init; } = "";

    private double Wanted { get; init; }

    /// <summary>Throws <see cref="ArgumentException"/> for params that name no wait.</summary>
    internal static WaitCondition From(Args args)
    {
        var id = Wire.Whole(args, "id");
        var timeout = args.GetValueOrDefault("timeout_ms") is null ? DefaultTimeoutMs : Wire.Count(args, "timeout_ms");
        if (timeout is < 1 or > MaxTimeoutMs)
        {
            throw new ArgumentException($"timeout_ms is a whole number from 1 to {MaxTimeoutMs}");
        }

        var gone = args.GetValueOrDefault("gone") switch
        {
            null => false,
            bool flag => flag,
            _ => throw new ArgumentException("gone must be true or false"),
        };
        if (gone)
        {
            return args.ContainsKey("component")
                ? throw new ArgumentException("gone waits on the entity, so it takes no component")
                : new WaitCondition { Id = id, TimeoutMs = timeout, Gone = true };
        }

        var op = Wire.Text(args, "op");
        return new WaitCondition
        {
            Id = id,
            TimeoutMs = timeout,
            Component = Wire.Text(args, "component"),
            Field = Wire.Text(args, "field"),
            Wanted = Wire.Number(args, "value"),
            _test = Tests.GetValueOrDefault(op)
                ?? throw new ArgumentException($"op must be one of {string.Join(" ", Operators)}"),
        };
    }

    /// <summary>
    /// Whether the wait is over, the value read for a field wait, and a line saying what was seen
    /// for a timeout to repeat. Throws <see cref="InvalidOperationException"/> for an entity that
    /// is not alive when it was meant to be, and <see cref="ArgumentException"/> for a component
    /// or field that is not there or a field that holds no number.
    /// </summary>
    internal (bool Met, object? Value, string Seen) Look(IWorldInspector world)
    {
        var components = world.Read(Id);
        if (Gone)
        {
            return (components is null, null, $"entity {Id} is still alive");
        }

        var value = ValueOf(components);
        var number = value switch
        {
            long whole => whole,
            double real => real,
            _ => throw new ArgumentException($"{Component}.{Field} holds {Wire.Plain(value) ?? "nothing"}, not a number"),
        };
        var seen = $"{Component}.{Field} of entity {Id} was {number.ToString(CultureInfo.InvariantCulture)}";
        return (_test(number, Wanted), value, seen);
    }

    private object? ValueOf(IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? components)
    {
        if (components is null)
        {
            throw new InvalidOperationException($"Entity {Id} is not alive");
        }

        if (!components.TryGetValue(Component, out var fields))
        {
            throw new ArgumentException($"Entity {Id} has no {Component}");
        }

        return fields.TryGetValue(Field, out var value) ? value : throw new ArgumentException($"{Field} is not in {Component}");
    }
}
