import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../constraints.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';
import 'two_bone_ik.dart';

/// Where a probe found the ground, in the world.
final class GroundHit {
  GroundHit({required Vector3 point, required Vector3 normal})
    : point = point.clone(),
      normal = normal.clone();

  final Vector3 point;

  /// Which way the ground faces. Does not have to be a unit vector.
  final Vector3 normal;
}

/// Looks for ground along a line in the world, from [from] to [to].
///
/// The rig does not know what the ground is made of. A game answers this with
/// whatever it has: a physics ray cast, a heightfield, a flat plane. Return
/// null when nothing is in the way.
typedef GroundProbe = GroundHit? Function(Vector3 from, Vector3 to);

/// One leg that has to stand on the ground.
final class FootLeg {
  const FootLeg({
    required this.thigh,
    required this.shin,
    this.foot,
    this.ankleHeight = 0,
    this.soleAxis = TrackAxis.negativeZ,
  });

  /// The upper bone of the leg.
  final String thigh;

  /// The lower bone. Its tail is the ankle, which is what gets placed.
  final String shin;

  /// The foot bone, tilted to the slope. Null leaves the foot as it is.
  final String? foot;

  /// How far the ankle is above the sole, in the model's units.
  final double ankleHeight;

  /// Which of the foot bone's own axes points up out of the sole.
  ///
  /// A foot bone that lies along the ground with no roll has its Z axis
  /// pointing down, which is what the default of negative Z is for. A rig
  /// that was built differently needs to say so.
  final TrackAxis soleAxis;
}

/// Puts feet on the ground, whatever shape it is.
///
/// A walk cycle is made for flat ground. On a ramp one foot hangs in the air
/// and the other is inside the slope. For each leg this asks the [probe]
/// where the ground is under the animated ankle, moves the ankle to stand on
/// it with two-bone IK, and tilts the foot to the slope.
///
/// The height the animation lifts a foot above its own ground is kept, so a
/// stride still steps. Only the planted part of a stride is pinned to the
/// ground. The animation's own ground is the plane through the model's
/// origin, across [up].
///
/// When [pelvis] is given it is lowered by the most a foot had to go down, so
/// the leg on the low side can still reach.
///
/// The ankle moves towards its goal at [ease] seconds, so a step in the
/// ground is a short glide and not a pop. A frame of no time moves it
/// nowhere. Zero ease snaps. The first frame after the modifier is made or
/// reset snaps too, because there is no earlier foot to glide from, so a
/// character that appears on a slope is already standing on it.
final class FootPlacement extends Modifier {
  FootPlacement({
    required this.legs,
    required this.probe,
    this.pelvis,
    Vector3? up,
    this.rise = 0.5,
    this.depth = 0.5,
    this.ease = 0.1,
    this.maxTilt = math.pi / 4,
    this.airborne = 0.1,
  }) : up = (up ?? Vector3(0, 1, 0)).normalized(),
       _offsets = List.filled(legs.length, 0.0) {
    if (legs.isEmpty) {
      throw ArgumentError.value(legs, 'legs', 'Give at least one leg.');
    }
    if (this.up.length2 < 0.5) {
      throw ArgumentError.value(up, 'up', 'Up needs a direction.');
    }
    for (final value in [rise, depth, ease, maxTilt, airborne]) {
      if (!value.isFinite || value < 0) {
        throw ArgumentError.value(value, 'value', 'This is 0 or more.');
      }
    }
  }

  final List<FootLeg> legs;
  final GroundProbe probe;

  /// The hip bone. Null keeps it where the animation has it.
  final String? pelvis;

  /// Which way is up, in the model's space. Always a unit vector.
  final Vector3 up;

  /// How far above the ankle a probe starts, so a foot inside a slope still
  /// finds the surface it is inside.
  final double rise;

  /// How far below the ankle a probe looks. A drop further than this is not
  /// stood on: the foot follows the animation.
  final double depth;

  /// Seconds for the foot to close about two thirds of a gap.
  final double ease;

  /// The most the foot tilts to follow the ground, in radians.
  final double maxTilt;

  /// How far above its own ground the animation lifts a foot before it is
  /// taken to be in the air, and left level.
  final double airborne;

  final List<double> _offsets;
  double _hips = 0;
  bool _placed = false;

  @override
  void check(Armature armature) {
    for (final leg in legs) {
      requireBone(armature, leg.thigh, 'so there is no leg to place');
      requireBone(armature, leg.shin, 'so there is no leg to place');
      if (armature[leg.shin]!.parent != leg.thigh) {
        throw ArmatureError(
          'Bone "${leg.shin}" is not a child of "${leg.thigh}", so the two '
          'are not one leg.',
        );
      }
      final foot = leg.foot;
      if (foot != null) requireBone(armature, foot, 'so there is no foot');
    }
    final hips = pelvis;
    if (hips != null) requireBone(armature, hips, 'so there is no pelvis');
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final plans = [for (final leg in legs) _look(leg, pose, frame)];
    final share = ease <= 0 || !_placed ? 1.0 : 1 - math.exp(-frame.dt / ease);
    _placed = true;

    for (var i = 0; i < plans.length; i++) {
      _offsets[i] += (plans[i].offset - _offsets[i]) * share;
    }
    _lowerPelvis(pose, plans, share);

    for (var i = 0; i < legs.length; i++) {
      final leg = legs[i];
      // A leg with nothing to correct is left to the animation. Re-solving it
      // would only nudge a pose that was already right.
      if (_offsets[i].abs() < 1e-9) continue;
      reachTwoBones(
        pose,
        root: leg.thigh,
        mid: leg.shin,
        target: plans[i].ankle + up * _offsets[i],
      );
      _tiltFoot(pose, leg, plans[i]);
    }
  }

  @override
  void reset() {
    _offsets.fillRange(0, _offsets.length, 0);
    _hips = 0;
    _placed = false;
  }

  _Look _look(FootLeg leg, Pose pose, ModifierFrame frame) {
    final ankle = pose.tailOf(leg.shin);
    final found = probe(
      frame.toWorld(ankle + up * rise),
      frame.toWorld(ankle - up * depth),
    );
    if (found == null) return _Look(ankle: ankle);

    final normal = frame.directionToModel(found.normal).normalized();
    final ground = frame.toModel(found.point);

    // Where, straight above or below the ankle, the sole would touch the
    // ground. The sloped ground is a plane, and the ankle stands off it by
    // its own height along the normal.
    final tilt = math.max(normal.dot(up), 0.2);
    final toGround = (ankle - ground).dot(normal);
    final lift = math.max(0.0, ankle.dot(up) - leg.ankleHeight);
    final offset = (leg.ankleHeight - toGround) / tilt + lift;

    return _Look(ankle: ankle, offset: offset, normal: normal, lift: lift);
  }

  void _lowerPelvis(Pose pose, List<_Look> plans, double share) {
    final hips = pelvis;
    if (hips == null) return;

    final lowest = plans.fold(0.0, (low, plan) => math.min(low, plan.offset));
    _hips += (lowest - _hips) * share;
    if (_hips == 0) return;

    final world = pose.worldOf(hips).clone();
    world.setTranslation(world.getTranslation() + up * _hips);
    placeBone(pose, hips, world);
  }

  void _tiltFoot(Pose pose, FootLeg leg, _Look plan) {
    final foot = leg.foot;
    final normal = plan.normal;
    if (foot == null || normal == null || maxTilt <= 0) return;

    final planted = 1 - (plan.lift / math.max(airborne, 1e-9)).clamp(0.0, 1.0);
    if (planted <= 0) return;

    final sole = leg.soleAxis.of(pose.worldOf(foot));
    final full = rotationBetween(sole, normal);
    final angle = turnAngle(full);
    final allowed = math.min(angle, maxTilt) * planted;
    if (angle < 1e-9) return;

    turnBone(
      pose,
      foot,
      slerpShortest(Quaternion.identity(), full, allowed / angle),
    );
  }
}

/// What a probe found under one ankle.
final class _Look {
  _Look({required this.ankle, this.offset = 0, this.normal, this.lift = 0});

  final Vector3 ankle;

  /// How far the ankle has to move along up to stand on the ground.
  final double offset;

  /// The ground's normal in the model's space, or null when none was found.
  final Vector3? normal;

  /// How far the animation has lifted the foot above its own ground.
  final double lift;
}
