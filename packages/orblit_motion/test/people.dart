import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

/// A person written out by hand, standing in Y up and facing +Z, so their
/// left is +X. The forward kinematics are here too, so a check never goes
/// through the code under test.
final class Person {
  Person._(this.names, this.parents, this.locals, this.slots);

  final List<String> names;
  final List<int> parents;
  final List<(Vector3, Quaternion)> locals;

  /// Which bone really is each part of the body, for a check to compare to.
  final Map<HumanoidBone, String> slots;

  RestSkeleton get skeleton => RestSkeleton(
    names: names,
    parents: parents,
    local: [
      for (final (position, turn) in locals)
        Matrix4.compose(position, turn, Vector3.all(1)),
    ],
  );

  int indexOf(String name) => names.indexOf(name);

  /// Every bone in the world, at rest or posed by [frame].
  List<Matrix4> world([Map<String, BoneLocal>? frame]) {
    final out = <Matrix4>[];
    for (var i = 0; i < names.length; i++) {
      final local = frame?[names[i]];
      final matrix = Matrix4.compose(
        local?.position ?? locals[i].$1,
        local?.rotation ?? locals[i].$2,
        local?.scale ?? Vector3.all(1),
      );
      out.add(parents[i] < 0 ? matrix : out[parents[i]].multiplied(matrix));
    }
    return out;
  }

  Vector3 originOf(String name, [Map<String, BoneLocal>? frame]) =>
      world(frame)[indexOf(name)].getTranslation();
}

/// Bone names as `bone_<index>`: nothing but the shape to go on.
String plainName(HumanoidBone slot, {bool end = false}) => '';

/// How a bone is called in a scheme, by the part of the body it is.
typedef Naming = String Function(HumanoidBone slot, {bool end});

/// Names as a game-engine mannequin writes them.
String mannequin(HumanoidBone slot, {bool end = false}) {
  final base = switch (slot) {
    HumanoidBone.hips => 'pelvis',
    HumanoidBone.spine => 'spine_01',
    HumanoidBone.chest => 'spine_02',
    HumanoidBone.upperChest => 'spine_03',
    HumanoidBone.neck => 'neck_01',
    HumanoidBone.head => 'head',
    HumanoidBone.leftShoulder => 'clavicle_l',
    HumanoidBone.leftUpperArm => 'upperarm_l',
    HumanoidBone.leftLowerArm => 'lowerarm_l',
    HumanoidBone.leftHand => 'hand_l',
    HumanoidBone.rightShoulder => 'clavicle_r',
    HumanoidBone.rightUpperArm => 'upperarm_r',
    HumanoidBone.rightLowerArm => 'lowerarm_r',
    HumanoidBone.rightHand => 'hand_r',
    HumanoidBone.leftUpperLeg => 'thigh_l',
    HumanoidBone.leftLowerLeg => 'calf_l',
    HumanoidBone.leftFoot => 'foot_l',
    HumanoidBone.leftToes => 'ball_l',
    HumanoidBone.rightUpperLeg => 'thigh_r',
    HumanoidBone.rightLowerLeg => 'calf_r',
    HumanoidBone.rightFoot => 'foot_r',
    HumanoidBone.rightToes => 'ball_r',
  };
  return end ? '${base}_end' : base;
}

/// Names as a motion-capture service writes them.
String capture(HumanoidBone slot, {bool end = false}) {
  final base = switch (slot) {
    HumanoidBone.hips => 'Hips',
    HumanoidBone.spine => 'Spine',
    HumanoidBone.chest => 'Spine1',
    HumanoidBone.upperChest => 'Spine2',
    HumanoidBone.neck => 'Neck',
    HumanoidBone.head => 'Head',
    HumanoidBone.leftShoulder => 'LeftShoulder',
    HumanoidBone.leftUpperArm => 'LeftArm',
    HumanoidBone.leftLowerArm => 'LeftForeArm',
    HumanoidBone.leftHand => 'LeftHand',
    HumanoidBone.rightShoulder => 'RightShoulder',
    HumanoidBone.rightUpperArm => 'RightArm',
    HumanoidBone.rightLowerArm => 'RightForeArm',
    HumanoidBone.rightHand => 'RightHand',
    HumanoidBone.leftUpperLeg => 'LeftUpLeg',
    HumanoidBone.leftLowerLeg => 'LeftLeg',
    HumanoidBone.leftFoot => 'LeftFoot',
    HumanoidBone.leftToes => 'LeftToeBase',
    HumanoidBone.rightUpperLeg => 'RightUpLeg',
    HumanoidBone.rightLowerLeg => 'RightLeg',
    HumanoidBone.rightFoot => 'RightFoot',
    HumanoidBone.rightToes => 'RightToeBase',
  };
  return end ? 'mixamorig:${base}_End' : 'mixamorig:$base';
}

/// A person [height] metres tall with their arms [armsDown] radians below
/// level, so zero is a T-pose and a bit under a right angle is an A.
///
/// [spine] is how many bones there are between hips and neck, from one to
/// three. [rightFirst] makes the right arm and leg before the left, so that
/// a bone's place in the list says nothing about its side.
Person person({
  Naming name = plainName,
  double height = 1.8,
  double armsDown = 0,
  int spine = 2,
  bool shoulders = true,
  bool rightFirst = false,
  bool endSites = false,
}) {
  final names = <String>[];
  final parents = <int>[];
  final locals = <(Vector3, Quaternion)>[];
  final slots = <HumanoidBone, String>{};

  int add(
    HumanoidBone slot,
    int parent,
    double x,
    double y,
    double z, {
    bool end = false,
    double turn = 0,
  }) {
    final index = names.length;
    final given = name(slot, end: end);
    final label = given.isEmpty ? 'bone_$index' : given;
    names.add(label);
    parents.add(parent);
    locals.add((
      Vector3(x, y, z) * height,
      Quaternion.axisAngle(Vector3(0, 0, 1), turn),
    ));
    if (!end) slots[slot] = label;
    return index;
  }

  final hips = add(HumanoidBone.hips, -1, 0, 0.55, 0);
  var top = hips;
  const spineSlots = [
    [HumanoidBone.spine],
    [HumanoidBone.spine, HumanoidBone.chest],
    [HumanoidBone.spine, HumanoidBone.chest, HumanoidBone.upperChest],
  ];
  for (final slot in spineSlots[spine - 1]) {
    top = add(slot, top, 0, 0.25 / spine, 0);
  }
  final neck = add(HumanoidBone.neck, top, 0, 0.07, 0);
  final head = add(HumanoidBone.head, neck, 0, 0.06, 0);
  if (endSites) add(HumanoidBone.head, head, 0, 0.07, 0, end: true);

  for (final left in rightFirst ? [false, true] : [true, false]) {
    final s = left ? 1.0 : -1.0;
    final arm = shoulders
        ? [
            left ? HumanoidBone.leftShoulder : HumanoidBone.rightShoulder,
            left ? HumanoidBone.leftUpperArm : HumanoidBone.rightUpperArm,
          ]
        : [left ? HumanoidBone.leftUpperArm : HumanoidBone.rightUpperArm];
    var at = top;
    if (shoulders) at = add(arm.first, at, s * 0.03, 0.05, 0);
    at = add(
      arm.last,
      at,
      s * (shoulders ? 0.07 : 0.1),
      shoulders ? 0 : 0.05,
      0,
      turn: -s * armsDown,
    );
    at = add(
      left ? HumanoidBone.leftLowerArm : HumanoidBone.rightLowerArm,
      at,
      s * 0.17,
      0,
      0,
    );
    at = add(
      left ? HumanoidBone.leftHand : HumanoidBone.rightHand,
      at,
      s * 0.14,
      0,
      0,
    );
    if (endSites) {
      add(
        left ? HumanoidBone.leftHand : HumanoidBone.rightHand,
        at,
        s * 0.05,
        0,
        0,
        end: true,
      );
    }

    var leg = add(
      left ? HumanoidBone.leftUpperLeg : HumanoidBone.rightUpperLeg,
      hips,
      s * 0.05,
      -0.03,
      0,
    );
    leg = add(
      left ? HumanoidBone.leftLowerLeg : HumanoidBone.rightLowerLeg,
      leg,
      0,
      -0.24,
      0,
    );
    leg = add(
      left ? HumanoidBone.leftFoot : HumanoidBone.rightFoot,
      leg,
      0,
      -0.24,
      0,
    );
    leg = add(
      left ? HumanoidBone.leftToes : HumanoidBone.rightToes,
      leg,
      0,
      -0.035,
      0.07,
    );
    if (endSites) {
      add(
        left ? HumanoidBone.leftToes : HumanoidBone.rightToes,
        leg,
        0,
        0,
        0.03,
        end: true,
      );
    }
  }
  return Person._(names, parents, locals, slots);
}

/// The angle between two rotations, in radians.
double turnBetween(Quaternion a, Quaternion b) {
  final along = (a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w).abs();
  return 2 * math.acos(along.clamp(0.0, 1.0));
}

ClipChannel<Quaternion> turns(String bone, List<(double, Quaternion)> keys) =>
    ClipChannel<Quaternion>(
      target: '',
      bone: bone,
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [for (final (at, turn) in keys) Key(at, turn, hold: Hold.linear)],
    );

ClipChannel<Vector3> moves(String bone, List<(double, Vector3)> keys) =>
    ClipChannel<Vector3>(
      target: '',
      bone: bone,
      property: 'position',
      kind: ChannelKind.vector,
      keys: [for (final (at, v) in keys) Key(at, v, hold: Hold.linear)],
    );

Quaternion about(double x, double y, double z, double degrees) =>
    Quaternion.axisAngle(Vector3(x, y, z).normalized(), radians(degrees));

/// Every bone of [skeleton] in the world with [frame] over its rest, written
/// out so a check never goes through the code under test.
List<Matrix4> posedWorld(RestSkeleton skeleton, Map<String, BoneLocal> frame) {
  final out = <Matrix4>[];
  for (var i = 0; i < skeleton.length; i++) {
    final local = frame[skeleton.names[i]];
    final matrix = Matrix4.compose(
      local?.position ?? skeleton.positionOf(i),
      local?.rotation ?? skeleton.rotationOf(i),
      local?.scale ?? skeleton.scaleOf(i),
    );
    final parent = skeleton.parents[i] < 0
        ? skeleton.parentWorldOf(i)
        : out[skeleton.parents[i]];
    out.add(parent.multiplied(matrix));
  }
  return out;
}
