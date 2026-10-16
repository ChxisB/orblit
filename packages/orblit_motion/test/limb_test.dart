import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

// A leg hanging from a hip, with the hip hung from something that is turned
// and scaled, as a rig out of another tool is.

Matrix4 at(double x, double y, double z) =>
    Matrix4.compose(Vector3(x, y, z), Quaternion.identity(), Vector3.all(1));

RestSkeleton leg({Matrix4? above}) => RestSkeleton(
  names: ['hips', 'thigh', 'shin', 'foot'],
  parents: [-1, 0, 1, 2],
  // Hips at the origin, then a thigh and a shin one long each, bent a little
  // forward at the knee so the leg has a side to bend on.
  local: [at(0, 0, 0), at(0, 0, 0), at(0, -1, 0.1), at(0, -1, -0.1)],
  above: above,
);

void expectNear(Vector3 got, Vector3 want, {double within = 1e-6}) {
  expect((got - want).length, lessThan(within), reason: '$got is not $want');
}

// What a clip would hold once the two turns are keyed: they replace the
// bones' rotations and leave the rest of each bone as it was.
Map<String, BoneLocal> withReach(
  Map<String, BoneLocal> pose,
  LimbReach reach,
) => {
  ...pose,
  'thigh': BoneLocal(
    position: pose['thigh']?.position,
    rotation: reach.root,
    scale: pose['thigh']?.scale,
  ),
  'shin': BoneLocal(
    position: pose['shin']?.position,
    rotation: reach.mid,
    scale: pose['shin']?.scale,
  ),
};

void main() {
  group('reachLimb', () {
    final skeleton = leg();

    test('puts the foot on a target the leg can reach', () {
      final target = Vector3(0.4, -1.3, 0.5);
      final reach = reachLimb(
        skeleton,
        const {},
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: target,
      );
      expect(reach.reachable, isTrue);

      final posed = skeleton.posedOrigins(withReach(const {}, reach));
      expectNear(posed[3], target);
      expectNear(reach.end, target);
    });

    test('keeps each bone as long as it was', () {
      final reach = reachLimb(
        skeleton,
        const {},
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: Vector3(-0.3, -1.1, 0.2),
      );
      final was = skeleton.posedOrigins(const {});
      final now = skeleton.posedOrigins(withReach(const {}, reach));
      expect((now[2] - now[1]).length, closeTo((was[2] - was[1]).length, 1e-9));
      expect((now[3] - now[2]).length, closeTo((was[3] - was[2]).length, 1e-9));
    });

    test('keeps the knee on the side it was on', () {
      final reach = reachLimb(
        skeleton,
        const {},
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: Vector3(0, -1.2, 0.6),
      );
      final posed = skeleton.posedOrigins(withReach(const {}, reach));
      expect(posed[2].z, greaterThan(0));
    });

    test('bends a straight leg the way the rest bends it', () {
      final straight = {
        'shin': BoneLocal(position: Vector3(0, -1, 0)),
        'foot': BoneLocal(position: Vector3(0, -1, 0)),
      };
      final reach = reachLimb(
        skeleton,
        straight,
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: Vector3(0, -1.5, 0.3),
      );
      final posed = skeleton.posedOrigins(withReach(straight, reach));
      expectNear(posed[3], Vector3(0, -1.5, 0.3));
      expect(posed[2].z, greaterThan(0.5));
    });

    test('bends towards a pole when given one', () {
      final reach = reachLimb(
        skeleton,
        const {},
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: Vector3(0, -1.2, 0.6),
        pole: Vector3(0, -0.5, -3),
      );
      final posed = skeleton.posedOrigins(withReach(const {}, reach));
      expect(posed[2].z, lessThan(0));
      expectNear(posed[3], Vector3(0, -1.2, 0.6));
    });

    test('stretches the limb straight at a target past its reach', () {
      final reach = reachLimb(
        skeleton,
        const {},
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: Vector3(0, -5, 0),
      );
      expect(reach.reachable, isFalse);
      final posed = skeleton.posedOrigins(withReach(const {}, reach));
      expect(posed[3].x, closeTo(0, 1e-3));
      expect(posed[3].y, lessThan(-1.9));
      expectNear(posed[3], reach.end, within: 1e-3);
    });

    test('works from the pose it is given, not the rest', () {
      final pose = {
        'hips': BoneLocal(
          position: Vector3(0.2, 0.1, 0),
          rotation: Quaternion.axisAngle(Vector3(0, 1, 0), 0.7),
        ),
        'thigh': BoneLocal(
          rotation: Quaternion.axisAngle(Vector3(1, 0, 0), -0.4),
        ),
      };
      final target = Vector3(0.5, -0.9, 0.4);
      final reach = reachLimb(
        skeleton,
        pose,
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: target,
      );
      final posed = skeleton.posedOrigins(withReach(pose, reach));
      expectNear(posed[3], target);
    });

    test('reaches through whatever the hips hang from', () {
      final turned = leg(
        above: Matrix4.compose(
          Vector3(2, 1, -3),
          Quaternion.axisAngle(Vector3(1, 0, 0), math.pi / 2),
          Vector3.all(0.5),
        ),
      );
      final target = turned.posedOrigins(const {})[0] + Vector3(0.2, 0, 0.3);
      final reach = reachLimb(
        turned,
        const {},
        root: 'thigh',
        mid: 'shin',
        tip: 'foot',
        target: target,
      );
      final posed = turned.posedOrigins(withReach(const {}, reach));
      expectNear(posed[3], target);
    });

    test('says what is wrong with a chain that is not one', () {
      expect(
        () => reachLimb(
          skeleton,
          const {},
          root: 'thigh',
          mid: 'foot',
          tip: 'shin',
          target: Vector3.zero(),
        ),
        throwsArgumentError,
      );
      expect(
        () => reachLimb(
          skeleton,
          const {},
          root: 'thigh',
          mid: 'shin',
          tip: 'toe',
          target: Vector3.zero(),
        ),
        throwsArgumentError,
      );
    });
  });
}
