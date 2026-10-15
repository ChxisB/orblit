import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import 'body.dart';
import 'camera_state.dart';
import 'guides.dart';
import 'lens.dart';

/// How far a ball of [radius] gets going from [from] towards [to] before it
/// meets something solid, in metres, or null when the way is clear.
///
/// The arm owns this shape and a game fills it in. Over `orblit_physics` it
/// is a sphere cast: `world.cast(from: ..., direction: ..., distance: ...,
/// shape: Sphere(radius))?.distance`. Nothing here imports a physics engine,
/// so a wall in a test can be one comparison.
typedef ArmProbe = double? Function(Vector3 from, Vector3 to, double radius);

/// Holds another body's camera on the near side of whatever stands between it
/// and the target.
///
/// A third-person camera sits on an arm: a line from a point on the target out
/// to where the camera wants to be. When a wall crosses that line the camera
/// is behind it and shows the back of the wall. This body casts a ball along
/// the arm every frame and stops the camera where the ball stops, so it shows
/// the room the target is in.
///
/// Shortening is instant, because a camera through a wall is wrong for even
/// one frame. Lengthening is whatever [inner] does to catch up, so the camera
/// eases back out at the same pace it settles anywhere else.
final class ArmBody implements CameraBody {
  ArmBody({
    required this.inner,
    required this.probe,
    this.radius = 0.25,
    this.height = 1.5,
  });

  /// Where the camera wants to be when nothing is in the way.
  final CameraBody inner;

  final ArmProbe probe;

  /// How big a ball the camera is, in metres. Wide enough to clear the near
  /// plane, or a wall beside the camera cuts into the frame.
  double radius;

  /// How far above the target's position the arm starts. A person's head
  /// rather than their feet, so a low step does not count as a wall.
  double height;

  /// The framing is [inner]'s. The arm only decides how far out it reaches.
  @override
  CameraGuides? get guides => inner.guides;

  @override
  Vector3 solve(
    Vector3 current,
    CameraTarget? follow, {
    required Quaternion rotation,
    required Lens lens,
    required double aspect,
    required double delta,
  }) {
    final wanted = inner.solve(
      current,
      follow,
      rotation: rotation,
      lens: lens,
      aspect: aspect,
      delta: delta,
    );
    if (follow == null) return wanted;

    final pivot = follow.position + Vector3(0, height, 0);
    final arm = wanted - pivot;
    final length = arm.length;
    if (length < 1e-9) return wanted;

    final reached = probe(pivot, wanted, radius);
    if (reached == null || reached >= length) return wanted;
    return pivot + arm * (math.max(0, reached) / length);
  }
}
