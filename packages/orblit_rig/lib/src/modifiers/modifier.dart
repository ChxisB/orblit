import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../ik.dart';

/// What a modifier is told about the frame it runs in.
///
/// A pose lives in the model's own space. Everything outside it, a target
/// the game holds or the ground the physics world has, lives in the world's.
/// The frame carries the matrix between them so a modifier never guesses.
final class ModifierFrame {
  /// A frame [dt] seconds long, for a model standing at [modelToWorld].
  ///
  /// Refuses a negative or non-finite [dt] and a matrix that squashes space
  /// flat, because nothing downstream could make sense of either.
  ModifierFrame({this.dt = 0, Matrix4? modelToWorld})
    : modelToWorld = modelToWorld?.clone() ?? Matrix4.identity(),
      worldToModel = _inverseOf(modelToWorld) {
    if (!dt.isFinite || dt < 0) {
      throw ArgumentError.value(
        dt,
        'dt',
        'A frame cannot last a negative or unbounded time.',
      );
    }
  }

  /// Seconds since the last frame. Zero for a pose that is solved once.
  ///
  /// A modifier that smooths or simulates moves nothing while this is zero,
  /// because no time has passed for it to move in.
  final double dt;

  final Matrix4 modelToWorld;
  final Matrix4 worldToModel;

  /// How much longer a length is in the world than in the model.
  double get scale {
    final column = modelToWorld.getColumn(0);
    return Vector3(column.x, column.y, column.z).length;
  }

  Vector3 toWorld(Vector3 point) => modelToWorld.transformed3(point);

  Vector3 toModel(Vector3 point) => worldToModel.transformed3(point);

  /// A direction or an offset, which a move does not change.
  Vector3 directionToWorld(Vector3 direction) =>
      modelToWorld.rotated3(direction.clone());

  Vector3 directionToModel(Vector3 direction) =>
      worldToModel.rotated3(direction.clone());

  static Matrix4 _inverseOf(Matrix4? matrix) {
    if (matrix == null) return Matrix4.identity();
    final inverse = Matrix4.zero();
    final determinant = inverse.copyInverse(matrix);
    if (determinant == 0 || !determinant.isFinite) {
      throw ArgumentError.value(
        matrix,
        'modelToWorld',
        'This matrix squashes space flat, so nothing can be taken back '
            'through it.',
      );
    }
    return inverse;
  }
}

/// A point in the model's space, or null when there is nothing to reach for.
///
/// A function and not a value, because a target is somewhere the game knows
/// this frame: another bone, the player, a sound. The frame turns a point in
/// the world into the model's space.
typedef ModelPoint = Vector3? Function(Pose pose, ModifierFrame frame);

/// One step of the stack: something that moves bones after playback.
///
/// A modifier writes the bones it wants moved, at full strength, and nothing
/// else. It never blends. The stack takes the pose from before the modifier
/// and the pose after it, and puts back the share its slot's influence says.
/// That is why a modifier has no influence of its own: a second blend inside
/// one would be multiplied by the first.
///
/// A modifier gives the same pose when it runs twice on the same input.
/// That is what lets it work on a bone the clip does not rewrite, where the
/// last frame's result is the input.
abstract class Modifier {
  const Modifier();

  /// Throws an [ArmatureError] if this modifier names a bone the armature
  /// does not have, or joins bones the armature does not join.
  ///
  /// Called before the first solve, so a typo is an error naming the bone
  /// and not a limb that quietly never moves.
  void check(Armature armature) {}

  /// Moves bones in [pose], which has been evaluated. Reads where bones are
  /// with [Pose.worldOf] and writes with the pose's transforms, calling
  /// [Pose.refresh] with `from` for the bones that hang below one it moved.
  void solve(Pose pose, ModifierFrame frame);

  /// Forgets whatever this modifier remembers between frames.
  ///
  /// Called by the stack when the modifier is switched off or its influence
  /// is zero, so a spring that was asleep starts from the animation and not
  /// from where it was ten seconds ago. Also the call for a teleport.
  void reset() {}
}

/// One place in a [ModifierStack]: a modifier and how much of it applies.
final class ModifierSlot {
  ModifierSlot._(
    this.name,
    this.modifier,
    double influence,
    this.influenceProperty,
  ) : _influence = influence.clamp(0.0, 1.0);

  final String name;
  final Modifier modifier;

  /// A pose property to take the influence from instead of [influence], so
  /// one number can be keyed, driven by a clip, or handed between two
  /// modifiers the way [BoneConstraint.influenceProperty] hands a limb
  /// between forward and inverse kinematics.
  final String? influenceProperty;

  double _influence;

  /// Switched off, the modifier is skipped and forgets what it remembered.
  bool enabled = true;

  /// How much of the modifier's result is kept, from zero to one.
  double get influence => _influence;

  set influence(double value) => _influence = value.clamp(0.0, 1.0);

  /// The influence to use this frame.
  double influenceIn(Pose pose) {
    final property = influenceProperty;
    final value = property == null
        ? _influence
        : pose.property(property) ?? _influence;
    return value.clamp(0.0, 1.0);
  }
}

/// The ordered list of modifiers that run between playback and skinning.
///
/// The order is the order they were added, and each one reads what the one
/// before it left. A foot is placed before the head is turned, so a head that
/// follows a target does so from where the hips ended up. Limits come after
/// what they limit. Springs come last, so they follow the final pose.
///
/// The stack does the blending. For a slot below full influence it keeps the
/// pose from before the modifier ran and mixes each bone's transform back
/// towards it: position and scale linearly, rotation by the shorter arc. A
/// modifier at influence one is not mixed at all.
///
/// ```dart
/// binding.poseFrom(frame.bones[''] ?? {}, pose);
/// pose.evaluate();
/// stack.apply(pose, frame: ModifierFrame(dt: dt));
/// final joints = binding.jointsFor(pose);
/// ```
///
/// The pose has to be evaluated first, and has to be a pose of bones the
/// clip wrote this frame. A bone the clip does not write keeps what the last
/// frame's modifiers left.
final class ModifierStack {
  ModifierStack();

  final List<ModifierSlot> _slots = [];
  final Map<String, PoseTransform> _before = {};
  Armature? _checked;

  /// The slots in the order they run.
  List<ModifierSlot> get slots => List.unmodifiable(_slots);

  int get length => _slots.length;

  /// The slot called [name], or null.
  ModifierSlot? operator [](String name) {
    for (final slot in _slots) {
      if (slot.name == name) return slot;
    }
    return null;
  }

  /// Puts [modifier] after every modifier already in the stack.
  ///
  /// [name] is how a slot is found again, so it has to be unique. [influence]
  /// is the share of its result that is kept, and [influenceProperty] names a
  /// pose property to read it from instead.
  ModifierSlot add(
    String name,
    Modifier modifier, {
    double influence = 1,
    String? influenceProperty,
  }) {
    if (this[name] != null) {
      throw ArgumentError.value(
        name,
        'name',
        'The stack already has a modifier called "$name".',
      );
    }
    final slot = ModifierSlot._(name, modifier, influence, influenceProperty);
    _slots.add(slot);
    _checked = null;
    return slot;
  }

  /// Takes the slot called [name] out. Returns whether there was one.
  bool remove(String name) {
    final slot = this[name];
    if (slot == null) return false;
    slot.modifier.reset();
    _slots.remove(slot);
    return true;
  }

  /// Clears what every modifier remembers.
  void reset() {
    for (final slot in _slots) {
      slot.modifier.reset();
    }
  }

  /// Runs every modifier on [pose], in order.
  ///
  /// [pose] has to have been evaluated. When this returns it is up to date,
  /// so [Pose.worldOf] and the skinning matrices read the modified bones.
  /// Evaluating again would solve every inverse-kinematics chain a second
  /// time, which is why this does not do it for you.
  void apply(Pose pose, {ModifierFrame? frame}) {
    final used = frame ?? ModifierFrame();
    _checkAll(pose.armature);

    for (final slot in _slots) {
      final amount = slot.enabled ? slot.influenceIn(pose) : 0.0;
      if (amount <= 0) {
        slot.modifier.reset();
        continue;
      }
      if (amount >= 1) {
        slot.modifier.solve(pose, used);
        pose.refresh();
        continue;
      }
      _keepPoseBefore(pose);
      slot.modifier.solve(pose, used);
      _mixBack(pose, amount);
      pose.refresh();
    }
  }

  void _checkAll(Armature armature) {
    if (identical(_checked, armature)) return;
    for (final slot in _slots) {
      slot.modifier.check(armature);
    }
    _checked = armature;
  }

  void _keepPoseBefore(Pose pose) {
    for (final bone in pose.armature.bones) {
      final keep = _before[bone.name] ??= PoseTransform();
      final now = pose[bone.name];
      keep.location.setFrom(now.location);
      keep.rotation.setFrom(now.rotation);
      keep.scale.setFrom(now.scale);
    }
  }

  void _mixBack(Pose pose, double amount) {
    for (final bone in pose.armature.bones) {
      final before = _before[bone.name]!;
      final now = pose[bone.name];
      now.location.setFrom(
        before.location + (now.location - before.location) * amount,
      );
      now.rotation = slerpShortest(before.rotation, now.rotation, amount);
      now.scale.setFrom(before.scale + (now.scale - before.scale) * amount);
    }
  }
}
