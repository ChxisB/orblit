import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import 'random.dart';

/// Where a particle starts and which way it first heads, in the emitter's own
/// space, with +Y up.
sealed class Shape {
  const Shape();

  /// Writes the start of particle [index] to [position] and the unit
  /// direction it heads in to [direction].
  void pick(int seed, int index, Vector3 position, Vector3 direction);
}

/// Every particle starts at the origin and heads off in a random direction.
final class PointShape extends Shape {
  const PointShape();

  @override
  void pick(int seed, int index, Vector3 position, Vector3 direction) {
    position.setZero();
    _anywhere(seed, index, direction);
  }
}

/// Starts anywhere inside a ball and heads away from its centre.
final class SphereShape extends Shape {
  SphereShape({this.radius = 1}) {
    _atLeastZero(radius, 'radius');
  }

  final double radius;

  @override
  void pick(int seed, int index, Vector3 position, Vector3 direction) {
    _anywhere(seed, index, direction);
    // The cube root makes the ball evenly filled and not crowded at the middle.
    final reach = radius * math.pow(unitAt(seed, index, 4), 1 / 3);
    position
      ..setFrom(direction)
      ..scale(reach.toDouble());
  }
}

/// Starts on a disc of [radius] in the XZ plane and heads up, spread by up to
/// [angle] radians from straight up.
final class ConeShape extends Shape {
  ConeShape({this.angle = 0.4, this.radius = 0}) {
    _atLeastZero(radius, 'radius');
    if (!(angle >= 0 && angle <= math.pi)) {
      throw ArgumentError.value(angle, 'angle', 'must be 0 to pi');
    }
  }

  final double angle;
  final double radius;

  @override
  void pick(int seed, int index, Vector3 position, Vector3 direction) {
    final across = radius * math.sqrt(unitAt(seed, index, 4));
    final around = 2 * math.pi * unitAt(seed, index, 5);
    position.setValues(across * math.cos(around), 0, across * math.sin(around));

    // Even over the cap of the sphere, so the middle of the cone is not denser.
    final up = 1 - unitAt(seed, index, 2) * (1 - math.cos(angle));
    final side = math.sqrt(math.max(0, 1 - up * up));
    final turn = 2 * math.pi * unitAt(seed, index, 3);
    direction.setValues(side * math.cos(turn), up, side * math.sin(turn));
  }
}

/// Starts anywhere inside a box of [halfExtents] and heads up.
final class BoxShape extends Shape {
  BoxShape(Vector3 halfExtents) : halfExtents = Vector3.copy(halfExtents) {
    for (final half in halfExtents.storage) {
      _atLeastZero(half, 'halfExtents');
    }
  }

  final Vector3 halfExtents;

  @override
  void pick(int seed, int index, Vector3 position, Vector3 direction) {
    position.setValues(
      (unitAt(seed, index, 2) * 2 - 1) * halfExtents.x,
      (unitAt(seed, index, 3) * 2 - 1) * halfExtents.y,
      (unitAt(seed, index, 4) * 2 - 1) * halfExtents.z,
    );
    direction.setValues(0, 1, 0);
  }
}

void _atLeastZero(double value, String name) {
  if (!(value >= 0 && value.isFinite)) {
    throw ArgumentError.value(value, name, 'must be 0 or more');
  }
}

/// A unit vector, evenly over the sphere.
void _anywhere(int seed, int index, Vector3 out) {
  final up = unitAt(seed, index, 2) * 2 - 1;
  final side = math.sqrt(1 - up * up);
  final turn = 2 * math.pi * unitAt(seed, index, 3);
  out.setValues(side * math.cos(turn), up, side * math.sin(turn));
}
