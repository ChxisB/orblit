import 'dart:math' as math;

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_rig/orblit_rig.dart';
import 'package:vector_math/vector_math_64.dart';

/// The name each of a skin's joints goes by as a bone, in the skin's order.
///
/// Usually its own. A file is free to leave a joint unnamed or to name two
/// joints alike, and an armature is not: bones are found by name, so two
/// answering to one would be a rig where posing one moves the other. An
/// unnamed joint is called `joint` and its position in the skin, which is the
/// number an [OrblitJointPose] reaches it by. A name already taken gets
/// `.001`, `.002` and so on, which is what Blender does to a duplicated bone
/// and so what an artist will recognise. The first joint with a name keeps it,
/// whatever comes after, so a renamed joint never takes a name from another.
///
/// Public because an armature made by hand has to use these names to reach a
/// joint the file named twice.
List<String> boneNamesOfSkin(OrblitSkinInfo skin) => _namesOf(skin.joints);

/// An armature for a model's skin, with one deforming bone per joint.
///
/// This is how a file's skeleton looks to `orblit_rig`: the same names, hung
/// the same way, with each bone's head where its joint stands at rest. A file
/// stores a joint as a frame, though, and a bone is two ends and a twist. So
/// the rest of each bone is worked out from the skeleton rather than read from
/// the file:
///
/// - **The tail** is the middle of the joint's children, which is where a limb
///   is going. A joint at the end of a chain has nothing to point at, so it
///   carries on the way it came, from its parent, by as far again. A fingertip
///   ends up as long as the last knuckle. A joint with neither points along
///   its own Y axis, a tenth of the skeleton's size long. No bone is left with
///   no length: a bone of no length has no direction, and anything that
///   measures a limb, such as a reach or a stretch, divides by it.
/// - **The roll** lays the bone's X axis as close to its joint's own X axis
///   as the bone's direction allows. A file exported from Blender keeps each
///   joint's frame the same as its bone's, so wherever a bone pointed at its
///   child it comes back exactly as it was authored. Where a joint's X runs
///   along the bone, there is nothing to lay, and the Z axes are matched
///   instead.
/// - **Names** come from [boneNamesOfSkin], so a file that names two joints
///   alike still makes an armature.
///
/// None of this has to agree with the joints for [OrblitSkinBinding] to be
/// right, because the binding takes up any difference between a bone's frame
/// and its joint's. It is a sensible default to author against, not a
/// requirement.
Armature armatureOfSkin(OrblitSkinInfo skin) {
  final shape = _SkinShape(skin);
  final count = shape.names.length;
  final heads = [for (final rest in shape.rest) rest.getTranslation()];

  final children = [for (var joint = 0; joint < count; joint++) <int>[]];
  for (var joint = 0; joint < count; joint++) {
    final parent = shape.parents[joint];
    if (parent >= 0) children[parent].add(joint);
  }

  // Measured against the skeleton's own size rather than in metres, so a
  // model in centimetres is judged the same as one in metres.
  final extent = _extentOf(heads);
  final negligible = math.max(extent * 1e-4, 1e-9);
  final fallback = extent > 1e-6 ? extent / 10 : 0.1;

  Vector3 tailOf(int joint) {
    final head = heads[joint];

    final ahead = [
      for (final child in children[joint])
        if ((heads[child] - head).length > negligible) heads[child],
    ];
    if (ahead.isNotEmpty) {
      final middle = Vector3.zero();
      for (final point in ahead) {
        middle.add(point);
      }
      middle.scale(1 / ahead.length);
      // Children spread evenly around a joint, like a pelvis between two
      // legs, can average out to the joint itself.
      if ((middle - head).length > negligible) return middle;
    }

    final parent = shape.parents[joint];
    if (parent >= 0) {
      final onward = head - heads[parent];
      if (onward.length > negligible) return head + onward;
    }

    final own = _axisOf(shape.rest[joint], 1);
    return own.length > 1e-12
        ? head + own.normalized() * fallback
        : head + Vector3(0, fallback, 0);
  }

  final tails = [for (var joint = 0; joint < count; joint++) tailOf(joint)];

  return Armature([
    for (final joint in shape.order)
      Bone(
        name: shape.names[joint],
        head: heads[joint],
        tail: tails[joint],
        roll: _rollFor(heads[joint], tails[joint], shape.rest[joint]),
        parent: switch (shape.parents[joint]) {
          final parent when parent >= 0 => shape.names[parent],
          _ => null,
        },
      ),
  ]);
}

/// An armature driving a model's skin.
///
/// This sits between a rig solved in Dart and a character drawn from a file.
/// A [Pose] says where bones are, and the renderer wants to know where joints
/// are, each relative to whatever it hangs from. This turns one into the
/// other.
///
/// Bones find joints by name, and nothing else about the armature has to
/// match the file. A bone may point another way from its joint, twist another
/// way, or sit somewhere else. What is kept from each pair is the offset
/// between the two at rest, and the joint is carried by its bone as if welded
/// to it at that offset. So an armature from [armatureOfSkin] works, and so
/// does one an artist built by hand. The one thing that has to match is the
/// armature's space, which must be the model's own: every rest is compared in
/// it, and a rig standing a metre to one side would turn the skin about points
/// a metre away from its joints.
///
/// The offsets are measured when the binding is made. An armature whose rest
/// is edited afterwards needs a new binding, in the same way a mesh has to be
/// bound again when its skeleton changes.
class OrblitSkinBinding {
  OrblitSkinBinding(
    Armature armature,
    OrblitSkinInfo skin, {
    required int index,
  }) : this._(armature, skin, index, _SkinShape(skin));

  OrblitSkinBinding._(this.armature, this.skin, this.index, _SkinShape shape)
    : bones = List.unmodifiable([
        for (final name in shape.names) armature.contains(name) ? name : null,
      ]),
      _parents = shape.parents,
      _order = shape.order,
      _rest = shape.rest,
      _local = shape.local,
      _offsets = [
        for (var joint = 0; joint < shape.names.length; joint++)
          armature.contains(shape.names[joint])
              ? Matrix4.inverted(
                  armature.restOf(shape.names[joint]),
                ).multiplied(shape.rest[joint])
              : null,
      ],
      _restFromParent = [
        for (var joint = 0; joint < shape.names.length; joint++)
          switch (shape.parents[joint]) {
            final parent when parent >= 0 => Matrix4.inverted(
              shape.rest[parent],
            ).multiplied(shape.rest[joint]),
            _ => shape.rest[joint],
          },
      ],
      _nodeFromParent = [
        for (var joint = 0; joint < shape.names.length; joint++)
          switch (shape.parents[joint]) {
            final parent when parent >= 0 =>
              shape.local[joint]
                  .multiplied(Matrix4.inverted(shape.rest[joint]))
                  .multiplied(shape.rest[parent]),
            _ => shape.local[joint].multiplied(
              Matrix4.inverted(shape.rest[joint]),
            ),
          },
      ];

  final Armature armature;
  final OrblitSkinInfo skin;

  /// Which skin this is, by its position in [OrblitAssetInfo.skins]. This is
  /// the number every [OrblitJointPose] from [jointsFor] carries.
  final int index;

  /// The bone each joint follows, in the skin's order, or null for a joint no
  /// bone is named after.
  ///
  /// Worth a look when an armature made by hand is first bound. A joint that
  /// no bone drives looks exactly like a joint that is meant to keep still,
  /// and a bone renamed on one side and not the other is otherwise found by
  /// watching an arm fail to move.
  final List<String?> bones;

  final List<int> _parents;
  final List<int> _order;
  final List<Matrix4> _rest;
  final List<Matrix4> _local;

  /// Where each joint sits in its bone's frame: `inverse(bone rest) * joint
  /// rest`, or null for a joint with no bone.
  final List<Matrix4?> _offsets;

  /// Where each joint rests in its parent joint's frame, or in the model's for
  /// a joint that hangs from no joint. This is what carries a joint no bone
  /// drives.
  final List<Matrix4> _restFromParent;

  /// What takes a world already brought into the parent joint's frame the rest
  /// of the way, into the joint's own parent node's frame.
  ///
  /// That node is not always the parent joint. The renderer names a joint's
  /// nearest ancestor that is a joint, and a file may put plain nodes between
  /// the two. Those nodes are nobody's to move, so they ride with the joint
  /// above them, and this is the constant step from it to them. For a joint
  /// that hangs from no joint, the parent node stays where the file put it,
  /// and this is the whole way there from the model's root.
  final List<Matrix4> _nodeFromParent;

  /// Every joint of the skin, placed as [pose] has its bones.
  ///
  /// [pose] has to be a pose of [armature], and it has to have been evaluated
  /// already. This does not evaluate it, because evaluating is not something
  /// to do twice. A pose writes what it solves for an inverse-kinematics chain
  /// back into itself, and a chain at part influence blends again on every
  /// evaluation, so a second one gives a different pose. Whoever evaluated it
  /// has already paid for it, too, to draw the bones or read a head.
  ///
  /// Every joint is returned, in the skin's order, including those still at
  /// rest. The renderer keeps a joint set by hand where it was set until the
  /// object stops being posed altogether. So a joint left out because it had
  /// gone back to rest would stay wherever it was last put: an elbow that
  /// never straightens. Telling which joints have moved would also cost as
  /// much as working them out. A joint no bone drives comes back exactly as
  /// the file has it, which, relative to its parent, keeps it where it rests.
  List<OrblitJointPose> jointsFor(Pose pose) {
    if (!identical(pose.armature, armature)) {
      throw ArgumentError.value(
        pose,
        'pose',
        'A pose of a different armature. The offsets were measured against '
            'the armature this binding was made with, and the bones of another '
            'one would carry the joints from somewhere they never rested.',
      );
    }

    final count = _order.length;
    final world = List<Matrix4?>.filled(count, null);
    for (final joint in _order) {
      final bone = bones[joint];
      final parent = _parents[joint];
      if (bone != null) {
        world[joint] = _posed(pose, bone).multiplied(_offsets[joint]!);
      } else if (parent >= 0) {
        world[joint] = world[parent]!.multiplied(_restFromParent[joint]);
      } else {
        world[joint] = _rest[joint];
      }
    }

    // A parent's inverse is shared by all its children, and a pelvis or a
    // hand has several.
    final undone = List<Matrix4?>.filled(count, null);

    Matrix4 localOf(int joint) {
      if (bones[joint] == null) return _local[joint].clone();
      final parent = _parents[joint];
      if (parent < 0) return _nodeFromParent[joint].multiplied(world[joint]!);
      final intoParent = undone[parent] ??= Matrix4.inverted(world[parent]!);
      return _nodeFromParent[joint]
          .multiplied(intoParent)
          .multiplied(world[joint]!);
    }

    return [
      for (var joint = 0; joint < count; joint++)
        OrblitJointPose(skin: index, joint: joint, transform: localOf(joint)),
    ];
  }

  /// Where a bone ended up, with a clearer error than the pose's own.
  ///
  /// A bone that is in the armature and missing from the pose's answers means
  /// the pose has never been evaluated. The pose itself would only report
  /// that it has no such bone, which sends whoever reads it off checking names
  /// that are fine.
  static Matrix4 _posed(Pose pose, String bone) {
    try {
      return pose.worldOf(bone);
    } on ArmatureError {
      throw ArmatureError(
        'Bone "$bone" has not been worked out in this pose. Call '
        'Pose.evaluate before asking where the joints are.',
      );
    }
  }
}

/// A skin as the binding reads it: every list as long as the joints, and a
/// tree whatever the description said.
///
/// A description from the renderer is already all of that. One built by hand,
/// or read from an older renderer that sent no rests, may not be. What it gets
/// is a skeleton in the wrong place, rather than an exception partway through
/// a frame or a walk up a loop that never ends.
class _SkinShape {
  _SkinShape._(this.names, this.parents, this.rest, this.local, this.order);

  factory _SkinShape(OrblitSkinInfo skin) {
    final count = skin.joints.length;

    final parents = [
      for (var joint = 0; joint < count; joint++)
        switch (joint < skin.parents.length ? skin.parents[joint] : -1) {
          final parent when parent >= 0 && parent < count && parent != joint =>
            parent,
          _ => -1,
        },
    ];
    _cutLoops(parents);

    final rest = [
      for (var joint = 0; joint < count; joint++)
        joint < skin.rest.length ? skin.rest[joint] : Matrix4.identity(),
    ];

    // With no local to go on, the likeliest shape is the plainest one: no
    // nodes between a joint and its parent, and a root hanging straight from
    // the model.
    final local = [
      for (var joint = 0; joint < count; joint++)
        if (joint < skin.local.length)
          skin.local[joint]
        else if (parents[joint] < 0)
          rest[joint]
        else
          Matrix4.inverted(rest[parents[joint]]).multiplied(rest[joint]),
    ];

    final order = <int>[];
    final placed = List<bool>.filled(count, false);
    void place(int joint) {
      if (placed[joint]) return;
      final parent = parents[joint];
      if (parent >= 0) place(parent);
      placed[joint] = true;
      order.add(joint);
    }

    for (var joint = 0; joint < count; joint++) {
      place(joint);
    }

    return _SkinShape._(_namesOf(skin.joints), parents, rest, local, order);
  }

  final List<String> names;

  /// Each joint's parent joint, or -1, with any loop cut.
  final List<int> parents;

  final List<Matrix4> rest;
  final List<Matrix4> local;

  /// Joint positions with every parent before its children. The skin's own
  /// order promises nothing of the kind.
  final List<int> order;

  /// Cuts every loop at the first joint in it, which then hangs from nothing.
  ///
  /// A joint that only leads into a loop is left alone. The loop is cut when
  /// its own first member is reached, and cutting the joint as well would
  /// detach a limb that was fine.
  static void _cutLoops(List<int> parents) {
    for (var joint = 0; joint < parents.length; joint++) {
      final walked = <int>{joint};
      for (var up = parents[joint]; up >= 0; up = parents[up]) {
        if (up == joint) {
          parents[joint] = -1;
          break;
        }
        if (!walked.add(up)) break;
      }
    }
  }
}

List<String> _namesOf(List<String> joints) {
  final taken = <String>{};
  final names = List<String?>.filled(joints.length, null);

  // Every name the file gives is claimed before anything is renamed, so a
  // renamed joint cannot take the name of one that comes after it.
  for (var joint = 0; joint < joints.length; joint++) {
    final name = joints[joint];
    if (name.isNotEmpty && taken.add(name)) names[joint] = name;
  }

  for (var joint = 0; joint < joints.length; joint++) {
    if (names[joint] != null) continue;
    final base = joints[joint].isEmpty ? 'joint $joint' : joints[joint];
    var name = base;
    for (var copy = 1; !taken.add(name); copy++) {
      name = '$base.${copy.toString().padLeft(3, '0')}';
    }
    names[joint] = name;
  }

  return [for (final name in names) name!];
}

/// The roll that lays a bone's X axis nearest its joint's.
///
/// Measured against the bone as the armature will build it with no roll,
/// rather than against a frame worked out here. That way the answer means the
/// same thing to [Bone.restMatrix] however it goes about building that frame.
double _rollFor(Vector3 head, Vector3 tail, Matrix4 rest) {
  final along = (tail - head).normalized();
  final unrolled = Bone(name: '', head: head, tail: tail).restMatrix;
  final x = _axisOf(unrolled, 0);
  final z = _axisOf(unrolled, 2);

  // Turning by an angle about the bone's own length takes X to
  // X cos(angle) - Z sin(angle), and Z to Z cos(angle) + X sin(angle).
  final jointX = _flattened(_axisOf(rest, 0), along);
  if (jointX != null) return math.atan2(-jointX.dot(z), jointX.dot(x));

  final jointZ = _flattened(_axisOf(rest, 2), along);
  if (jointZ != null) return math.atan2(jointZ.dot(x), jointZ.dot(z));

  return 0;
}

Vector3 _axisOf(Matrix4 matrix, int column) {
  final axis = matrix.getColumn(column);
  return Vector3(axis.x, axis.y, axis.z);
}

/// [axis], with the part of it that runs along [along] taken out, or null when
/// that leaves nothing to point with.
Vector3? _flattened(Vector3 axis, Vector3 along) {
  final length = axis.length;
  if (length < 1e-12) return null;
  final unit = axis / length;
  final flat = unit - along * unit.dot(along);
  return flat.length < 1e-6 ? null : flat;
}

/// The diagonal of the box around every point.
double _extentOf(List<Vector3> points) {
  if (points.isEmpty) return 0;
  final low = points.first.clone();
  final high = points.first.clone();
  for (final point in points) {
    Vector3.min(low, point, low);
    Vector3.max(high, point, high);
  }
  return (high - low).length;
}
