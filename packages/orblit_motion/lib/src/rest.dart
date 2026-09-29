import 'package:vector_math/vector_math_64.dart';

/// A skeleton standing still: which bones it has, how they hang from one
/// another, and where each sits in its parent.
///
/// What a clip's bone channels are measured against. A clip says where a bone
/// is in its parent, in absolute terms, so it means nothing until it is set
/// beside the skeleton it was made for. Two skeletons that stand the same
/// way with different bone lengths and different rest turns are what
/// retargeting bridges, and this is each of them.
///
/// Bones come parents first, so a bone's parent is always an earlier index.
/// Only rotation, position and scale of each bone are kept; a matrix that
/// shears is read as the nearest of those.
final class RestSkeleton {
  /// [local] is each bone's rest transform in its parent. [above] is where
  /// whatever the root bones hang from stands in the world: an armature node
  /// that turns a Z-up rig upright, or scales a rig made in centimetres.
  ///
  /// Throws [ArgumentError] when the lists differ in length, a name is used
  /// twice, or a parent does not come before its child.
  RestSkeleton({
    required List<String> names,
    required List<int> parents,
    required List<Matrix4> local,
    Matrix4? above,
  }) : names = List<String>.unmodifiable(names),
       parents = List<int>.unmodifiable(parents),
       _above = (above ?? Matrix4.identity()).clone(),
       _local = [for (final matrix in local) matrix.clone()] {
    if (parents.length != names.length || local.length != names.length) {
      throw ArgumentError('A parent and a rest transform for every bone.');
    }
    if (names.toSet().length != names.length) {
      throw ArgumentError.value(names, 'names', 'Two bones share a name.');
    }
    for (var bone = 0; bone < parents.length; bone++) {
      if (parents[bone] < -1 || parents[bone] >= bone) {
        throw ArgumentError.value(
          parents,
          'parents',
          'Bone $bone hangs from ${parents[bone]}, which does not come '
              'before it.',
        );
      }
    }
  }

  final List<String> names;

  /// Each bone's parent, or -1 for a root.
  final List<int> parents;

  final Matrix4 _above;
  final List<Matrix4> _local;

  int get length => names.length;

  /// The bone called [name], or null when there is none.
  int? indexOf(String name) {
    final at = names.indexOf(name);
    return at < 0 ? null : at;
  }

  late final List<_Parts> _parts = [
    for (final matrix in _local) _Parts.of(matrix),
  ];

  late final List<Matrix4> _world = () {
    final out = <Matrix4>[];
    for (var bone = 0; bone < length; bone++) {
      final parent = parents[bone];
      out.add((parent < 0 ? _above : out[parent]).multiplied(_local[bone]));
    }
    return out;
  }();

  late final List<Quaternion> _worldRotation = [
    for (final matrix in _world) _Parts.of(matrix).rotation,
  ];

  late final Quaternion _aboveRotation = _Parts.of(_above).rotation;

  /// Where [bone] sits in its parent, at rest.
  Vector3 positionOf(int bone) => _parts[bone].position.clone();

  /// How [bone] is turned in its parent, at rest.
  Quaternion rotationOf(int bone) => _parts[bone].rotation.clone();

  Vector3 scaleOf(int bone) => _parts[bone].scale.clone();

  /// How [bone] is turned in the world, at rest.
  Quaternion worldRotationOf(int bone) => _worldRotation[bone].clone();

  /// How the parent of [bone] is turned in the world, at rest: for a root,
  /// whatever it hangs from.
  Quaternion parentWorldRotationOf(int bone) => parents[bone] < 0
      ? _aboveRotation.clone()
      : _worldRotation[parents[bone]].clone();

  /// The parent of [bone] in the world at rest, as a matrix, scale and all.
  Matrix4 parentWorldOf(int bone) =>
      (parents[bone] < 0 ? _above : _world[parents[bone]]).clone();

  /// How far the farthest bone stands from where the roots hang, in the
  /// world, at rest.
  ///
  /// One number for how big the skeleton is, whichever way it faces and
  /// however its bones are named. A person's is about their height.
  late final double reach = () {
    final origin = _above.getTranslation();
    var farthest = 0.0;
    for (final world in _world) {
      final away = world.getTranslation().distanceTo(origin);
      if (away > farthest) farthest = away;
    }
    return farthest;
  }();

  @override
  String toString() => 'RestSkeleton($length bones)';
}

class _Parts {
  _Parts(this.position, this.rotation, this.scale);

  factory _Parts.of(Matrix4 matrix) {
    final position = Vector3.zero();
    final rotation = Quaternion.identity();
    final scale = Vector3.zero();
    matrix.decompose(position, rotation, scale);
    return _Parts(position, rotation, scale);
  }

  final Vector3 position;
  final Quaternion rotation;
  final Vector3 scale;
}
