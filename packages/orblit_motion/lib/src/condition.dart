import 'package:orblit_scene/orblit_scene.dart' show Values;

/// When a change is taken: a test of the blend's inputs, and of how far the
/// state it leaves has played.
///
/// Data rather than a function, so a blend can be written to a file, shown
/// in an editor and checked without playing it. A file writes one as a
/// small object:
///
/// - `{"input": "speed", "above": 0.1}` and `{"input": "speed", "below":
///   0.1}` compare an input with a number.
/// - `{"input": "jump"}` is an input that is on: anything but nought.
/// - `{"through": 1}` is the state having played that many times through,
///   so `1` is the end of a clip that holds and the first lap of one that
///   loops, and `0.8` starts the fade before the end.
/// - `{"all": [...]}`, `{"any": [...]}` and `{"not": {...}}` put them
///   together.
///
/// No condition at all is always, which is how a change taken as soon as it
/// can be is written.
sealed class BlendCondition {
  const BlendCondition();

  /// Always true. A change with nothing to wait for.
  static const BlendCondition always = _Always();

  /// [input] above [value].
  const factory BlendCondition.above(String input, double value) = _Above;

  /// [input] below [value].
  const factory BlendCondition.below(String input, double value) = _Below;

  /// [input] anything but nought: a flag, or a button held.
  const factory BlendCondition.on(String input) = _On;

  /// The state being left has played [times] times through.
  const factory BlendCondition.through(double times) = _Through;

  /// Every one of [all].
  const factory BlendCondition.all(List<BlendCondition> all) = _All;

  /// Any one of [any].
  const factory BlendCondition.any(List<BlendCondition> any) = _Any;

  /// Not [condition].
  const factory BlendCondition.not(BlendCondition condition) = _Not;

  /// Whether it holds, with [read] giving an input's value and [lap] how many
  /// times through the state being left has played.
  bool holds(double Function(String input) read, double lap);

  /// Every input it reads.
  Iterable<String> get inputs;

  /// Decoded JSON, the way [fromJson] reads it back.
  Map<String, Object?> toJson();

  /// A condition out of a file, or null with a note when it is not one.
  ///
  /// Null rather than always: a change whose test cannot be read would
  /// otherwise be taken the moment it could be, which is the one thing its
  /// author certainly did not mean.
  static BlendCondition? fromJson(
    Object? raw,
    String where,
    List<String> problems,
  ) {
    if (raw is! Map<String, Object?>) {
      problems.add('$where has a condition that is not one.');
      return null;
    }
    if (raw.isEmpty) return always;

    if (raw.containsKey('all') || raw.containsKey('any')) {
      final every = raw.containsKey('all');
      final parts = raw[every ? 'all' : 'any'];
      if (parts is! List) {
        problems.add('$where puts together something that is not a list.');
        return null;
      }
      final read = <BlendCondition>[];
      for (final part in parts) {
        final one = fromJson(part, where, problems);
        if (one == null) return null;
        read.add(one);
      }
      return every ? _All(read) : _Any(read);
    }
    if (raw.containsKey('not')) {
      final inner = fromJson(raw['not'], where, problems);
      return inner == null ? null : _Not(inner);
    }
    if (raw.containsKey('through')) {
      final times = Values.maybeNumber(raw, 'through');
      if (times == null || !times.isFinite) {
        problems.add(
          '$where waits to be through a number of times that is not a '
          'number.',
        );
        return null;
      }
      return _Through(times);
    }

    final input = Values.text(raw, 'input');
    if (input == null || input.isEmpty) {
      problems.add('$where has a condition this Orblit does not know.');
      return null;
    }
    if (raw.containsKey('above') || raw.containsKey('below')) {
      final above = raw.containsKey('above');
      final value = Values.maybeNumber(raw, above ? 'above' : 'below');
      if (value == null || !value.isFinite) {
        problems.add(
          '$where compares "$input" with something that is not a number.',
        );
        return null;
      }
      return above ? _Above(input, value) : _Below(input, value);
    }
    return _On(input);
  }
}

class _Always extends BlendCondition {
  const _Always();

  @override
  bool holds(double Function(String input) read, double lap) => true;

  @override
  Iterable<String> get inputs => const [];

  @override
  Map<String, Object?> toJson() => const {};
}

class _Above extends BlendCondition {
  const _Above(this.input, this.value);

  final String input;
  final double value;

  @override
  bool holds(double Function(String input) read, double lap) =>
      read(input) > value;

  @override
  Iterable<String> get inputs => [input];

  @override
  Map<String, Object?> toJson() => {'input': input, 'above': value};
}

class _Below extends BlendCondition {
  const _Below(this.input, this.value);

  final String input;
  final double value;

  @override
  bool holds(double Function(String input) read, double lap) =>
      read(input) < value;

  @override
  Iterable<String> get inputs => [input];

  @override
  Map<String, Object?> toJson() => {'input': input, 'below': value};
}

class _On extends BlendCondition {
  const _On(this.input);

  final String input;

  @override
  bool holds(double Function(String input) read, double lap) =>
      read(input) != 0;

  @override
  Iterable<String> get inputs => [input];

  @override
  Map<String, Object?> toJson() => {'input': input};
}

class _Through extends BlendCondition {
  const _Through(this.times);

  final double times;

  @override
  bool holds(double Function(String input) read, double lap) => lap >= times;

  @override
  Iterable<String> get inputs => const [];

  @override
  Map<String, Object?> toJson() => {'through': times};
}

class _All extends BlendCondition {
  const _All(this.all);

  final List<BlendCondition> all;

  @override
  bool holds(double Function(String input) read, double lap) =>
      all.every((one) => one.holds(read, lap));

  @override
  Iterable<String> get inputs => {for (final one in all) ...one.inputs};

  @override
  Map<String, Object?> toJson() => {
    'all': [for (final one in all) one.toJson()],
  };
}

class _Any extends BlendCondition {
  const _Any(this.any);

  final List<BlendCondition> any;

  @override
  bool holds(double Function(String input) read, double lap) =>
      any.any((one) => one.holds(read, lap));

  @override
  Iterable<String> get inputs => {for (final one in any) ...one.inputs};

  @override
  Map<String, Object?> toJson() => {
    'any': [for (final one in any) one.toJson()],
  };
}

class _Not extends BlendCondition {
  const _Not(this.condition);

  final BlendCondition condition;

  @override
  bool holds(double Function(String input) read, double lap) =>
      !condition.holds(read, lap);

  @override
  Iterable<String> get inputs => condition.inputs;

  @override
  Map<String, Object?> toJson() => {'not': condition.toJson()};
}
