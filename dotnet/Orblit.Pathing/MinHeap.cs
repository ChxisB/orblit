namespace Orblit.Pathing;

/// <summary>
/// A queue that gives back the item with the smallest key first.
/// An item can be added again with a better key. The caller drops the stale
/// copy when it comes out, which is cheaper than finding and moving it.
/// </summary>
internal sealed class MinHeap
{
    private readonly List<double> _keys = [];
    private readonly List<int> _items = [];

    internal bool IsEmpty => _items.Count == 0;

    internal void Add(int item, double key)
    {
        var at = _items.Count;
        _items.Add(item);
        _keys.Add(key);
        while (at > 0)
        {
            var parent = (at - 1) >> 1;
            if (_keys[parent] <= _keys[at])
            {
                break;
            }

            Swap(parent, at);
            at = parent;
        }
    }

    internal int RemoveFirst()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        var first = _items[0];
        var lastItem = _items[^1];
        var lastKey = _keys[^1];
        _items.RemoveAt(_items.Count - 1);
        _keys.RemoveAt(_keys.Count - 1);
        if (_items.Count > 0)
        {
            _items[0] = lastItem;
            _keys[0] = lastKey;
            SinkFromTop();
        }

        return first;
    }

    private void SinkFromTop()
    {
        var at = 0;
        var count = _items.Count;
        while (true)
        {
            var left = (2 * at) + 1;
            if (left >= count)
            {
                return;
            }

            var right = left + 1;
            var smaller = right < count && _keys[right] < _keys[left] ? right : left;
            if (_keys[at] <= _keys[smaller])
            {
                return;
            }

            Swap(at, smaller);
            at = smaller;
        }
    }

    private void Swap(int a, int b)
    {
        (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
        (_items[a], _items[b]) = (_items[b], _items[a]);
    }
}
