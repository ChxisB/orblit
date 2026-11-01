namespace Orblit.Filament;

/// <summary>
/// Things sent once and pointed at by position, because the same mesh, picture or path is usually
/// on many objects and the message crosses the boundary on every frame.
/// </summary>
internal sealed class IndexedSet<T>
    where T : notnull
{
    private readonly Dictionary<T, int> _at = [];

    /// <summary>The things in the order they were first asked for.</summary>
    internal List<T> Items { get; } = [];

    /// <summary>Where <paramref name="item"/> sits, adding it on the first ask.</summary>
    internal int IndexOf(T item)
    {
        if (_at.TryGetValue(item, out var at))
        {
            return at;
        }

        _at.Add(item, Items.Count);
        Items.Add(item);
        return Items.Count - 1;
    }
}
