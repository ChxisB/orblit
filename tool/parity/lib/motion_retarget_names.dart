import 'package:orblit_motion/orblit_motion.dart';
// ignore: implementation_imports, cleanName is not part of the public API
import 'package:orblit_motion/src/bone_names.dart' show cleanName;
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:vector_math/vector_math_64.dart';

typedef _Naming = String Function(HumanoidBone slot, {bool end});

typedef _Place = (Vector3, Quaternion, Vector3);

/// A skeleton built bone by bone, with each bone's place in its parent.
final class _Body {
  _Body({this.above});

  final List<String> names = [];
  final List<int> parents = [];
  final List<Vector3> positions = [];
  final List<Quaternion> turns = [];
  final _Place? above;

  int add(String name, int parent, Vector3 position, [Quaternion? turn]) {
    names.add(name);
    parents.add(parent);
    positions.add(position);
    turns.add(turn ?? Quaternion.identity());
    return names.length - 1;
  }

  /// This skeleton with one more bone, under [parent] or as a root.
  _Body plus(String name, String? parent, Vector3 position) {
    final out = _copy();
    final at = parent == null ? -1 : names.indexOf(parent);
    if (parent != null && at < 0) throw StateError('No bone "$parent".');
    out.add(name, at, position);
    return out;
  }

  /// This skeleton with [name] standing at [position].
  _Body placed(String name, Vector3 position) {
    final out = _copy();
    out.positions[names.indexOf(name)] = position;
    return out;
  }

  _Body mapped(Vector3 Function(Vector3 position) move, {_Place? above}) {
    final out = _copy(above: above);
    for (var i = 0; i < out.positions.length; i++) {
      out.positions[i] = move(out.positions[i]);
    }
    return out;
  }

  _Body renamed(String Function(String name) rename) {
    final out = _Body(above: above);
    for (var i = 0; i < names.length; i++) {
      out.add(rename(names[i]), parents[i], positions[i].clone(), turns[i]);
    }
    return out;
  }

  /// Without the bones in [roots] and everything that hangs from them.
  _Body without(Set<String> roots) => _keeping((i) => !_hangsFrom(i, roots));

  /// Without the bones in [gone]. What hung from one hangs from the next
  /// bone up.
  _Body skipping(Set<String> gone) => _keeping((i) => !gone.contains(names[i]));

  Map<String, Object?> get spec {
    final lifted = above;
    return {
      'names': names,
      'parents': parents,
      'local': [
        for (var i = 0; i < names.length; i++)
          partDump(positions[i], turns[i], Vector3.all(1)),
      ],
      'above': lifted == null
          ? null
          : partDump(lifted.$1, lifted.$2, lifted.$3),
    };
  }

  _Body _copy({_Place? above}) {
    final out = _Body(above: above ?? this.above);
    for (var i = 0; i < names.length; i++) {
      out.add(names[i], parents[i], positions[i].clone(), turns[i].clone());
    }
    return out;
  }

  bool _hangsFrom(int index, Set<String> roots) {
    for (var at = index; at >= 0; at = parents[at]) {
      if (roots.contains(names[at])) return true;
    }
    return false;
  }

  _Body _keeping(bool Function(int index) keep) {
    final out = _Body(above: above);
    final placedAt = <int, int>{};
    for (var i = 0; i < names.length; i++) {
      if (!keep(i)) continue;
      var parent = parents[i];
      while (parent >= 0 && !keep(parent)) {
        parent = parents[parent];
      }
      placedAt[i] = out.add(
        names[i],
        parent < 0 ? -1 : placedAt[parent]!,
        positions[i].clone(),
        turns[i].clone(),
      );
    }
    return out;
  }
}

/// A person and which bone really is each part of the body.
final class _Person {
  _Person(this.body, this.slots);

  final _Body body;
  final Map<HumanoidBone, String> slots;

  String operator [](HumanoidBone slot) => slots[slot]!;
}

/// Builds the person the animation tests stand up, in Y up facing +Z so
/// their left is +X.
final class _Builder {
  _Builder(this.naming, this.height);

  final _Naming naming;
  final double height;
  final _Body body = _Body();
  final Map<HumanoidBone, String> slots = {};

  int add(
    HumanoidBone slot,
    int parent,
    double x,
    double y,
    double z, {
    bool end = false,
    double turn = 0,
  }) {
    final given = naming(slot, end: end);
    final label = given.isEmpty ? 'bone_${body.names.length}' : given;
    if (!end) slots[slot] = label;
    return body.add(
      label,
      parent,
      Vector3(x, y, z) * height,
      Quaternion.axisAngle(Vector3(0, 0, 1), turn),
    );
  }

  void arm({
    required bool left,
    required int top,
    required double armsDown,
    required bool shoulders,
    required bool endSites,
  }) {
    final s = left ? 1.0 : -1.0;
    HumanoidBone pick(HumanoidBone l, HumanoidBone r) => left ? l : r;
    var at = top;
    if (shoulders) {
      at = add(
        pick(HumanoidBone.leftShoulder, HumanoidBone.rightShoulder),
        at,
        s * 0.03,
        0.05,
        0,
      );
    }
    at = add(
      pick(HumanoidBone.leftUpperArm, HumanoidBone.rightUpperArm),
      at,
      s * (shoulders ? 0.07 : 0.1),
      shoulders ? 0 : 0.05,
      0,
      turn: -s * armsDown,
    );
    at = add(
      pick(HumanoidBone.leftLowerArm, HumanoidBone.rightLowerArm),
      at,
      s * 0.17,
      0,
      0,
    );
    final hand = pick(HumanoidBone.leftHand, HumanoidBone.rightHand);
    at = add(hand, at, s * 0.14, 0, 0);
    if (endSites) add(hand, at, s * 0.05, 0, 0, end: true);
  }

  void leg({required bool left, required int hips, required bool endSites}) {
    final s = left ? 1.0 : -1.0;
    HumanoidBone pick(HumanoidBone l, HumanoidBone r) => left ? l : r;
    var at = add(
      pick(HumanoidBone.leftUpperLeg, HumanoidBone.rightUpperLeg),
      hips,
      s * 0.05,
      -0.03,
      0,
    );
    at = add(
      pick(HumanoidBone.leftLowerLeg, HumanoidBone.rightLowerLeg),
      at,
      0,
      -0.24,
      0,
    );
    at = add(
      pick(HumanoidBone.leftFoot, HumanoidBone.rightFoot),
      at,
      0,
      -0.24,
      0,
    );
    final toes = pick(HumanoidBone.leftToes, HumanoidBone.rightToes);
    at = add(toes, at, 0, -0.035, 0.07);
    if (endSites) add(toes, at, 0, 0, 0.03, end: true);
  }
}

const _spineSlots = [
  HumanoidBone.spine,
  HumanoidBone.chest,
  HumanoidBone.upperChest,
];

/// A person [height] tall with arms [armsDown] radians below level.
///
/// [spine] is how many bones lie between hips and neck. [rightFirst] makes
/// the right arm and leg before the left. [rooted] hangs the hips from an
/// armature bone.
_Person _person(
  _Naming naming, {
  double height = 1.8,
  double armsDown = 0,
  int spine = 2,
  bool shoulders = true,
  bool rightFirst = false,
  bool endSites = false,
  bool rooted = false,
}) {
  final b = _Builder(naming, height);
  final root = rooted ? b.body.add('Armature', -1, Vector3.zero()) : -1;
  final hips = b.add(HumanoidBone.hips, root, 0, 0.55, 0);
  var top = hips;
  for (final slot in _spineSlots.take(spine)) {
    top = b.add(slot, top, 0, 0.25 / spine, 0);
  }
  final neck = b.add(HumanoidBone.neck, top, 0, 0.07, 0);
  final head = b.add(HumanoidBone.head, neck, 0, 0.06, 0);
  if (endSites) b.add(HumanoidBone.head, head, 0, 0.07, 0, end: true);
  for (final left in rightFirst ? [false, true] : [true, false]) {
    b.arm(
      left: left,
      top: top,
      armsDown: armsDown,
      shoulders: shoulders,
      endSites: endSites,
    );
    b.leg(left: left, hips: hips, endSites: endSites);
  }
  return _Person(b.body, b.slots);
}

String _plain(HumanoidBone slot, {bool end = false}) => '';

/// A naming scheme from a word for each part of the body and a way to write
/// the side after it.
_Naming _scheme(
  Map<String, String> words,
  String Function(String word, String? side) join, {
  String endMark = '_end',
}) => (slot, {end = false}) {
  final name = slot.name;
  final left = name.startsWith('left');
  final side = left
      ? 'L'
      : name.startsWith('right')
      ? 'R'
      : null;
  final rest = side == null ? name : name.substring(left ? 4 : 5);
  final key = side == null
      ? rest
      : '${rest[0].toLowerCase()}${rest.substring(1)}';
  final base = join(words[key]!, side);
  return end ? '$base$endMark' : base;
};

final Map<String, _Naming> _schemes = {
  'plain': _plain,
  'mannequin': _scheme({
    'hips': 'pelvis',
    'spine': 'spine_01',
    'chest': 'spine_02',
    'upperChest': 'spine_03',
    'neck': 'neck_01',
    'head': 'head',
    'shoulder': 'clavicle',
    'upperArm': 'upperarm',
    'lowerArm': 'lowerarm',
    'hand': 'hand',
    'upperLeg': 'thigh',
    'lowerLeg': 'calf',
    'foot': 'foot',
    'toes': 'ball',
  }, (word, side) => side == null ? word : '${word}_${side.toLowerCase()}'),
  'capture': _scheme(
    {
      'hips': 'Hips',
      'spine': 'Spine',
      'chest': 'Spine1',
      'upperChest': 'Spine2',
      'neck': 'Neck',
      'head': 'Head',
      'shoulder': 'Shoulder',
      'upperArm': 'Arm',
      'lowerArm': 'ForeArm',
      'hand': 'Hand',
      'upperLeg': 'UpLeg',
      'lowerLeg': 'Leg',
      'foot': 'Foot',
      'toes': 'ToeBase',
    },
    (word, side) =>
        'mixamorig:${side == null
            ? ''
            : side == 'L'
            ? 'Left'
            : 'Right'}$word',
    endMark: '_End',
  ),
  'blender': _scheme(_blenderWords, _blender),
  'def': _scheme(_blenderWords, (word, side) => 'DEF-${_blender(word, side)}'),
  'bip': _scheme(
    {
      'hips': 'Pelvis',
      'spine': 'Spine',
      'chest': 'Spine1',
      'upperChest': 'Spine2',
      'neck': 'Neck',
      'head': 'Head',
      'shoulder': 'Clavicle',
      'upperArm': 'UpperArm',
      'lowerArm': 'Forearm',
      'hand': 'Hand',
      'upperLeg': 'Thigh',
      'lowerLeg': 'Calf',
      'foot': 'Foot',
      'toes': 'Toe0',
    },
    (word, side) => side == null ? 'Bip01 $word' : 'Bip01 $side $word',
    endMark: ' Nub',
  ),
  'joint': _scheme(
    {
      'hips': 'hips',
      'spine': 'spine',
      'chest': 'chest',
      'upperChest': 'upperchest',
      'neck': 'neck',
      'head': 'head',
      'shoulder': 'shoulder',
      'upperArm': 'arm',
      'lowerArm': 'forearm',
      'hand': 'hand',
      'upperLeg': 'leg',
      'lowerLeg': 'knee',
      'foot': 'foot',
      'toes': 'toe',
    },
    (word, side) => side == null
        ? 'Skeleton_${word}_joint'
        : 'Skeleton_${word}_joint_${side}__4_',
  ),
};

const _blenderWords = {
  'hips': 'hips',
  'spine': 'spine',
  'chest': 'chest',
  'upperChest': 'upper_chest',
  'neck': 'neck',
  'head': 'head',
  'shoulder': 'shoulder',
  'upperArm': 'upper_arm',
  'lowerArm': 'forearm',
  'hand': 'hand',
  'upperLeg': 'thigh',
  'lowerLeg': 'shin',
  'foot': 'foot',
  'toes': 'toe',
};

String _blender(String word, String? side) =>
    side == null ? word : '$word.$side';

/// What [_person] is called with, besides its naming scheme: the spine, the
/// shoulders, which side comes first and whether the chains end in a site.
final List<(int, bool, bool, bool)> _shapes = [
  for (final spine in [1, 2, 3])
    for (final shoulders in [true, false])
      for (final rightFirst in [false, true])
        for (final endSites in [false, true])
          (spine, shoulders, rightFirst, endSites),
];

const _heights = [1.8, 180.0, 0.9];
const _armsDown = [0.0, 0.6, 0.35];

/// Every person, by name: each scheme as it comes, then in every shape at a
/// height and an arm angle that go round.
Map<String, _Body> _people() {
  final out = <String, _Body>{};
  var count = 0;
  for (final MapEntry(key: scheme, value: naming) in _schemes.entries) {
    out[scheme] = _person(naming).body;
    for (final (spine, shoulders, rightFirst, endSites) in _shapes) {
      final tags =
          's$spine${shoulders ? '' : '.bare'}'
          '${rightFirst ? '.rf' : ''}${endSites ? '.end' : ''}';
      out['$scheme.$tags'] = _person(
        naming,
        height: _heights[count % 3],
        armsDown: _armsDown[count % 3],
        spine: spine,
        shoulders: shoulders,
        rightFirst: rightFirst,
        endSites: endSites,
      ).body;
      count++;
    }
  }
  return out;
}

Vector3 _at(double x, double y, double z) => Vector3(x, y, z) * 1.8;

HumanoidBone _armRoot(_Person p, {required bool left}) {
  final shoulder = left
      ? HumanoidBone.leftShoulder
      : HumanoidBone.rightShoulder;
  final upper = left ? HumanoidBone.leftUpperArm : HumanoidBone.rightUpperArm;
  return p.slots.containsKey(shoulder) ? shoulder : upper;
}

/// The bone the neck hangs from, which is where the arms leave.
String _spineTop(_Person p) =>
    p.body.names[p.body.parents[p.body.names.indexOf(p[HumanoidBone.neck])]];

_Place _upright(double scale) => (
  Vector3.zero(),
  Quaternion.axisAngle(Vector3(1, 0, 0), 3.141592653589793 / 2),
  Vector3.all(scale),
);

/// Changes that break a person in one way each, to reach the branches of
/// the finder that say what they could not place.
final Map<String, _Body Function(_Person p)> _edits = {
  'noLegs': (p) => p.body.without({
    p[HumanoidBone.leftUpperLeg],
    p[HumanoidBone.rightUpperLeg],
  }),
  'oneLeg': (p) => p.body.without({p[HumanoidBone.rightUpperLeg]}),
  'threeLegs': (p) => p.body
      .plus('legC1', p[HumanoidBone.hips], _at(0, -0.03, 0))
      .plus('legC2', 'legC1', _at(0, -0.24, 0))
      .plus('legC3', 'legC2', _at(0, -0.24, 0)),
  'sameSide': (p) =>
      p.body.placed(p[HumanoidBone.rightUpperLeg], _at(0.05, -0.03, 0)),
  'longLeg': (p) =>
      p.body.plus('legExtra', p[HumanoidBone.leftToes], _at(0, 0, 0.05)),
  'shortLeg': (p) => p.body.without({p[HumanoidBone.leftFoot]}),
  'longArm': (p) =>
      p.body.plus('armExtra', p[HumanoidBone.leftHand], _at(0.05, 0, 0)),
  'shortArm': (p) => p.body.without({p[HumanoidBone.leftLowerArm]}),
  'noArms': (p) =>
      p.body.without({p[_armRoot(p, left: true)], p[_armRoot(p, left: false)]}),
  'oneArm': (p) => p.body.without({p[_armRoot(p, left: false)]}),
  'threeArms': (p) => p.body
      .plus('armC1', _spineTop(p), _at(0, 0.05, 0.05))
      .plus('armC2', 'armC1', _at(0, 0, 0.17))
      .plus('armC3', 'armC2', _at(0, 0, 0.14)),
  'noHead': (p) => p.body.without({p[HumanoidBone.neck]}),
  'noNeck': (p) => p.body.skipping({p[HumanoidBone.neck]}),
  'topHead': (p) => p.body
      .skipping({p[HumanoidBone.neck]})
      .renamed((name) => name == p[HumanoidBone.head] ? '${name}_top' : name),
  'extraSpine': (p) =>
      p.body.plus('spineX', p[HumanoidBone.hips], _at(0, 0.3, 0)),
  'props': (p) => p.body
      .plus('sword', p[HumanoidBone.rightHand], _at(-0.1, 0, 0.05))
      .plus('blade', 'sword', _at(0, 0.8, 0))
      .plus('holster', p[HumanoidBone.hips], _at(0.3, 0, 0.1)),
  'fingers': (p) => p.body
      .plus('fingerA', p[HumanoidBone.leftHand], _at(0.03, 0, 0.02))
      .plus('fingerB', p[HumanoidBone.leftHand], _at(0.03, 0, -0.02))
      .plus('fingerC', p[HumanoidBone.rightHand], _at(-0.03, 0, 0.02))
      .plus('fingerD', p[HumanoidBone.rightHand], _at(-0.03, 0, -0.02)),
  'twoRoots': (p) => p.body
      .plus('extraRoot', null, _at(0, 1, 0))
      .plus('extraChild', 'extraRoot', _at(0, 1, 0)),
  'flat': (p) => p.body.mapped((v) => Vector3.zero()),
  'tiny': (p) => p.body.mapped((v) => v * 1e-7),
  'huge': (p) => p.body.mapped((v) => v * 1e7),
  'mirrored': (p) => p.body.mapped((v) => Vector3(-v.x, v.y, v.z)),
  'upsideDown': (p) => p.body.mapped((v) => Vector3(v.x, -v.y, v.z)),
  'zUp': (p) => p.body.mapped((v) => Vector3(v.x, -v.z, v.y)),
  'zUpAbove': (p) => p.body.mapped((v) => v, above: _upright(1)),
  'centimetres': (p) => p.body.mapped((v) => v * 100, above: _upright(0.01)),
};

/// An up and a forward to try as well as the default, for the edits that
/// stand a person in some other way.
final Map<String, List<(Vector3, Vector3)>> _stances = {
  'zUp': [
    (Vector3(0, 0, 1), Vector3(0, -1, 0)),
    (Vector3(0, 0, 5), Vector3(0, -3, 0)),
    (Vector3(0, 0, 1), Vector3(0, 0, 1)),
    (Vector3(0, 0, 0), Vector3(0, -1, 0)),
  ],
  'zUpAbove': [
    (Vector3(0, 0, 1), Vector3(0, -1, 0)),
    (Vector3(0, 1, 0), Vector3(0, 0, 1)),
  ],
  'centimetres': [(Vector3(0, 0, 1), Vector3(0, -1, 0))],
  'mirrored': [(Vector3(0, 1, 0), Vector3(0, 0, -1))],
  'upsideDown': [
    (Vector3(0, -1, 0), Vector3(0, 0, 1)),
    (Vector3(0, -1, 0), Vector3(0, 0, -1)),
  ],
};

/// Skeletons made of names alone, for the matching by name.
final Map<String, List<String>> _nameLists = {
  'n.a': [
    'root',
    'hand.L',
    'hand_l',
    'HAND.R',
    'spine',
    'Spine1',
    'x',
    'knee.L',
    'knee.R',
  ],
  'n.b': ['Root', 'hand.L', 'hand.R', 'spine_01', 'spine', 'x2', 'Knee_Left'],
  'n.c': [
    'mixamorig:Root',
    'mixamorig:LeftHand',
    'mixamorig:RightHand',
    'mixamorig:Spine',
    'mixamorig:Spine1',
    'Armature|knee.l',
    'Armature|knee.r',
  ],
  'n.empty': [],
};

/// Every skeleton the fixture holds, by name.
final Map<String, _Body> _bodies = _makeBodies();

Map<String, _Body> _makeBodies() {
  final out = _people();
  for (final MapEntry(:key, :value) in _nameLists.entries) {
    final body = _Body();
    for (final (i, name) in value.indexed) {
      body.add(name, i - 1, Vector3(0, 1, 0));
    }
    out[key] = body;
  }
  for (final base in ['plain', 'capture']) {
    final person = _person(_schemes[base]!);
    for (final MapEntry(:key, :value) in _edits.entries) {
      out['v.$base.$key'] = value(person);
    }
    out['v.$base.spine0'] = _person(_schemes[base]!, spine: 0).body;
    out['v.$base.rooted'] = _person(_schemes[base]!, rooted: true).body;
  }
  final mannequin = _person(_schemes['mannequin']!);
  out['v.mannequin.halfNamed'] = mannequin.body.renamed(
    (name) => name.endsWith('_r') ? name.substring(0, name.length - 2) : name,
  );
  out['v.empty'] = _Body();
  out['v.single'] = _Body()..add('only', -1, Vector3(2, 0, 0));
  out['capture.a60'] = _person(_schemes['capture']!, armsDown: 0.6).body;
  _addLimbs(out);
  _addEdges(out);
  return out;
}

/// Limbs that are not a person's, to reach with.
void _addLimbs(Map<String, _Body> out) {
  Vector3 up(double length) => Vector3(0, length, 0);
  final turn = Quaternion.axisAngle(Vector3(0, 0, 1), 0.4);
  out['rod'] = _Body()
    ..add('base', -1, Vector3(0, 0, 0), turn)
    ..add('a', 0, up(1))
    ..add('b', 1, up(1))
    ..add('c', 2, up(1));
  out['rod.above'] = out['rod']!.mapped(
    (v) => v,
    above: (
      Vector3(1, 2, 3),
      Quaternion.axisAngle(Vector3(1, 1, 0).normalized(), 0.9),
      Vector3.all(2),
    ),
  );
  out['bent'] = _Body()
    ..add(
      'a',
      -1,
      Vector3(0, 0, 0),
      Quaternion.axisAngle(Vector3(0, 1, 0), 0.7),
    )
    ..add(
      'b',
      0,
      Vector3(1, 0.5, 0),
      Quaternion.axisAngle(Vector3(1, 0, 0), 0.3),
    )
    ..add('c', 1, Vector3(0, 1, 0.5));
  out['scaled'] = out['bent']!.mapped(
    (v) => v * 100,
    above: (Vector3.zero(), Quaternion.identity(), Vector3.all(0.01)),
  );
  out['folded'] = _Body()
    ..add('a', -1, Vector3.zero())
    ..add('b', 0, up(1))
    ..add('c', 1, up(-1));
  out['zero'] = _Body()
    ..add('a', -1, Vector3.zero())
    ..add('b', 0, Vector3.zero())
    ..add('c', 1, up(1));
}

/// Bones that stand exactly on a number the finder or the reach compares
/// with, so that moving the comparison by one step shows.
void _addEdges(Map<String, _Body> out) {
  out['edge.span'] = _Body()
    ..add('a', -1, Vector3.zero())
    ..add('b', 0, Vector3(0, 1e-6, 0));
  out['edge.leg'] = _stub(hips: 0.5, footA: 0, footB: 0.2, crown: 1);
  out['edge.head'] = _stub(hips: 0.5, spine: 1 - 0.2, crown: 1);
  out['edge.head2'] = _stub(hips: 1, spine: 1.7, crown: 2);
  out['edge.sides'] = _stub(hips: 0.5, crown: 1, sideA: 1e-6, sideB: 0);
  out['edge.sides2'] = _stub(hips: 1, crown: 2, sideA: 1e-6, sideB: 0);
  out['edge.tiny'] = _Body()
    ..add('a', -1, Vector3.zero())
    ..add('b', 0, Vector3(1, 0, 0))
    ..add('c', 1, Vector3(-1, 1e-9, 0));
  out['edge.bend1'] = _Body()
    ..add('a', -1, Vector3.zero())
    ..add('b', 0, Vector3(1e-6, 0.5, 0))
    ..add('c', 1, Vector3(-1e-6, 0.5, 0));
  out['edge.bend2'] = _Body()
    ..add('a', -1, Vector3.zero())
    ..add('b', 0, Vector3(1e-6, 1, 0))
    ..add('c', 1, Vector3(-1e-6, 1, 0));
}

/// Hips with two one-bone legs standing at [footA] and [footB], a spine bone
/// when [spine] is given, and a bone at [crown] that sets the ceiling.
_Body _stub({
  required double hips,
  required double crown,
  double footA = 0,
  double footB = 0,
  double? spine,
  double sideA = 0.1,
  double sideB = -0.1,
}) {
  final body = _Body();
  final at = body.add('hips', -1, Vector3(0, hips, 0));
  body
    ..add('alpha', at, Vector3(sideA, footA - hips, 0))
    ..add('beta', at, Vector3(sideB, footB - hips, 0));
  if (spine != null) body.add('spine', at, Vector3(0, spine - hips, 0));
  return body..add('crown', -1, Vector3(0, crown, 0));
}

typedef _Limb = ({
  String skeleton,
  String root,
  String mid,
  String tip,
  String pose,
});

const List<_Limb> _limbs = [
  (
    skeleton: 'capture',
    root: 'mixamorig:LeftArm',
    mid: 'mixamorig:LeftForeArm',
    tip: 'mixamorig:LeftHand',
    pose: 'none',
  ),
  (
    skeleton: 'capture',
    root: 'mixamorig:LeftArm',
    mid: 'mixamorig:LeftForeArm',
    tip: 'mixamorig:LeftHand',
    pose: 'person',
  ),
  (
    skeleton: 'capture',
    root: 'mixamorig:RightUpLeg',
    mid: 'mixamorig:RightLeg',
    tip: 'mixamorig:RightFoot',
    pose: 'none',
  ),
  (
    skeleton: 'capture.a60',
    root: 'mixamorig:RightArm',
    mid: 'mixamorig:RightForeArm',
    tip: 'mixamorig:RightHand',
    pose: 'none',
  ),
  (
    skeleton: 'capture.a60',
    root: 'mixamorig:LeftUpLeg',
    mid: 'mixamorig:LeftLeg',
    tip: 'mixamorig:LeftFoot',
    pose: 'person',
  ),
  (skeleton: 'rod', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'rod', root: 'a', mid: 'b', tip: 'c', pose: 'rod'),
  (skeleton: 'rod.above', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'bent', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'scaled', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'folded', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'zero', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'edge.tiny', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'edge.bend1', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
  (skeleton: 'edge.bend2', root: 'a', mid: 'b', tip: 'c', pose: 'none'),
];

List<Object?> _bone(Vector3? p, Quaternion? q, Vector3? s) => [
  p == null ? null : vecDump(p),
  q == null ? null : quatDump(q),
  s == null ? null : vecDump(s),
];

Quaternion _about(double x, double y, double z, double radians) =>
    Quaternion.axisAngle(Vector3(x, y, z).normalized(), radians);

/// Poses that put some bones somewhere other than rest.
final Map<String, Map<String, List<Object?>>> _poses = {
  'none': {},
  'person': {
    'mixamorig:Hips': _bone(Vector3(0, 1.1, 0), null, null),
    'mixamorig:LeftArm': _bone(null, _about(0, 0, 1, 0.5), null),
    'mixamorig:LeftForeArm': _bone(null, _about(0, 1, 0, 0.9), null),
    'mixamorig:LeftUpLeg': _bone(null, _about(1, 0, 0, -0.4), null),
    'mixamorig:RightUpLeg': _bone(null, _about(1, 0, 0, 0.7), null),
    'mixamorig:RightLeg': _bone(null, _about(1, 0, 0, 0.8), null),
  },
  'rod': {
    'base': _bone(null, _about(0, 0, 1, -0.9), null),
    'a': _bone(null, _about(1, 0, 0, 0.3), null),
    'b': _bone(Vector3(0.2, 1, 0), _about(0, 1, 0, 0.6), null),
  },
};

/// The fixture's one library case: every skeleton and pose, by name.
List<Case> libraryCases() => [
  {
    'op': 'library',
    'in': <Object?>[],
    'expect': {
      'skeletons': {
        for (final MapEntry(:key, :value) in _bodies.entries) key: value.spec,
      },
      'poses': _poses,
    },
  },
];

final Map<String, RestSkeleton> _built = {};

RestSkeleton _skeletonNamed(Object? name) =>
    _built.putIfAbsent(name! as String, () => _skeleton(_bodies[name]!.spec));

RestSkeleton _skeleton(Map<String, Object?> spec) {
  final above = spec['above'];
  return RestSkeleton(
    names: [for (final n in spec['names']! as List<Object?>) n! as String],
    parents: [for (final p in spec['parents']! as List<Object?>) p! as int],
    local: [for (final one in spec['local']! as List<Object?>) _composed(one)],
    above: above == null ? null : _composed(above),
  );
}

Matrix4 _composed(Object? parts) {
  final [position, turn, scale] = parts! as List<Object?>;
  return Matrix4.compose(_vector(position), _turn(turn), _vector(scale));
}

Vector3 _vector(Object? raw) {
  final [x, y, z] = raw! as List<Object?>;
  return Vector3(
    (x! as num).toDouble(),
    (y! as num).toDouble(),
    (z! as num).toDouble(),
  );
}

Quaternion _turn(Object? raw) {
  final [x, y, z, w] = raw! as List<Object?>;
  return Quaternion(
    (x! as num).toDouble(),
    (y! as num).toDouble(),
    (z! as num).toDouble(),
    (w! as num).toDouble(),
  );
}

Vector3? _maybeVector(Object? raw) => raw == null ? null : _vector(raw);

/// Names as bones carry them, with the namespace, case, punctuation and side
/// the cleaning has to take off.
List<String> _tangledNames() => [
  '',
  'a',
  'Hips',
  ':',
  '|',
  'a::',
  '::a',
  'x|y:z',
  'mixamorig:Hips',
  'mixamorig:LeftHand',
  'mixamorig:RightFoot_End',
  'Armature|hand.L',
  'Armature|Hand.R',
  'hand.L',
  'hand.R',
  'hand_L',
  'hand_l',
  'hand_R',
  'hand-r',
  'hand-L',
  'HAND_R',
  'HAND.L',
  'LeftHand',
  'RightHand',
  'Left_Hand',
  'Right-Hand',
  'left hand',
  'right hand',
  'Hand Left',
  'Hand_Right',
  'hand_left',
  'hand-Right',
  'handLeft',
  'handRight',
  'lefthand',
  'Lefty',
  'leftover',
  'Rightful',
  'Leftleft',
  'left.L',
  'right.L',
  'Bip01 L Hand',
  'Bip01 R Hand',
  'Bip01 Spine',
  'bip01 l thigh',
  'DEF-upper_arm.L',
  'DEF-upper_arm.R',
  'ORG-hand.R',
  'def-hand.l',
  'l',
  'L',
  'r',
  'R',
  '_l',
  '_L',
  '.L',
  '.r',
  'L_',
  'left',
  'right',
  'Left',
  'Right',
  'left_',
  '_left',
  '-right',
  'hand.L.001',
  'arm.L.002',
  'spine.001',
  'spine_01',
  'Spine1',
  'thigh_l',
  'ball_r',
  'LeftToeBase',
  'LeftToe_End',
  'hand_l_end',
  'Skeleton_arm_joint_L__4_',
  'Skeleton_arm_joint_R__4_',
  'neck_01',
  'twist.L',
  'forearm_twist_r',
  'LeftUpLeg',
  'Left Up Leg',
  'a.b.c.L',
  'x_l_y',
  'hand_l\n',
  '\nhand_l',
  'left\n',
  'hand l',
  'hand l ',
  ' hand_l',
  'HAND',
  '123',
  'bone_12',
  '_',
  '--',
  'ÜpperÄrm',
  'hand_lé',
  'handé.L',
  'İstanbul',
  'ΣΑΣ',
  'hand_${String.fromCharCode(0x212A)}',
  'Left${String.fromCharCode(0x212A)}ey',
  'hand${String.fromCharCode(0x200B)}.L',
  'hand${String.fromCharCode(0xFEFF)}_r',
  '手.L',
  'l${String.fromCharCode(0x0131)}',
];

List<Case> cleanCases() => [
  for (final name in _tangledNames())
    caseOf('clean', [name], (input) {
      final (:base, :side) = cleanName(input[0]! as String);
      return {'base': base, 'side': side};
    }),
];

/// Every pair of skeletons that are named to be matched.
const _nameSets = [
  'n.a',
  'n.b',
  'n.c',
  'n.empty',
  'plain',
  'mannequin',
  'capture',
  'blender',
  'bip',
];

/// Pairs: the name sets with each other, and every person against two
/// fixed ones, once as the source and once as the target.
List<(String, String)> _pairs() => [
  for (final from in _nameSets)
    for (final to in _nameSets) (from, to),
  for (final name in _bodies.keys.where(
    (name) => !name.startsWith('n.') && !_nameSets.contains(name),
  )) ...[('capture', name), (name, 'mannequin')],
];

List<Object?> _pairList(Map<String, String> pairs) => [
  for (final MapEntry(:key, :value) in pairs.entries) [key, value],
];

List<Case> matchCases() => [
  for (final (from, to) in _pairs())
    caseOf(
      'match',
      [from, to],
      (input) => _pairList(
        matchBones(_skeletonNamed(input[0]), _skeletonNamed(input[1])),
      ),
    ),
];

List<Case> matchHumanoidCases() => [
  for (final (from, to) in _pairs())
    caseOf(
      'matchHumanoid',
      [from, to],
      (input) => _pairList(
        matchHumanoid(_skeletonNamed(input[0]), _skeletonNamed(input[1])),
      ),
    ),
];

Case _humanoidCase(String name, [(Vector3, Vector3)? stance]) => caseOf(
  'humanoid',
  [
    name,
    stance == null ? null : vecDump(stance.$1),
    stance == null ? null : vecDump(stance.$2),
  ],
  (input) {
    final found = humanoidOf(
      _skeletonNamed(input[0]),
      up: _maybeVector(input[1]),
      forward: _maybeVector(input[2]),
    );
    return {
      'bones': [
        for (final MapEntry(:key, :value) in found.bones.entries)
          [key.name, value],
      ],
      'problems': found.problems,
    };
  },
);

List<Case> humanoidCases() => [
  for (final name in _bodies.keys.where((name) => !name.startsWith('n.')))
    _humanoidCase(name),
  for (final base in ['plain', 'capture'])
    _humanoidCase(base, (Vector3(0, 1, 0), Vector3(0, 0, -1))),
  for (final MapEntry(:key, :value) in _stances.entries)
    for (final base in ['plain', 'capture'])
      for (final stance in value) _humanoidCase('v.$base.$key', stance),
];

/// Places to put a hand, as shares of the limb's reach in some directions.
const _shares = [0.5, 0.95, 1.0, 1.4];

List<Case> reachCases() => [
  for (final limb in _limbs) ..._reachesOf(limb),
  ..._refusedReaches(),
];

Case _reachCase(_Limb limb, Vector3 target, Vector3? pole) => caseOf(
  'reach',
  [
    limb.skeleton,
    limb.pose,
    limb.root,
    limb.mid,
    limb.tip,
    vecDump(target),
    pole == null ? null : vecDump(pole),
  ],
  (input) {
    final skeleton = _skeletonNamed(input[0]);
    final pose = {
      for (final MapEntry(:key, :value) in _poses[input[1]]!.entries)
        key: BoneLocal(
          position: _maybeVector(value[0]),
          rotation: value[1] == null ? null : _turn(value[1]),
          scale: _maybeVector(value[2]),
        ),
    };
    final reach = reachLimb(
      skeleton,
      pose,
      root: input[2]! as String,
      mid: input[3]! as String,
      tip: input[4]! as String,
      target: _vector(input[5]),
      pole: _maybeVector(input[6]),
    );
    return {
      'root': quatDump(reach.root),
      'mid': quatDump(reach.mid),
      'end': vecDump(reach.end),
      'reachable': reach.reachable,
    };
  },
);

/// Targets in every direction at some shares of the limb's reach, with no
/// pole and with one in front, and then a few poles that give no direction.
List<Case> _reachesOf(_Limb limb) {
  final skeleton = _skeletonNamed(limb.skeleton);
  final worlds = skeleton.posedWorlds({
    for (final MapEntry(:key, :value) in _poses[limb.pose]!.entries)
      key: BoneLocal(
        position: _maybeVector(value[0]),
        rotation: value[1] == null ? null : _turn(value[1]),
        scale: _maybeVector(value[2]),
      ),
  });
  Vector3 head(String bone) => worlds[skeleton.indexOf(bone)!].getTranslation();
  final root = head(limb.root);
  final along = head(limb.tip) - root;
  final reach =
      (head(limb.mid) - root).length + (head(limb.tip) - head(limb.mid)).length;
  final span = reach == 0 ? 1.0 : reach;
  final directions = [
    Vector3(1, 0, 0),
    Vector3(-1, 0, 0),
    Vector3(0, 1, 0),
    Vector3(0, -1, 0),
    Vector3(0, 0, 1),
    Vector3(0, 0, -1),
    Vector3(1, 1, 0).normalized(),
    Vector3(1, -1, 1).normalized(),
    if (along.length > 0) along.normalized(),
    if (along.length > 0) -along.normalized(),
  ];
  final front = root + Vector3(0, 0, span);
  return [
    _reachCase(limb, root, null),
    for (final share in _shares)
      for (final direction in directions)
        for (final pole in [null, front])
          _reachCase(limb, root + direction * (span * share), pole),
    for (final direction in directions)
      for (final pole in [root + Vector3(0, span, 0), root])
        _reachCase(limb, root + direction * (span * 0.7), pole),
  ];
}

/// Calls that name bones that are not a limb, which are all refused.
List<Case> _refusedReaches() {
  const tries = [
    ('nope', 'b', 'c', 'bent'),
    ('a', 'nope', 'c', 'bent'),
    ('a', 'b', 'nope', 'bent'),
    ('b', 'a', 'c', 'bent'),
    ('c', 'b', 'a', 'bent'),
    ('a', 'a', 'a', 'bent'),
    ('a', 'c', 'c', 'bent'),
    ('a', 'b', 'b', 'bent'),
    ('', '', '', 'bent'),
    ('a', 'b', 'c', 'v.empty'),
  ];
  return [
    for (final (root, mid, tip, skeleton) in tries)
      _reachCase(
        (skeleton: skeleton, root: root, mid: mid, tip: tip, pose: 'none'),
        Vector3(1, 1, 1),
        null,
      ),
  ];
}
