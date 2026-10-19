import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import 'backend.dart';

/// Metres a second, in air.
const double speedOfSound = 343;

/// A position that knows how fast it is moving. Velocity comes from the last
/// two positions, because the physics engine is not always there and is not
/// the only thing that moves a camera.
base mixin Motion {
  Vector3 position = Vector3.zero();
  Vector3 velocity = Vector3.zero();

  /// Moves to [to] over [seconds]. A frame of no time gives no velocity.
  void moveTo(Vector3 to, double seconds) {
    velocity = seconds > 0 ? (to - position) / seconds : Vector3.zero();
    position = to.clone();
  }

  /// Jumps to [to]. A cut is not a speed, so Doppler hears none.
  void teleport(Vector3 to) {
    position = to.clone();
    velocity = Vector3.zero();
  }

  /// [Audio.advance] calls this after every frame. A velocity is good for the
  /// frame its move was made in, so something that is no longer moved is no
  /// longer heard moving.
  void settle() => velocity = Vector3.zero();
}

/// Where the sound is heard from. It faces down -Z with +Y up until told
/// otherwise.
final class Listener with Motion {
  Vector3 forward = Vector3(0, 0, -1);
  Vector3 up = Vector3(0, 1, 0);
}

enum FalloffCurve { linear, inverse }

/// How a sound fades with distance. Full gain up to [near], silent from
/// [far], and [curve] between. Both curves reach exactly 0 at [far], so a
/// backend can stop mixing a voice that is out of range.
final class Falloff {
  Falloff({this.near = 1, this.far = 50, this.curve = FalloffCurve.inverse}) {
    if (!(near > 0 && far > near && far.isFinite)) {
      throw ArgumentError('near must be above 0 and far above near, finite.');
    }
  }

  final double near;
  final double far;
  final FalloffCurve curve;

  double gainAt(double distance) {
    if (distance <= near) return 1;
    if (distance >= far) return 0;
    return switch (curve) {
      FalloffCurve.linear => (far - distance) / (far - near),
      FalloffCurve.inverse => (near / distance - near / far) / (1 - near / far),
    };
  }
}

/// A sound that is louder in front. Angles are radians either side of the
/// way the emitter faces. Full gain inside [inner], [outerGain] past [outer]
/// and a straight line between.
final class Cone {
  Cone({required this.inner, required this.outer, this.outerGain = 0}) {
    if (!(inner >= 0 && outer >= inner && outer <= math.pi)) {
      throw ArgumentError('Need 0 <= inner <= outer <= pi.');
    }
    if (!(outerGain >= 0 && outerGain <= 1)) {
      throw ArgumentError.value(outerGain, 'outerGain', 'must be from 0 to 1');
    }
  }

  final double inner;
  final double outer;
  final double outerGain;

  double gainAt(double angle) {
    if (angle <= inner) return 1;
    if (angle >= outer) return outerGain;
    return 1 + (outerGain - 1) * (angle - inner) / (outer - inner);
  }
}

/// A place a sound comes from. It owns how that sound is shaped by where the
/// [Listener] is.
final class Emitter with Motion {
  Emitter({
    required Vector3 position,
    Falloff? falloff,
    this.cone,
    Vector3? forward,
    this.doppler = 1,
  }) : falloff = falloff ?? Falloff(),
       forward = forward?.clone() ?? Vector3(0, 0, -1) {
    if (!doppler.isFinite || doppler < 0) {
      throw ArgumentError.value(doppler, 'doppler', 'must be zero or more');
    }
    this.position = position.clone();
  }

  final Falloff falloff;
  final Cone? cone;

  /// The way the emitter faces, for its [cone].
  Vector3 forward;

  /// How much of the real pitch shift is heard. 0 is none and 1 is real.
  final double doppler;

  /// How a listener hears this emitter: the gain from distance and cone, the
  /// pitch from Doppler and the pan from direction.
  Level heardBy(Listener listener) {
    final toEmitter = position - listener.position;
    final distance = toEmitter.length;
    final gain = falloff.gainAt(distance);
    // No direction when they coincide, so no cone, pan or Doppler either.
    if (distance == 0) return Level(gain: gain);
    final direction = toEmitter / distance;
    return Level(
      gain: gain * _coneGain(direction),
      pitch: _pitch(listener, direction),
      pan: _pan(listener, direction, distance),
    );
  }

  double _coneGain(Vector3 direction) {
    final shape = cone;
    if (shape == null) return 1;
    final facing = forward.normalized().dot(-direction);
    return shape.gainAt(math.acos(facing.clamp(-1.0, 1.0)));
  }

  /// A speed towards the other side is clamped to half the speed of sound,
  /// which keeps the ratio between 1/3 and 3 for any [doppler].
  double _pitch(Listener listener, Vector3 direction) {
    final limit = speedOfSound / 2 / doppler;
    final listenerToward = math.min(
      limit,
      math.max(-limit, listener.velocity.dot(direction)),
    );
    final emitterToward = math.min(
      limit,
      math.max(-limit, -velocity.dot(direction)),
    );
    return (speedOfSound + doppler * listenerToward) /
        (speedOfSound - doppler * emitterToward);
  }

  /// Inside [Falloff.near] the sound is taken to be at the listener, so the
  /// pan eases to the middle instead of swinging across as it passes through.
  double _pan(Listener listener, Vector3 direction, double distance) {
    final right = listener.forward.cross(listener.up).normalized();
    return direction.dot(right) * math.min(1.0, distance / falloff.near);
  }
}
