import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'ramp.dart';

/// Where a point is at a time, for a path that is known: a sequence, a
/// ballistic arc, anything that can be asked.
typedef PathAt = Vector3 Function(double time);

/// The points of a trail at one moment, newest first, as flat arrays a
/// renderer can read without unpacking.
final class TrailStrip {
  TrailStrip(this.capacity)
    : positions = Float32List(capacity * 3),
      widths = Float32List(capacity) {
    if (capacity < 2) {
      throw ArgumentError.value(capacity, 'capacity', 'must be 2 or more');
    }
  }

  final int capacity;

  /// x, y, z per point.
  final Float32List positions;

  /// Width per point, in metres.
  final Float32List widths;

  /// How many slots hold a point.
  int count = 0;

  void _add(Vector3 position, double width) {
    position.copyIntoArray(positions, count * 3);
    widths[count++] = width;
  }
}

/// A strip that follows a point: a sword swing, a wheel mark, a streak behind
/// something fast. This holds the settings and nothing else.
///
/// It tapers by a width ramp over the [life], so a width that ends at 0 is
/// how the tail fades, since nothing here has alpha or a colour that changes.
final class Trail {
  Trail({this.life = 0.5, this.rate = 60, List<double> width = const [0.2, 0]})
    : width = List.unmodifiable(width) {
    _need(life > 0 && life.isFinite, 'life', life, 'must be above 0');
    _need(rate > 0 && rate.isFinite, 'rate', rate, 'must be above 0');
    _need(
      this.width.isNotEmpty && this.width.every((w) => w >= 0 && w.isFinite),
      'width',
      this.width,
      'needs one or more widths of 0 or more',
    );
    _need(
      points <= maxPoints,
      'life',
      life,
      'at $rate samples a second needs $points points, and a strip holds '
          '$maxPoints',
    );
  }

  /// Most points a strip can have. A skin holds 256 joints and a point takes
  /// two of them, one for each edge.
  static const int maxPoints = 128;

  /// Seconds a point stays in the strip.
  final double life;

  /// Samples a second.
  final double rate;

  /// Width over the life, spread evenly from the head to the tail.
  final List<double> width;

  /// Points a strip of this trail holds at most: one for each sample in the
  /// life, and the head.
  int get points => (life * rate).ceil() + 1;

  /// Fills [out] with the strip at [time], from a path that can be asked for
  /// any time. Nothing is kept, so a seek gives the same strip and the strip
  /// never jumps. The path is not asked for a time before 0.
  void sampleAlong(double time, PathAt path, TrailStrip out) {
    out.count = 0;
    if (!(time >= 0 && time.isFinite)) return;
    final step = life / (points - 1);
    final count = points < out.capacity ? points : out.capacity;
    for (var i = 0; i < count && time - i * step >= 0; i++) {
      out._add(path(time - i * step), rampAt(width, 1, 0, i * step / life));
    }
  }
}

/// The history of a [Trail] whose path is not known ahead, such as a physics
/// body. Call [record] each frame with the time and where the point is.
///
/// Time going backwards, or a step longer than the whole life, clears it,
/// since what it kept belongs to a different run. A scrub forward that is
/// shorter than the life cannot be told from a slow frame, so call [clear] on
/// a seek you know of.
final class TrailRing {
  TrailRing(this.trail);

  final Trail trail;

  // Oldest first.
  final List<(double, Vector3)> _kept = [];
  final Vector3 _here = Vector3.zero();
  double _now = double.nan;

  void record(double time, Vector3 position) {
    if (!(time >= 0 && time.isFinite)) return;
    if (time < _now || time - _now > trail.life) _kept.clear();
    _now = time;
    _here.setFrom(position);
    if (_kept.isEmpty || time - _kept.last.$1 >= 1 / trail.rate) {
      _kept.add((time, position.clone()));
    }
    final expired = _kept.indexWhere((s) => time - s.$1 <= trail.life);
    _kept.removeRange(0, expired < 0 ? _kept.length : expired);
  }

  void clear() {
    _kept.clear();
    _now = double.nan;
  }

  /// Fills [out] with the strip as of the last [record].
  void fill(TrailStrip out) {
    out.count = 0;
    if (_now.isNaN) return;
    out._add(_here, rampAt(trail.width, 1, 0, 0));
    for (var i = _kept.length - 1; i >= 0 && out.count < out.capacity; i--) {
      final age = _now - _kept[i].$1;
      if (age <= 0) continue;
      out._add(_kept[i].$2, rampAt(trail.width, 1, 0, age / trail.life));
    }
  }
}

void _need(bool ok, String name, Object? value, String why) {
  if (!ok) throw ArgumentError.value(value, name, why);
}
