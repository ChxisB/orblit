import 'package:vector_math/vector_math_64.dart';

import 'bone_names.dart';
import 'humanoid.dart';
import 'rest.dart';

/// How one bone of the target is worked out from the source's bones.
///
/// The turn is `left * down⁻¹ * up * right`. [up] is the source bones from
/// below the shared ancestor of the driver and the pivot down to the driver,
/// outermost first, and [down] the same down to the pivot. The pivot is the
/// source bone driving the nearest target ancestor that has one. Their poses
/// above the shared ancestor cancel, so nothing there is looked at.
final class RetargetLink {
  const RetargetLink({
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

  /// Every source bone whose turn changes this bone's.
  Iterable<int> get involved => {...up, ...down};
}

/// Which bone of [from] drives each bone of [to], with [bones] naming them
/// by hand over [matchBones] and, for [humanoid], over the parts of a person.
///
/// A bone of [to] in [unpaired] is left out of whatever [matchBones] and
/// [humanoid] paired it with, so nothing drives it. A bone named in [bones]
/// is paired by hand, and so cannot also be unpaired.
///
/// Throws [ArgumentError] when [bones] names a bone one skeleton lacks, or
/// when [unpaired] names a bone [to] lacks or one that [bones] pairs.
RetargetPlan planRetarget({
  required RestSkeleton from,
  required RestSkeleton to,
  Map<String, String> bones = const {},
  Set<String> unpaired = const {},
  bool humanoid = false,
  bool fixRest = false,
}) {
  _checkUnpaired(to, bones, unpaired);
  final problems = <String>[];
  final named = {
    for (final MapEntry(:key, :value) in {
      ...matchBones(from, to),
      if (humanoid) ..._byBody(from, to, problems),
    }.entries)
      if (!unpaired.contains(key)) key: value,
    ...bones,
  };
  final drivers = <int, int>{};
  for (final MapEntry(key: mine, value: theirs) in named.entries) {
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
  return RetargetPlan(
    from: from,
    to: to,
    drivers: drivers,
    fixRest: fixRest,
    problems: problems,
  );
}

void _checkUnpaired(
  RestSkeleton to,
  Map<String, String> bones,
  Set<String> unpaired,
) {
  for (final name in unpaired) {
    if (to.indexOf(name) == null) {
      throw ArgumentError.value(
        unpaired,
        'unpaired',
        '"$name" is not a bone of the target skeleton.',
      );
    }
    if (bones.containsKey(name)) {
      throw ArgumentError.value(
        unpaired,
        'unpaired',
        '"$name" is paired by hand in bones, so it cannot be left unpaired.',
      );
    }
  }
}

Map<String, String> _byBody(
  RestSkeleton from,
  RestSkeleton to,
  List<String> problems,
) {
  for (final (name, skeleton) in [('source', from), ('target', to)]) {
    for (final problem in humanoidOf(skeleton).problems) {
      problems.add('In the $name skeleton: $problem');
    }
  }
  return matchHumanoid(from, to);
}

/// How the bones of one skeleton move the bones of another, worked out once
/// so a baked clip and a live frame turn a bone the same way.
final class RetargetPlan {
  /// [drivers] is target bone to the source bone that moves it.
  ///
  /// With [fixRest] each driven bone is first turned to point the way its
  /// driver does at rest, so a target stood in an A-pose follows a source
  /// stood in a T-pose by where the limbs point and not by how far they move.
  RetargetPlan({
    required this.from,
    required this.to,
    required this.drivers,
    bool fixRest = false,
    List<String> problems = const [],
  }) : fixesRest = fixRest,
       _problems = [...problems],
       size = from.reach > 1e-9 && to.reach > 1e-9 ? to.reach / from.reach : 1 {
    _stance = fixRest ? _stances() : List.filled(to.length, _none);
  }

  static final Quaternion _none = Quaternion.identity();

  final RestSkeleton from;
  final RestSkeleton to;
  final Map<int, int> drivers;
  final bool fixesRest;

  /// How much larger the target is: the ratio of their [RestSkeleton.reach].
  final double size;

  final List<String> _problems;
  late final List<Quaternion> _stance;

  List<String> get problems => List.unmodifiable(_problems);

  late final List<Quaternion> _rest = [
    for (var bone = 0; bone < from.length; bone++) from.rotationOf(bone),
  ];

  late final Map<int, RetargetLink> _links = {
    for (final bone in drivers.keys) bone: _link(bone),
  };

  /// How [source] is turned in its parent, at rest.
  Quaternion restTurnOf(int source) => _rest[source].clone();

  RetargetLink linkOf(int bone) => _links[bone]!;

  /// The turn of [bone] in its parent, given how each source bone is turned
  /// in its own, by [turn].
  Quaternion rotationOf(int bone, Quaternion Function(int source) turn) {
    final link = linkOf(bone);
    return link.left *
        _product(link.down, turn).inverted() *
        _product(link.up, turn) *
        link.right;
  }

  /// Whether the whole source at rest puts [bone] anywhere but where it
  /// rests. Only [fixesRest] can make that so.
  bool restsElsewhere(int bone) {
    if (!fixesRest) return false;
    final off = rotationOf(bone, restTurnOf) * to.rotationOf(bone).inverted();
    return 1 - off.w.abs() > 1e-9;
  }

  /// How the target's parent of [bone] is turned from its rest to point the
  /// way the source does. Identity unless [fixesRest].
  Quaternion parentStanceOf(int bone) {
    final parent = to.parents[bone];
    return (parent < 0 ? _none : _stance[parent]).clone();
  }

  /// Where [bone] goes in its parent when its driver is at [local] in its.
  Vector3 positionOf(int bone, Vector3 local) {
    final source = drivers[bone]!;
    return changeOf(bone, local - from.positionOf(source))
      ..add(to.positionOf(bone));
  }

  /// A move or a slope of the driver, as the same move of [bone]: carried
  /// through the world between the two parents, and made as much larger as
  /// the target is. Where the parents stand is not part of a move.
  Vector3 changeOf(int bone, Vector3 change) =>
      _across[bone]!.rotated3(change.clone()..scale(size));

  late final Map<int, Matrix4> _across = {
    for (final bone in drivers.keys) bone: _acrossFor(bone),
  };

  Matrix4 _acrossFor(int bone) {
    final stance = Matrix4.compose(
      Vector3.zero(),
      parentStanceOf(bone),
      Vector3.all(1),
    );
    return stance.multiplied(to.parentWorldOf(bone))
      ..invert()
      ..multiply(from.parentWorldOf(drivers[bone]!));
  }

  Quaternion _product(List<int> chain, Quaternion Function(int) turn) {
    var out = Quaternion.identity();
    for (final source in chain) {
      out = out * turn(source);
    }
    return out;
  }

  /// A bone of the target as it would stand with the source at rest.
  Quaternion _restWorld(int bone) => _stance[bone] * to.worldRotationOf(bone);

  RetargetLink _link(int bone) {
    final source = drivers[bone]!;
    final right = from.worldRotationOf(source).inverted() * _restWorld(bone);
    final pivot = _pivot(bone);
    final up = _pathTo(source);
    if (pivot == null) {
      // Nothing above drives, so the parent stays where it rests.
      return RetargetLink(
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
    final offset = from.worldRotationOf(held).inverted() * _restWorld(pivot);
    return RetargetLink(
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

  /// Where [bone] goes in scale when its driver is at [scale].
  Vector3 scaleOf(int bone, Vector3 scale) {
    final rest = from.scaleOf(drivers[bone]!);
    final mine = to.scaleOf(bone);
    double per(double value, double at) => at.abs() < 1e-12 ? 1 : value / at;
    return Vector3(
      mine.x * per(scale.x, rest.x),
      mine.y * per(scale.y, rest.y),
      mine.z * per(scale.z, rest.z),
    );
  }

  // Rest fixing.

  late final List<List<int>> _children = () {
    final out = List.generate(to.length, (_) => <int>[]);
    for (var bone = 0; bone < to.length; bone++) {
      final parent = to.parents[bone];
      if (parent >= 0) out[parent].add(bone);
    }
    return out;
  }();

  /// Each bone's rotation in the world, once the driven ones are turned to
  /// point the way their drivers do. Parents come first, and a bone with
  /// nothing to point at goes where its parent takes it.
  List<Quaternion> _stances() {
    final out = <Quaternion>[];
    for (var bone = 0; bone < to.length; bone++) {
      final parent = to.parents[bone];
      final inherited = parent < 0 ? Quaternion.identity() : out[parent];
      out.add(_swing(bone, inherited) * inherited);
    }
    return out;
  }

  /// The turn that points [bone] where its driver points, from where its
  /// parent has left it. Identity where there is one limb to follow no more
  /// than one way, which is why a hip or a shoulder girdle is left alone.
  Quaternion _swing(int bone, Quaternion inherited) {
    final aim = _aimOf(bone);
    if (aim == null) return Quaternion.identity();
    final mine = inherited.asRotationMatrix().transformed(aim.mine);
    final theirs = aim.theirs;
    if (mine.length < 1e-9 || theirs.length < 1e-9) {
      return Quaternion.identity();
    }
    mine.normalize();
    theirs.normalize();
    if (mine.dot(theirs) < -0.7) {
      _problems.add(
        '"${to.names[bone]}" points more than 134 degrees from the bone that '
        'drives it at rest, so its rest was left as it is.',
      );
      return Quaternion.identity();
    }
    return Quaternion.fromTwoVectors(mine, theirs);
  }

  /// Where [bone] points at rest, and where its driver does, or null when
  /// there is no one bone below it that is driven by a bone below its driver.
  ({Vector3 mine, Vector3 theirs})? _aimOf(int bone) {
    final source = drivers[bone];
    if (source == null) return null;
    final below = _drivenBelow(bone);
    if (below.length != 1) return null;
    final far = drivers[below.single]!;
    if (!_isBelow(far, source)) return null;
    return (
      mine: to.originOf(below.single) - to.originOf(bone),
      theirs: from.originOf(far) - from.originOf(source),
    );
  }

  /// The nearest driven bone on each branch below [bone].
  List<int> _drivenBelow(int bone) {
    final found = <int>[];
    void visit(int at) {
      for (final kid in _children[at]) {
        if (drivers.containsKey(kid)) {
          found.add(kid);
        } else {
          visit(kid);
        }
      }
    }

    visit(bone);
    return found;
  }

  bool _isBelow(int bone, int ancestor) {
    for (var up = from.parents[bone]; up >= 0; up = from.parents[up]) {
      if (up == ancestor) return true;
    }
    return false;
  }
}
