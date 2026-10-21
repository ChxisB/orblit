using System.Collections.ObjectModel;

namespace Orblit.Asset;

/// <summary>
/// What a cook is being cooked for. The bytes an importer writes depend on the machine that will
/// read them, which is a fact about the target and not about the asset. Plain data, so the cook
/// never depends on the renderer.
/// </summary>
public sealed class CookTarget
{
    public CookTarget(string name, IReadOnlyDictionary<string, object?>? properties = null)
    {
        Name = name;
        Properties = new ReadOnlyDictionary<string, object?>(Ordered.Copy(properties));
    }

    /// <summary>A target with no properties at all, which every importer must still produce something for.</summary>
    public static CookTarget Any { get; } = new("any");

    /// <summary>
    /// What to call this target in logs and in a cooked bundle's folder name: <c>macos</c>,
    /// <c>android</c>, <c>web</c>. Part of the key.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Everything else the importers read, by their own agreed names. Only things that change the
    /// bytes belong here, or the cache splits for nothing.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }

    /// <summary>What goes into a <see cref="CookKey"/>'s <c>target</c>.</summary>
    public IReadOnlyDictionary<string, object?> Recipe =>
        Ordered.Copy(new Dictionary<string, object?> { ["name"] = Name }.Concat(Properties).ToDictionary());

    public override string ToString() => $"CookTarget({Name})";
}
