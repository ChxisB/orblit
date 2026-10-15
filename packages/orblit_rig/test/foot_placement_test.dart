import 'dart:math' as math;

import 'package:orblit_rig/orblit_rig.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A hip, a bent leg and a foot lying forwards, standing on y = 0.
///
/// The ankle is [ankle] high. A leg that is a hair short of straight leaves
/// the knee room to bend when the ground comes up and not to stretch when it
/// goes down, which is what the pelvis is for.
Armature body({double ankle = 0.1}) => Armature([
  Bone(name: 'hips', head: Vector3(0, 1, 0), tail: Vector3(0, 1.2, 0)),
  Bone(
    name: 'thigh',
    head: Vector3(0, 1, 0),
    tail: Vector3(0, 0.55 + ankle - 0.1, 0.1),
    parent: 'hips',
  ),
  Bone(
    name: 'shin',
    head: Vector3(0, 0.55 + ankle - 0.1, 0.1),
    tail: Vector3(0, ankle, 0),
    parent: 'thigh',
    connected: true,
  ),
  Bone(
    name: 'foot',
    head: Vector3(0, ankle, 0),
    tail: Vector3(0, ankle, 0.2),
    parent: 'shin',
    connected: true,
  ),
]);

const leg = FootLeg(
  thigh: 'thigh',
  shin: 'shin',
  foot: 'foot',
  ankleHeight: 0.1,
);

/// Ground that is flat at [height].
GroundProbe flat(double height) => (from, to) {
  if (from.y < height || to.y > height) return null;
  return GroundHit(
    point: Vector3(from.x, height, from.z),
    normal: Vector3(0, 1, 0),
  );
};

/// Ground that is a plane through y = [height] at the origin, tipped by
/// [angle] about X so it climbs along +Z.
GroundProbe sloped(double height, double angle) {
  final normal = Vector3(0, math.cos(angle), -math.sin(angle));
  return (from, to) {
    final y = height + from.z * math.tan(angle);
    if (from.y < y || to.y > y) return null;
    return GroundHit(point: Vector3(from.x, y, from.z), normal: normal);
  };
}

void place(
  Pose pose,
  FootPlacement modifier, {
  double dt = 0,
  Matrix4? toWorld,
}) {
  pose.evaluate();
  ModifierStack()
    ..add('feet', modifier)
    ..apply(
      pose,
      frame: ModifierFrame(dt: dt, modelToWorld: toWorld),
    );
}

FootPlacement snapped(GroundProbe probe, {String? pelvis, double? ease}) =>
    FootPlacement(
      legs: const [leg],
      probe: probe,
      pelvis: pelvis,
      ease: ease ?? 0,
    );

void main() {
  group('foot placement', () {
    test('leaves a foot alone on ground the animation was made for', () {
      final pose = Pose(body());
      place(pose, snapped(flat(0)));
      expect(pose['thigh'].isRest, isTrue);
      expect(pose['shin'].isRest, isTrue);
    });

    test('stands the ankle on higher ground', () {
      final pose = Pose(body());
      place(pose, snapped(flat(0.2)));
      expect(pose.tailOf('shin').y, closeTo(0.3, 1e-3));
    });

    test('keeps the ankle where it was across the ground', () {
      final pose = Pose(body());
      place(pose, snapped(flat(0.2)));
      expect(pose.tailOf('shin').x, closeTo(0, 1e-6));
      expect(pose.tailOf('shin').z, closeTo(0, 1e-3));
    });

    test('keeps the height the animation lifted the foot', () {
      // The ankle is 0.15 above where a planted one would be, and the ground
      // is 0.2 up. It stays 0.15 above that.
      final pose = Pose(body(ankle: 0.25));
      place(pose, snapped(flat(0.2)));
      expect(pose.tailOf('shin').y, closeTo(0.25 + 0.2, 1e-3));
    });

    test('follows the animation when the probe finds nothing', () {
      final pose = Pose(body());
      place(pose, snapped((_, _) => null));
      expect(pose['thigh'].isRest, isTrue);
    });

    test('lowers the pelvis so a leg can reach ground that falls away', () {
      final pose = Pose(body());
      place(pose, snapped(flat(-0.2), pelvis: 'hips'));
      expect(pose.tailOf('shin').y, closeTo(-0.1, 1e-3));
      expect(pose['hips'].location.y, closeTo(-0.2, 1e-6));
    });

    test('does not move the pelvis for ground that rises', () {
      final pose = Pose(body());
      place(pose, snapped(flat(0.2), pelvis: 'hips'));
      expect(pose['hips'].isRest, isTrue);
    });

    test('tilts a planted foot to the slope', () {
      final pose = Pose(body());
      final angle = 0.3;
      place(pose, snapped(sloped(0, angle)));

      final sole = TrackAxis.negativeZ.of(pose.worldOf('foot'));
      final normal = Vector3(0, math.cos(angle), -math.sin(angle));
      expect(math.acos(sole.dot(normal).clamp(-1.0, 1.0)), lessThan(1e-3));
    });

    test('tilts no further than the limit', () {
      final pose = Pose(body());
      final angle = 0.6;
      place(
        pose,
        FootPlacement(
          legs: const [leg],
          probe: sloped(0, angle),
          ease: 0,
          maxTilt: 0.2,
        ),
      );

      final sole = TrackAxis.negativeZ.of(pose.worldOf('foot'));
      final level = Vector3(0, 1, 0);
      expect(math.acos(sole.dot(level).clamp(-1.0, 1.0)), lessThan(0.25));
    });

    test('leaves a foot in the air level', () {
      // Lifted 0.3 above its own ground, well past the airborne limit.
      final pose = Pose(body(ankle: 0.4));
      place(pose, snapped(sloped(0, 0.3)));
      final sole = TrackAxis.negativeZ.of(pose.worldOf('foot'));
      expect(sole.dot(Vector3(0, 1, 0)), closeTo(1, 1e-3));
    });

    test('places a foot on the first frame even while easing', () {
      final pose = Pose(body());
      place(pose, snapped(flat(0.2), ease: 0.5), dt: 0.01);
      expect(pose.tailOf('shin').y, closeTo(0.3, 1e-3));
    });

    test('closes part of the gap in the time that passed', () {
      final pose = Pose(body());
      var ground = 0.0;
      final stack = ModifierStack()
        ..add(
          'feet',
          FootPlacement(
            legs: const [leg],
            probe: (from, to) => flat(ground)(from, to),
            ease: 0.1,
          ),
        );
      Pose step(double dt) {
        pose.evaluate();
        stack.apply(pose, frame: ModifierFrame(dt: dt));
        return pose;
      }

      step(1 / 60);
      ground = 0.2;
      step(0.1);
      final expected = 0.1 + 0.2 * (1 - math.exp(-1));
      expect(pose.tailOf('shin').y, closeTo(expected, 2e-3));
    });

    test('does not move in a frame of no time while easing', () {
      final pose = Pose(body());
      var ground = 0.0;
      final stack = ModifierStack()
        ..add(
          'feet',
          FootPlacement(
            legs: const [leg],
            probe: (from, to) => flat(ground)(from, to),
            ease: 0.1,
          ),
        );
      pose.evaluate();
      stack.apply(pose, frame: ModifierFrame(dt: 1 / 60));
      ground = 0.2;
      pose.evaluate();
      stack.apply(pose, frame: ModifierFrame(dt: 0));
      expect(pose.tailOf('shin').y, closeTo(0.1, 1e-6));
    });

    test('snaps again after a reset', () {
      final pose = Pose(body());
      double? ground = 0.2;
      final stack = ModifierStack()
        ..add(
          'feet',
          FootPlacement(
            legs: const [leg],
            probe: (from, to) => ground == null ? null : flat(ground)(from, to),
            ease: 0.1,
          ),
        );
      pose.evaluate();
      stack.apply(pose, frame: ModifierFrame(dt: 1 / 60));
      expect(pose.tailOf('shin').y, closeTo(0.3, 1e-3));

      // Without the reset the foot would ease back from 0.3 and still be up.
      stack.reset();
      ground = null;
      pose.reset();
      pose.evaluate();
      stack.apply(pose, frame: ModifierFrame(dt: 1 / 60));
      expect(pose.tailOf('shin').y, closeTo(0.1, 1e-6));
    });

    test('asks the probe in the world, not in the model', () {
      final pose = Pose(body());
      final asked = <Vector3>[];
      place(
        pose,
        snapped((from, to) {
          asked
            ..add(from)
            ..add(to);
          return null;
        }),
        toWorld: Matrix4.translationValues(10, 0, 0),
      );
      expect(asked[0].x, closeTo(10, 1e-9));
      expect(asked[0].y, closeTo(0.1 + 0.5, 1e-9));
      expect(asked[1].y, closeTo(0.1 - 0.5, 1e-9));
    });

    test('reads a hit in the world back into the model', () {
      final pose = Pose(body());
      place(
        pose,
        snapped(
          (from, to) => GroundHit(
            point: Vector3(from.x, 5.2, from.z),
            normal: Vector3(0, 1, 0),
          ),
        ),
        toWorld: Matrix4.translationValues(0, 5, 0),
      );
      expect(pose.tailOf('shin').y, closeTo(0.3, 1e-3));
    });

    test('refuses two bones that are not one leg', () {
      final pose = Pose(body())..evaluate();
      final stack = ModifierStack()
        ..add(
          'feet',
          FootPlacement(
            legs: const [FootLeg(thigh: 'hips', shin: 'shin')],
            probe: flat(0),
          ),
        );
      expect(() => stack.apply(pose), throwsA(isA<ArmatureError>()));
    });

    test('refuses a rig with no legs', () {
      expect(
        () => FootPlacement(legs: const [], probe: flat(0)),
        throwsArgumentError,
      );
    });
  });
}
