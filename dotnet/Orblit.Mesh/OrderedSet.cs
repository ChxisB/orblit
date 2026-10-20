using System.Collections;

namespace Orblit.Mesh;

/// <summary>
/// A set that enumerates in the order its members went in, as a Dart <c>Set</c> does. Which
/// edge a hole fill starts from, and which face a merge begins with, follow that order.
/// </summary>
public sealed class OrderedSet<T> : IReadOnlySet<T>
    where T : notnull
{
    private readonly List<T> _order = [];
    private readonly HashSet<T> _members = [];

    public OrderedSet()
    {
    }

    public OrderedSet(IEnumerable<T> items)
    {
        AddRange(items);
    }

    public int Count => _order.Count;

    public bool Add(T item)
    {
        if (!_members.Add(item))
        {
            return false;
        }

        _order.Add(item);
        return true;
    }

    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public bool Remove(T item)
    {
        if (!_members.Remove(item))
        {
            return false;
        }

        _order.Remove(item);
        return true;
    }

    public bool Contains(T item) => _members.Contains(item);

    public bool IsSubsetOf(IEnumerable<T> other) => _members.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<T> other) => _members.IsSupersetOf(other);

    public bool IsProperSubsetOf(IEnumerable<T> other) => _members.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<T> other) => _members.IsProperSupersetOf(other);

    public bool Overlaps(IEnumerable<T> other) => _members.Overlaps(other);

    public bool SetEquals(IEnumerable<T> other) => _members.SetEquals(other);

    public IEnumerator<T> GetEnumerator() => _order.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
