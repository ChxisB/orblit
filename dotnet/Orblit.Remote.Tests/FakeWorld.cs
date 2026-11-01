namespace Orblit.Remote.Tests;

/// <summary>A world held in a map, so a verb can be tried with no native code.</summary>
internal sealed class FakeWorld : IWorldInspector
{
    internal Dictionary<long, Dictionary<string, Dictionary<string, object?>>> Held { get; } = new()
    {
        [1] = new() { ["Position"] = new() { ["x"] = 1L, ["y"] = 2L } },
        [2] = new()
        {
            ["Position"] = new() { ["x"] = 3L, ["y"] = 4L },
            ["Tag"] = new() { ["n"] = 9L },
        },
    };

    public IReadOnlyList<EntityRow> Entities(IReadOnlyList<string> filter) =>
    [
        .. Held
            .Where(entry => filter.All(entry.Value.ContainsKey))
            .OrderBy(entry => entry.Key)
            .Select(entry => new EntityRow(entry.Key, [.. entry.Value.Keys.Order(StringComparer.Ordinal)])),
    ];

    // A copy, as the real inspector builds one, so a snapshot cannot alias it.
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Read(long id) =>
        Held.TryGetValue(id, out var components)
            ? components.ToDictionary(
                component => component.Key,
                component => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(component.Value))
            : null;

    public void Write(long id, string component, string field, double value)
    {
        var components = Held.GetValueOrDefault(id)
            ?? throw new InvalidOperationException($"Entity {id} is not alive");
        var fields = components.GetValueOrDefault(component);
        if (fields is null || !fields.ContainsKey(field))
        {
            throw new ArgumentException($"{field} is not in {component}");
        }

        fields[field] = value;
    }
}
