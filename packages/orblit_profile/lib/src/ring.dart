import 'dart:typed_data';

/// The last few numbers, fixed in size so a profiler that runs for hours holds
/// the same memory it held in the first minute.
final class Ring {
  Ring(int size) : _values = Float64List(size) {
    if (size <= 0) throw ArgumentError.value(size, 'size', 'must be above 0');
  }

  final Float64List _values;
  int _count = 0;
  int _next = 0;

  int get length => _count;

  void add(double value) {
    _values[_next] = value;
    _next = (_next + 1) % _values.length;
    if (_count < _values.length) _count++;
  }

  /// Oldest first.
  List<double> get values {
    final start = (_next - _count) % _values.length;
    return [
      for (var i = 0; i < _count; i++) _values[(start + i) % _values.length],
    ];
  }

  double get last => _count == 0 ? 0 : _values[(_next - 1) % _values.length];

  double get mean {
    if (_count == 0) return 0;
    var sum = 0.0;
    for (final value in values) {
      sum += value;
    }
    return sum / _count;
  }

  double get max => values.fold(0.0, (a, b) => b > a ? b : a);

  /// The value [fraction] of the way up the sorted ring, nearest rank.
  double percentile(double fraction) {
    if (_count == 0) return 0;
    final sorted = values..sort();
    return sorted[((_count - 1) * fraction).round()];
  }
}
