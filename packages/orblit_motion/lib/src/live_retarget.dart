import 'package:orblit_rig/orblit_rig.dart' show ModelPoint;
import 'package:vector_math/vector_math_64.dart';

import 'frame.dart';
import 'rest.dart';
import 'retarget_plan.dart';

/// One skeleton moved by the pose of another, a frame at a time.
///
/// The same turn as [retargetClip], worked out per frame instead of baked:
/// whatever poses the source, a clip, a blend, a ragdoll or a capture, the
/// target follows it with nothing to bake and nothing to reload when the
/// source changes. A source frame is a map of bone name to [BoneLocal], as
/// `ClipFrame.bones` holds for a target. A bone it leaves out is at rest.
///
/// ```dart fragment
/// final follow = LiveRetarget(from: giant, to: dwarf, humanoid: true);
/// final pose = follow.follow(frame.bones['']!);
/// ```
final class LiveRetarget {
  /// Pairs the bones of [from] and [to] the way [retargetClip] does.
  ///
  /// Throws [ArgumentError] when [bones] names a bone one skeleton lacks, or
  /// [unpaired] names one [to] lacks or one [bones] pairs.
  LiveRetarget({
    required this.from,
    required this.to,
    Map<String, String> bones = const {},
    Set<String> unpaired = const {},
    bool humanoid = false,
    bool fixRest = false,
  }) : _plan = planRetarget(
         from: from,
         to: to,
         bones: bones,
         unpaired: unpaired,
         humanoid: humanoid,
         fixRest: fixRest,
       );

  final RestSkeleton from;
  final RestSkeleton to;
  final RetargetPlan _plan;

  /// What could not be paired or fixed, in words for a person.
  List<String> get problems => _plan.problems;

  /// Where [source] puts each bone of the target that something drives.
  ///
  /// A rotation for every driven bone. A position or a scale only where the
  /// source frame has one, since a bone that is not moved keeps its rest.
  Map<String, BoneLocal> follow(Map<String, BoneLocal> source) {
    Quaternion turn(int bone) =>
        source[from.names[bone]]?.rotation ?? _plan.restTurnOf(bone);
    final out = <String, BoneLocal>{};
    for (final MapEntry(key: bone, value: driver) in _plan.drivers.entries) {
      final local = source[from.names[driver]];
      final position = local?.position;
      final scale = local?.scale;
      out[to.names[bone]] = BoneLocal(
        rotation: _plan.rotationOf(bone, turn),
        position: position == null ? null : _plan.positionOf(bone, position),
        scale: scale == null ? null : _plan.scaleOf(bone, scale),
      );
    }
    return out;
  }

  late final Matrix4 _intoTarget = to.parentWorldOf(0)..invert();

  /// Where [bone] of the source stands, as a point in the target's own
  /// space, and made as much larger as the target is.
  ///
  /// The two models are taken to stand with their origin at the floor, so
  /// that scaling a foot's place scales its height off the floor too.
  ///
  /// Throws [ArgumentError] when the source has no such bone.
  Vector3 pointOf(String bone, Map<String, BoneLocal> source) {
    final start = from.indexOf(bone);
    if (start == null) {
      throw ArgumentError.value(bone, 'bone', 'The source has no such bone.');
    }
    final chain = [for (var up = start; up >= 0; up = from.parents[up]) up];
    final world = from.parentWorldOf(chain.last);
    for (final index in chain.reversed) {
      final local = source[from.names[index]];
      world.multiply(
        Matrix4.compose(
          local?.position ?? from.positionOf(index),
          local?.rotation ?? from.rotationOf(index),
          local?.scale ?? from.scaleOf(index),
        ),
      );
    }
    final away = world.getTranslation()..scale(_plan.size);
    return _intoTarget.transformed3(away);
  }

  /// A point for a [TwoBoneIk] `target`: where [bone] of the source is, read
  /// from whatever [source] answers each time it is solved, in the target's
  /// own space. Null while [source] answers null, which leaves the limb as it
  /// is.
  ///
  /// With [bone] a source foot and the limb the target's leg, the foot of the
  /// target stands where the source's does, so it slides no further than the
  /// source's slid.
  ///
  /// Throws [ArgumentError] when the source has no such bone.
  ModelPoint pinned(
    String bone, {
    required Map<String, BoneLocal>? Function() source,
  }) {
    if (from.indexOf(bone) == null) {
      throw ArgumentError.value(bone, 'bone', 'The source has no such bone.');
    }
    return (pose, frame) {
      final now = source();
      return now == null ? null : pointOf(bone, now);
    };
  }
}
