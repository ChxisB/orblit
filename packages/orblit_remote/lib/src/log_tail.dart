import 'dart:collection';

/// The last lines a game logged, so a session can read what just happened
/// without finding the console.
final class LogTail {
  /// Throws [ArgumentError] for a capacity below one.
  LogTail({this.capacity = 500}) {
    if (capacity < 1) {
      throw ArgumentError.value(capacity, 'capacity', 'is below 1');
    }
  }

  final int capacity;
  final Queue<String> _lines = Queue();

  /// Keeps [line], and drops the oldest when the ring is full.
  void add(String line) {
    _lines.addLast(line);
    if (_lines.length > capacity) _lines.removeFirst();
  }

  /// The last [count] lines, oldest first. Throws [ArgumentError] when
  /// [count] is negative.
  List<String> tail(int count) {
    RangeError.checkNotNegative(count, 'count');
    final skip = _lines.length > count ? _lines.length - count : 0;
    return _lines.skip(skip).toList();
  }
}
