import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_rig/orblit_rig.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

Matrix4 placed(Vector3 at, Vector3 axis, double angle) => Matrix4.compose(
  at,
  Quaternion.axisAngle(axis.normalized(), angle),
  Vector3.all(1),
);

void expectVector(Vector3 actual, Vector3 expected, {String? reason}) {
  final because = reason == null ? '' : ' — $reason';
  expect(actual.x, closeTo(expected.x, 1e-5), reason: 'x of $actual$because');
  expect(actual.y, closeTo(expected.y, 1e-5), reason: 'y of $actual$because');
  expect(actual.z, closeTo(expected.z, 1e-5), reason: 'z of $actual$because');
}

void expectMatrix(Matrix4 actual, Matrix4 expected, {String? reason}) {
  final because = reason == null ? '' : ' of $reason';
  for (var i = 0; i < 16; i++) {
    expect(
      actual.storage[i],
      closeTo(expected.storage[i], 1e-5),
      reason: 'element $i$because',
    );
  }
}

/// A small skeleton, turned every which way at rest, built node by node the
/// way the renderer's tree has it.
///
/// Built from the nodes up rather than from the answers down, so the test
/// knows where every joint is without asking the code under test. Nothing in
/// it lines up with anything else. The hips hang from a plain node that is
/// offset and turned. The neck hangs from the spine through a second plain
/// node, which is the case where a joint's parent joint is not its parent
/// node. Every joint's frame is rotated away from the direction its bone will
/// point. The skin lists its joints children first, which is allowed.
class Figure {
  static final holder = placed(Vector3(0.4, 0.9, -0.2), Vector3(0, 0, 1), 0.35);
  static final hipsLocal = placed(Vector3(0.05, 0.1, 0), Vector3(1, 1, 0), 0.6);
  static final spineLocal = placed(
    Vector3(0, 0.3, 0.02),
    Vector3(1, 0, 0.2),
    -0.5,
  );
  static final socket = placed(Vector3(0.01, 0.25, 0), Vector3(0, 1, 0), 0.8);
  static final neckLocal = placed(
    Vector3(0, 0.12, 0.03),
    Vector3(0.3, 0, 1),
    1.1,
  );
  static final tailLocal = placed(
    Vector3(0, -0.05, -0.25),
    Vector3(1, 0, 0),
    2.0,
  );

  static const neck = 0, hips = 1, tail = 2, spine = 3;

  /// Every joint's world, from each joint's local, the way the renderer
  /// composes its nodes.
  static List<Matrix4> worldsFrom(List<Matrix4> locals) {
    final hipsWorld = holder.multiplied(locals[hips]);
    final spineWorld = hipsWorld.multiplied(locals[spine]);
    return [
      spineWorld.multiplied(socket).multiplied(locals[neck]),
      hipsWorld,
      hipsWorld.multiplied(locals[tail]),
      spineWorld,
    ];
  }

  static final locals = [neckLocal, hipsLocal, tailLocal, spineLocal];

  static final skin = OrblitSkinInfo(
    name: 'figure',
    joints: const ['neck', 'hips', 'tail', 'spine'],
    parents: const [spine, -1, hips, hips],
    rest: worldsFrom(locals),
    local: locals,
  );

  static Vector3 headOf(int joint) => skin.rest[joint].getTranslation();

  /// Where each joint ends up, as the renderer would put it, given what a
  /// binding returned.
  static List<Matrix4> worldsOf(List<OrblitJointPose> joints) {
    expect([for (final joint in joints) joint.joint], [0, 1, 2, 3]);
    return worldsFrom([for (final joint in joints) joint.transform]);
  }
}

/// Turns a bone by [turn] as seen from outside the rig, about its own head.
///
/// A pose's rotation is in the bone's own frame, so one turn in the world is a
/// different pose rotation for two bones that rest differently. This is what
/// lets two armatures be given the same pose.
void turnInWorld(Pose pose, String bone, Quaternion turn) {
  pose.evaluate();
  final base = Quaternion.identity();
  pose.baseOf(bone).decompose(Vector3.zero(), base, Vector3.zero());
  pose[bone].rotation = base.conjugated() * turn * base;
}

/// The same four bones as [armatureOfSkin] makes, with the same heads, pointed
/// and twisted however an artist happened to leave them.
Armature handMade() => Armature([
  Bone(
    name: 'hips',
    head: Figure.headOf(Figure.hips),
    tail: Figure.headOf(Figure.hips) + Vector3(0, 0.2, 0),
    roll: 0.9,
  ),
  Bone(
    name: 'spine',
    head: Figure.headOf(Figure.spine),
    tail: Figure.headOf(Figure.spine) + Vector3(0.2, 0.1, 0),
    roll: -1.3,
    parent: 'hips',
  ),
  Bone(
    name: 'neck',
    head: Figure.headOf(Figure.neck),
    tail: Figure.headOf(Figure.neck) + Vector3(0, 0, 0.1),
    roll: 2.2,
    parent: 'spine',
  ),
  Bone(
    name: 'tail',
    head: Figure.headOf(Figure.tail),
    tail: Figure.headOf(Figure.tail) + Vector3(0, -0.1, -0.1),
    roll: 0.4,
    parent: 'hips',
  ),
]);

void main() {
  group('an armature made from a skin', () {
    test('has a bone for every joint, hung from the same parents', () {
      final armature = armatureOfSkin(Figure.skin);

      expect(armature.length, 4);
      expect(armature['hips']!.parent, isNull);
      expect(armature['spine']!.parent, 'hips');
      expect(armature['neck']!.parent, 'spine');
      expect(armature['tail']!.parent, 'hips');
      expect(armature.deformingBones, hasLength(4));
    });

    test('puts each bone\'s head where its joint rests', () {
      final armature = armatureOfSkin(Figure.skin);

      for (var joint = 0; joint < 4; joint++) {
        final name = Figure.skin.joints[joint];
        expectVector(armature[name]!.head, Figure.headOf(joint), reason: name);
      }
    });

    test('points a bone at its children, and gives every bone a length', () {
      final armature = armatureOfSkin(Figure.skin);

      expectVector(armature['spine']!.tail, Figure.headOf(Figure.neck));
      expectVector(
        armature['hips']!.tail,
        (Figure.headOf(Figure.spine) + Figure.headOf(Figure.tail)) / 2,
      );
      // The end of a chain carries on the way it came, by as far again.
      final neckHead = Figure.headOf(Figure.neck);
      expectVector(
        armature['neck']!.tail,
        neckHead + (neckHead - Figure.headOf(Figure.spine)),
      );
      for (final bone in armature.bones) {
        expect(bone.length, greaterThan(1e-3), reason: bone.name);
      }
    });

    test('rolls each bone so its X axis lies nearest its joint\'s', () {
      final armature = armatureOfSkin(Figure.skin);

      for (var joint = 0; joint < 4; joint++) {
        final bone = armature[Figure.skin.joints[joint]]!;
        final along = bone.direction;
        final column = Figure.skin.rest[joint].getColumn(0);
        final jointX = Vector3(column.x, column.y, column.z);
        // The nearest a bone's X can come is the joint's X with the part
        // along the bone taken out.
        final nearest = (jointX - along * jointX.dot(along)).normalized();
        final boneX = bone.restMatrix.getColumn(0);

        expectVector(
          Vector3(boneX.x, boneX.y, boneX.z),
          nearest,
          reason: bone.name,
        );
      }
    });

    test('where a joint\'s X runs along its bone, matches the Z axes '
        'instead', () {
      final turned = placed(Vector3.zero(), Vector3(1, 0, 0), 0.7);
      final skin = OrblitSkinInfo(
        name: 'along',
        joints: const ['root', 'end'],
        parents: const [-1, 0],
        rest: [turned, Matrix4.translationValues(1, 0, 0)],
      );

      final bone = armatureOfSkin(skin)['root']!;
      final boneZ = bone.restMatrix.getColumn(2);
      final jointZ = turned.getColumn(2);

      expectVector(bone.direction, Vector3(1, 0, 0));
      expectVector(
        Vector3(boneZ.x, boneZ.y, boneZ.z),
        Vector3(jointZ.x, jointZ.y, jointZ.z),
      );
    });

    test('a joint with no name, or a name already taken, still makes a '
        'bone', () {
      final skin = OrblitSkinInfo(
        name: 'careless',
        joints: const ['', 'arm', 'arm', 'arm.001'],
        parents: const [-1, 0, 1, 2],
        rest: [
          for (var joint = 0; joint < 4; joint++)
            Matrix4.translationValues(0, joint * 0.3, 0),
        ],
      );

      expect(boneNamesOfSkin(skin), ['joint 0', 'arm', 'arm.002', 'arm.001']);
      final armature = armatureOfSkin(skin);
      expect(armature.length, 4);
      expect(armature['arm.002']!.parent, 'arm');
      expect(
        OrblitSkinBinding(armature, skin, index: 0).bones,
        isNot(contains(null)),
      );
    });

    test('joints that all stand in one place still make bones with '
        'lengths', () {
      final skin = OrblitSkinInfo(
        name: 'heap',
        joints: const ['a', 'b', 'c'],
        parents: const [-1, 0, 0],
        rest: [Matrix4.identity(), Matrix4.identity(), Matrix4.identity()],
      );

      for (final bone in armatureOfSkin(skin).bones) {
        expect(bone.length, greaterThan(0), reason: bone.name);
      }
    });
  });

  group('a skin bound to an armature', () {
    /// Bound to the made armature without its tail bone, so one joint has no
    /// bone to follow.
    ({Pose pose, OrblitSkinBinding binding}) bound() {
      final armature = armatureOfSkin(Figure.skin)..remove('tail');
      return (
        pose: Pose(armature),
        binding: OrblitSkinBinding(armature, Figure.skin, index: 2),
      );
    }

    test('at rest, gives every joint back as the file has it', () {
      for (final armature in [armatureOfSkin(Figure.skin), handMade()]) {
        final pose = Pose(armature)..evaluate();
        final joints = OrblitSkinBinding(
          armature,
          Figure.skin,
          index: 2,
        ).jointsFor(pose);

        expect(joints, hasLength(4));
        for (final joint in joints) {
          expect(joint.skin, 2);
          expectMatrix(
            joint.transform,
            Figure.skin.local[joint.joint],
            reason: Figure.skin.joints[joint.joint],
          );
        }
      }
    });

    test('moves each posed joint to where the rig puts its bone\'s head', () {
      final (:pose, :binding) = bound();
      pose['hips']
        ..location = Vector3(0.1, -0.05, 0.2)
        ..rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 0.7);
      pose['spine'].rotation = Quaternion.axisAngle(
        Vector3(1, 0, 0.5).normalized(),
        -0.9,
      );
      pose.evaluate();

      final worlds = Figure.worldsOf(binding.jointsFor(pose));

      for (final (joint, bone) in [
        (Figure.hips, 'hips'),
        (Figure.spine, 'spine'),
        (Figure.neck, 'neck'),
      ]) {
        expectVector(
          worlds[joint].getTranslation(),
          pose.headOf(bone),
          reason: bone,
        );
        // Carried as if welded: the joint's frame keeps its rest offset from
        // its bone's.
        expectMatrix(
          Matrix4.inverted(pose.worldOf(bone)).multiplied(worlds[joint]),
          Matrix4.inverted(
            pose.armature.restOf(bone),
          ).multiplied(Figure.skin.rest[joint]),
          reason: bone,
        );
      }
      // Far enough that the test cannot pass by nothing having moved.
      expect(
        (worlds[Figure.neck].getTranslation() - Figure.headOf(Figure.neck))
            .length,
        greaterThan(0.1),
      );
    });

    test('carries a joint no bone is named after rigidly with its parent', () {
      final (:pose, :binding) = bound();
      expect(binding.bones, ['neck', 'hips', null, 'spine']);

      pose['hips'].rotation = Quaternion.axisAngle(Vector3(0, 1, 1), 1.2);
      pose.evaluate();
      final joints = binding.jointsFor(pose);
      final worlds = Figure.worldsOf(joints);

      expectMatrix(joints[Figure.tail].transform, Figure.tailLocal);
      expectMatrix(
        Matrix4.inverted(worlds[Figure.hips]).multiplied(worlds[Figure.tail]),
        Matrix4.inverted(
          Figure.skin.rest[Figure.hips],
        ).multiplied(Figure.skin.rest[Figure.tail]),
      );
      expect(
        (worlds[Figure.tail].getTranslation() - Figure.headOf(Figure.tail))
            .length,
        greaterThan(0.05),
      );
    });

    test('an armature made by hand, twisted differently, gives the same joints '
        'for the same turn', () {
      final made = armatureOfSkin(Figure.skin);
      final byHand = handMade();
      // Not the same armature under another name: every bone rests in a
      // different frame.
      for (final bone in made.bones) {
        final difference = made.restOf(bone.name) - byHand.restOf(bone.name);
        expect(
          difference.storage
              .map((value) => value.abs())
              .reduce((a, b) => a > b ? a : b),
          greaterThan(0.1),
          reason: bone.name,
        );
      }

      List<OrblitJointPose> posed(Armature armature) {
        final pose = Pose(armature);
        turnInWorld(
          pose,
          'hips',
          Quaternion.axisAngle(Vector3(0.2, 1, 0).normalized(), 0.8),
        );
        turnInWorld(
          pose,
          'spine',
          Quaternion.axisAngle(Vector3(1, 0, 0.3).normalized(), -0.6),
        );
        pose.evaluate();
        return OrblitSkinBinding(
          armature,
          Figure.skin,
          index: 0,
        ).jointsFor(pose);
      }

      final fromMade = posed(made);
      final fromHand = posed(byHand);
      final madeWorlds = Figure.worldsOf(fromMade);
      final handWorlds = Figure.worldsOf(fromHand);

      for (var joint = 0; joint < 4; joint++) {
        final name = Figure.skin.joints[joint];
        expectVector(
          handWorlds[joint].getTranslation(),
          madeWorlds[joint].getTranslation(),
          reason: name,
        );
        expectMatrix(
          fromHand[joint].transform,
          fromMade[joint].transform,
          reason: name,
        );
      }
      expect(
        (madeWorlds[Figure.neck].getTranslation() - Figure.headOf(Figure.neck))
            .length,
        greaterThan(0.1),
      );
    });

    test('refuses a pose of some other armature', () {
      final (:pose, :binding) = bound();
      final other = Pose(pose.armature.copy())..evaluate();

      expect(() => binding.jointsFor(other), throwsArgumentError);
    });

    test('says a pose has not been evaluated, rather than that a bone is '
        'missing', () {
      final (:pose, :binding) = bound();

      expect(
        () => binding.jointsFor(pose),
        throwsA(
          isA<ArmatureError>().having(
            (error) => error.message,
            'message',
            contains('Pose.evaluate'),
          ),
        ),
      );
    });
  });
}
