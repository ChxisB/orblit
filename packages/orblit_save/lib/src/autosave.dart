import 'dart:math';

/// Picks which keys to save on each tick. A key comes due once in [every]
/// ticks plus up to [jitter], and a tick claims at most [perTick] of the keys
/// that are due, the longest overdue first. A key seen for the first time
/// comes due somewhere in its first [every] ticks, so players who joined
/// together do not save together.
// shortcut: a tick sorts the due keys, which is fine to a few thousand keys.
// Upgrade to a heap past that.
final class AutoSave {
  AutoSave({
    required this.perTick,
    required this.every,
    required this.jitter,
    required Random random,
  }) : _random = random {
    if (perTick < 1) {
      throw ArgumentError.value(perTick, 'perTick', 'is below 1');
    }
    if (every < 1) {
      throw ArgumentError.value(every, 'every', 'is below 1');
    }
    if (jitter < 0) {
      throw ArgumentError.value(jitter, 'jitter', 'is negative');
    }
  }

  final int perTick;
  final int every;
  final int jitter;
  final Random _random;

  final Map<String, int> _dueAt = {};

  /// The keys of [live] to save on [tick]. A key that is no longer in [live]
  /// is forgotten.
  // F5: claiming a key is what moves its next turn, as removeFirst does.
  List<String> claim(Iterable<String> live, int tick) {
    final keys = live.toSet();
    _dueAt.removeWhere((key, _) => !keys.contains(key));
    for (final key in keys) {
      _dueAt.putIfAbsent(key, () => tick + 1 + _random.nextInt(every));
    }
    final due =
        [
          for (final MapEntry(:key, :value) in _dueAt.entries)
            if (value <= tick) (key, value),
        ]..sort(
          (a, b) => a.$2 != b.$2 ? a.$2.compareTo(b.$2) : a.$1.compareTo(b.$1),
        );
    final picked = [for (final (key, _) in due.take(perTick)) key];
    for (final key in picked) {
      _dueAt[key] = tick + every + _random.nextInt(jitter + 1);
    }
    return picked;
  }
}
