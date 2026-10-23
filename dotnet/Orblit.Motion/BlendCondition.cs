using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>
/// When a change is taken: a test of the blend's inputs, and of how far the state it leaves has played.
/// </summary>
/// <remarks>
/// Data rather than a function, so a blend can be written to a file, shown in an editor and checked without
/// playing it. A file writes one as a small object:
/// <list type="bullet">
/// <item><c>{"input": "speed", "above": 0.1}</c> and <c>{"input": "speed", "below": 0.1}</c> compare an input with a number.</item>
/// <item><c>{"input": "jump"}</c> is an input that is on: anything but nought.</item>
/// <item><c>{"through": 1}</c> is the state having played that many times through, so <c>1</c> is the end of a clip that holds and the first lap of one that loops, and <c>0.8</c> starts the fade before the end.</item>
/// <item><c>{"all": [...]}</c>, <c>{"any": [...]}</c> and <c>{"not": {...}}</c> put them together.</item>
/// </list>
/// No condition at all is always, which is how a change taken as soon as it can be is written.
/// </remarks>
public abstract class BlendCondition
{
    /// <summary>Always true. A change with nothing to wait for.</summary>
    public static readonly BlendCondition Always = new AlwaysCase();

    // Private so the cases below are the only ones there are.
    private protected BlendCondition()
    {
    }

    /// <summary>Every input it reads, once each.</summary>
    public abstract IEnumerable<string> Inputs { get; }

    /// <summary><paramref name="input"/> above <paramref name="value"/>.</summary>
    public static BlendCondition Above(string input, double value) => new AboveCase(input, value);

    /// <summary><paramref name="input"/> below <paramref name="value"/>.</summary>
    public static BlendCondition Below(string input, double value) => new BelowCase(input, value);

    /// <summary><paramref name="input"/> anything but nought: a flag, or a button held.</summary>
    public static BlendCondition On(string input) => new OnCase(input);

    /// <summary>The state being left has played <paramref name="times"/> times through.</summary>
    public static BlendCondition Through(double times) => new ThroughCase(times);

    /// <summary>Every one of <paramref name="all"/>.</summary>
    public static BlendCondition AllOf(IReadOnlyList<BlendCondition> all) => new AllCase(all);

    /// <summary>Any one of <paramref name="any"/>.</summary>
    public static BlendCondition AnyOf(IReadOnlyList<BlendCondition> any) => new AnyCase(any);

    /// <summary>Not <paramref name="condition"/>.</summary>
    public static BlendCondition Not(BlendCondition condition) => new NotCase(condition);

    /// <summary>
    /// A condition out of a file, or null with a note when it is not one.
    /// </summary>
    /// <remarks>
    /// Null rather than always: a change whose test cannot be read would otherwise be taken the moment it
    /// could be, which is the one thing its author certainly did not mean.
    /// </remarks>
    public static BlendCondition? FromJson(object? raw, string subject, List<string> problems)
    {
        if (raw is not JsonMap map)
        {
            problems.Add($"{subject} has a condition that is not one.");
            return null;
        }

        if (map.Count == 0)
        {
            return Always;
        }

        if (map.ContainsKey("all") || map.ContainsKey("any"))
        {
            return ReadGroup(map, subject, problems);
        }

        if (map.ContainsKey("not"))
        {
            return FromJson(map["not"], subject, problems) is { } inner ? new NotCase(inner) : null;
        }

        return map.ContainsKey("through") ? ReadThrough(map, subject, problems) : ReadInput(map, subject, problems);
    }

    /// <summary>
    /// Whether it holds, with <paramref name="read"/> giving an input's value and <paramref name="lap"/> how
    /// many times through the state being left has played.
    /// </summary>
    public abstract bool Holds(Func<string, double> read, double lap);

    /// <summary>Decoded JSON, the way <see cref="FromJson"/> reads it back.</summary>
    public abstract OrderedDictionary<string, object?> ToJson();

    private static BlendCondition? ReadGroup(JsonMap map, string subject, List<string> problems)
    {
        var every = map.ContainsKey("all");
        if (map[every ? "all" : "any"] is not JsonList parts)
        {
            problems.Add($"{subject} puts together something that is not a list.");
            return null;
        }

        var read = new List<BlendCondition>();
        foreach (var part in parts)
        {
            if (FromJson(part, subject, problems) is not { } one)
            {
                return null;
            }

            read.Add(one);
        }

        return every ? new AllCase(read) : new AnyCase(read);
    }

    private static ThroughCase? ReadThrough(JsonMap map, string subject, List<string> problems)
    {
        if (Values.MaybeNumber(map, "through") is { } times && double.IsFinite(times))
        {
            return new ThroughCase(times);
        }

        problems.Add($"{subject} waits to be through a number of times that is not a number.");
        return null;
    }

    private static BlendCondition? ReadInput(JsonMap map, string subject, List<string> problems)
    {
        if (Values.Text(map, "input") is not { Length: > 0 } input)
        {
            problems.Add($"{subject} has a condition this Orblit does not know.");
            return null;
        }

        if (!map.ContainsKey("above") && !map.ContainsKey("below"))
        {
            return new OnCase(input);
        }

        var above = map.ContainsKey("above");
        if (Values.MaybeNumber(map, above ? "above" : "below") is not { } value || !double.IsFinite(value))
        {
            problems.Add($"{subject} compares \"{input}\" with something that is not a number.");
            return null;
        }

        return above ? new AboveCase(input, value) : new BelowCase(input, value);
    }

    private sealed class AlwaysCase : BlendCondition
    {
        public override IEnumerable<string> Inputs => [];

        public override bool Holds(Func<string, double> read, double lap) => true;

        public override OrderedDictionary<string, object?> ToJson() => [];
    }

    private sealed class AboveCase(string input, double value) : BlendCondition
    {
        public override IEnumerable<string> Inputs => [input];

        public override bool Holds(Func<string, double> read, double lap) => read(input) > value;

        public override OrderedDictionary<string, object?> ToJson() => new() { ["input"] = input, ["above"] = value };
    }

    private sealed class BelowCase(string input, double value) : BlendCondition
    {
        public override IEnumerable<string> Inputs => [input];

        public override bool Holds(Func<string, double> read, double lap) => read(input) < value;

        public override OrderedDictionary<string, object?> ToJson() => new() { ["input"] = input, ["below"] = value };
    }

    private sealed class OnCase(string input) : BlendCondition
    {
        public override IEnumerable<string> Inputs => [input];

        public override bool Holds(Func<string, double> read, double lap) => read(input) != 0;

        public override OrderedDictionary<string, object?> ToJson() => new() { ["input"] = input };
    }

    private sealed class ThroughCase(double times) : BlendCondition
    {
        public override IEnumerable<string> Inputs => [];

        public override bool Holds(Func<string, double> read, double lap) => lap >= times;

        public override OrderedDictionary<string, object?> ToJson() => new() { ["through"] = times };
    }

    private sealed class AllCase(IReadOnlyList<BlendCondition> all) : BlendCondition
    {
        public override IEnumerable<string> Inputs => all.SelectMany(static one => one.Inputs).Distinct();

        public override bool Holds(Func<string, double> read, double lap) => all.All(one => one.Holds(read, lap));

        public override OrderedDictionary<string, object?> ToJson() =>
            new() { ["all"] = all.Select(static one => (object?)one.ToJson()).ToList() };
    }

    private sealed class AnyCase(IReadOnlyList<BlendCondition> any) : BlendCondition
    {
        public override IEnumerable<string> Inputs => any.SelectMany(static one => one.Inputs).Distinct();

        public override bool Holds(Func<string, double> read, double lap) => any.Any(one => one.Holds(read, lap));

        public override OrderedDictionary<string, object?> ToJson() =>
            new() { ["any"] = any.Select(static one => (object?)one.ToJson()).ToList() };
    }

    private sealed class NotCase(BlendCondition condition) : BlendCondition
    {
        public override IEnumerable<string> Inputs => condition.Inputs;

        public override bool Holds(Func<string, double> read, double lap) => !condition.Holds(read, lap);

        public override OrderedDictionary<string, object?> ToJson() => new() { ["not"] = condition.ToJson() };
    }
}
