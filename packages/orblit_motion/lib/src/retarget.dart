import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';
import 'kind.dart';
import 'rest.dart';
import 'retarget_plan.dart';

/// A clip made for another skeleton, and what of it could not be carried.
class ClipRetargeted {
  const ClipRetargeted({required this.clip, this.problems = const []});

  final ClipDocument clip;

  final List<String> problems;
}

/// [clip], made for the bones of [from], made to move the bones of [to].
///
/// Each bone of [to] is turned so it stands in the world the way the bone
/// that drives it stood in the clip, measured from where each rests. A
/// shoulder that tips forward in the source tips forward in the target,
/// whatever way the two skeletons happen to point their bones. Bones are
/// driven by [matchBones], and [bones] adds to that or overrides it, naming
/// a bone of [to] and the bone of [from] that moves it. One bone can drive
/// several. A bone of [to] in [unpaired] is left out of the automatic
/// pairing, so nothing moves it. A bone cannot be both named in [bones] and
/// unpaired.
///
/// With [humanoid] the bones are also paired by the part of a person each
/// is, from how they hang and where they stand, which is what carries a clip
/// between two rigs that share no names. What could not be paired is in
/// [ClipRetargeted.problems].
///
/// With [fixRest] each bone is first turned to point the way its driver
/// does at rest, so a target stood in an A-pose follows a source stood in a
/// T-pose by where its limbs point and not by how far they move. A bone
/// whose rest this changes gets a key even when the source never moves it.
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
/// have, or [unpaired] names one [to] does not have or one [bones] pairs.
ClipRetargeted retargetClip(
  ClipDocument clip, {
  required RestSkeleton from,
  required RestSkeleton to,
  Map<String, String> bones = const {},
  Set<String> unpaired = const {},
  String target = '',
  bool humanoid = false,
  bool fixRest = false,
}) {
  final plan = planRetarget(
    from: from,
    to: to,
    bones: bones,
    unpaired: unpaired,
    humanoid: humanoid,
    fixRest: fixRest,
  );
  return _Retarget(clip: clip, plan: plan, target: target).run();
}

final class _Retarget {
  _Retarget({required this.clip, required this.plan, required this.target});

  final ClipDocument clip;
  final RetargetPlan plan;
  final String target;

  RestSkeleton get from => plan.from;
  RestSkeleton get to => plan.to;
  Map<int, int> get drivers => plan.drivers;

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
        ...plan.problems,
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
    final link = plan.linkOf(bone);
    final moving = [
      for (final source in link.involved)
        if (_turn(source) != null) source,
    ];
    if (moving.isEmpty && !plan.restsElsewhere(bone)) return null;
    final alone = moving.length == 1 && moving.single == link.source;
    return ClipChannel<Quaternion>(
      target: target,
      bone: to.names[bone],
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: alone && link.up.isNotEmpty
          ? _carried(bone, link.source)
          : _baked(bone, moving),
    );
  }

  /// The driver's own keys, turned: the one bone that moves is the last
  /// factor of `up`, so its key is multiplied by constants either side.
  List<Key<Quaternion>> _carried(int bone, int source) {
    Quaternion carry(Quaternion q) => plan.rotationOf(
      bone,
      (other) => other == source ? q : plan.restTurnOf(other),
    );
    return _mapKeys<Quaternion>(
      _turn(source)!.keys,
      (q) => carry(q)..normalize(),
      carry,
    );
  }

  /// A key at each moment a moving bone has one, or a single key at the
  /// start when none moves and the rest was fixed.
  List<Key<Quaternion>> _baked(int bone, List<int> moving) {
    final times = <double>{
      for (final source in moving)
        for (final key in _turn(source)!.keys) key.at,
      if (moving.isEmpty) 0,
    }.toList()..sort();
    return [
      for (final at in times)
        Key<Quaternion>(
          at,
          plan.rotationOf(
            bone,
            (source) => _turn(source)?.valueAt(at) ?? plan.restTurnOf(source),
          )..normalize(),
          hold: Hold.linear,
        ),
    ];
  }

  // Position and scale.

  ClipChannel<Vector3>? _position(int bone) {
    final source = drivers[bone]!;
    final channel = _vector(source, 'position');
    final rest = from.positionOf(source);
    if (channel == null || _staysAt(channel, rest)) return null;
    return _vectorChannel(
      bone,
      'position',
      _mapKeys<Vector3>(
        channel.keys,
        (v) => plan.positionOf(bone, v),
        (slope) => plan.changeOf(bone, slope),
      ),
    );
  }

  ClipChannel<Vector3>? _scale(int bone) {
    final source = drivers[bone]!;
    final channel = _vector(source, 'scale');
    if (channel == null || _staysAt(channel, from.scaleOf(source))) return null;
    return _vectorChannel(
      bone,
      'scale',
      _mapKeys<Vector3>(
        channel.keys,
        (v) => plan.scaleOf(bone, v),
        (v) => plan.scaleOf(bone, v),
      ),
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
        (plan.parentStanceOf(bone) * to.parentWorldRotationOf(bone))
            .inverted() *
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

/// The first few of [names], and how many more there are.
String _some(List<String> names) {
  final unique = names.toSet().toList();
  final shown = unique.take(5).map((name) => '"$name"').join(', ');
  return unique.length > 5 ? '$shown and ${unique.length - 5} more' : shown;
}
