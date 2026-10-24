using System.Collections;

namespace Orblit.Input;

/// <summary>
/// A set that enumerates in the order things were added, as a Dart set does. A
/// <see cref="HashSet{T}"/> forgets the order after a removal, and the order of the held
/// controls decides which one a rebind captures first.
/// </summary>
internal sealed class OrderedSet<T> : IReadOnlyCollection<T>
    where T : notnull
{
    private readonly OrderedDictionary<T, bool> _items = [];

    internal OrderedSet()
    {
    }

    internal OrderedSet(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            _items.TryAdd(item, true);
        }
    }

    public int Count => _items.Count;

    internal bool Add(T item) => _items.TryAdd(item, true);

    internal bool Remove(T item) => _items.Remove(item);

    internal bool Contains(T item) => _items.ContainsKey(item);

    internal void Clear() => _items.Clear();

    public IEnumerator<T> GetEnumerator() => _items.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
