/// A queue that gives back the item with the smallest key first.
///
/// An item can be added again with a better key. The caller drops the stale
/// copy when it comes out, which is cheaper than finding and moving it.
final class MinHeap {
  final List<double> _keys = [];
  final List<int> _items = [];

  bool get isEmpty => _items.isEmpty;

  void add(int item, double key) {
    var at = _items.length;
    _items.add(item);
    _keys.add(key);
    while (at > 0) {
      final parent = (at - 1) >> 1;
      if (_keys[parent] <= _keys[at]) break;
      _swap(parent, at);
      at = parent;
    }
  }

  int removeFirst() {
    final first = _items.first;
    final lastItem = _items.removeLast();
    final lastKey = _keys.removeLast();
    if (_items.isNotEmpty) {
      _items[0] = lastItem;
      _keys[0] = lastKey;
      _sinkFromTop();
    }
    return first;
  }

  void _sinkFromTop() {
    var at = 0;
    final count = _items.length;
    while (true) {
      final left = 2 * at + 1;
      if (left >= count) return;
      final right = left + 1;
      final smaller = right < count && _keys[right] < _keys[left]
          ? right
          : left;
      if (_keys[at] <= _keys[smaller]) return;
      _swap(at, smaller);
      at = smaller;
    }
  }

  void _swap(int a, int b) {
    final key = _keys[a];
    _keys[a] = _keys[b];
    _keys[b] = key;
    final item = _items[a];
    _items[a] = _items[b];
    _items[b] = item;
  }
}
