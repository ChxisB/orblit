namespace Orblit.Progress;

/// <summary>How a <see cref="Condition"/> compares a named value with a number.</summary>
public sealed class Compare
{
    public static readonly Compare Equal = new("==", (have, want) => have == want);

    public static readonly Compare NotEqual = new("!=", (have, want) => have != want);

    public static readonly Compare Less = new("<", (have, want) => have < want);

    public static readonly Compare LessOrEqual = new("<=", (have, want) => have <= want);

    public static readonly Compare Greater = new(">", (have, want) => have > want);

    public static readonly Compare GreaterOrEqual = new(">=", (have, want) => have >= want);

    private readonly Func<long, long, bool> _holds;

    private Compare(string symbol, Func<long, long, bool> holds)
    {
        Symbol = symbol;
        _holds = holds;
    }

    // After the instances, which it lists.
    public static IReadOnlyList<Compare> Values { get; } = [Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual];

    /// <summary>What a file writes for it.</summary>
    public string Symbol { get; }

    public bool Holds(long have, long want) => _holds(have, want);

    /// <summary>The comparison written as <paramref name="symbol"/>, or null for any other text.</summary>
    public static Compare? BySymbol(string symbol) => Values.FirstOrDefault(compare => compare.Symbol == symbol);
}

/// <summary>
/// A comparison of one named value with a number, such as a quest stage at
/// least 2. A list of them holds when every one does. For "or", write two lists
/// and let the first that holds win.
/// </summary>
public sealed class Condition
{
    private const long All = 0xFFFFFFFF;

    public Condition(string name, Compare compare, long value)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The name is empty.");
        }

        if (value is < 0 or > All)
        {
            throw new ArgumentException($"The value {value} is outside 0 to {All}.");
        }

        Name = name;
        Compare = compare;
        Value = value;
    }

    public string Name { get; }

    public Compare Compare { get; }

    public long Value { get; }

    /// <summary>Reads <c>{"name": ..., "op": ..., "value": ...}</c>. Throws <see cref="FormatException"/> for anything else.</summary>
    public static Condition FromJson(object? json)
    {
        if (json is IReadOnlyDictionary<string, object?> map
            && map.GetValueOrDefault("name") is string { Length: > 0 } name
            && map.GetValueOrDefault("op") is string op
            && Compare.BySymbol(op) is { } compare
            && WholeNumber(map.GetValueOrDefault("value")) is >= 0 and <= All and var value)
        {
            return new Condition(name, compare, value);
        }

        throw new FormatException(
            $"A condition is {{\"name\": text, \"op\": one of {string.Join(' ', Compare.Values.Select(c => c.Symbol))}, "
            + $"\"value\": a whole number from 0 to {All}}}, not {json}.");
    }

    /// <summary>Reads a list of conditions. Null is no conditions.</summary>
    public static IReadOnlyList<Condition> ListFromJson(object? json) => json switch
    {
        null => [],
        IReadOnlyList<object?> list => list.Select(FromJson).ToList().AsReadOnly(),
        _ => throw new FormatException($"Conditions are a list, not {json}."),
    };

    /// <summary>Whether every condition in <paramref name="conditions"/> holds in <paramref name="table"/>.</summary>
    public static bool AllHold(IEnumerable<Condition> conditions, ProgressTable table) =>
        conditions.All(condition => condition.Holds(table));

    /// <summary>Throws <see cref="ArgumentException"/> when the table's schema has no such name.</summary>
    public bool Holds(ProgressTable table) => Compare.Holds(table.Get(Name), Value);

    public IReadOnlyDictionary<string, object?> ToJson() => new OrderedDictionary<string, object?>
    {
        ["name"] = Name,
        ["op"] = Compare.Symbol,
        ["value"] = Value,
    };

    // A whole number in decoded JSON is an int or a long. A double is not one, even if it is 1.0.
    private static long? WholeNumber(object? value) => value switch
    {
        long whole => whole,
        int whole => whole,
        _ => null,
    };
}
