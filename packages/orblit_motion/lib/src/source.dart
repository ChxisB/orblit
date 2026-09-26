import 'package:orblit_scene/orblit_scene.dart' show Values;

/// What a state of a blend plays: one clip, or clips mixed by the blend's
/// inputs.
///
/// A clip is named, never held, the way a scene's motion component names
/// the clips it has: whoever plays the blend hands over the clips by the
/// same names. So one blend serves every character with the same set of
/// moves, and the file says which clips it wants without carrying them.
///
/// The mixes nest. A point on a line or a plane plays anything a state can,
/// so running can be a plane of directions at each of three speeds.
sealed class BlendSource {
  const BlendSource();

  /// Every clip this plays and how much say each has, with [read] giving an
  /// input's value, in the order they are written. Clips with no say are
  /// left out, and a clip named twice has the two says added together.
  Map<String, double> weigh(double Function(String input) read) {
    final out = <String, double>{};
    _weigh(read, 1, out);
    return out;
  }

  /// Every clip this names, whether or not it has a say right now.
  Iterable<String> get clips;

  /// Every input this reads.
  Iterable<String> get inputs;

  void _weigh(
    double Function(String input) read,
    double scale,
    Map<String, double> out,
  );

  /// Decoded JSON, as a state or a point writes it: one of `clip`, `line`
  /// or `plane`.
  Map<String, Object?> toJson();

  /// What a state or a point in a file plays, or null with a note when it
  /// names nothing that can be played. [where] says whose it is, for the
  /// note.
  static BlendSource? fromJson(
    Map<String, Object?> raw,
    String where,
    List<String> problems,
  ) {
    final clip = Values.text(raw, 'clip');
    if (clip != null && clip.isNotEmpty) return BlendClip(clip);
    if (raw['line'] case final Map<String, Object?> line) {
      return BlendLine._read(line, where, problems);
    }
    if (raw['plane'] case final Map<String, Object?> plane) {
      return BlendPlane._read(plane, where, problems);
    }
    problems.add(
      '${_capital(where)} plays nothing: it names no clip, line or plane.',
    );
    return null;
  }
}

String _capital(String text) =>
    text.isEmpty ? text : text[0].toUpperCase() + text.substring(1);

/// One clip, played as it is.
class BlendClip extends BlendSource {
  const BlendClip(this.clip);

  /// The clip's name, as whoever plays the blend knows it.
  final String clip;

  @override
  Iterable<String> get clips => [clip];

  @override
  Iterable<String> get inputs => const [];

  @override
  void _weigh(
    double Function(String input) read,
    double scale,
    Map<String, double> out,
  ) {
    if (scale > 0) out[clip] = (out[clip] ?? 0) + scale;
  }

  @override
  Map<String, Object?> toJson() => {'clip': clip};
}

/// One place along a [BlendLine], and what plays there.
class LinePoint {
  const LinePoint(this.at, this.plays);

  /// Where along the line's input.
  final double at;

  final BlendSource plays;
}

/// Clips along one input: a walk at one speed and a run at another, and
/// between them a mix of the two in proportion.
///
/// Below the first point is the first point, and above the last is the
/// last, so a speed past the fastest clip runs as the fastest clip does
/// rather than as something nobody made.
class BlendLine extends BlendSource {
  BlendLine(this.input, List<LinePoint> points)
    : points = List<LinePoint>.unmodifiable(_inOrder(points)) {
    if (points.isEmpty) {
      throw ArgumentError.value(points, 'points', 'A line needs a point.');
    }
    for (final point in points) {
      if (!point.at.isFinite) {
        throw ArgumentError.value(point.at, 'at', 'A point is somewhere.');
      }
    }
  }

  /// The input it runs along.
  final String input;

  /// In order along the line. Two at one place are a step: the earlier in
  /// the list has everything below, and the later everything from there up.
  final List<LinePoint> points;

  @override
  Iterable<String> get clips => [
    for (final point in points) ...point.plays.clips,
  ];

  @override
  Iterable<String> get inputs => {
    input,
    for (final point in points) ...point.plays.inputs,
  };

  @override
  void _weigh(
    double Function(String input) read,
    double scale,
    Map<String, double> out,
  ) {
    final value = read(input);
    if (points.length == 1 || !(value >= points.first.at)) {
      points.first.plays._weigh(read, scale, out);
      return;
    }
    if (value >= points.last.at) {
      points.last.plays._weigh(read, scale, out);
      return;
    }
    for (var i = 0; i + 1 < points.length; i++) {
      final low = points[i];
      final high = points[i + 1];
      if (value >= high.at) continue;
      final along = (value - low.at) / (high.at - low.at);
      low.plays._weigh(read, scale * (1 - along), out);
      high.plays._weigh(read, scale * along, out);
      return;
    }
  }

  @override
  Map<String, Object?> toJson() => {
    'line': {
      'input': input,
      'points': [
        for (final point in points) {'at': point.at, ...point.plays.toJson()},
      ],
    },
  };

  static BlendLine? _read(
    Map<String, Object?> raw,
    String where,
    List<String> problems,
  ) {
    final input = Values.text(raw, 'input');
    if (input == null || input.isEmpty) {
      problems.add('The line in $where runs along no input.');
      return null;
    }
    final points = <LinePoint>[];
    final rawPoints = raw['points'];
    for (final entry in rawPoints is List ? rawPoints : const []) {
      final at = entry is Map<String, Object?>
          ? Values.maybeNumber(entry, 'at')
          : null;
      if (entry is! Map<String, Object?> || at == null || !at.isFinite) {
        problems.add(
          'A point on the line in $where is at no number, so it was left '
          'out.',
        );
        continue;
      }
      final plays = BlendSource.fromJson(
        entry,
        'the point at $at on the line in $where',
        problems,
      );
      if (plays != null) points.add(LinePoint(at, plays));
    }
    if (points.isEmpty) {
      problems.add('The line in $where has no points.');
      return null;
    }
    return BlendLine(input, points);
  }

  /// [points] in order along the line, keeping the order of any at one
  /// place.
  static List<LinePoint> _inOrder(List<LinePoint> points) {
    final numbered = [for (var i = 0; i < points.length; i++) (i, points[i])]
      ..sort((a, b) {
        final by = a.$2.at.compareTo(b.$2.at);
        return by != 0 ? by : a.$1.compareTo(b.$1);
      });
    return [for (final (_, point) in numbered) point];
  }
}

/// One place on a [BlendPlane], and what plays there.
class PlanePoint {
  const PlanePoint(this.x, this.y, this.plays);

  /// Where, in the plane's [BlendPlane.x] input.
  final double x;

  /// Where, in the plane's [BlendPlane.y] input.
  final double y;

  final BlendSource plays;
}

/// Clips over two inputs: walks forwards, backwards and to either side, with
/// the direction of travel choosing between them.
///
/// The points go anywhere, in no pattern. Each has everything at its own
/// place, and its say falls away towards each of the others along the line
/// between the two, so the mix is smooth everywhere, exact at every point,
/// and settles on the nearest points outside them all. The two inputs are
/// measured against each other, so they want to be in comparable units: a
/// velocity's two parts, not a speed and an angle.
class BlendPlane extends BlendSource {
  BlendPlane(this.x, this.y, List<PlanePoint> points)
    : points = List<PlanePoint>.unmodifiable(points) {
    if (points.isEmpty) {
      throw ArgumentError.value(points, 'points', 'A plane needs a point.');
    }
    for (final point in points) {
      if (!point.x.isFinite || !point.y.isFinite) {
        throw ArgumentError('A point on a plane is somewhere.');
      }
    }
  }

  /// The input across.
  final String x;

  /// The input up.
  final String y;

  final List<PlanePoint> points;

  @override
  Iterable<String> get clips => [
    for (final point in points) ...point.plays.clips,
  ];

  @override
  Iterable<String> get inputs => {
    x,
    y,
    for (final point in points) ...point.plays.inputs,
  };

  /// How much say each point has at ([atX], [atY]), adding to one.
  List<double> weightsAt(double atX, double atY) {
    final weights = List<double>.filled(points.length, 0);
    var total = 0.0;
    for (var i = 0; i < points.length; i++) {
      final from = points[i];
      final toX = atX - from.x;
      final toY = atY - from.y;
      var weight = 1.0;
      for (var j = 0; j < points.length && weight > 0; j++) {
        if (j == i) continue;
        final acrossX = points[j].x - from.x;
        final acrossY = points[j].y - from.y;
        final length2 = acrossX * acrossX + acrossY * acrossY;
        // Two points in one place share it.
        if (length2 < 1e-12) continue;
        final along = (toX * acrossX + toY * acrossY) / length2;
        final left = (1 - along).clamp(0.0, 1.0);
        if (left < weight) weight = left;
      }
      weights[i] = weight;
      total += weight;
    }
    if (total > 0) {
      for (var i = 0; i < weights.length; i++) {
        weights[i] /= total;
      }
      return weights;
    }
    // Nothing has a say only where every point is shadowed by another,
    // which rounding can make true at a corner. The nearest has it all.
    var nearest = 0;
    var best = double.infinity;
    for (var i = 0; i < points.length; i++) {
      final dx = atX - points[i].x;
      final dy = atY - points[i].y;
      final distance = dx * dx + dy * dy;
      if (distance < best) {
        best = distance;
        nearest = i;
      }
    }
    weights[nearest] = 1;
    return weights;
  }

  @override
  void _weigh(
    double Function(String input) read,
    double scale,
    Map<String, double> out,
  ) {
    final weights = weightsAt(read(x), read(y));
    for (var i = 0; i < points.length; i++) {
      if (weights[i] > 0) points[i].plays._weigh(read, scale * weights[i], out);
    }
  }

  @override
  Map<String, Object?> toJson() => {
    'plane': {
      'x': x,
      'y': y,
      'points': [
        for (final point in points)
          {
            'at': [point.x, point.y],
            ...point.plays.toJson(),
          },
      ],
    },
  };

  static BlendPlane? _read(
    Map<String, Object?> raw,
    String where,
    List<String> problems,
  ) {
    final x = Values.text(raw, 'x');
    final y = Values.text(raw, 'y');
    if (x == null || x.isEmpty || y == null || y.isEmpty) {
      problems.add('The plane in $where needs an input across and one up.');
      return null;
    }
    final points = <PlanePoint>[];
    final rawPoints = raw['points'];
    for (final entry in rawPoints is List ? rawPoints : const []) {
      final at = entry is Map<String, Object?> ? entry['at'] : null;
      if (entry is! Map<String, Object?> ||
          at is! List ||
          at.length != 2 ||
          at.any((one) => one is! num || !one.isFinite)) {
        problems.add(
          'A point on the plane in $where is not at two numbers, '
          'so it was left out.',
        );
        continue;
      }
      final atX = (at[0] as num).toDouble();
      final atY = (at[1] as num).toDouble();
      final plays = BlendSource.fromJson(
        entry,
        'the point at ($atX, $atY) on the plane in $where',
        problems,
      );
      if (plays != null) points.add(PlanePoint(atX, atY, plays));
    }
    if (points.isEmpty) {
      problems.add('The plane in $where has no points.');
      return null;
    }
    return BlendPlane(x, y, points);
  }
}
