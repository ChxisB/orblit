namespace Orblit.Asset;

/// <summary>
/// One uniform an author declared. The hints are kept for the editor: the engine itself needs only the name,
/// the type and the default.
/// </summary>
public sealed class SurfaceParam
{
    public required string Name { get; init; }

    public required SurfaceType Type { get; init; }

    public required int Line { get; init; }

    /// <summary>Whether each object may carry its own value.</summary>
    public bool Instance { get; init; }

    public string? Group { get; init; }

    /// <summary>
    /// Hint name to its arguments, as written: <c>source_color</c> is empty and <c>hint_range(0, 1)</c> is
    /// <c>["0", "1"]</c>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Hints { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// A boxed <see cref="long"/> or <see cref="double"/> for a number type, a list of them for a vector, a
    /// <see cref="bool"/> for a bool, or null for a texture and for a uniform with no default.
    /// </summary>
    public object? DefaultValue { get; init; }

    public bool IsColor => Hints.ContainsKey("source_color");

    /// <summary>The param as the cook writes it into <c>params.json</c>.</summary>
    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["name"] = Name,
            ["type"] = Type.Word(),
            ["instance"] = Instance,
        };
        if (Group is not null)
        {
            json["group"] = Group;
        }

        if (Hints.Count > 0)
        {
            json["hints"] = new OrderedDictionary<string, object?>(
                Hints.Select(hint => KeyValuePair.Create(hint.Key, (object?)hint.Value.Cast<object?>().ToList())));
        }

        if (DefaultValue is not null)
        {
            json["default"] = DefaultValue;
        }

        return json;
    }
}
