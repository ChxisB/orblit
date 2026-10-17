import 'table.dart';

const int _all = 0xFFFFFFFF;

/// How a [Condition] compares a named value with a number.
enum Compare {
  equal('=='),
  notEqual('!='),
  less('<'),
  lessOrEqual('<='),
  greater('>'),
  greaterOrEqual('>=');

  const Compare(this.symbol);

  /// What a file writes for it.
  final String symbol;

  bool holds(int have, int want) => switch (this) {
    equal => have == want,
    notEqual => have != want,
    less => have < want,
    lessOrEqual => have <= want,
    greater => have > want,
    greaterOrEqual => have >= want,
  };

  /// The comparison written as [symbol], or null for any other text.
  static Compare? bySymbol(String symbol) {
    for (final compare in values) {
      if (compare.symbol == symbol) return compare;
    }
    return null;
  }
}

/// A comparison of one named value with a number, such as a quest stage at
/// least 2.
///
/// A list of them holds when every one does. For "or", write two lists and
/// let the first that holds win.
final class Condition {
  Condition(this.name, this.compare, this.value) {
    if (name.isEmpty) throw ArgumentError.value(name, 'name', 'is empty');
    if (value < 0 || value > _all) {
      throw ArgumentError.value(value, 'value', 'is outside 0 to $_all');
    }
  }

  /// Reads `{"name": ..., "op": ..., "value": ...}`. Throws
  /// [FormatException] for anything else.
  factory Condition.fromJson(Object? json) {
    if (json is Map<String, Object?>) {
      final name = json['name'];
      final op = json['op'];
      final value = json['value'];
      final compare = op is String ? Compare.bySymbol(op) : null;
      if (name is String &&
          name.isNotEmpty &&
          compare != null &&
          value is int &&
          value >= 0 &&
          value <= _all) {
        return Condition(name, compare, value);
      }
    }
    throw FormatException(
      'A condition is {"name": text, "op": one of '
      '${Compare.values.map((c) => c.symbol).join(' ')}, "value": a whole '
      'number from 0 to $_all}, not $json.',
    );
  }

  /// Reads a list of conditions. Null is no conditions.
  static List<Condition> listFromJson(Object? json) {
    if (json == null) return const [];
    if (json is! List<Object?>) {
      throw FormatException('Conditions are a list, not $json.');
    }
    return List.unmodifiable(json.map(Condition.fromJson));
  }

  /// Whether every condition in [conditions] holds in [table].
  static bool allHold(Iterable<Condition> conditions, ProgressTable table) =>
      conditions.every((condition) => condition.holds(table));

  final String name;
  final Compare compare;
  final int value;

  /// Throws [ArgumentError] when [table]'s schema has no such name.
  bool holds(ProgressTable table) => compare.holds(table.get(name), value);

  Map<String, Object?> toJson() => {
    'name': name,
    'op': compare.symbol,
    'value': value,
  };
}
