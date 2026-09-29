import 'package:orblit_rig/orblit_rig.dart' show BoneNaming;
import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';
import 'kind.dart';
import 'rest.dart';

/// A clip made for another skeleton, and what of it could not be carried.
class ClipRetargeted {
  const ClipRetargeted({required this.clip, this.problems = const []});

  final ClipDocument clip;

  final List<String> problems;
}

/// Which bone of [from] moves each bone of [to], by name.
///
/// A bone with the same name in both is matched first. The rest are matched
/// by what is left of the name once the things that vary between tools are
/// taken off: a namespace such as `mixamorig:`, a `DEF-` or `ORG-` prefix,
/// case, punctuation, and which side it is on, whether written `.L`, `_L` or
/// `Left`. So `mixamorig:LeftHand` and `hand.L` are the same bone.
///
/// A name that two bones of one skeleton share once cleaned matches nothing,
/// since guessing would drive a bone from the wrong side of the body. The
/// bones this leaves out are the ones to name by hand, in the `bones` of
/// [retargetClip], as target bone to source bone.
Map<String, String> matchBones(RestSkeleton from, RestSkeleton to) {
  final matched = <String, String>{
    for (final name in to.names)
      if (from.indexOf(name) != null) name: name,
  };
  final taken = matched.values.toSet();
  final sources = _byKey([
    for (final name in from.names)
      if (!taken.contains(name)) name,
  ]);
  final targets = _byKey([
    for (final name in to.names)
      if (!matched.containsKey(name)) name,
  ]);
  for (final MapEntry(:key, :value) in targets.entries) {
    final source = sources[key];
    if (source != null) matched[value] = source;
  }
  return matched;
}

/// [names] by their cleaned name, without any that share one.
Map<String, String> _byKey(List<String> names) {
  final seen = <String, String>{};
  final shared = <String>{};
  for (final name in names) {
    final key = _keyOf(name);
    if (seen.containsKey(key)) shared.add(key);
    seen[key] = name;
  }
  return {
    for (final MapEntry(:key, :value) in seen.entries)
      if (!shared.contains(key)) key: value,
  };
}

final RegExp _namespace = RegExp(r'[:|]');
final RegExp _sideWord = RegExp(
  r'^(left|right)[_ -]?|[_ -]?(left|right)$|[_ -]([lr])$',
);
final RegExp _punctuation = RegExp(r'[^a-z0-9]');

String _keyOf(String name) {
  final bare = name.substring(name.lastIndexOf(_namespace) + 1);
  var side = BoneNaming.sideOf(bare)?.suffix.substring(1);
  var base = BoneNaming.baseOf(bare).toLowerCase();
  final word = _sideWord.firstMatch(base);
  if (word != null) {
    side ??= (word[1] ?? word[2] ?? word[3])![0].toUpperCase();
    base = base.replaceRange(word.start, word.end, '');
  }
  return '${base.replaceAll(_punctuation, '')}/${side ?? ''}';
}

/// [clip], made for the bones of [from], made to move the bones of [to].
///
/// Each bone of [to] is turned so it stands in the world the way the bone
/// that drives it stood in the clip, measured from where each rests. A
/// shoulder that tips forward in the source tips forward in the target,
/// whatever way the two skeletons happen to point their bones. Bones are
/// driven by [matchBones], and [bones] adds to that or overrides it, naming
/// a bone of [to] and the bone of [from] that moves it. One bone can drive
/// several.
///
/// - **Rotations** are worked out exactly at every key when the bone and its
///   parent line up with the bones driving them, and easing, holds and cubic
///   slopes come across as they are. Where they do not, because the target
///   has fewer spine bones than the source, or a parent nothing drives, the
///   turn is worked out at each moment any of the bones involved has a key,
///   and joined with straight lines.
/// - **Positions** are the change from rest, in the world, made as much
///   larger as [to] is than [from]: the ratio of their [RestSkeleton.reach].
///   A tall character's hips travel further over a stride than a short one's.
///   A position that never leaves rest is left out.
/// - **Scales** are the change from rest, and an axis is assumed to be the
///   same axis in both.
///
/// Root motion follows the bone that carried it. Only bone channels of
/// [target] are retargeted; every other channel is kept as it was. Marks,
/// length and rate are kept.
///
/// Throws [ArgumentError] when [bones] names a bone one skeleton does not
/// have.
ClipRetargeted retargetClip(
  ClipDocument clip, {
  required RestSkeleton from,
  required RestSkeleton to,
  Map<String, String> bones = const {},
  String target = '',
}) {
  final drivers = <int, int>{};
  for (final MapEntry(key: mine, value: theirs) in {
    ...matchBones(from, to),
    ...bones,
  }.entries) {
    final into = to.indexOf(mine);
    final source = from.indexOf(theirs);
    if (into == null || source == null) {
      throw ArgumentError.value(
        bones,
        'bones',
        '"$mine" or "$theirs" is not a bone of the skeleton it is named in.',
      );
    }
    drivers[into] = source;
  }
  return _Retarget(
    clip: clip,
    from: from,
    to: to,
    drivers: drivers,
    target: target,
  ).run();
}

/// How one bone of the target is worked out from the source's bones.
///
/// The turn is `left * down⁻¹ * up * right`. [up] is the source bones from
/// below the shared ancestor of the driver and the pivot down to the driver,
/// outermost first, and [down] the same down to the pivot. The pivot is the
/// source bone driving the nearest target ancestor that has one. Their poses
/// above the shared ancestor cancel, so nothing there is looked at.
final class _Link {
  _Link({
    required this.source,
    required this.left,
    required this.right,
    required this.up,
    required this.down,
  });

  final int source;
  final Quaternion left;
  final Quaternion right;
  final List<int> up;
  final List<int> down;
}

final class _Retarget {
  _Retarget({
    required this.clip,
    required this.from,
    required this.to,
    required this.drivers,
    required this.target,
  });

  final ClipDocument clip;
  final RestSkeleton from;
  final RestSkeleton to;

  /// Target bone to the source bone that moves it.
  final Map<int, int> drivers;
  final String target;

  /// Source bone, then property, then what the clip has there.
  late final Map<int, Map<String, ClipChannel<Object>>> _moves = () {
    final out = <int, Map<String, ClipChannel<Object>>>{};
    for (final channel in clip.channels) {
      final name = channel.bone;
      if (name == null || channel.target != target) continue;
      final bone = from.indexOf(name);
      if (bone != null) (out[bone] ??= {})[channel.property] = channel;
    }
    return out;
  }();

  late final double _size = from.reach > 1e-9 && to.reach > 1e-9
      ? to.reach / from.reach
      : 1;

  late final List<Quaternion> _rest = [
    for (var bone = 0; bone < from.length; bone++) from.rotationOf(bone),
  ];

  ClipRetargeted run() {
    final carried = [
      for (final channel in clip.channels)
        if (channel.bone == null || channel.target != target) channel,
    ];
    final made = [
      for (final bone in drivers.keys.toList()..sort()) ..._channelsOf(bone),
    ];
    final root = _rootMotion();
    return ClipRetargeted(
      clip: clip.copyWith(
        channels: [...carried, ...made],
        rootMotion: root,
        clearRootMotion: root == null,
      ),
      problems: [
        ..._leftOut(),
        if (root == null && clip.rootMotion != null) ..._lostRoot(),
      ],
    );
  }

  List<ClipChannel<Object>> _channelsOf(int bone) => [
    ?_rotation(bone),
    ?_position(bone),
    ?_scale(bone),
  ];

  ClipChannel<Quaternion>? _turn(int source) {
    final channel = _moves[source]?['rotation'];
    return channel is ClipChannel<Quaternion> ? channel : null;
  }

  ClipChannel<Vector3>? _vector(int source, String property) {
    final channel = _moves[source]?[property];
    return channel is ClipChannel<Vector3> ? channel : null;
  }

  // Rotation.

  ClipChannel<Quaternion>? _rotation(int bone) {
    final link = _link(bone);
    final moving = [
      for (final source in {...link.up, ...link.down})
        if (_turn(source) != null) source,
    ];
    if (moving.isEmpty) return null;
    final alone = moving.length == 1 && moving.single == link.source;
    return ClipChannel<Quaternion>(
      target: target,
      bone: to.names[bone],
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: alone && link.up.isNotEmpty ? _carried(link) : _baked(link, moving),
    );
  }

  /// The driver's own keys, turned: the one bone that moves is the last
  /// factor of `up`, so its key is multiplied by constants either side.
  List<Key<Quaternion>> _carried(_Link link) {
    Quaternion carry(Quaternion q) =>
        _through(link, 0, stand: (link.source, q)) * link.right;
    return _mapKeys<Quaternion>(
      _turn(link.source)!.keys,
      (q) => carry(q)..normalize(),
      carry,
    );
  }

  List<Key<Quaternion>> _baked(_Link link, List<int> moving) {
    final times = <double>{
      for (final source in moving)
        for (final key in _turn(source)!.keys) key.at,
    }.toList()..sort();
    return [
      for (final at in times)
        Key<Quaternion>(
          at,
          (_through(link, at) * link.right)..normalize(),
          hold: Hold.linear,
        ),
    ];
  }

  Quaternion _through(_Link link, double at, {(int, Quaternion)? stand}) =>
      link.left *
      _product(link.down, at, stand).inverted() *
      _product(link.up, at, stand);

  Quaternion _product(List<int> chain, double at, (int, Quaternion)? stand) {
    var out = Quaternion.identity();
    for (final source in chain) {
      out =
          out *
          (stand != null && stand.$1 == source
              ? stand.$2
              : _turn(source)?.valueAt(at) ?? _rest[source]);
    }
    return out;
  }

  _Link _link(int bone) {
    final source = drivers[bone]!;
    final right =
        from.worldRotationOf(source).inverted() * to.worldRotationOf(bone);
    final pivot = _pivot(bone);
    final up = _pathTo(source);
    if (pivot == null) {
      // Nothing above drives, so the parent stays where it rests.
      return _Link(
        source: source,
        left:
            to.parentWorldRotationOf(bone).inverted() *
            from.parentWorldRotationOf(up.first),
        right: right,
        up: up,
        down: const [],
      );
    }
    final held = drivers[pivot]!;
    final down = _pathTo(held);
    while (up.isNotEmpty && down.isNotEmpty && up.first == down.first) {
      up.removeAt(0);
      down.removeAt(0);
    }
    final between =
        to.worldRotationOf(pivot).inverted() * to.parentWorldRotationOf(bone);
    final offset =
        from.worldRotationOf(held).inverted() * to.worldRotationOf(pivot);
    return _Link(
      source: source,
      left: between.inverted() * offset.inverted(),
      right: right,
      up: up,
      down: down,
    );
  }

  /// The nearest bone above [bone] that something drives.
  int? _pivot(int bone) {
    for (var up = to.parents[bone]; up >= 0; up = to.parents[up]) {
      if (drivers.containsKey(up)) return up;
    }
    return null;
  }

  /// The source bones from a root down to [source].
  List<int> _pathTo(int source) {
    final path = <int>[];
    for (var up = source; up >= 0; up = from.parents[up]) {
      path.insert(0, up);
    }
    return path;
  }

  // Position and scale.

  ClipChannel<Vector3>? _position(int bone) {
    final source = drivers[bone]!;
    final channel = _vector(source, 'position');
    final rest = from.positionOf(source);
    if (channel == null || _staysAt(channel, rest)) return null;
    // The two parents are in different frames, and the target's may be a
    // different size: the change is carried through the world between them.
    final across = to.parentWorldOf(bone)
      ..invert()
      ..multiply(from.parentWorldOf(source));
    Vector3 carry(Vector3 v) => across.transform3(v.clone()..scale(_size));
    final mine = to.positionOf(bone);
    return _vectorChannel(
      bone,
      'position',
      _mapKeys<Vector3>(channel.keys, (v) => carry(v - rest)..add(mine), carry),
    );
  }

  ClipChannel<Vector3>? _scale(int bone) {
    final source = drivers[bone]!;
    final channel = _vector(source, 'scale');
    final rest = from.scaleOf(source);
    if (channel == null || _staysAt(channel, rest)) return null;
    final mine = to.scaleOf(bone);
    Vector3 carry(Vector3 v) => Vector3(
      mine.x * _per(v.x, rest.x),
      mine.y * _per(v.y, rest.y),
      mine.z * _per(v.z, rest.z),
    );
    return _vectorChannel(
      bone,
      'scale',
      _mapKeys<Vector3>(channel.keys, carry, carry),
    );
  }

  ClipChannel<Vector3> _vectorChannel(
    int bone,
    String property,
    List<Key<Vector3>> keys,
  ) => ClipChannel<Vector3>(
    target: target,
    bone: to.names[bone],
    property: property,
    kind: ChannelKind.vector,
    keys: keys,
  );

  // Root motion.

  RootMotion? _rootMotion() {
    final root = clip.rootMotion;
    final name = root?.bone;
    if (root == null || name == null || root.target != target) return root;
    final source = from.indexOf(name);
    final carriers = [
      for (final MapEntry(:key, :value) in drivers.entries)
        if (value == source) key,
    ];
    if (source == null || carriers.isEmpty) return null;
    final bone = carriers.reduce((a, b) => a < b ? a : b);
    final turn =
        to.parentWorldRotationOf(bone).inverted() *
        from.parentWorldRotationOf(source);
    return RootMotion(
      target: root.target,
      bone: to.names[bone],
      turns: root.turns,
      rises: root.rises,
      up: turn.asRotationMatrix().transformed(root.up),
    );
  }

  // What was lost.

  List<String> _leftOut() {
    final unknown = <String>[];
    final undriven = <String>[];
    for (final channel in clip.channels) {
      final name = channel.bone;
      if (name == null || channel.target != target) continue;
      final source = from.indexOf(name);
      if (source == null) {
        unknown.add(name);
      } else if (!drivers.containsValue(source)) {
        undriven.add(name);
      }
    }
    return [
      if (unknown.isNotEmpty)
        'The clip moves ${_some(unknown)}, which the source skeleton does '
            'not have, so ${unknown.length == 1 ? 'it was' : 'they were'} '
            'left out.',
      if (undriven.isNotEmpty)
        'Nothing in the target skeleton is moved by ${_some(undriven)}, so '
            '${undriven.length == 1 ? 'its' : 'their'} motion was left out.',
    ];
  }

  List<String> _lostRoot() => [
    'The bone that carried root motion moves nothing in the target, so the '
        'clip has none.',
  ];
}

/// [keys] with each value and slope carried through [value] and [slope].
List<Key<T>> _mapKeys<T extends Object>(
  List<Key<T>> keys,
  T Function(T) value,
  T Function(T) slope,
) {
  T? carry(T? one) => one == null ? null : slope(one);
  return [
    for (final key in keys)
      Key<T>(
        key.at,
        value(key.value),
        hold: key.hold,
        shape: key.shape,
        slopeIn: carry(key.slopeIn),
        slopeOut: carry(key.slopeOut),
      ),
  ];
}

/// Whether [channel] never leaves [rest].
bool _staysAt(ClipChannel<Vector3> channel, Vector3 rest) {
  bool still(Vector3? slope) => slope == null || slope.length < 1e-9;
  return channel.keys.every(
    (key) =>
        (key.value - rest).length <= 1e-6 * (1 + rest.length) &&
        still(key.slopeIn) &&
        still(key.slopeOut),
  );
}

double _per(double value, double rest) => rest.abs() < 1e-12 ? 1 : value / rest;

/// The first few of [names], and how many more there are.
String _some(List<String> names) {
  final unique = names.toSet().toList();
  final shown = unique.take(5).map((name) => '"$name"').join(', ');
  return unique.length > 5 ? '$shown and ${unique.length - 5} more' : shown;
}
