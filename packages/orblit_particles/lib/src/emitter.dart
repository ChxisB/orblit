import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import 'buffer.dart';
import 'random.dart';
import 'shape.dart';

/// Where an emitter was at a time. A particle that rides with its emitter asks
/// for the moment it is drawn, one that is left behind asks for the moment it
/// was born. A fixed emitter passes `(_) => matrix`.
typedef Placement = Matrix4 Function(double time);

/// Whether particles stay put once born or travel with the emitter.
enum ParticleSpace { world, local }

/// A span of values. A particle takes one at random, but always the same one.
final class Range {
  const Range(this.min, this.max);

  const Range.of(double value) : min = value, max = value;

  final double min;
  final double max;

  double at(double fraction) => min + (max - min) * fraction;
}

/// [count] particles all born at [time] seconds.
final class Burst {
  Burst(this.time, this.count) {
    if (!(time >= 0 && time.isFinite)) {
      throw ArgumentError.value(time, 'time', 'must be 0 or more');
    }
    if (count < 1 || count > _burstStride - 1) {
      throw ArgumentError.value(count, 'count', 'must be 1 to 65535');
    }
  }

  final double time;
  final int count;
}

const int _burstStride = 65536;
const int _burstBase = 0x40000000;

/// What makes smoke, sparks, dust or fire: where particles start, how many,
/// how long they live and what happens to them on the way.
///
/// An emitter is data. It keeps no particles. Ask it for the frame at a time
/// with `sample`, and the same time gives the same frame on every machine.
final class Emitter {
  Emitter({
    this.seed = 0,
    this.shape = const PointShape(),
    this.rate = 10,
    List<Burst> bursts = const [],
    this.duration,
    this.lifetime = const Range(1, 2),
    this.speed = const Range(1, 2),
    List<double> size = const [0.1],
    List<double> colour = const [1, 1, 1],
    Vector3? gravity,
    this.drag = 0,
    this.noiseStrength = 0,
    this.noiseScale = 1,
    this.spin = const Range.of(0),
    this.space = ParticleSpace.world,
    this.maxParticles = 1000,
  }) : bursts = List.unmodifiable(bursts),
       size = List.unmodifiable(size),
       colour = List.unmodifiable(colour),
       gravity = Vector3.copy(gravity ?? Vector3.zero()) {
    _check();
  }

  final int seed;
  final Shape shape;

  /// Particles born each second, from time 0 until [duration].
  final double rate;
  final List<Burst> bursts;

  /// Seconds after which no more steady births happen, or null for no end.
  /// A particle born before then lives its whole life. Bursts ignore it.
  final double? duration;

  /// Seconds a particle lives.
  final Range lifetime;

  /// Metres per second at birth, along the direction the shape gives.
  final Range speed;

  /// Edge length over a particle's life, spread evenly from birth to death.
  final List<double> size;

  /// Red, green, blue triples over a particle's life.
  final List<double> colour;

  /// Metres per second squared. Do not change it after construction.
  final Vector3 gravity;

  /// Per second, the share of speed lost to the air.
  final double drag;

  /// Metres a particle is pushed about by smooth noise.
  final double noiseStrength;

  /// Noise cycles per metre.
  final double noiseScale;

  /// Radians per second.
  final Range spin;
  final ParticleSpace space;

  /// Particles alive at once. When more are due, the oldest die first.
  final int maxParticles;

  // shortcut: a copy cannot clear [duration]. Build a new Emitter to remove it.
  Emitter copyWith({
    int? seed,
    Shape? shape,
    double? rate,
    List<Burst>? bursts,
    double? duration,
    Range? lifetime,
    Range? speed,
    List<double>? size,
    List<double>? colour,
    Vector3? gravity,
    double? drag,
    double? noiseStrength,
    double? noiseScale,
    Range? spin,
    ParticleSpace? space,
    int? maxParticles,
  }) => Emitter(
    seed: seed ?? this.seed,
    shape: shape ?? this.shape,
    rate: rate ?? this.rate,
    bursts: bursts ?? this.bursts,
    duration: duration ?? this.duration,
    lifetime: lifetime ?? this.lifetime,
    speed: speed ?? this.speed,
    size: size ?? this.size,
    colour: colour ?? this.colour,
    gravity: gravity ?? this.gravity,
    drag: drag ?? this.drag,
    noiseStrength: noiseStrength ?? this.noiseStrength,
    noiseScale: noiseScale ?? this.noiseScale,
    spin: spin ?? this.spin,
    space: space ?? this.space,
    maxParticles: maxParticles ?? this.maxParticles,
  );

  /// Id of particle [number] of the burst at [index] in [bursts]. Ids of the
  /// steady stream are 0, 1, 2 and so on, so the two never meet.
  static int burstId(int index, int number) =>
      _burstBase + index * _burstStride + number;

  /// Seconds particle [id] lives.
  double lifeOf(int id) => lifetime.at(unitAt(seed, id, 0));

  /// Writes where particle [id] starts and how fast it heads off, in the
  /// emitter's own space.
  void birth(int id, Vector3 position, Vector3 velocity) {
    shape.pick(seed, id, position, velocity);
    velocity.scale(speed.at(unitAt(seed, id, 1)));
  }

  /// Writes the size, colour and angle of particle [id] to [slot] of [out],
  /// [age] seconds into a life of [life].
  void look(ParticleBuffer out, int slot, int id, double age, double life) {
    final fraction = age / life;
    out.sizes[slot] = _ramp(size, 1, 0, fraction);
    for (var channel = 0; channel < 3; channel++) {
      out.colours[slot * 3 + channel] = _ramp(colour, 3, channel, fraction);
    }
    out.angles[slot] =
        unitAt(seed, id, 8) * 2 * math.pi + spin.at(unitAt(seed, id, 7)) * age;
  }

  void _check() {
    _need(rate >= 0 && rate.isFinite, 'rate', rate, 'must be 0 or more');
    _need(
      (duration ?? 0) >= 0 && (duration ?? 0).isFinite,
      'duration',
      duration,
      'must be 0 or more',
    );
    _span(lifetime, 'lifetime');
    _need(lifetime.min > 0, 'lifetime', lifetime.min, 'must be above 0');
    _span(speed, 'speed');
    _span(spin, 'spin');
    _need(
      size.isNotEmpty && size.every((s) => s >= 0 && s.isFinite),
      'size',
      size,
      'needs one or more sizes of 0 or more',
    );
    _need(
      colour.isNotEmpty &&
          colour.length % 3 == 0 &&
          colour.every((c) => c.isFinite),
      'colour',
      colour,
      'needs red, green, blue triples',
    );
    _need(drag >= 0 && drag.isFinite, 'drag', drag, 'must be 0 or more');
    _need(
      noiseStrength >= 0 && noiseStrength.isFinite,
      'noiseStrength',
      noiseStrength,
      'must be 0 or more',
    );
    _need(
      noiseScale > 0 && noiseScale.isFinite,
      'noiseScale',
      noiseScale,
      'must be above 0',
    );
    _need(
      gravity.storage.every((g) => g.isFinite),
      'gravity',
      gravity,
      'must be finite',
    );
    _need(maxParticles >= 1, 'maxParticles', maxParticles, 'must be 1 or more');
  }

  void _span(Range range, String name) => _need(
    range.min.isFinite && range.max.isFinite && range.min <= range.max,
    name,
    '${range.min} to ${range.max}',
    'must run from low to high',
  );
}

void _need(bool ok, String name, Object? value, String why) {
  if (!ok) throw ArgumentError.value(value, name, why);
}

/// [values] read as [stride] numbers per stop, spread evenly over 0 to 1.
double _ramp(List<double> values, int stride, int channel, double fraction) {
  final last = values.length ~/ stride - 1;
  if (last == 0) return values[channel];
  final at = fraction.clamp(0.0, 1.0) * last;
  final stop = math.min(at.floor(), last - 1);
  final across = at - stop;
  return values[stop * stride + channel] * (1 - across) +
      values[(stop + 1) * stride + channel] * across;
}
