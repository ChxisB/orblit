import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

/// The extension a spline is written under.
const String pathExtension = '.opath';

/// A spline file that could not be read.
final class PathFormatException implements Exception {
  const PathFormatException(this.message);

  final String message;

  @override
  String toString() => 'PathFormatException: $message';
}

/// A point a [Spline] passes through, how the way tilts there, and the
/// handles that bend the curve into and out of it.
///
/// A handle is an offset from the point, in metres. A knot with no handles
/// is a corner: the curve arrives and leaves it in a straight line.
final class PathKnot {
  PathKnot(
    Vector3 position, {
    this.tilt = 0,
    Vector3? handleIn,
    Vector3? handleOut,
  }) : _position = position.clone(),
       _in = handleIn?.clone() ?? Vector3.zero(),
       _out = handleOut?.clone() ?? Vector3.zero() {
    final finite =
        tilt.isFinite &&
        _position.storage.every((v) => v.isFinite) &&
        _in.storage.every((v) => v.isFinite) &&
        _out.storage.every((v) => v.isFinite);
    if (!finite) {
      throw ArgumentError('A knot is made of finite numbers.');
    }
  }

  final Vector3 _position;
  final Vector3 _in;
  final Vector3 _out;

  /// How far the up direction is rolled about the way forward, in radians.
  /// Positive rolls it towards the right-hand side of travel.
  final double tilt;

  Vector3 get position => _position.clone();

  /// Where the curve comes from, as an offset from [position].
  Vector3 get handleIn => _in.clone();

  /// Where the curve is going, as an offset from [position].
  Vector3 get handleOut => _out.clone();
}

/// Where a spline is at one distance, and which way it faces there.
final class PathFrame {
  const PathFrame({
    required this.position,
    required this.forward,
    required this.up,
  });

  final Vector3 position;

  /// The direction of travel, a unit vector.
  final Vector3 forward;

  /// Which way is up for something riding the curve, a unit vector at a
  /// right angle to [forward] with the knots' tilt already in it.
  final Vector3 up;

  /// The right-hand side of travel.
  Vector3 get right => forward.cross(up);
}

/// The point of a spline nearest to somewhere.
final class PathNearest {
  const PathNearest({
    required this.distance,
    required this.position,
    required this.offset,
  });

  /// How far along the spline the point is, in metres.
  final double distance;

  final Vector3 position;

  /// How far the place asked about is from [position].
  final double offset;
}

/// A curve through its knots, measured in metres so it can be walked by
/// distance and not by a parameter that bunches up at every tight bend.
///
/// Made once and read many times: the table that turns a distance into a
/// place is built the first time it is asked for.
final class Spline {
  /// A cubic Bezier through [knots], bent by their handles.
  Spline.bezier(List<PathKnot> knots, {this.closed = false})
    : knots = List.unmodifiable(knots) {
    if (this.knots.length < 2) {
      throw ArgumentError.value(
        this.knots.length,
        'knots',
        'A spline needs at least two knots.',
      );
    }
  }

  /// A smooth curve through [points] that leaves each one in the direction
  /// of its neighbours. [tilts] is one angle a point.
  ///
  /// Each handle is a third of the way to the neighbour it faces, so a
  /// short gap next to a long one bends gently and does not loop.
  factory Spline.through(
    List<Vector3> points, {
    List<double>? tilts,
    bool closed = false,
  }) {
    final count = points.length;
    final angles = _tiltsFor(points, tilts);
    Vector3 neighbour(int i) =>
        points[closed ? (i % count + count) % count : i.clamp(0, count - 1)];
    PathKnot knotAt(int i) {
      final previous = neighbour(i - 1);
      final next = neighbour(i + 1);
      final heading = next - previous;
      final way = heading.length2 > 1e-18
          ? heading.normalized()
          : Vector3.zero();
      return PathKnot(
        points[i],
        tilt: angles[i],
        handleIn: way * (-points[i].distanceTo(previous) / 3),
        handleOut: way * (points[i].distanceTo(next) / 3),
      );
    }

    return Spline.bezier([
      for (var i = 0; i < count; i++) knotAt(i),
    ], closed: closed);
  }

  /// Straight lines between [points], with a corner at each.
  factory Spline.line(
    List<Vector3> points, {
    List<double>? tilts,
    bool closed = false,
  }) {
    final angles = _tiltsFor(points, tilts);
    return Spline.bezier([
      for (var i = 0; i < points.length; i++)
        PathKnot(points[i], tilt: angles[i]),
    ], closed: closed);
  }

  static List<double> _tiltsFor(List<Vector3> points, List<double>? tilts) {
    if (tilts != null && tilts.length != points.length) {
      throw ArgumentError.value(
        tilts.length,
        'tilts',
        'Needs one angle for each of the ${points.length} points.',
      );
    }
    return tilts ?? List.filled(points.length, 0);
  }

  static const String marker = 'orblit.path';
  static const int formatVersion = 1;

  /// Samples between two knots when measuring. A bend is followed to
  /// within a few millimetres at a knot spacing of a few metres.
  static const int _steps = 64;

  final List<PathKnot> knots;

  /// Whether the last knot runs back round to the first.
  final bool closed;

  int get _segments => closed ? knots.length : knots.length - 1;

  late final List<({Vector3 a, Vector3 b, Vector3 c, Vector3 d})> _controls = [
    for (var i = 0; i < _segments; i++) _controlsOf(i),
  ];

  ({Vector3 a, Vector3 b, Vector3 c, Vector3 d}) _controlsOf(int segment) {
    final from = knots[segment];
    final to = knots[(segment + 1) % knots.length];
    return (
      a: from._position,
      b: from._position + from._out,
      c: to._position + to._in,
      d: to._position,
    );
  }

  /// Distance along the curve at each sample, and the sample's position as
  /// three numbers.
  late final ({Float64List along, Float64List points}) _table = () {
    final count = _segments * _steps + 1;
    final along = Float64List(count);
    final points = Float64List(count * 3);
    var previous = _pointAt(0, 0);
    for (var k = 0; k < count; k++) {
      final segment = math.min(k ~/ _steps, _segments - 1);
      final here = _pointAt(segment, (k - segment * _steps) / _steps);
      if (k > 0) along[k] = along[k - 1] + here.distanceTo(previous);
      points.setRange(k * 3, k * 3 + 3, here.storage);
      previous = here;
    }
    return (along: along, points: points);
  }();

  /// How long the curve is, in metres.
  double get length => _table.along.last;

  Vector3 _pointAt(int segment, double t) {
    final c = _controls[segment];
    final u = 1 - t;
    return c.a * (u * u * u) +
        c.b * (3 * u * u * t) +
        c.c * (3 * u * t * t) +
        c.d * (t * t * t);
  }

  Vector3 _slopeAt(int segment, double t) {
    final c = _controls[segment];
    final u = 1 - t;
    return (c.b - c.a) * (3 * u * u) +
        (c.c - c.b) * (6 * u * t) +
        (c.d - c.c) * (3 * t * t);
  }

  /// The segment and parameter at [distance], taken as it is on a closed
  /// curve and clamped to the ends on an open one. [sample] and [part] say
  /// where it falls between two samples of the table.
  ({int segment, double t, int sample, double part}) _locate(double distance) {
    final along = _table.along;
    final total = along.last;
    var d = distance;
    if (closed && total > 0) {
      d = distance % total;
    }
    d = d.clamp(0.0, total);
    var low = 0;
    var high = along.length - 1;
    while (high - low > 1) {
      final mid = (low + high) >> 1;
      if (along[mid] <= d) {
        low = mid;
      } else {
        high = mid;
      }
    }
    final span = along[low + 1] - along[low];
    final part = span <= 1e-12 ? 0.0 : (d - along[low]) / span;
    final global = low + part;
    final segment = math.min(global ~/ _steps, _segments - 1);
    return (
      segment: segment,
      t: (global - segment * _steps) / _steps,
      sample: low,
      part: part,
    );
  }

  /// The point of the table's polyline at [at]. The table is cut finely
  /// enough that this is within a hair of the curve, and unlike the curve
  /// it moves the same distance for the same part of a sample.
  Vector3 _onTable(({int segment, double t, int sample, double part}) at) {
    final samples = _table.points;
    final a = Vector3.array(samples, at.sample * 3);
    final b = Vector3.array(samples, at.sample * 3 + 3);
    return a + (b - a) * at.part;
  }

  /// The place [distance] metres along the curve, and which way it faces.
  PathFrame frameAt(double distance) {
    final at = _locate(distance);
    final forward = _forwardAt(at.segment, at.t);
    final from = knots[at.segment].tilt;
    final to = knots[(at.segment + 1) % knots.length].tilt;
    return PathFrame(
      position: _onTable(at),
      forward: forward,
      up: _upFor(forward, from + (to - from) * at.t),
    );
  }

  Vector3 positionAt(double distance) {
    return _onTable(_locate(distance));
  }

  Vector3 _forwardAt(int segment, double t) {
    final slope = _slopeAt(segment, t);
    if (slope.length2 > 1e-12) return slope.normalized();
    final c = _controls[segment];
    final chord = c.d - c.a;
    return chord.length2 > 1e-12 ? chord.normalized() : Vector3(0, 0, -1);
  }

  static Vector3 _upFor(Vector3 forward, double tilt) {
    var up = Vector3(0, 1, 0) - forward * forward.y;
    if (up.length2 < 1e-9) up = Vector3(0, 0, -1) - forward * forward.z;
    up.normalize();
    final rolled = up * math.cos(tilt) + forward.cross(up) * math.sin(tilt);
    return rolled..normalize();
  }

  /// The point of the curve nearest [point], and how far along it is.
  PathNearest closest(Vector3 point) {
    final along = _table.along;
    final samples = _table.points;
    var best = double.infinity;
    var bestDistance = 0.0;
    for (var k = 0; k + 1 < along.length; k++) {
      final a = Vector3.array(samples, k * 3);
      final b = Vector3.array(samples, k * 3 + 3);
      final edge = b - a;
      final run = edge.length2;
      final u = run < 1e-18
          ? 0.0
          : ((point - a).dot(edge) / run).clamp(0.0, 1.0);
      final gap = point.distanceToSquared(a + edge * u);
      if (gap < best) {
        best = gap;
        bestDistance = along[k] + u * (along[k + 1] - along[k]);
      }
    }
    final position = positionAt(bestDistance);
    return PathNearest(
      distance: bestDistance,
      position: position,
      offset: point.distanceTo(position),
    );
  }

  // The file.

  Map<String, Object?> toJson() => {
    'kind': marker,
    'formatVersion': formatVersion,
    'closed': closed,
    'knots': [for (final knot in knots) _knotJson(knot)],
  };

  static Map<String, Object?> _knotJson(PathKnot knot) => {
    'at': knot._position.storage.toList(),
    if (knot.tilt != 0) 'tilt': knot.tilt,
    if (knot._in.length2 > 0) 'in': knot._in.storage.toList(),
    if (knot._out.length2 > 0) 'out': knot._out.storage.toList(),
  };

  /// The file's text, a knot to a line so the diff of two versions is the
  /// knots that moved.
  String encode() {
    final knots = [
      for (final knot in this.knots) '    ${jsonEncode(_knotJson(knot))}',
    ];
    return '{\n'
        '  "kind": ${jsonEncode(marker)},\n'
        '  "formatVersion": $formatVersion,\n'
        '  "closed": $closed,\n'
        '  "knots": [\n${knots.join(',\n')}\n  ]\n'
        '}\n';
  }

  /// A spline out of a file's text.
  ///
  /// Throws [PathFormatException] when the text is not a path, is from a
  /// newer Orblit, or has a knot that is not made of numbers.
  static Spline decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw PathFormatException('This is not a path file: ${error.message}');
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw const PathFormatException('This is not a path file.');
    }
    final version = parsed['formatVersion'];
    if (version is int && version > formatVersion) {
      throw const PathFormatException(
        'This path was written by a newer Orblit.',
      );
    }
    final raw = parsed['knots'];
    if (raw is! List || raw.length < 2) {
      throw const PathFormatException('A path needs at least two knots.');
    }
    return Spline.bezier([
      for (var i = 0; i < raw.length; i++) _knotFrom(raw[i], i),
    ], closed: parsed['closed'] == true);
  }

  static PathKnot _knotFrom(Object? raw, int index) {
    if (raw is! Map<String, Object?>) {
      throw PathFormatException('Knot $index is not an object.');
    }
    final at = _vectorFrom(raw['at'], 'at', index);
    if (at == null) {
      throw PathFormatException('Knot $index does not say where it is.');
    }
    final tilt = raw['tilt'];
    if (tilt != null && (tilt is! num || !tilt.isFinite)) {
      throw PathFormatException('Knot $index has a tilt that is not a number.');
    }
    return PathKnot(
      at,
      tilt: (tilt as num?)?.toDouble() ?? 0,
      handleIn: _vectorFrom(raw['in'], 'in', index),
      handleOut: _vectorFrom(raw['out'], 'out', index),
    );
  }

  static Vector3? _vectorFrom(Object? raw, String key, int index) {
    if (raw == null) return null;
    if (raw is! List ||
        raw.length != 3 ||
        !raw.every((v) => v is num && v.isFinite)) {
      throw PathFormatException('Knot $index has "$key" that is not x, y, z.');
    }
    return Vector3(
      (raw[0] as num).toDouble(),
      (raw[1] as num).toDouble(),
      (raw[2] as num).toDouble(),
    );
  }
}
