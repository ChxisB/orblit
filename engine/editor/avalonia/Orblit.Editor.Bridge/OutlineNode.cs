namespace Orblit.Editor.Bridge;

/// <summary>What the editor has done to an entity besides its place: hidden, locked against picking, or grouped.</summary>
[Flags]
public enum OutlineFlags
{
    None = 0,
    Hidden = 1,
    Locked = 2,
    Grouped = 4,
}

/// <summary>One entity of the open scene. The handle is valid until the scene closes.</summary>
public sealed record OutlineNode(nint Handle, string Name, string ClassName, IReadOnlyList<OutlineNode> Children, OutlineFlags Flags = OutlineFlags.None)
{
    public bool Hidden => Flags.HasFlag(OutlineFlags.Hidden);

    public bool Locked => Flags.HasFlag(OutlineFlags.Locked);

    public bool Grouped => Flags.HasFlag(OutlineFlags.Grouped);

    /// <summary>Only an entity with a place in a plane or in space can be hidden, locked or grouped in the viewport.</summary>
    public static bool IsPlaced(string className) =>
        className.EndsWith("2D", StringComparison.Ordinal) || className.EndsWith("3D", StringComparison.Ordinal);

    /// <summary>The nodes from this one down to the entity with this handle, or none when it is not under this node.</summary>
    public IReadOnlyList<OutlineNode> PathTo(nint handle)
    {
        if (Handle == handle)
        {
            return [this];
        }
        var below = Children.Select(child => child.PathTo(handle)).FirstOrDefault(path => path.Count > 0);
        return below is null ? [] : [this, .. below];
    }

    /// <summary>The entity with this handle in this node's tree, or null.</summary>
    public OutlineNode? Find(nint handle) =>
        Handle == handle ? this : Children.Select(child => child.Find(handle)).FirstOrDefault(found => found is not null);
}
