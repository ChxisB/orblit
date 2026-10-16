import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// A shape a spring chain may not pass through.
///
/// A collider is in the space of [bone], or in the model's space when [bone]
/// is null, so a sphere on the head moves with the head.
sealed class SpringCollider {
  SpringCollider({this.bone});

  final String? bone;
}

/// A solid ball.
final class SphereCollider extends SpringCollider {
  SphereCollider({super.bone, required this.centre, required this.radius}) {
    _requireSize(radius);
  }

  final Vector3 centre;
  final double radius;
}

/// A solid rod with rounded ends, such as a limb or a torso.
final class CapsuleCollider extends SpringCollider {
  CapsuleCollider({
    super.bone,
    required this.start,
    required this.end,
    required this.radius,
  }) {
    _requireSize(radius);
  }

  final Vector3 start;
  final Vector3 end;
  final double radius;
}

/// A wall that stops everything on the far side of it, such as a floor.
final class PlaneCollider extends SpringCollider {
  PlaneCollider({super.bone, required this.point, required Vector3 normal})
    : normal = normal.normalized() {
    if (this.normal.length2 < 0.5) {
      throw ArgumentError.value(normal, 'normal', 'A plane needs a normal.');
    }
  }

  final Vector3 point;

  /// The side things are kept on. Always a unit vector.
  final Vector3 normal;
}

void _requireSize(double radius) {
  if (!radius.isFinite || radius < 0) {
    throw ArgumentError.value(radius, 'radius', 'A radius is 0 or more.');
  }
}

/// Makes a chain of bones swing, such as hair, a tail, a cloak or a tassel.
///
/// Each bone's tail is a particle. It is pulled back to where the animation
/// has it by [stiffness], slowed by [drag], pulled down by [gravity], kept
/// the length of its bone from the bone's head, and kept out of the
/// [colliders]. The bone is then aimed at the particle.
///
/// A tail that a collider stops slides along the collider's surface at the
/// bone's length, so a chain that hangs onto a floor fans out across it and
/// does not push through.
///
/// It runs in the world, so a character that runs and stops leaves its hair
/// swinging on, which a simulation in the model's space cannot do. Colliders
/// are given in the model's space and carried into the world by the frame.
///
/// List [bones] from the root of the chain outwards. The first frame passes
/// as animated, and a frame of no time holds the bones where they were. After
/// moving the model in one jump, reset the stack: a head that moves further
/// than [snapDistance] in a frame is taken to have done so, and the springs
/// start again from the animation.
final class SpringBones extends Modifier {
  SpringBones({
    required this.bones,
    this.stiffness = 80,
    this.drag = 6,
    Vector3? gravity,
    this.margin = 0,
    this.colliders = const [],
    this.snapDistance = 2,
  }) : gravity = gravity?.clone() ?? Vector3(0, -9.81, 0) {
    if (bones.isEmpty) {
      throw ArgumentError.value(bones, 'bones', 'Give at least one bone.');
    }
    for (final value in [stiffness, drag, margin, snapDistance]) {
      if (!value.isFinite || value < 0) {
        throw ArgumentError.value(value, 'value', 'This is 0 or more.');
      }
    }
  }

  /// From the root of the chain outwards.
  final List<String> bones;

  /// How hard a tail is pulled back to the animated pose, in 1/s². Zero lets
  /// it hang free of the animation.
  final double stiffness;

  /// How quickly a tail loses speed, in 1/s.
  final double drag;

  /// Acceleration in the world, in units a second squared.
  final Vector3 gravity;

  /// How far a tail keeps from a collider, so hair rests on a shoulder and
  /// does not sink into it.
  final double margin;

  final List<SpringCollider> colliders;

  /// A head that moves further than this in one frame has been teleported.
  final double snapDistance;

  static const _step = 1 / 60;
  static const _most = 8;

  /// Rounds of keeping out of the colliders and back to length per step. One
  /// collider needs one. Several that squeeze a tail need a few.
  static const _relax = 3;

  final Map<String, _Tail> _tails = {};

  @override
  void check(Armature armature) {
    for (var i = 0; i < bones.length; i++) {
      requireBone(armature, bones[i], 'so there is nothing to swing');
      final parent = armature[bones[i]]!.parent;
      if (bones.indexOf(parent ?? '') > i) {
        throw ArmatureError(
          'Bone "${bones[i]}" is listed before its parent "$parent", so it '
          'would be simulated from last frame.',
        );
      }
    }
    for (final collider in colliders) {
      final bone = collider.bone;
      if (bone != null) {
        requireBone(armature, bone, 'so a collider has nothing to follow');
      }
    }
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final count = (frame.dt / _step).ceil().clamp(1, _most);
    final solids = [for (final c in colliders) _solidOf(c, pose, frame)];
    for (final name in bones) {
      final head = frame.toWorld(pose.headOf(name));
      final animated = frame.toWorld(pose.tailOf(name));
      final tail = _tails[name];

      if (tail == null || tail.head.distanceTo(head) > snapDistance) {
        _tails[name] = _Tail(animated, head);
        continue;
      }

      final pull = _Pull(head: head, animated: animated, solids: solids);
      final step = frame.dt / count;
      for (var i = 0; i < count && frame.dt > 0; i++) {
        _advance(tail, pull, step);
      }
      tail.head.setFrom(head);
      aimBone(pose, name, frame.toModel(tail.position) - pose.headOf(name));
    }
  }

  @override
  void reset() => _tails.clear();

  void _advance(_Tail tail, _Pull pull, double step) {
    final start = tail.position.clone();

    tail.velocity
      ..addScaled(pull.animated - tail.position, stiffness * step)
      ..addScaled(gravity, step)
      ..scale(1 / (1 + drag * step));

    var next = _onSphere(pull.head, start + tail.velocity * step, pull.length);
    for (var i = 0; i < _relax && pull.solids.isNotEmpty; i++) {
      for (final solid in pull.solids) {
        next = _outAtLength(solid, pull.head, next, pull.length);
      }
    }

    tail.position.setFrom(next);
    tail.velocity.setFrom((next - start) / step);
  }

  _Solid _solidOf(SpringCollider collider, Pose pose, ModifierFrame frame) {
    final bone = collider.bone;
    final space = bone == null
        ? frame.modelToWorld
        : frame.modelToWorld.multiplied(pose.worldOf(bone));
    final column = space.getColumn(0);
    final scale = Vector3(column.x, column.y, column.z).length;

    switch (collider) {
      case SphereCollider(:final centre, :final radius):
        return _Ball(space.transformed3(centre), radius * scale + margin);
      case CapsuleCollider(:final start, :final end, :final radius):
        return _Rod(
          space.transformed3(start),
          space.transformed3(end),
          radius * scale + margin,
        );
      case PlaneCollider(point: final origin, :final normal):
        final facing = space.rotated3(normal.clone()).normalized();
        return _Wall(space.transformed3(origin) + facing * margin, facing);
    }
  }
}

/// A tail's particle in the world.
final class _Tail {
  _Tail(Vector3 position, Vector3 head)
    : position = position.clone(),
      head = head.clone(),
      velocity = Vector3.zero();

  final Vector3 position;
  final Vector3 velocity;

  /// Where the bone's head was last frame, to tell a teleport from a walk.
  final Vector3 head;
}

/// What holds a tail for one frame: its head, the pose's own tail, and the
/// things it may not enter.
final class _Pull {
  _Pull({required this.head, required this.animated, required this.solids})
    : length = animated.distanceTo(head);

  final Vector3 head;
  final Vector3 animated;
  final double length;
  final List<_Solid> solids;
}

/// A collider in the world, already grown by the margin.
sealed class _Solid {}

final class _Ball extends _Solid {
  _Ball(this.centre, this.radius);

  final Vector3 centre;
  final double radius;
}

final class _Rod extends _Solid {
  _Rod(this.start, this.end, this.radius);

  final Vector3 start;
  final Vector3 end;
  final double radius;
}

final class _Wall extends _Solid {
  _Wall(this.point, this.normal);

  final Vector3 point;
  final Vector3 normal;
}

/// [point] brought to [length] from [anchor] along its own line.
Vector3 _onSphere(Vector3 anchor, Vector3 point, double length) {
  final line = point - anchor;
  if (line.length2 < 1e-18) return anchor + Vector3(0, -length, 0);
  return anchor + line.normalized() * length;
}

/// [point], which is [length] from [head], moved out of [solid] along the
/// sphere of that radius around [head], to the nearest place that is clear.
///
/// Pushing a point out and pulling it back to length undoes itself when the
/// push is along the bone. Walking the sphere's own surface does not.
Vector3 _outAtLength(_Solid solid, Vector3 head, Vector3 point, double length) {
  switch (solid) {
    case _Ball(:final centre, :final radius):
      return _aroundBall(head, point, length, centre, radius);
    case _Rod(:final start, :final end, :final radius):
      final centre = _nearestOnSegment(start, end, point);
      return _aroundBall(head, point, length, centre, radius);
    case _Wall(point: final origin, :final normal):
      return _abovePlane(head, point, length, origin, normal);
  }
}

Vector3 _aroundBall(
  Vector3 head,
  Vector3 point,
  double length,
  Vector3 centre,
  double radius,
) {
  if (point.distanceTo(centre) >= radius) return point;

  // The sphere of the bone's reach and the ball meet in a circle. With no
  // circle the head is inside the ball or the ball is out of reach, and there
  // is no way round to find.
  final toCentre = centre - head;
  final apart = toCentre.length;
  final nothing =
      apart < 1e-9 ||
      apart > length + radius ||
      apart < (length - radius).abs();
  if (nothing) return point;

  final axis = toCentre / apart;
  final along =
      (length * length - radius * radius + apart * apart) / (2 * apart);
  final middle = head + axis * along;
  final ring = math.sqrt(math.max(0.0, length * length - along * along));
  return middle + _sideways(point - middle, axis) * ring;
}

Vector3 _abovePlane(
  Vector3 head,
  Vector3 point,
  double length,
  Vector3 origin,
  Vector3 normal,
) {
  if ((point - origin).dot(normal) >= 0) return point;

  final height = (head - origin).dot(normal);
  if (height.abs() >= length) return point;

  final middle = head - normal * height;
  final ring = math.sqrt(length * length - height * height);
  return middle + _sideways(point - middle, normal) * ring;
}

/// The unit vector across [axis] that [offset] leans towards. Any one when it
/// leans along the axis, which is a tail straight below its head on a floor.
Vector3 _sideways(Vector3 offset, Vector3 axis) {
  final across = offset - axis * offset.dot(axis);
  if (across.length2 > 1e-18) return across.normalized();
  final other = axis.x.abs() < 0.9 ? Vector3(1, 0, 0) : Vector3(0, 1, 0);
  return axis.cross(other).normalized();
}

Vector3 _nearestOnSegment(Vector3 a, Vector3 b, Vector3 point) {
  final along = b - a;
  final length2 = along.length2;
  if (length2 < 1e-18) return a.clone();
  final t = ((point - a).dot(along) / length2).clamp(0.0, 1.0);
  return a + along * t;
}
