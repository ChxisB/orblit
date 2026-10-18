import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../values.dart';

/// Where an emitter's particles start.
enum EmitterShape { point, sphere, cone, box }

/// Whether particles stay where they were born or travel with the entity.
enum EmitterSpace { world, local }

/// [count] particles all born [time] seconds after the emitter began.
final class EmitterBurst {
  const EmitterBurst(this.time, this.count);

  final double time;
  final int count;
}

/// An entity that gives off particles: smoke, sparks, dust or fire, from where
/// the entity is.
///
/// This holds the settings and nothing else. A particle is worked out from
/// them and the time, so the file says what an effect is and never where its
/// particles are. The shape reads [radius] for a sphere and for the disc a
/// cone starts on, [angle] for how far a cone spreads from straight up, and
/// [extents], half a side each way, for a box. A point reads none of them.
///
/// A file whose numbers do not make an emitter reads as the default for that
/// number. A lifetime that is not above nothing, a range that runs backwards,
/// a colour that is not red, green and blue, or a size below nothing is one of
/// those, since an effect that looks wrong is better than a scene that will
/// not open.
class ParticleEmitterComponent extends SceneComponent {
  ParticleEmitterComponent({
    this.seed = 0,
    this.shape = EmitterShape.point,
    this.radius = 0.5,
    this.angle = 0.4,
    Vector3? extents,
    this.rate = 10,
    this.bursts = const [],
    this.duration,
    this.lifetimeMin = 1,
    this.lifetimeMax = 2,
    this.speedMin = 1,
    this.speedMax = 2,
    this.spinMin = 0,
    this.spinMax = 0,
    this.size = const [0.1],
    this.colour = const [1, 1, 1],
    Vector3? gravity,
    this.drag = 0,
    this.noiseStrength = 0,
    this.noiseScale = 1,
    this.space = EmitterSpace.world,
    this.maxParticles = 1000,
  }) : extents = extents?.clone() ?? Vector3.all(0.5),
       gravity = gravity?.clone() ?? Vector3.zero();

  static ParticleEmitterComponent fromJson(Map<String, Object?> json) {
    final lifetime = _pair(json['lifetime'], 1, 2, (low, _) => low > 0);
    final speed = _pair(json['speed'], 1, 2);
    final spin = _pair(json['spin'], 0, 0);
    return ParticleEmitterComponent(
      seed: _seed(json),
      shape:
          Values.named(EmitterShape.values, json['shape']) ??
          EmitterShape.point,
      radius: _atLeast(json, 'radius', 0.5),
      angle: _spread(json),
      extents: _extents(json['extents']),
      rate: _atLeast(json, 'rate', 10),
      bursts: _bursts(json['bursts']),
      duration: _duration(json),
      lifetimeMin: lifetime.$1,
      lifetimeMax: lifetime.$2,
      speedMin: speed.$1,
      speedMax: speed.$2,
      spinMin: spin.$1,
      spinMax: spin.$2,
      size: _ramp(json['size'], 1, const [0.1], nonNegative: true),
      colour: _ramp(json['colour'], 3, const [1, 1, 1]),
      gravity: _gravity(json['gravity']),
      drag: _atLeast(json, 'drag', 0),
      noiseStrength: _atLeast(json, 'noiseStrength', 0),
      noiseScale: _atLeast(json, 'noiseScale', 1, above: true),
      space:
          Values.named(EmitterSpace.values, json['space']) ??
          EmitterSpace.world,
      maxParticles: _cap(json),
    );
  }

  /// The same emitter with some of it changed. A [duration] cannot be cleared
  /// here, only set; build a new emitter to give it no end.
  ParticleEmitterComponent copyWith({
    int? seed,
    EmitterShape? shape,
    double? radius,
    double? angle,
    Vector3? extents,
    double? rate,
    List<EmitterBurst>? bursts,
    double? duration,
    double? lifetimeMin,
    double? lifetimeMax,
    double? speedMin,
    double? speedMax,
    double? spinMin,
    double? spinMax,
    List<double>? size,
    List<double>? colour,
    Vector3? gravity,
    double? drag,
    double? noiseStrength,
    double? noiseScale,
    EmitterSpace? space,
    int? maxParticles,
  }) => ParticleEmitterComponent(
    seed: seed ?? this.seed,
    shape: shape ?? this.shape,
    radius: radius ?? this.radius,
    angle: angle ?? this.angle,
    extents: extents ?? this.extents,
    rate: rate ?? this.rate,
    bursts: bursts ?? this.bursts,
    duration: duration ?? this.duration,
    lifetimeMin: lifetimeMin ?? this.lifetimeMin,
    lifetimeMax: lifetimeMax ?? this.lifetimeMax,
    speedMin: speedMin ?? this.speedMin,
    speedMax: speedMax ?? this.speedMax,
    spinMin: spinMin ?? this.spinMin,
    spinMax: spinMax ?? this.spinMax,
    size: size ?? this.size,
    colour: colour ?? this.colour,
    gravity: gravity ?? this.gravity,
    drag: drag ?? this.drag,
    noiseStrength: noiseStrength ?? this.noiseStrength,
    noiseScale: noiseScale ?? this.noiseScale,
    space: space ?? this.space,
    maxParticles: maxParticles ?? this.maxParticles,
  );

  /// Which particles these are. Two emitters with one seed and the same
  /// settings give the same particles.
  final int seed;
  final EmitterShape shape;

  /// Metres.
  final double radius;

  /// Radians from straight up.
  final double angle;

  /// Half a side each way, in metres.
  final Vector3 extents;

  /// Particles born each second.
  final double rate;
  final List<EmitterBurst> bursts;

  /// Seconds after which no more are born, or null for no end.
  final double? duration;

  /// Seconds a particle lives, from [lifetimeMin] to [lifetimeMax].
  final double lifetimeMin;
  final double lifetimeMax;

  /// Metres per second at birth.
  final double speedMin;
  final double speedMax;

  /// Radians per second.
  final double spinMin;
  final double spinMax;

  /// Edge length in metres, spread evenly over a life.
  final List<double> size;

  /// Red, green, blue triples, spread evenly over a life.
  final List<double> colour;

  /// Metres per second squared.
  final Vector3 gravity;

  /// Per second, the share of speed lost to the air.
  final double drag;

  /// Metres of push about by smooth noise.
  final double noiseStrength;
  final double noiseScale;
  final EmitterSpace space;

  /// Particles alive at once. The oldest die first when more are due.
  final int maxParticles;

  @override
  String get type => SceneComponents.particles;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'seed': seed,
    'shape': shape.name,
    'radius': radius,
    'angle': angle,
    'extents': Values.vectorToJson(extents),
    'rate': rate,
    'bursts': [
      for (final burst in bursts) {'time': burst.time, 'count': burst.count},
    ],
    'duration': duration,
    'lifetime': [lifetimeMin, lifetimeMax],
    'speed': [speedMin, speedMax],
    'spin': [spinMin, spinMax],
    'size': size,
    'colour': colour,
    'gravity': Values.vectorToJson(gravity),
    'drag': drag,
    'noiseStrength': noiseStrength,
    'noiseScale': noiseScale,
    'space': space.name,
    'maxParticles': maxParticles,
  });
}

/// A number of nought or more, or [fallback]. With [above], above nought.
double _atLeast(
  Map<String, Object?> json,
  String key,
  double fallback, {
  bool above = false,
}) {
  final value = Values.number(json, key, fallback);
  final ok = value.isFinite && (above ? value > 0 : value >= 0);
  return ok ? value : fallback;
}

int _seed(Map<String, Object?> json) {
  final value = Values.number(json, 'seed', 0);
  return value.isFinite ? value.round() : 0;
}

Vector3 _gravity(Object? raw) {
  final read = Values.vector(raw);
  return read.storage.every((g) => g.isFinite) ? read : Vector3.zero();
}

double _spread(Map<String, Object?> json) {
  final value = Values.number(json, 'angle', 0.4);
  return value >= 0 && value <= 3.141592653589793 ? value : 0.4;
}

double? _duration(Map<String, Object?> json) {
  final value = Values.maybeNumber(json, 'duration');
  return value != null && value.isFinite && value >= 0 ? value : null;
}

int _cap(Map<String, Object?> json) {
  final value = Values.number(json, 'maxParticles', 1000);
  return value.isFinite && value >= 1 ? value.round() : 1000;
}

Vector3 _extents(Object? raw) {
  final read = Values.vector(raw, fallback: 0.5);
  final ok = read.storage.every((half) => half >= 0 && half.isFinite);
  return ok ? read : Vector3.all(0.5);
}

/// Two numbers that run from low to high, or the fallback pair. [accept] can
/// refuse a pair that is in order but still no good.
(double, double) _pair(
  Object? raw,
  double low,
  double high, [
  bool Function(double low, double high)? accept,
]) {
  final read = Values.numbers(raw);
  if (read.length != 2) return (low, high);
  final ok =
      read[0].isFinite &&
      read[1].isFinite &&
      read[0] <= read[1] &&
      (accept?.call(read[0], read[1]) ?? true);
  return ok ? (read[0], read[1]) : (low, high);
}

/// Stops of [stride] numbers each, or [fallback] when they do not make whole
/// stops, or when one is not finite.
List<double> _ramp(
  Object? raw,
  int stride,
  List<double> fallback, {
  bool nonNegative = false,
}) {
  final read = Values.numbers(raw);
  final ok =
      read.isNotEmpty &&
      read.length % stride == 0 &&
      read.every((n) => n.isFinite && (!nonNegative || n >= 0));
  return ok ? read : fallback;
}

List<EmitterBurst> _bursts(Object? raw) {
  if (raw is! List) return const [];
  final read = <EmitterBurst>[];
  for (final one in raw) {
    if (one is! Map<String, Object?>) continue;
    final time = Values.number(one, 'time', -1);
    final count = Values.number(one, 'count', 0);
    // A burst is dropped whole: counting its particles from nowhere would
    // invent a burst the file did not describe.
    if (time >= 0 && time.isFinite && count >= 1 && count <= 65535) {
      read.add(EmitterBurst(time, count.round()));
    }
  }
  return read;
}
