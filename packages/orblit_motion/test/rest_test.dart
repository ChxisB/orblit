import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'people.dart';

Matrix4 at(double x, double y, double z) =>
    Matrix4.compose(Vector3(x, y, z), Quaternion.identity(), Vector3.all(1));

void expectNear(Vector3 got, Vector3 want) {
  expect((got - want).length, lessThan(1e-9), reason: '$got is not $want');
}

void main() {
  group('posed origins', () {
    final arm = RestSkeleton(
      names: ['root', 'upper', 'lower'],
      parents: [-1, 0, 1],
      local: [at(0, 1, 0), at(1, 0, 0), at(1, 0, 0)],
    );

    test('are the rest origins when nothing is posed', () {
      final posed = arm.posedOrigins(const {});
      for (var bone = 0; bone < arm.length; bone++) {
        expectNear(posed[bone], arm.originOf(bone));
      }
    });

    test('carry a turn of a parent down to its children', () {
      // A quarter turn about Z lifts the arm from along +X to along +Y.
      final posed = arm.posedOrigins({
        'upper': BoneLocal(
          rotation: Quaternion.axisAngle(Vector3(0, 0, 1), math.pi / 2),
        ),
      });
      expectNear(posed[0], Vector3(0, 1, 0));
      expectNear(posed[1], Vector3(1, 1, 0));
      expectNear(posed[2], Vector3(1, 2, 0));
    });

    test('move a bone by its own position and scale its children away', () {
      final posed = arm.posedOrigins({
        'root': BoneLocal(position: Vector3(0, 0, 2), scale: Vector3.all(2)),
      });
      expectNear(posed[0], Vector3(0, 0, 2));
      expectNear(posed[1], Vector3(2, 0, 2));
      expectNear(posed[2], Vector3(4, 0, 2));
    });

    test('keep the rest for a part the pose leaves null', () {
      final posed = arm.posedOrigins({'lower': BoneLocal()});
      expectNear(posed[2], arm.originOf(2));
    });

    test('ignore a name the skeleton does not have', () {
      final posed = arm.posedOrigins({
        'tail': BoneLocal(position: Vector3(9, 9, 9)),
      });
      expectNear(posed[2], arm.originOf(2));
    });

    test('hang from whatever the roots hang from', () {
      final lifted = RestSkeleton(
        names: ['root', 'upper'],
        parents: [-1, 0],
        local: [at(0, 0, 0), at(1, 0, 0)],
        above: at(0, 5, 0),
      );
      final posed = lifted.posedOrigins({
        'root': BoneLocal(position: Vector3(0, 1, 0)),
      });
      expectNear(posed[0], Vector3(0, 6, 0));
      expectNear(posed[1], Vector3(1, 6, 0));
    });

    test('agree with forward kinematics worked out by hand on a person', () {
      final who = person(name: mannequin, armsDown: 0.6);
      final pose = {
        who.slots[HumanoidBone.hips]!: BoneLocal(
          position: Vector3(0, 0.6, 0.1),
        ),
        who.slots[HumanoidBone.spine]!: BoneLocal(
          rotation: Quaternion.axisAngle(Vector3(1, 0, 0), 0.3),
        ),
        who.slots[HumanoidBone.leftUpperArm]!: BoneLocal(
          rotation: Quaternion.axisAngle(Vector3(0, 0, 1), -0.8),
        ),
      };
      final posed = who.skeleton.posedOrigins(pose);
      for (final name in who.names) {
        expectNear(posed[who.indexOf(name)], who.originOf(name, pose));
      }
    });
  });
}
