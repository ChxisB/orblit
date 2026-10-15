import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

/// Where a two-bone chain ends up reaching for something.
class IkSolution {
  const IkSolution({
    required this.joint,
    required this.end,
    required this.reachable,
    this.stretch = 1,
  });

  /// The elbow or knee.
  final Vector3 joint;

  /// The hand or foot. Equal to the target when it was reachable.
  final Vector3 end;

  /// False when the target was further than the limb is long, or so close the
  /// limb would have to fold through itself. The chain still points at it,
  /// straightened or folded as far as it goes — refusing to solve would leave
  /// the limb wherever it was, which looks broken rather than strained.
  final bool reachable;

  /// How much longer than its rest length the limb had to become, as a
  /// multiplier. One when it did not stretch.
  ///
  /// Reported rather than baked into the positions, because stretching is
  /// something the *bones* do: the caller has to scale them, or the mesh
  /// stays the length it was and tears away from the joint.
  final double stretch;
}

/// Places a two-bone chain so its end reaches a target.
///
/// Solved with the law of cosines rather than iterated. A limb is a triangle
/// with two known sides, so there is a closed-form answer, and an iterative
/// solver would spend a budget converging on something already known — and
/// converge differently on different frames, which is how IK gets its
/// reputation for jitter.
///
/// [pole] decides which way the joint bends. Two solutions satisfy any
/// reachable target, mirrored about the line from root to target, and the
/// pole picks one — it is the difference between a knee and a backwards knee.
/// [stretch] lets the limb grow rather than stopping short: at zero it stops
/// at full extension, at one it reaches anything. Partial values are the
/// useful ones — a little stretch hides the pop as a limb straightens without
/// making the character rubbery.
IkSolution solveTwoBoneIk({
  required Vector3 root,
  required Vector3 pole,
  required Vector3 target,
  required double upperLength,
  required double lowerLength,
  double stretch = 0,
}) {
  final toTarget = target - root;
  final reach = toTarget.length;

  if (reach < 1e-9 || upperLength <= 0 || lowerLength <= 0) {
    return IkSolution(
      joint: root + Vector3(0, upperLength, 0),
      end: root + Vector3(0, upperLength + lowerLength, 0),
      reachable: false,
    );
  }

  final direction = toTarget / reach;
  final maximum = upperLength + lowerLength;
  final minimum = (upperLength - lowerLength).abs();

  // Past full extension with stretch allowed, the limb is a straight line and
  // there is no triangle left to solve — it is one division, not a special
  // case of the law of cosines.
  if (stretch > 0 && reach > maximum) {
    final factor = 1 + (reach / maximum - 1) * stretch.clamp(0.0, 1.0);
    return IkSolution(
      joint: root + direction * (upperLength * factor),
      end: root + direction * (maximum * factor),
      // Only a limb allowed to stretch the whole way actually arrives.
      reachable: stretch >= 1,
      stretch: factor,
    );
  }

  // Kept just inside the limits: exactly straight or exactly folded makes the
  // bend plane undefined, and the joint flips between frames as rounding
  // pushes it either side.
  const margin = 1e-4;
  final clamped = reach.clamp(minimum + margin, maximum - margin);
  final reachable = reach > minimum + margin && reach < maximum - margin;

  // The angle at the root of the triangle whose sides are the two bones and
  // the distance to the target.
  final cosRoot =
      ((upperLength * upperLength +
                  clamped * clamped -
                  lowerLength * lowerLength) /
              (2 * upperLength * clamped))
          .clamp(-1.0, 1.0);
  final rootAngle = math.acos(cosRoot);

  // The plane the limb bends in, set by the pole.
  var bendAxis = direction.cross(pole - root);
  if (bendAxis.length2 < 1e-12) {
    // The pole is on the line to the target, so it names no plane. Any
    // perpendicular will do and at least keeps the limb from collapsing.
    bendAxis = direction.cross(Vector3(0, 0, 1));
    if (bendAxis.length2 < 1e-12) bendAxis = direction.cross(Vector3(1, 0, 0));
  }
  bendAxis.normalize();

  // Through the matrix: `Quaternion.rotated` turns by the inverse, which
  // would bend the joint away from the pole.
  final upperDirection = Quaternion.axisAngle(
    bendAxis,
    rootAngle,
  ).asRotationMatrix().transformed(direction).normalized();

  final joint = root + upperDirection * upperLength;

  // The end lands on the target when it was reachable, and on the straightened
  // or folded limit when it was not.
  final end = reachable
      ? target.clone()
      : joint + (target - joint).normalized() * lowerLength;

  return IkSolution(joint: joint, end: end, reachable: reachable);
}

/// The local rotation that points a bone's Y axis along [direction].
///
/// [world] is where the bone stands now and [base] is its parent's world
/// times its rest offset, which is what a pose rotation is measured from.
/// The shortest turn is used, so the bone keeps whatever roll it had. Null
/// when [direction] has no length to point along.
Quaternion? aimedRotation({
  required Matrix4 world,
  required Matrix4 base,
  required Vector3 direction,
}) {
  if (direction.length2 < 1e-12) return null;

  final column = world.getColumn(1);
  final currentAxis = Vector3(column.x, column.y, column.z)..normalize();

  final currentRotation = Quaternion.identity();
  world.decompose(Vector3.zero(), currentRotation, Vector3.zero());
  currentRotation.normalize();

  final correction = rotationBetween(currentAxis, direction.normalized());
  final wanted = correction * currentRotation;

  final baseRotation = Quaternion.identity();
  base.decompose(Vector3.zero(), baseRotation, Vector3.zero());
  baseRotation.normalize();

  // Converted back to the bone's own space, because that is where a pose
  // lives. A world rotation would be undone the moment the parent moved.
  return (baseRotation.conjugated() * wanted).normalized();
}

/// The scale that lengthens a bone by [factor] and keeps its volume.
///
/// The cross-section narrows as the inverse square root of the stretch,
/// which is what stops a stretched forearm from also getting fatter.
Vector3 stretchScale(double factor) {
  final cross = factor <= 0 ? 1.0 : 1 / math.sqrt(factor);
  return Vector3(cross, factor, cross);
}

/// [vector] turned by [rotation].
///
/// Through the matrix, because `Quaternion.rotated` turns by the inverse of
/// the rotation it is given.
Vector3 turned(Quaternion rotation, Vector3 vector) =>
    rotation.asRotationMatrix().transformed(vector);

/// How far [rotation] turns, from zero to pi.
double turnAngle(Quaternion rotation) =>
    2 * math.acos(rotation.w.abs().clamp(0.0, 1.0));

/// Spherical interpolation that always takes the shorter arc.
Quaternion slerpShortest(Quaternion a, Quaternion b, double t) {
  if (t <= 0) return a.clone();
  if (t >= 1) return b.clone();

  var dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
  var target = b;
  if (dot < 0) {
    target = Quaternion(-b.x, -b.y, -b.z, -b.w);
    dot = -dot;
  }

  if (dot > 0.9995) {
    return Quaternion(
      a.x + (target.x - a.x) * t,
      a.y + (target.y - a.y) * t,
      a.z + (target.z - a.z) * t,
      a.w + (target.w - a.w) * t,
    )..normalize();
  }

  final theta = math.acos(dot.clamp(-1.0, 1.0));
  final sinTheta = math.sin(theta);
  final from = math.sin((1 - t) * theta);
  final to = math.sin(t * theta);
  return Quaternion(
    (a.x * from + target.x * to) / sinTheta,
    (a.y * from + target.y * to) / sinTheta,
    (a.z * from + target.z * to) / sinTheta,
    (a.w * from + target.w * to) / sinTheta,
  )..normalize();
}

/// The rotation taking one direction to another by the shortest path.
Quaternion rotationBetween(Vector3 from, Vector3 to) {
  final a = from.normalized();
  final b = to.normalized();
  final dot = a.dot(b);

  if (dot > 0.999999) return Quaternion.identity();
  if (dot < -0.999999) {
    var axis = a.cross(Vector3(1, 0, 0));
    if (axis.length2 < 1e-6) axis = a.cross(Vector3(0, 0, 1));
    return Quaternion.axisAngle(axis.normalized(), math.pi);
  }

  final axis = a.cross(b);
  return Quaternion(axis.x, axis.y, axis.z, 1 + dot)..normalize();
}

/// Splits [rotation] into the turn about [axis] and the swing that is left.
///
/// The two multiply back to the rotation: `swing * twist`. [axis] has to be a
/// unit vector. A half turn that swings straight across the axis has no twist
/// to speak of, and comes back with an identity twist.
({Quaternion swing, Quaternion twist}) swingTwist(
  Quaternion rotation,
  Vector3 axis,
) {
  // Both signs of a quaternion are the same turn. Taking the one with a
  // positive w keeps the twist angle below a half turn either way round.
  final q = rotation.normalized();
  if (q.w < 0) q.setValues(-q.x, -q.y, -q.z, -q.w);

  final along = q.x * axis.x + q.y * axis.y + q.z * axis.z;
  final twist = Quaternion(axis.x * along, axis.y * along, axis.z * along, q.w);
  if (twist.length < 1e-9) return (swing: q, twist: Quaternion.identity());

  twist.normalize();
  return (swing: (q * twist.conjugated()).normalized(), twist: twist);
}

/// How far a twist from [swingTwist] turns about [axis], signed, in radians.
double twistAngle(Quaternion twist, Vector3 axis) {
  final along = twist.x * axis.x + twist.y * axis.y + twist.z * axis.z;
  return 2 * math.atan2(along, twist.w);
}

/// Moves a chain of [points] so its last point lands on [target].
///
/// Each point is pulled to where the one after it wants it and then pushed
/// back to where the one before it wants it, which is FABRIK. The gap between
/// two neighbours is kept at the length it began with, and the first point
/// does not move. A target out of reach straightens the chain towards it.
///
/// It reaches an answer by repetition, so [iterations] is a budget and not a
/// promise. Twelve is plenty for a spine or a tail. The result has the same
/// number of points as the input, which is left alone.
List<Vector3> solveFabrik({
  required List<Vector3> points,
  required Vector3 target,
  int iterations = 12,
  double tolerance = 1e-4,
}) {
  final result = [for (final point in points) point.clone()];
  if (result.length < 2) return result;

  final gaps = [
    for (var i = 0; i + 1 < result.length; i++)
      result[i].distanceTo(result[i + 1]),
  ];
  final root = result.first.clone();
  final reach = gaps.fold(0.0, (sum, gap) => sum + gap);

  if (root.distanceTo(target) >= reach) {
    final direction = target - root;
    if (direction.length2 < 1e-12) return result;
    direction.normalize();
    for (var i = 0; i < gaps.length; i++) {
      result[i + 1] = result[i] + direction * gaps[i];
    }
    return result;
  }

  for (var pass = 0; pass < iterations; pass++) {
    if (result.last.distanceTo(target) <= tolerance) break;
    result.last.setFrom(target);
    for (var i = gaps.length - 1; i >= 0; i--) {
      result[i] = _keepGap(result[i + 1], result[i], gaps[i]);
    }
    result.first.setFrom(root);
    for (var i = 0; i < gaps.length; i++) {
      result[i + 1] = _keepGap(result[i], result[i + 1], gaps[i]);
    }
  }
  return result;
}

/// [moving] pulled in or pushed out along its line from [anchor] until it is
/// [gap] away.
Vector3 _keepGap(Vector3 anchor, Vector3 moving, double gap) {
  final line = moving - anchor;
  // Two points on top of each other name no line. Straight up is as good as
  // any, and it keeps the chain from collapsing into a point.
  if (line.length2 < 1e-18) return anchor + Vector3(0, gap, 0);
  return anchor + line.normalized() * gap;
}
