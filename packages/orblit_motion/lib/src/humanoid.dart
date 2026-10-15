import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import 'bone_names.dart';
import 'rest.dart';

/// A place in a person's body that a bone can fill.
///
/// The same set whichever tool made the rig, so two skeletons with nothing
/// in common but their shape can be paired slot for slot.
enum HumanoidBone {
  hips,
  spine,
  chest,
  upperChest,
  neck,
  head,
  leftShoulder,
  leftUpperArm,
  leftLowerArm,
  leftHand,
  rightShoulder,
  rightUpperArm,
  rightLowerArm,
  rightHand,
  leftUpperLeg,
  leftLowerLeg,
  leftFoot,
  leftToes,
  rightUpperLeg,
  rightLowerLeg,
  rightFoot,
  rightToes,
}

/// Which bone of a skeleton fills each [HumanoidBone], and what could not be
/// worked out.
final class HumanoidMap {
  HumanoidMap._(Map<HumanoidBone, String> bones, List<String> problems)
    : bones = Map.unmodifiable(bones),
      problems = List.unmodifiable(problems);

  /// The bone filling each slot the skeleton has. A slot it lacks, such as a
  /// shoulder or toes, is absent.
  final Map<HumanoidBone, String> bones;

  /// Why a slot was left empty, in words for a person.
  final List<String> problems;

  String? operator [](HumanoidBone slot) => bones[slot];
}

/// Finds the parts of a person in [skeleton] from how its bones hang and
/// where they stand at rest.
///
/// Names are looked at only to tell left from right. A rig from any tool
/// has legs that hang from the hips and reach the floor, a spine that
/// reaches the head, and arms that leave the top of the spine, and that is
/// what is searched for. So `Skeleton_arm_joint_L__4_` and `LeftArm` both
/// land in [HumanoidBone.leftUpperArm] when each is the first bone of an arm.
///
/// [up] is which way is up in the skeleton's world, and [forward] which way a
/// person standing in it faces. The left side is then `up × forward`, which
/// is +X for a glTF model. A name that says a side is believed over that.
///
/// A skeleton that is not a person, or one with more bones in a limb than a
/// slot for each, is mapped as far as it can be and says what it left out in
/// [HumanoidMap.problems]. Bones it did not place are named by hand in the
/// `bones` of [retargetClip].
HumanoidMap humanoidOf(RestSkeleton skeleton, {Vector3? up, Vector3? forward}) {
  final finder = _Finder(
    skeleton,
    up: (up ?? Vector3(0, 1, 0)).normalized(),
    forward: (forward ?? Vector3(0, 0, 1)).normalized(),
  )..run();
  return HumanoidMap._(finder.found, finder.problems);
}

/// Which bone of [from] moves each bone of [to], by the slot each fills.
///
/// Only the slots both skeletons have are paired. A target with one spine
/// bone against a source with three is paired at the spine, and what the
/// other two do reaches the target through the bones below.
Map<String, String> matchHumanoid(RestSkeleton from, RestSkeleton to) {
  final source = humanoidOf(from).bones;
  return {
    for (final MapEntry(:key, :value) in humanoidOf(to).bones.entries)
      if (source[key] case final bone?) value: bone,
  };
}

/// A leg or arm bone that ends a chain: no more bones to stand for.
final RegExp _endSite = RegExp(r'(end|top|tip|nub|site)', caseSensitive: false);

/// A foot and a head are within this share of the skeleton's height of the
/// floor and the ceiling.
const double _near = 0.2;

final class _Finder {
  _Finder(this.skeleton, {required this.up, required Vector3 forward})
    : left = up.cross(forward);

  final RestSkeleton skeleton;
  final Vector3 up;
  final Vector3 left;

  final Map<HumanoidBone, String> found = {};
  final List<String> problems = [];

  late final List<List<int>> _children = () {
    final out = List.generate(skeleton.length, (_) => <int>[]);
    for (var bone = 0; bone < skeleton.length; bone++) {
      final parent = skeleton.parents[bone];
      if (parent >= 0) out[parent].add(bone);
    }
    return out;
  }();

  late final List<double> _height = [
    for (var bone = 0; bone < skeleton.length; bone++)
      skeleton.originOf(bone).dot(up),
  ];

  // The lowest and highest point of each bone and everything below it, and
  // how many bones that is.
  late final List<double> _low = List.of(_height);
  late final List<double> _high = List.of(_height);
  late final List<int> _size = List.filled(skeleton.length, 1);

  late final double _floor = _height.reduce(math.min);
  late final double _ceiling = _height.reduce(math.max);
  late final double _span = _ceiling - _floor;

  void run() {
    for (var bone = skeleton.length - 1; bone > 0; bone--) {
      final parent = skeleton.parents[bone];
      if (parent < 0) continue;
      _low[parent] = math.min(_low[parent], _low[bone]);
      _high[parent] = math.max(_high[parent], _high[bone]);
      _size[parent] += _size[bone];
    }
    if (_span < 1e-6) {
      problems.add('The skeleton has no height, so no body was looked for.');
      return;
    }
    final hips = _findHips();
    if (hips == null) {
      problems.add(
        'No bone has two legs below it and a spine above it, so the '
        'skeleton was not read as a person.',
      );
      return;
    }
    found[HumanoidBone.hips] = skeleton.names[hips];
    _legs(hips);
    final top = _spine(hips);
    if (top == null) {
      // Legs and no shoulders is a table or a quadruped. Placing its front
      // legs as thighs would drive them from the wrong part of a person.
      found.clear();
      problems.add('The skeleton was not read as a person.');
      return;
    }
    _head(top);
    _arms(top);
  }

  // Finding the body.

  bool _isLeg(int parent, int child) =>
      _low[child] <= _floor + _near * _span &&
      _high[child] <= _height[parent] + 0.1 * _span;

  bool _reachesCeiling(int bone) => _high[bone] >= _ceiling - _near * _span;

  int? _findHips() {
    int? legsOnly;
    for (var bone = 0; bone < skeleton.length; bone++) {
      final kids = _children[bone];
      if (kids.where((kid) => _isLeg(bone, kid)).length < 2) continue;
      if (kids.any(_reachesCeiling)) return bone;
      legsOnly ??= bone;
    }
    return legsOnly;
  }

  /// [start] and every bone below it while each has exactly one child.
  /// An end site, a bone that only marks where the last one stops, is left
  /// off.
  List<int> _chain(int start) {
    final chain = [start];
    while (_children[chain.last].length == 1) {
      chain.add(_children[chain.last].single);
    }
    if (chain.length > 1 && _endSite.hasMatch(skeleton.names[chain.last])) {
      chain.removeLast();
    }
    return chain;
  }

  /// Which of two limbs is the left, going by the name of each and, where a
  /// name says nothing, by which side of the body it is on.
  ({int left, int right})? _sides(int a, int b, String what) {
    final named = (
      cleanName(skeleton.names[a]).side,
      cleanName(skeleton.names[b]).side,
    );
    if (named case ('L', 'R')) return (left: a, right: b);
    if (named case ('R', 'L')) return (left: b, right: a);
    final across =
        skeleton.originOf(a).dot(left) - skeleton.originOf(b).dot(left);
    if (across.abs() < 1e-6 * _span) {
      problems.add(
        'The two $what start on the same side of the body, so which is '
        'left was not worked out.',
      );
      return null;
    }
    return across > 0 ? (left: a, right: b) : (left: b, right: a);
  }

  // Legs.

  void _legs(int hips) {
    final legs = [
      for (final kid in _children[hips])
        if (_isLeg(hips, kid)) kid,
    ];
    if (legs.length != 2) {
      problems.add(
        'The hips have ${legs.length} legs. Two were expected, so none were '
        'placed.',
      );
      return;
    }
    final sides = _sides(legs[0], legs[1], 'legs');
    if (sides == null) return;
    _leg(_chain(sides.left), 'left', const [
      HumanoidBone.leftUpperLeg,
      HumanoidBone.leftLowerLeg,
      HumanoidBone.leftFoot,
      HumanoidBone.leftToes,
    ]);
    _leg(_chain(sides.right), 'right', const [
      HumanoidBone.rightUpperLeg,
      HumanoidBone.rightLowerLeg,
      HumanoidBone.rightFoot,
      HumanoidBone.rightToes,
    ]);
  }

  void _leg(List<int> chain, String side, List<HumanoidBone> slots) {
    if (chain.length > slots.length) {
      problems.add(
        'The $side leg has ${chain.length} bones in a line. A leg has '
        'slots for ${slots.length}, so it was not placed.',
      );
      return;
    }
    _place(chain, slots);
  }

  void _place(List<int> chain, List<HumanoidBone> slots) {
    for (var i = 0; i < chain.length; i++) {
      found[slots[i]] = skeleton.names[chain[i]];
    }
  }

  // Spine and head.

  /// The child that goes highest.
  int? _upChild(int bone) {
    int? best;
    for (final kid in _children[bone]) {
      if (best == null || _high[kid] > _high[best]) best = kid;
    }
    return best;
  }

  /// The children of [bone] that look like arms: not the way up, and with
  /// enough bones below to be a shoulder, an elbow and a hand.
  List<int> _armsOf(int bone) {
    final up = _upChild(bone);
    return [
      for (final kid in _children[bone])
        if (kid != up && _size[kid] >= 3) kid,
    ];
  }

  /// Places the spine and answers its top bone, the one the arms and neck
  /// hang from, or null when there is none to be found.
  int? _spine(int hips) {
    final first = _children[hips]
        .where(_reachesCeiling)
        .fold<int?>(
          null,
          (best, kid) => best == null || _high[kid] > _high[best] ? kid : best,
        );
    if (first == null) {
      problems.add('Nothing above the hips reaches the head, so no spine.');
      return null;
    }
    final chain = [first];
    while (_armsOf(chain.last).length < 2) {
      final next = _upChild(chain.last);
      if (next == null) {
        problems.add('The spine runs out before any arms hang from it.');
        return null;
      }
      chain.add(next);
    }
    _spineSlots(chain);
    return chain.last;
  }

  void _spineSlots(List<int> chain) {
    found[HumanoidBone.spine] = skeleton.names[chain.first];
    if (chain.length == 1) return;
    final upper = chain.length >= 3;
    found[HumanoidBone.chest] =
        skeleton.names[chain[upper ? chain.length - 2 : 1]];
    if (upper) found[HumanoidBone.upperChest] = skeleton.names[chain.last];
  }

  void _head(int top) {
    final up = _upChild(top);
    if (up == null) return;
    final chain = _chain(up);
    if (chain.length == 1) {
      found[HumanoidBone.head] = skeleton.names[chain.single];
      return;
    }
    found[HumanoidBone.neck] = skeleton.names[chain.first];
    found[HumanoidBone.head] = skeleton.names[chain.last];
  }

  // Arms.

  void _arms(int top) {
    final arms = _armsOf(top);
    if (arms.length != 2) {
      problems.add(
        'The top of the spine has ${arms.length} arms. Two were expected, '
        'so none were placed.',
      );
      return;
    }
    final sides = _sides(arms[0], arms[1], 'arms');
    if (sides == null) return;
    _arm(_chain(sides.left), 'left', const [
      HumanoidBone.leftShoulder,
      HumanoidBone.leftUpperArm,
      HumanoidBone.leftLowerArm,
      HumanoidBone.leftHand,
    ]);
    _arm(_chain(sides.right), 'right', const [
      HumanoidBone.rightShoulder,
      HumanoidBone.rightUpperArm,
      HumanoidBone.rightLowerArm,
      HumanoidBone.rightHand,
    ]);
  }

  /// [slots] are shoulder, upper arm, lower arm, hand. A chain of three has
  /// no shoulder, and a chain of four starts with one.
  void _arm(List<int> chain, String side, List<HumanoidBone> slots) {
    if (chain.length < 3 || chain.length > 4) {
      problems.add(
        'The $side arm has ${chain.length} bones in a line. Three or four '
        'were expected, so it was not placed.',
      );
      return;
    }
    _place(chain, chain.length == 4 ? slots : slots.sublist(1));
  }
}
