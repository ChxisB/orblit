import 'dart:collection';

/// One place two documents differ, as a path of keys and indices joined with
/// `/`. A value that is absent on one side is null there.
typedef Difference = ({String path, Object? from, Object? to});

/// Every place the JSON documents [a] and [b] differ.
///
/// Maps and lists are walked together. A value that changed kind is reported
/// once, where it changed, whole. A missing key and a null look the same,
/// which is the price of one comparison that serves entities and any extras
/// beside them.
List<Difference> diffJson(Object? a, Object? b, [String path = '']) {
  if (a is Map<String, Object?> && b is Map<String, Object?>) {
    return [
      for (final key in {...a.keys, ...b.keys}.toList()..sort())
        ...diffJson(a[key], b[key], _join(path, key)),
    ];
  }
  if (a is List<Object?> && b is List<Object?>) {
    final longest = a.length > b.length ? a.length : b.length;
    return [
      for (var i = 0; i < longest; i++)
        ...diffJson(
          i < a.length ? a[i] : null,
          i < b.length ? b[i] : null,
          _join(path, '$i'),
        ),
    ];
  }
  return a == b ? const [] : [(path: path, from: a, to: b)];
}

String _join(String path, String key) => path.isEmpty ? key : '$path/$key';

/// The last few snapshots, by the id each was given.
final class SnapshotStore {
  /// Throws [ArgumentError] for a capacity below one.
  SnapshotStore({this.capacity = 8}) {
    if (capacity < 1) {
      throw ArgumentError.value(capacity, 'capacity', 'is below 1');
    }
  }

  final int capacity;
  final LinkedHashMap<String, Map<String, Object?>> _byId = LinkedHashMap();
  int _count = 0;

  /// The id of the snapshot added last. Throws [StateError] before the first.
  String get lastId => _byId.keys.last;

  /// Keeps [snapshot] under a new id, and drops the oldest when full.
  void add(Map<String, Object?> snapshot) {
    _count++;
    _byId['s$_count'] = snapshot;
    if (_byId.length > capacity) _byId.remove(_byId.keys.first);
  }

  /// The snapshot kept under [id], or null when there is none.
  Map<String, Object?>? find(String id) => _byId[id];
}
