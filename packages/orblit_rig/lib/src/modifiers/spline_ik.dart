import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import 'bone_edit.dart';
import 'chain_ik.dart';
import 'modifier.dart';

/// Lays a chain of bones along a curve, such as a tail along a path or a
/// spine along an arc.
///
/// The curve passes through every control point, as a Catmull-Rom spline. The
/// first bone's head is put on the start of the curve and each bone after it
/// reaches along the curve for a distance of its own length, so the chain
/// keeps its length and does not stretch. A chain longer than the curve
/// carries on straight from where the curve ended.
final class SplineIk extends Modifier {
  const SplineIk({
    required this.root,
    required this.tip,
    required this.points,
    this.detail = 12,
  });

  final String root;
  final String tip;

  /// The control points in the model's space, at least two. Null leaves the
  /// chain as it is.
  final List<Vector3>? Function(Pose pose, ModifierFrame frame) points;

  /// Straight pieces per span of the curve.
  final int detail;

  @override
  void check(Armature armature) {
    requireBone(armature, root, 'so the chain has no start');
    requireBone(armature, tip, 'so the chain has no end');
    if (bonePath(armature, root, tip) == null) {
      throw ArmatureError(
        'Bone "$tip" is not below "$root", so they are not '
        'one chain.',
      );
    }
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final controls = points(pose, frame);
    if (controls == null || controls.length < 2) return;
    final curve = sampleCurve(controls, detail);
    final path = bonePath(pose.armature, root, tip)!;

    final start = pose.worldOf(root).clone()..setTranslation(curve.first);
    placeBone(pose, root, start);

    final lengths = [
      for (var i = 0; i < path.length; i++)
        (i + 1 < path.length ? pose.headOf(path[i + 1]) : pose.tailOf(path[i]))
            .distanceTo(pose.headOf(path[i])),
    ];
    pointAlong(pose, path, walkCurve(curve, lengths));
  }
}

/// A Catmull-Rom curve through [controls] as a polyline, [detail] pieces to a
/// span.
List<Vector3> sampleCurve(List<Vector3> controls, int detail) {
  final steps = detail < 1 ? 1 : detail;
  final result = <Vector3>[controls.first.clone()];
  for (var i = 0; i + 1 < controls.length; i++) {
    final before = controls[i == 0 ? 0 : i - 1];
    final after = controls[i + 2 < controls.length ? i + 2 : i + 1];
    for (var step = 1; step <= steps; step++) {
      final t = step / steps;
      result.add(_catmullRom(before, controls[i], controls[i + 1], after, t));
    }
  }
  return result;
}

Vector3 _catmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, double t) {
  final t2 = t * t;
  final t3 = t2 * t;
  return (b * 2.0 +
          (c - a) * t +
          (a * 2.0 - b * 5.0 + c * 4.0 - d) * t2 +
          (b * 3.0 - a - c * 3.0 + d) * t3) *
      0.5;
}

/// The points reached by stepping along [curve] with a stride of each of
/// [lengths] in turn, a straight line away from the point before.
///
/// Starts at the curve's first point and so returns one more point than there
/// are lengths. Where the curve ends the walk goes on in the direction of its
/// last piece.
List<Vector3> walkCurve(List<Vector3> curve, List<double> lengths) {
  final result = <Vector3>[curve.first.clone()];
  var piece = 0;
  for (final length in lengths) {
    final from = result.last;
    var found = false;
    while (piece + 1 < curve.length) {
      final hit = _leaveSphere(from, length, curve[piece], curve[piece + 1]);
      if (hit != null) {
        result.add(hit);
        found = true;
        break;
      }
      piece++;
    }
    if (!found) result.add(from + _lastDirection(curve) * length);
  }
  return result;
}

/// Where the segment from [a] to [b] leaves the sphere of [radius] about
/// [centre], or null when it does not leave it.
///
/// The far crossing is the one wanted: the near one is behind the walker.
Vector3? _leaveSphere(Vector3 centre, double radius, Vector3 a, Vector3 b) {
  final along = b - a;
  final offset = a - centre;
  final qa = along.dot(along);
  final qb = 2 * offset.dot(along);
  final qc = offset.dot(offset) - radius * radius;
  final discriminant = qb * qb - 4 * qa * qc;
  if (qa < 1e-18 || discriminant < 0) return null;

  final t = (-qb + math.sqrt(discriminant)) / (2 * qa);
  if (t < 0 || t > 1) return null;
  return a + along * t;
}

Vector3 _lastDirection(List<Vector3> curve) {
  for (var i = curve.length - 1; i > 0; i--) {
    final line = curve[i] - curve[i - 1];
    if (line.length2 > 1e-18) return line.normalized();
  }
  return Vector3(0, 1, 0);
}
