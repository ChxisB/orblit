import 'dart:async';
import 'dart:typed_data';

import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// Makes one model file out of [placements] of the model at [mesh], with each
/// placement baked into the vertices, or returns null to leave those objects
/// as they are.
///
/// `bakeInstances` in `orblit_asset` does the work. Reading [mesh] is the
/// caller's job, so the stage never touches a file.
typedef StaticBaker =
    Future<Uint8List?> Function(String mesh, List<Matrix4> placements);

/// What objects must share to be baked together. The square of ground is in it
/// so a merge is still small enough for the renderer to cull.
typedef _Group = ({
  String mesh,
  int? material,
  double red,
  double green,
  double blue,
  bool casts,
  bool receives,
  int layer,
  int east,
  int south,
});

/// Where an object was and since when.
final class _Track {
  _Track(this.transform, this.group, this.since);

  final Matrix4 transform;
  final _Group group;
  final int since;
}

/// One baked file standing in for [members].
final class _Merge {
  _Merge(this.key, this.group, this.members);

  final int key;
  final _Group group;

  /// Each member's key and where it stood when baked, in key order.
  final Map<int, Matrix4> members;

  /// The resource name once the bake is ready, null while it runs.
  String? mesh;
}

/// Draws many objects that never move as one object per model and square of
/// ground.
///
/// A renderer pays for every object it draws, so a thousand rocks cost a
/// thousand draws however small they are. This watches a scene, and when
/// [minimum] or more objects of one model and material in one square of ground
/// have held still for [settle] observations, it has the caller bake them into
/// one file and swaps that file in for them. When one of them moves, hides or
/// leaves, the group goes back to single objects at once and bakes again
/// without it.
///
/// Call [observe] and then [replace] on the same scene each time you publish
/// it. [observe] changes state and starts bakes. [replace] answers with the
/// scene to draw, and checks every member itself, so a scene it has not
/// observed is still drawn correctly, only without a merge it has no right to.
///
/// What it leaves alone: an object with a clip, morph weights, joints or a
/// variant, one that is hidden, and one without a mesh file. The built-in cube
/// already batches natively. A model the baker declines, because it has skins
/// or compressed buffers, is declined for good and its objects stay single.
///
/// Costs. A bake is Dart work and each one makes a new file. The renderer
/// keeps what it loaded from a file until it is torn down, so a group bakes at
/// most [maxBakes] times and then stays as single objects. A merge also holds a
/// second copy of the geometry beside the model it came from.
final class OrblitStaticMerge {
  OrblitStaticMerge({
    required this.bake,
    this.provide = OrblitResources.provide,
    this.release = OrblitResources.release,
    this.onReady,
    this.settle = 30,
    this.minimum = 8,
    this.cellSize = 64,
    this.maxBakes = 8,
    this.keyBase = 0x4d470000,
  }) {
    if (settle < 0) throw ArgumentError.value(settle, 'settle', 'below zero');
    if (minimum < 2) throw ArgumentError.value(minimum, 'minimum', 'below 2');
    if (cellSize <= 0) {
      throw ArgumentError.value(cellSize, 'cellSize', 'not above zero');
    }
    if (maxBakes < 1) {
      throw ArgumentError.value(maxBakes, 'maxBakes', 'below 1');
    }
  }

  final StaticBaker bake;

  /// Hand bytes to the renderer under a name. The default is
  /// [OrblitResources.provide].
  final Future<void> Function(String name, Uint8List bytes) provide;

  /// Take them back. The default is [OrblitResources.release].
  final Future<bool> Function(String name) release;

  /// Called when a bake lands. Publish the scene again, because the merge only
  /// shows in a scene that has been through [replace] since.
  final void Function()? onReady;

  /// How many observations an object must hold still before it is merged.
  final int settle;

  /// How few objects are worth a merge.
  final int minimum;

  /// The side of a square of ground, in the scene's units.
  final double cellSize;

  /// How many times one group may bake. See the costs above.
  final int maxBakes;

  /// The first key of the merged objects, clear of the scene's own keys.
  final int keyBase;

  // A name loads once, so names count across every instance. Two merges that
  // numbered their own would hand the renderer the same name twice.
  static int _serial = 0;

  var _frame = 0;
  var _nextKey = 0;
  var _disposed = false;
  var _tracks = <int, _Track>{};
  final _merges = <_Group, _Merge>{};
  final _bakes = <_Group, int>{};
  final _provided = <String>{};
  final _declined = <String, Object?>{};

  /// The models the baker would not take, each with the error it threw, or
  /// null when it said no.
  Map<String, Object?> get declined => Map.unmodifiable(_declined);

  /// Notes where every object is and starts a bake for each group that has
  /// held still long enough.
  void observe(OrblitScene scene) {
    _frame++;
    final tracks = <int, _Track>{};
    final settled = <_Group, Map<int, Matrix4>>{};
    for (final object in scene.objects) {
      final group = _groupOf(object);
      if (group == null) continue;
      final track = _trackOf(object, group);
      tracks[object.key] = track;
      if (_frame - track.since >= settle) {
        (settled[group] ??= {})[object.key] = track.transform;
      }
    }
    _tracks = tracks;
    _merges.removeWhere((group, _) => (settled[group]?.length ?? 0) < minimum);
    for (final MapEntry(:key, :value) in settled.entries) {
      if (value.length >= minimum) _consider(key, value);
    }
  }

  /// [scene] with each ready merge drawn in place of its members. A merge
  /// whose members are not all there, still, and alike is not drawn.
  OrblitScene replace(OrblitScene scene) {
    final owner = {
      for (final merge in _merges.values)
        if (merge.mesh != null)
          for (final key in merge.members.keys) key: merge,
    };
    if (owner.isEmpty) return scene;
    final held = <_Merge, int>{};
    for (final object in scene.objects) {
      final merge = owner[object.key];
      if (merge != null && _holds(merge, object)) {
        held[merge] = (held[merge] ?? 0) + 1;
      }
    }
    final live = {
      for (final MapEntry(:key, :value) in held.entries)
        if (value == key.members.length) key,
    };
    if (live.isEmpty) return scene;
    return scene.copyWith(
      objects: [
        for (final object in scene.objects)
          if (!live.contains(owner[object.key])) object,
        for (final merge in live) _objectOf(merge),
      ],
    );
  }

  /// Stops merging and releases every file provided so far. A bake that lands
  /// afterwards releases its own file.
  Future<void> dispose() async {
    _disposed = true;
    _merges.clear();
    final names = _provided.toList();
    _provided.clear();
    for (final name in names) {
      await release(name);
    }
  }

  _Group? _groupOf(OrblitObject object) {
    final mesh = object.mesh;
    if (mesh == null ||
        !object.visible ||
        object.animation != null ||
        object.morphWeights != null ||
        object.joints != null ||
        object.variant != null) {
      return null;
    }
    final at = object.transform.storage;
    // A position that is not a number has no square of ground, and floor
    // throws on it.
    if (!at[12].isFinite || !at[14].isFinite) return null;
    return (
      mesh: mesh,
      material: object.material,
      red: object.colour.x,
      green: object.colour.y,
      blue: object.colour.z,
      casts: object.castShadows,
      receives: object.receiveShadows,
      layer: object.layer,
      east: (at[12] / cellSize).floor(),
      south: (at[14] / cellSize).floor(),
    );
  }

  _Track _trackOf(OrblitObject object, _Group group) {
    final old = _tracks[object.key];
    if (old != null &&
        old.group == group &&
        _same(old.transform, object.transform)) {
      return old;
    }
    return _Track(Matrix4.copy(object.transform), group, _frame);
  }

  bool _holds(_Merge merge, OrblitObject object) {
    final stood = merge.members[object.key];
    return stood != null &&
        _same(stood, object.transform) &&
        _groupOf(object) == merge.group;
  }

  void _consider(_Group group, Map<int, Matrix4> settled) {
    final current = _merges[group];
    if (current != null && _sameMembers(current.members, settled)) return;
    final started = _bakes[group] ?? 0;
    if (started >= maxBakes || _declined.containsKey(group.mesh)) {
      _merges.remove(group);
      return;
    }
    _bakes[group] = started + 1;
    final ordered = settled.entries.toList()
      ..sort((a, b) => a.key.compareTo(b.key));
    final merge = _Merge(keyBase + _nextKey++, group, Map.fromEntries(ordered));
    _merges[group] = merge;
    unawaited(_land(merge));
  }

  Future<void> _land(_Merge merge) async {
    final String? name;
    try {
      name = await _prepare(merge);
    } on Object catch (error) {
      _declined[merge.group.mesh] = error;
      return;
    }
    if (name != null && identical(_merges[merge.group], merge)) {
      onReady?.call();
    }
  }

  /// Bakes and provides [merge]'s file, and returns its name, or null when the
  /// merge is no longer wanted or the baker said no.
  Future<String?> _prepare(_Merge merge) async {
    final group = merge.group;
    final bytes = await bake(group.mesh, merge.members.values.toList());
    if (bytes == null) {
      _declined[group.mesh] = null;
      return null;
    }
    if (!identical(_merges[group], merge)) return null;
    final name = OrblitResources.nameFor('static-merge/${_serial++}.glb');
    await provide(name, bytes);
    if (_disposed) {
      await release(name);
      return null;
    }
    _provided.add(name);
    merge.mesh = name;
    return name;
  }

  OrblitObject _objectOf(_Merge merge) {
    final group = merge.group;
    return OrblitObject(
      key: merge.key,
      transform: Matrix4.identity(),
      colour: Vector3(group.red, group.green, group.blue),
      mesh: merge.mesh,
      material: group.material,
      castShadows: group.casts,
      receiveShadows: group.receives,
      layer: group.layer,
    );
  }
}

bool _same(Matrix4 a, Matrix4 b) {
  final x = a.storage;
  final y = b.storage;
  for (var i = 0; i < 16; i++) {
    if (x[i] != y[i]) return false;
  }
  return true;
}

bool _sameMembers(Map<int, Matrix4> a, Map<int, Matrix4> b) {
  if (a.length != b.length) return false;
  for (final MapEntry(:key, :value) in b.entries) {
    final other = a[key];
    if (other == null || !_same(other, value)) return false;
  }
  return true;
}
