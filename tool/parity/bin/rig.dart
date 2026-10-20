import 'dart:convert';
import 'dart:math' as math;

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_rig/orblit_rig.dart';
import 'package:vector_math/vector_math_64.dart';

typedef Case = Map<String, Object?>;

// --- Reading a spec ---------------------------------------------------------
//
// Every case is written as JSON and read back before Dart runs it, so the
// answer Dart gave comes from the same text the C# port reads.

double d(Object? value) => (value as num).toDouble();

double orElse(Object? value, double fallback) =>
    value == null ? fallback : d(value);

List<Object?> list(Object? value) => value as List<Object?>;

Case map(Object? value) => value as Case;

List<String> strings(Object? value) => [
  for (final item in list(value)) item as String,
];

Vector3 v3(Object? json) {
  final p = list(json);
  return Vector3(d(p[0]), d(p[1]), d(p[2]));
}

Quaternion q4(Object? json) {
  final p = list(json);
  return Quaternion(d(p[0]), d(p[1]), d(p[2]), d(p[3]));
}

Matrix4 m16(Object? json) =>
    Matrix4.fromList([for (final item in list(json)) d(item)]);

Object vec(Vector3 v) => [number(v.x), number(v.y), number(v.z)];

Object quat(Quaternion q) => [
  number(q.x),
  number(q.y),
  number(q.z),
  number(q.w),
];

Object mat(Matrix4 m) => [for (final item in m.storage) number(item)];

Bone boneFrom(Object? json) {
  final b = list(json);
  return Bone(
    name: b[0] as String,
    head: v3(b[1]),
    tail: v3(b[2]),
    roll: d(b[3]),
    parent: b[4] as String?,
    connected: b[5] as bool,
    deform: b[6] as bool,
  );
}

Armature armatureFrom(Object? json) =>
    Armature([for (final b in list(json)) boneFrom(b)]);

void applyEdits(Pose pose, Object? edits) {
  for (final edit in list(edits ?? <Object?>[])) {
    final e = list(edit);
    final transform = pose[e[0] as String];
    if (e[1] != null) {
      transform.location = v3(e[1]);
    }
    if (e[2] != null) {
      transform.rotation = q4(e[2]);
    }
    if (e[3] != null) {
      transform.scale = v3(e[3]);
    }
  }
}

BoneConstraint constraintFrom(Case s) {
  final influence = orElse(s['influence'], 1);
  final property = s['influenceProperty'] as String?;
  final invertInfluence = s['invertInfluence'] == true;
  return switch (s['kind']) {
    'copyRotation' => CopyRotation(
      s['source'] as String,
      influence: influence,
      influenceProperty: property,
      invertInfluence: invertInfluence,
      invert: s['invert'] == true,
    ),
    'dampedTrack' => DampedTrack(
      target: s['target'] as String,
      axis: TrackAxis.values.byName(s['axis'] as String? ?? 'y'),
      influence: influence,
      influenceProperty: property,
      invertInfluence: invertInfluence,
    ),
    'limitRotation' => LimitRotation(
      maximumAngle: d(s['maximumAngle']),
      influence: influence,
      influenceProperty: property,
      invertInfluence: invertInfluence,
    ),
    'copyTransform' => CopyTransform(
      s['source'] as String,
      influence: influence,
      influenceProperty: property,
      invertInfluence: invertInfluence,
    ),
    'stretchTo' => StretchTo(
      target: s['target'] as String,
      restLength: d(s['restLength']),
      volume: orElse(s['volume'], 1),
      influence: influence,
      influenceProperty: property,
      invertInfluence: invertInfluence,
    ),
    _ => throw ArgumentError('no constraint ${s['kind']}'),
  };
}

IkChain chainFrom(Case s) => IkChain(
  root: s['root'] as String,
  mid: s['mid'] as String,
  target: s['target'] as String,
  pole: s['pole'] as String?,
  influence: orElse(s['influence'], 1),
  influenceProperty: s['influenceProperty'] as String?,
  stretch: orElse(s['stretch'], 0),
  stretchProperty: s['stretchProperty'] as String?,
);

/// A point in model space: fixed, the head of a bone, or nothing at all.
ModelPoint? pointFrom(Object? json) {
  if (json == null) {
    return null;
  }
  final s = map(json);
  if (s['none'] == true) {
    return (pose, frame) => null;
  }
  final at = s['at'];
  if (at != null) {
    final fixed = v3(at);
    return (pose, frame) => fixed.clone();
  }
  final head = s['head'] as String;
  return (pose, frame) => pose.headOf(head);
}

SpringCollider colliderFrom(Case s) {
  final bone = s['bone'] as String?;
  return switch (s['kind']) {
    'sphere' => SphereCollider(
      bone: bone,
      centre: v3(s['centre']),
      radius: d(s['radius']),
    ),
    'capsule' => CapsuleCollider(
      bone: bone,
      start: v3(s['start']),
      end: v3(s['end']),
      radius: d(s['radius']),
    ),
    'plane' => PlaneCollider(
      bone: bone,
      point: v3(s['point']),
      normal: v3(s['normal']),
    ),
    _ => throw ArgumentError('no collider ${s['kind']}'),
  };
}

/// The ground as a plane through (0, height, 0): a ray from [from] to [to]
/// that crosses it hits it.
GroundProbe probeFrom(Case s) {
  final height = d(s['height']);
  final normal = v3(s['normal']).normalized();
  final point = Vector3(0, height, 0);
  return (from, to) {
    final start = normal.dot(from - point);
    final end = normal.dot(to - point);
    if (start * end > 0 || start == end) {
      return null;
    }
    final along = start / (start - end);
    return GroundHit(point: from + (to - from) * along, normal: normal);
  };
}

Modifier modifierFrom(Case s) {
  final bone = s['bone'] as String?;
  return switch (s['kind']) {
    'twoBoneIk' => TwoBoneIk(
      root: s['root'] as String,
      mid: s['mid'] as String,
      target: pointFrom(s['target'])!,
      pole: pointFrom(s['pole']),
      stretch: orElse(s['stretch'], 0),
    ),
    'chainIk' => ChainIk(
      root: s['root'] as String,
      tip: s['tip'] as String,
      target: pointFrom(s['target'])!,
      iterations: s['iterations'] as int? ?? 12,
    ),
    'splineIk' => () {
      final controls = s['points'];
      final fixed = controls == null
          ? null
          : [for (final p in list(controls)) v3(p)];
      return SplineIk(
        root: s['root'] as String,
        tip: s['tip'] as String,
        points: (pose, frame) => fixed,
        detail: s['detail'] as int? ?? 12,
      );
    }(),
    'coneLimit' => ConeLimit(
      bone: bone!,
      swing: d(s['swing']),
      twist: orElse(s['twist'], math.pi),
    ),
    'hingeLimit' => HingeLimit(
      bone: bone!,
      min: d(s['min']),
      max: d(s['max']),
      axis: s['axis'] == null ? null : v3(s['axis']),
    ),
    'twistSpread' => TwistSpread(
      source: s['source'] as String,
      bones: strings(s['bones']),
      shares: s['shares'] == null
          ? null
          : [for (final share in list(s['shares'])) d(share)],
    ),
    'lookAt' => LookAt(
      bone: bone!,
      target: pointFrom(s['target'])!,
      axis: TrackAxis.values.byName(s['axis'] as String? ?? 'y'),
      limit: orElse(s['limit'], math.pi / 2),
      ease: orElse(s['ease'], 0),
    ),
    'speedLimit' => SpeedLimit(
      bones: strings(s['bones']),
      turnSpeed: s['turnSpeed'] == null ? null : d(s['turnSpeed']),
      moveSpeed: s['moveSpeed'] == null ? null : d(s['moveSpeed']),
    ),
    'boneAttachment' => BoneAttachment(
      bone: bone!,
      offset: s['offset'] == null ? null : m16(s['offset']),
    ),
    'constrain' => Constrain(
      bone: bone!,
      constraint: constraintFrom(map(s['constraint'])),
    ),
    'springBones' => SpringBones(
      bones: strings(s['bones']),
      stiffness: orElse(s['stiffness'], 80),
      drag: orElse(s['drag'], 6),
      gravity: s['gravity'] == null ? null : v3(s['gravity']),
      margin: orElse(s['margin'], 0),
      colliders: [
        for (final c in list(s['colliders'] ?? <Object?>[]))
          colliderFrom(map(c)),
      ],
      snapDistance: orElse(s['snapDistance'], 2),
    ),
    'footPlacement' => FootPlacement(
      legs: [
        for (final leg in list(s['legs']))
          FootLeg(
            thigh: map(leg)['thigh'] as String,
            shin: map(leg)['shin'] as String,
            foot: map(leg)['foot'] as String?,
            ankleHeight: orElse(map(leg)['ankleHeight'], 0),
            soleAxis: TrackAxis.values.byName(
              map(leg)['soleAxis'] as String? ?? 'negativeZ',
            ),
          ),
      ],
      probe: probeFrom(map(s['probe'])),
      pelvis: s['pelvis'] as String?,
      up: s['up'] == null ? null : v3(s['up']),
      rise: orElse(s['rise'], 0.5),
      depth: orElse(s['depth'], 0.5),
      ease: orElse(s['ease'], 0.1),
      maxTilt: orElse(s['maxTilt'], math.pi / 4),
      airborne: orElse(s['airborne'], 0.1),
    ),
    _ => throw ArgumentError('no modifier ${s['kind']}'),
  };
}

RigType rigTypeFrom(Case s) => switch (s['kind']) {
  'limb' => LimbRig(stretch: orElse(s['stretch'], 0)),
  'finger' => const FingerRig(),
  'spine' => const SpineRig(),
  'copy' => const CopyRig(),
  'jaw' =>
    s['maximumAngle'] == null
        ? const JawRig()
        : JawRig(maximumAngle: d(s['maximumAngle'])),
  'eye' =>
    s['distance'] == null ? const EyeRig() : EyeRig(distance: d(s['distance'])),
  'eyelid' =>
    s['follow'] == null
        ? EyelidRig(eye: s['eye'] as String)
        : EyelidRig(eye: s['eye'] as String, follow: d(s['follow'])),
  _ => throw ArgumentError('no rig type ${s['kind']}'),
};

// --- Writing a spec ---------------------------------------------------------

List<Object?> boneSpec(
  String name,
  List<double> head,
  List<double> tail, {
  double roll = 0,
  String? parent,
  bool connected = false,
  bool deform = true,
}) => [name, head, tail, roll, parent, connected, deform];

/// A run of bones, each starting where the one before ends.
List<List<Object?>> run(
  String prefix,
  List<double> from,
  List<double> step,
  int count, {
  String? parent,
  bool connectFirst = false,
  String suffix = '',
}) => [
  for (var i = 0; i < count; i++)
    boneSpec(
      '$prefix$i$suffix',
      [for (var k = 0; k < 3; k++) from[k] + step[k] * i],
      [for (var k = 0; k < 3; k++) from[k] + step[k] * (i + 1)],
      parent: i == 0 ? parent : '$prefix${i - 1}$suffix',
      connected: i > 0 || connectFirst,
    ),
];

List<List<Object?>> arm({String suffix = '.L', double side = 1}) => [
  boneSpec('upper_arm$suffix', [0.2 * side, 1.5, 0], [0.6 * side, 1.5, -0.1]),
  boneSpec(
    'forearm$suffix',
    [0.6 * side, 1.5, -0.1],
    [1.0 * side, 1.5, 0],
    parent: 'upper_arm$suffix',
    connected: true,
  ),
  boneSpec(
    'hand$suffix',
    [1.0 * side, 1.5, 0],
    [1.2 * side, 1.5, 0],
    parent: 'forearm$suffix',
    connected: true,
  ),
];

List<Object?> edit(
  String bone, {
  List<double>? at,
  List<double>? turn,
  List<double>? scale,
}) => [bone, at, turn, scale];

List<double> turnAbout(List<double> axis, double angle) {
  final q = Quaternion.axisAngle(Vector3(axis[0], axis[1], axis[2]), angle);
  return [q.x, q.y, q.z, q.w];
}

List<double> randomVec(math.Random random, double extent) => [
  for (var i = 0; i < 3; i++) (random.nextDouble() * 2 - 1) * extent,
];

List<double> randomTurn(math.Random random) {
  final axis = randomVec(random, 1);
  final length = math.sqrt(axis.fold(0.0, (sum, v) => sum + v * v));
  return turnAbout([
    for (final v in axis) v / length,
  ], (random.nextDouble() * 2 - 1) * math.pi);
}

/// The case as JSON text read back, then run: [run] sees what C# sees.
Case make(Case spec, Object? Function(Case) run) {
  final read = map(jsonDecode(jsonEncode(spec)));
  return {...read, 'expect': attempt(() => run(read))};
}

// --- bone --------------------------------------------------------------------

Object? boneDump(Case s) {
  final bone = boneFrom(s['bone']);
  return {
    'length': number(bone.length),
    'direction': vec(bone.direction),
    'orientation': quat(bone.orientation),
    'rest': mat(bone.restMatrix),
  };
}

List<Case> boneCases() {
  final random = math.Random(11);
  final bones = <List<Object?>>[
    boneSpec('up', [0, 0, 0], [0, 1, 0]),
    boneSpec('upRolled', [0, 0, 0], [0, 2, 0], roll: 1.2),
    boneSpec('down', [0, 0, 0], [0, -1, 0]),
    boneSpec('downRolled', [1, 2, 3], [1, 1, 3], roll: 0.7),
    boneSpec('nearlyDown', [0, 0, 0], [0, -1, 1e-4]),
    boneSpec('nearlyUp', [0, 0, 0], [1e-3, 1, 0]),
    boneSpec('barelyOffUp', [0, 0, 0], [2e-3, 1, 0]),
    boneSpec('zero', [1, 1, 1], [1, 1, 1], roll: 0.5),
    boneSpec('tiny', [0, 0, 0], [5e-10, 0, 0]),
    boneSpec('small', [0, 0, 0], [2e-9, 0, 0]),
    boneSpec('x', [0, 0, 0], [1, 0, 0]),
    boneSpec('negX', [0, 0, 0], [-1, 0, 0], roll: -0.4),
    boneSpec('z', [0, 0, 0], [0, 0, 3]),
    boneSpec('negZ', [0, 0, 0], [0, 0, -3], roll: 2),
    boneSpec('long', [-100, 50, 20], [400, -250, 90], roll: 3),
    for (var i = 0; i < 60; i++)
      boneSpec(
        'r$i',
        randomVec(random, 5),
        randomVec(random, 5),
        roll: i % 4 == 0 ? 0 : (random.nextDouble() * 2 - 1) * math.pi,
      ),
  ];
  return [
    for (final b in bones) make({'op': 'bone', 'bone': b}, boneDump),
  ];
}

// --- naming ------------------------------------------------------------------

BoneRole roleNamed(Object? name) => BoneRole.values.byName(name as String);

Side? sideNamed(Object? name) =>
    name == null ? null : Side.values.byName(name as String);

Object? namingDump(Case s) {
  final a = list(s['args']);
  return switch (s['fn']) {
    'unique' => BoneNaming.unique(strings(a[0])),
    'roleOf' => BoneNaming.roleOf(a[0] as String).name,
    'sideOf' => BoneNaming.sideOf(a[0] as String)?.name,
    'baseOf' => BoneNaming.baseOf(a[0] as String),
    'compose' => BoneNaming.compose(
      a[0] as String,
      role: roleNamed(a[1]),
      side: sideNamed(a[2]),
    ),
    'asRole' => BoneNaming.asRole(a[0] as String, roleNamed(a[1])),
    'mirror' => BoneNaming.mirror(a[0] as String),
    'areMirrors' => BoneNaming.areMirrors(a[0] as String, a[1] as String),
    _ => throw ArgumentError('no naming function ${s['fn']}'),
  };
}

List<Case> namingCases() {
  const names = [
    'arm',
    'arm.L',
    'arm.R',
    'arm.L.001',
    'arm.R.012',
    'arm.001',
    'arm.0001',
    'arm.01',
    'ORG-arm.L',
    'DEF-arm.R',
    'MCH-arm',
    'MCH-leg.R.003',
    'DEF-DEF-x.L',
    'ORG-',
    'ORG-.L',
    '.L',
    '.R.001',
    '.001',
    '',
    'arm.l',
    'arm.LL',
    'arm_ik.L',
    'x.L.R',
    'org-arm',
    'upper arm.L',
    'ORGarm.L',
    'ORG-DEF-arm',
  ];
  const roles = ['control', 'original', 'deform', 'mechanism'];
  final cases = <Case>[];
  void call(String fn, List<Object?> args) =>
      cases.add(make({'op': 'naming', 'fn': fn, 'args': args}, namingDump));

  for (final name in names) {
    call('roleOf', [name]);
    call('sideOf', [name]);
    call('baseOf', [name]);
    call('mirror', [name]);
    for (final role in roles) {
      call('asRole', [name, role]);
      for (final side in const [null, 'left', 'right']) {
        call('compose', [name, role, side]);
      }
    }
  }
  for (final a in names) {
    for (final b in const ['arm.L', 'arm.R', 'ORG-arm.R', 'arm', 'DEF-arm.L']) {
      call('areMirrors', [a, b]);
    }
  }
  const lists = [
    <String>[],
    ['a'],
    ['a', 'a'],
    ['a', 'a', 'a'],
    ['a', 'a.001', 'a'],
    ['a.001', 'a', 'a'],
    ['', ''],
    ['', 'joint 0'],
    ['joint 1', '', ''],
    ['b', 'a', 'b', 'a', 'b'],
    ['arm.L', 'arm.L', 'arm.L.001', 'arm.L'],
  ];
  for (final names in lists) {
    call('unique', [names]);
  }
  return cases;
}

// --- IK ----------------------------------------------------------------------

Object ikDump(IkSolution s) => {
  'joint': vec(s.joint),
  'end': vec(s.end),
  'reachable': s.reachable,
  'stretch': number(s.stretch),
};

Object? twoBoneDump(Case s) => ikDump(
  solveTwoBoneIk(
    root: v3(s['root']),
    pole: v3(s['pole']),
    target: v3(s['target']),
    upperLength: d(s['upper']),
    lowerLength: d(s['lower']),
    stretch: orElse(s['stretch'], 0),
  ),
);

List<Case> twoBoneCases() {
  final random = math.Random(21);
  Case at(
    List<double> target, {
    List<double> root = const [0, 0, 0],
    List<double> pole = const [0, 1, 2],
    double upper = 1,
    double lower = 1,
    double stretch = 0,
  }) => {
    'op': 'twoBoneIk',
    'root': root,
    'pole': pole,
    'target': target,
    'upper': upper,
    'lower': lower,
    'stretch': stretch,
  };

  final specs = <Case>[
    at([0, 1.5, 0]),
    at([1, 1, 0]),
    at([0, 0, 0]),
    at([1e-10, 0, 0]),
    at([0, 2, 0]),
    at([0, 1.9999, 0]),
    at([0, 1.99999, 0]),
    at([0, 3, 0]),
    at([0, 3, 0], stretch: 0.5),
    at([0, 3, 0], stretch: 1),
    at([0, 3, 0], stretch: 2),
    at([0, 2, 0], stretch: 1),
    at([0, 0.5, 0], upper: 1, lower: 2),
    at([0, 1, 0], upper: 1, lower: 2),
    at([0, 1.00001, 0], upper: 1, lower: 2),
    at([0, 0.2, 0]),
    at([0, 1.5, 0], pole: [0, 4, 0]),
    at([0, 1.5, 0], pole: [0, 0, 0]),
    at([1, 1.5, 0], pole: [0, 1.5, 0]),
    at([0, 1.5, 0], upper: 0),
    at([0, 1.5, 0], lower: -1),
    at([0, 1.5, 0], lower: 0),
    at([1 + 1e-4, 0, 0], upper: 2, lower: 1),
    at([3 - 1e-4, 0, 0], upper: 2, lower: 1),
    at([0, 1.5, 0], upper: 1e-5, lower: 1e-5),
    at([0.6, 0.6, 0.6], root: [1, 2, 3], upper: 2, lower: 3),
    at([5, 5, 5], root: [1, 2, 3], upper: 2, lower: 3, stretch: 0.3),
    for (var i = 0; i < 70; i++)
      at(
        randomVec(random, 3),
        root: randomVec(random, 1),
        pole: randomVec(random, 3),
        upper: 0.5 + random.nextDouble() * 1.5,
        lower: 0.5 + random.nextDouble() * 1.5,
        stretch: i % 3 == 0 ? random.nextDouble() : 0,
      ),
  ];
  return [for (final s in specs) make(s, twoBoneDump)];
}

Object? fabrikDump(Case s) => [
  for (final p in solveFabrik(
    points: [for (final p in list(s['points'])) v3(p)],
    target: v3(s['target']),
    iterations: s['iterations'] as int? ?? 12,
    tolerance: orElse(s['tolerance'], 1e-4),
  ))
    vec(p),
];

List<Case> fabrikCases() {
  final random = math.Random(31);
  List<List<double>> straight(int count, double gap) => [
    for (var i = 0; i < count; i++) [0, gap * i, 0],
  ];
  Case with_(
    List<List<double>> points,
    List<double> target, {
    int? iterations,
    double? tolerance,
  }) => {
    'op': 'fabrik',
    'points': points,
    'target': target,
    'iterations': ?iterations,
    'tolerance': ?tolerance,
  };

  final specs = <Case>[
    with_(straight(4, 1), [1, 1, 1]),
    with_(straight(4, 1), [0, 3, 0]),
    with_(straight(4, 1), [0, 5, 0]),
    with_(straight(4, 1), [0, 0, 0]),
    with_(straight(4, 1), [1e-7, 0, 0]),
    with_(straight(4, 1), [2, 0.5, 0], iterations: 1),
    with_(straight(4, 1), [2, 0.5, 0], iterations: 0),
    with_(straight(4, 1), [2, 0.5, 0], tolerance: 10),
    with_(straight(4, 1), [2, 0.5, 0], tolerance: 0),
    with_(straight(1, 1), [2, 0.5, 0]),
    with_(
      [
        [0, 0, 0],
        [1, 0, 0],
        [1, 1, 0],
      ],
      [0, 2, 0],
    ),
    with_(
      [
        [0, 0, 0],
        [1, 0, 0],
        [1, 1, 0],
      ],
      [1, 0.5, 0],
      tolerance: 0.5,
    ),
    with_(<List<double>>[], [2, 0.5, 0]),
    with_(
      [
        [0, 0, 0],
        [0, 0, 0],
        [0, 1, 0],
      ],
      [0.5, 0.5, 0],
    ),
    with_(
      [
        [0, 0, 0],
        [0, 1, 0],
        [0, 1, 0],
        [0, 2, 0],
      ],
      [1, 1, 0],
    ),
    for (var i = 0; i < 50; i++)
      with_(
        [
          for (var k = 0; k < 2 + i % 5; k++)
            [k * 0.5 + random.nextDouble() * 0.2, random.nextDouble(), 0],
        ],
        randomVec(random, 2.5),
        iterations: i % 4 == 0 ? 3 : null,
      ),
  ];
  return [for (final s in specs) make(s, fabrikDump)];
}

// --- curves and widgets -------------------------------------------------------

Object? curveDump(Case s) => [
  for (final p in sampleCurve([
    for (final p in list(s['controls'])) v3(p),
  ], s['detail'] as int))
    vec(p),
];

List<Case> curveCases() {
  final random = math.Random(41);
  Case with_(int count, int detail) => {
    'op': 'curve',
    'controls': [for (var i = 0; i < count; i++) randomVec(random, 2)],
    'detail': detail,
  };
  return [
    for (final count in const [0, 1, 2, 3, 6])
      for (final detail in const [-1, 0, 1, 3, 12])
        make(with_(count, detail), curveDump),
  ];
}

Object? widgetDump(Case s) => [
  for (final line in BoneWidget(
    shape: WidgetShape.values.byName(s['shape'] as String),
    size: orElse(s['size'], 1),
    offset: s['offset'] == null ? null : v3(s['offset']),
    roll: orElse(s['roll'], 0),
  ).outline(d(s['length'])))
    [for (final p in line) vec(p)],
];

List<Case> widgetCases() => [
  for (final shape in WidgetShape.values)
    for (final (size, offset, roll, length) in const [
      (1.0, null, 0.0, 1.0),
      (2.0, null, 0.0, 0.5),
      (1.0, [0.0, 1.0, 0.0], 0.0, 2.0),
      (0.8, [0.1, -0.2, 0.3], 1.1, 1.5),
      (1.0, null, -2.5, 3.0),
    ])
      make({
        'op': 'widget',
        'shape': shape.name,
        'size': size,
        'offset': offset,
        'roll': roll,
        'length': length,
      }, widgetDump),
];

// --- a pose ------------------------------------------------------------------

Object poseDump(Pose pose) => {
  'order': pose.evaluationOrder,
  'bones': [
    for (final b in pose.armature.bones)
      {
        'name': b.name,
        'base': mat(pose.baseOf(b.name)),
        'world': mat(pose.worldOf(b.name)),
        'head': vec(pose.headOf(b.name)),
        'tail': vec(pose.tailOf(b.name)),
        'skin': mat(pose.skinningOf(b.name)),
      },
  ],
};

Object? poseRun(Case s) {
  final pose = Pose(armatureFrom(s['bones']));
  final properties = map(s['properties'] ?? <String, Object?>{});
  for (final entry in properties.entries) {
    pose.setProperty(entry.key, d(entry.value));
  }
  for (final constraint in list(s['constraints'] ?? <Object?>[])) {
    final c = list(constraint);
    pose.constrain(c[0] as String, constraintFrom(map(c[1])));
  }
  for (final chain in list(s['chains'] ?? <Object?>[])) {
    pose.addIkChain(chainFrom(map(chain)));
  }
  applyEdits(pose, s['edits']);
  pose.evaluate();
  final first = poseDump(pose);

  final then = s['then'];
  if (then == null) {
    return {'first': first};
  }
  final t = map(then);
  applyEdits(pose, t['edits']);
  pose.refresh(from: t['from'] as String?);
  return {'first': first, 'second': poseDump(pose)};
}

List<Object?> randomEdits(math.Random random, List<String> bones) => [
  for (final bone in bones)
    edit(
      bone,
      at: randomVec(random, 0.5),
      turn: randomTurn(random),
      scale: random.nextBool()
          ? [
              0.6 + random.nextDouble(),
              0.6 + random.nextDouble(),
              0.6 + random.nextDouble(),
            ]
          : null,
    ),
];

List<Case> poseCases() {
  final random = math.Random(51);
  final chain = run('c', [0, 0, 0], [0, 1, 0], 4);
  final connected = run('k', [0, 0, 0], [0.3, 1, 0.1], 4, connectFirst: false);
  final spread = [
    boneSpec('hip', [0, 1, 0], [0, 1.5, 0], roll: 0.4),
    boneSpec('left', [0.2, 1, 0], [0.6, 0.6, 0], parent: 'hip', roll: -1),
    boneSpec('right', [-0.2, 1, 0], [-0.6, 0.6, 0], parent: 'hip'),
    boneSpec('foot', [0.6, 0.6, 0], [0.7, 0.6, 0.3], parent: 'left'),
  ];
  final withTargets = [
    ...arm(),
    boneSpec('goal', [0.8, 1.0, 0.5], [0.8, 1.2, 0.5]),
    boneSpec('pole', [0.6, 1.5, 1.5], [0.6, 1.7, 1.5]),
  ];
  final cases = <Case>[
    {'op': 'pose', 'bones': <Object?>[]},
    {'op': 'pose', 'bones': chain},
    {
      'op': 'pose',
      'bones': chain,
      'edits': [
        edit('c1', turn: turnAbout([0, 0, 1], 0.7)),
      ],
    },
    {
      'op': 'pose',
      'bones': connected,
      'edits': randomEdits(random, ['k0', 'k1', 'k2', 'k3']),
    },
    {
      'op': 'pose',
      'bones': spread,
      'edits': randomEdits(random, ['hip', 'left', 'foot']),
      'then': {
        'edits': [
          edit('left', turn: turnAbout([1, 0, 0], 0.5)),
        ],
        'from': 'left',
      },
    },
    {
      'op': 'pose',
      'bones': spread,
      'edits': randomEdits(random, ['hip', 'right']),
      'then': {
        'edits': [
          edit('hip', at: [0.2, 0.1, 0]),
        ],
        'from': null,
      },
    },
    {
      'op': 'pose',
      'bones': [
        boneSpec('a', [0, 0, 0], [0, 1, 0], parent: 'b'),
        boneSpec('b', [0, 0, 0], [0, 1, 0], parent: 'a'),
      ],
    },
    {
      'op': 'pose',
      'bones': [
        boneSpec('a', [0, 0, 0], [0, 1, 0], parent: 'missing'),
      ],
    },
    for (var i = 0; i < 12; i++)
      {
        'op': 'pose',
        'bones': i.isEven ? chain : spread,
        'edits': randomEdits(
          random,
          i.isEven
              ? ['c0', 'c1', 'c2', 'c3']
              : ['hip', 'left', 'right', 'foot'],
        ),
      },
  ];

  Case constrained(
    List<Object?> bones,
    List<List<Object?>> constraints, {
    Object? edits,
    Case? properties,
  }) => {
    'op': 'pose',
    'bones': bones,
    'constraints': constraints,
    'edits': edits,
    'properties': ?properties,
  };

  final sources = [
    boneSpec('target', [1, 0, 0], [1, 1, 0]),
    boneSpec('follower', [0, 0, 0], [0, 1, 0]),
  ];
  final targetEdit = [
    edit(
      'target',
      at: [0.3, 0.2, -0.5],
      turn: turnAbout([0, 1, 0], 1.1),
      scale: [1.4, 1.4, 1.4],
    ),
  ];
  for (final influence in const [1.0, 0.5, 0.0]) {
    for (final invert in const [false, true]) {
      cases.add(
        constrained(sources, [
          [
            'follower',
            {
              'kind': 'copyRotation',
              'source': 'target',
              'influence': influence,
              'invert': invert,
            },
          ],
        ], edits: targetEdit),
      );
    }
    cases.add(
      constrained(sources, [
        [
          'follower',
          {'kind': 'copyTransform', 'source': 'target', 'influence': influence},
        ],
      ], edits: targetEdit),
    );
    cases.add(
      constrained(
        sources,
        [
          [
            'follower',
            {
              'kind': 'limitRotation',
              'maximumAngle': 0.4,
              'influence': influence,
            },
          ],
        ],
        edits: [
          edit('follower', turn: turnAbout([1, 1, 0], 1.3)),
        ],
      ),
    );
  }
  for (final axis in TrackAxis.values) {
    cases.add(
      constrained(sources, [
        [
          'follower',
          {'kind': 'dampedTrack', 'target': 'target', 'axis': axis.name},
        ],
      ], edits: targetEdit),
    );
  }
  for (final (volume, influence) in const [
    (1.0, 1.0),
    (0.0, 1.0),
    (0.5, 0.6),
    (2.0, 1.0),
  ]) {
    cases.add(
      constrained(sources, [
        [
          'follower',
          {
            'kind': 'stretchTo',
            'target': 'target',
            'restLength': 1.0,
            'volume': volume,
            'influence': influence,
          },
        ],
      ], edits: targetEdit),
    );
  }
  cases
    ..add(
      constrained(
        sources,
        [
          [
            'follower',
            {'kind': 'stretchTo', 'target': 'target', 'restLength': 1.0},
          ],
        ],
        edits: [
          edit('target', at: [-1 + 1e-5, 0, 0]),
        ],
      ),
    )
    ..add(
      constrained(
        sources,
        [
          [
            'follower',
            {
              'kind': 'copyRotation',
              'source': 'target',
              'influenceProperty': 'blend',
              'invertInfluence': true,
            },
          ],
        ],
        edits: targetEdit,
        properties: {'blend': 0.25},
      ),
    )
    ..add(
      constrained(sources, [
        [
          'follower',
          {
            'kind': 'copyRotation',
            'source': 'target',
            'influenceProperty': 'blend',
            'influence': 0.75,
          },
        ],
      ], edits: targetEdit),
    )
    ..add(
      constrained(
        sources,
        [
          [
            'follower',
            {
              'kind': 'copyRotation',
              'source': 'target',
              'influenceProperty': 'blend',
            },
          ],
        ],
        edits: targetEdit,
        properties: {'blend': 7.0},
      ),
    )
    ..add(
      constrained(sources, [
        [
          'follower',
          {'kind': 'copyTransform', 'source': 'follower'},
        ],
      ]),
    )
    ..add(
      constrained(
        [
          boneSpec('a', [0, 0, 0], [0, 1, 0]),
          boneSpec('b', [1, 0, 0], [1, 1, 0]),
        ],
        [
          [
            'a',
            {'kind': 'copyTransform', 'source': 'b'},
          ],
          [
            'b',
            {'kind': 'copyTransform', 'source': 'a'},
          ],
        ],
      ),
    )
    ..add(
      constrained(
        [
          boneSpec('a', [0, 0, 0], [0, 1, 0]),
        ],
        [
          [
            'a',
            {'kind': 'copyRotation', 'source': 'ghost'},
          ],
        ],
      ),
    );

  Case chained({
    String? pole = 'pole',
    double influence = 1,
    String? influenceProperty,
    double stretch = 0,
    String? stretchProperty,
    required List<double> goal,
    Case? properties,
  }) => {
    'op': 'pose',
    'bones': withTargets,
    'chains': [
      {
        'root': 'upper_arm.L',
        'mid': 'forearm.L',
        'target': 'goal',
        'pole': pole,
        'influence': influence,
        'influenceProperty': influenceProperty,
        'stretch': stretch,
        'stretchProperty': stretchProperty,
      },
    ],
    'edits': [
      edit('goal', at: [goal[0] - 0.8, goal[1] - 1.0, goal[2] - 0.5]),
    ],
    'properties': ?properties,
  };

  for (final goal in const [
    [0.8, 1.0, 0.5],
    [0.5, 1.5, 0.3],
    [3.0, 1.5, 0.0],
    [0.0, 0.0, 0.0],
    [0.2, 1.5, 0.0],
  ]) {
    cases
      ..add(chained(goal: goal))
      ..add(chained(goal: goal, pole: null))
      ..add(chained(goal: goal, influence: 0.5))
      ..add(chained(goal: goal, stretch: 1))
      ..add(chained(goal: goal, stretch: 0.4));
  }
  cases
    ..add(
      chained(
        goal: [3.0, 1.5, 0.0],
        stretch: 1,
        stretchProperty: 'reach',
        properties: {'reach': 0.0},
      ),
    )
    ..add(
      chained(
        goal: [3.0, 1.5, 0.0],
        stretch: 0,
        stretchProperty: 'reach',
        properties: {'reach': 1.0},
      ),
    )
    ..add(
      chained(
        goal: [0.5, 1.5, 0.3],
        influence: 1,
        influenceProperty: 'follow',
        properties: {'follow': 0.3},
      ),
    )
    ..add(chained(goal: [0.5, 1.5, 0.3], influence: 3, stretch: -2));
  return [for (final s in cases) make(s, poseRun)];
}

// --- modifiers ---------------------------------------------------------------

Object? modifierRun(Case s) {
  final pose = Pose(armatureFrom(s['bones']));
  final properties = map(s['properties'] ?? <String, Object?>{});
  for (final entry in properties.entries) {
    pose.setProperty(entry.key, d(entry.value));
  }

  final stack = ModifierStack();
  final built = <Modifier>[];
  for (final slot in list(s['modifiers'])) {
    final m = map(slot);
    final modifier = modifierFrom(map(m['spec']));
    built.add(modifier);
    stack.add(
      m['name'] as String,
      modifier,
      influence: orElse(m['influence'], 1),
      influenceProperty: m['influenceProperty'] as String?,
    );
  }

  final watch = strings(s['watch']);
  final out = <Object?>[];
  for (final frame in list(s['frames'])) {
    final f = map(frame);
    for (var i = 0; i < (f['repeat'] as int? ?? 1); i++) {
      pose.reset();
      applyEdits(pose, f['edits']);
      pose.evaluate();
      stack.apply(
        pose,
        frame: ModifierFrame(
          dt: orElse(f['dt'], 0),
          modelToWorld: f['toWorld'] == null ? null : m16(f['toWorld']),
        ),
      );
    }
    out.add({
      'worlds': [for (final bone in watch) mat(pose.worldOf(bone))],
      'rest': [for (final bone in watch) pose[bone].isRest],
      'attachments': [
        for (final modifier in built)
          if (modifier is BoneAttachment)
            {
              'model': modifier.model == null ? null : mat(modifier.model!),
              'world': modifier.world == null ? null : mat(modifier.world!),
            },
      ],
    });
  }
  return out;
}

Case stacked(
  List<Object?> bones,
  Case spec,
  List<Case> frames, {
  required List<String> watch,
  double influence = 1,
  String? influenceProperty,
  Case? properties,
  List<Case> more = const [],
}) => {
  'op': 'modifier',
  'bones': bones,
  'modifiers': [
    {
      'name': 'first',
      'influence': influence,
      'influenceProperty': influenceProperty,
      'spec': spec,
    },
    for (var i = 0; i < more.length; i++) {'name': 'more$i', 'spec': more[i]},
  ],
  'frames': frames,
  'watch': watch,
  'properties': ?properties,
};

Case frame({
  double dt = 0,
  List<Object?> edits = const [],
  List<double>? toWorld,
  int repeat = 1,
}) => {'dt': dt, 'edits': edits, 'toWorld': toWorld, 'repeat': repeat};

List<double> translation(double x, double y, double z) => [
  1, 0, 0, 0, //
  0, 1, 0, 0, //
  0, 0, 1, 0, //
  x, y, z, 1,
];

List<double> turnedAbout(double angle, double x, double y, double z) {
  final m = Matrix4.rotationY(angle)..setTranslationRaw(x, y, z);
  return m.storage.toList();
}

List<Case> modifierCases() {
  final random = math.Random(61);
  final limb = [
    ...arm(),
    boneSpec('goal', [0.8, 1.0, 0.5], [0.8, 1.2, 0.5]),
    boneSpec('pole', [0.6, 1.5, 1.5], [0.6, 1.7, 1.5]),
  ];
  final armWatch = ['upper_arm.L', 'forearm.L', 'hand.L'];
  List<Case> goals(List<List<double>> points) => [
    for (final p in points) frame(edits: [edit('goal', at: p)]),
  ];
  final cases = <Case>[];

  // two-bone IK
  for (final stretch in const [0.0, 0.5, 1.0]) {
    cases.add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'forearm.L',
          'target': {'head': 'goal'},
          'pole': {'head': 'pole'},
          'stretch': stretch,
        },
        goals(const [
          [0, 0, 0],
          [0.3, 0.2, -0.4],
          [1.5, 0.3, 0],
          [-0.6, -0.2, 0.2],
        ]),
        watch: armWatch,
      ),
    );
  }
  cases
    ..add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'forearm.L',
          'target': {
            'at': [0.7, 1.2, 0.4],
          },
        },
        [frame()],
        watch: armWatch,
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'forearm.L',
          'target': {'none': true},
        },
        [frame()],
        watch: armWatch,
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'forearm.L',
          'target': {'head': 'goal'},
          'pole': {'head': 'pole'},
        },
        goals(const [
          [0.3, 0.2, -0.4],
        ]),
        watch: armWatch,
        influence: 0.4,
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'forearm.L',
          'target': {'head': 'goal'},
        },
        [
          frame(
            toWorld: turnedAbout(0.8, 3, 0, -1),
            edits: [
              edit('goal', at: [0.2, 0.1, 0]),
            ],
          ),
          frame(toWorld: translation(0, 2, 0)),
        ],
        watch: armWatch,
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'hand.L',
          'target': {'head': 'goal'},
        },
        [frame()],
        watch: armWatch,
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'twoBoneIk',
          'root': 'upper_arm.L',
          'mid': 'forearm.L',
          'target': {'head': 'goal'},
        },
        [frame(toWorld: List<double>.filled(16, 0))],
        watch: armWatch,
      ),
    );

  // a chain
  final spine = [
    ...run('s', [0, 0, 0], [0, 0.5, 0], 5),
    boneSpec('goal', [0.5, 2.0, 0], [0.5, 2.2, 0]),
  ];
  final spineWatch = ['s0', 's1', 's2', 's3', 's4'];
  for (final iterations in const [12, 2, 1]) {
    cases.add(
      stacked(
        spine,
        {
          'kind': 'chainIk',
          'root': 's0',
          'tip': 's4',
          'target': {'head': 'goal'},
          'iterations': iterations,
        },
        goals(const [
          [0, 0, 0],
          [0.6, 0.3, 0.2],
          [3, 3, 0],
          [-0.5, -1.5, 0.3],
        ]),
        watch: spineWatch,
      ),
    );
  }
  cases
    ..add(
      stacked(
        spine,
        {
          'kind': 'chainIk',
          'root': 's1',
          'tip': 's3',
          'target': {'head': 'goal'},
        },
        goals(const [
          [0.4, 0.4, 0],
        ]),
        watch: spineWatch,
      ),
    )
    ..add(
      stacked(
        spine,
        {
          'kind': 'chainIk',
          'root': 's3',
          'tip': 's1',
          'target': {'head': 'goal'},
        },
        [frame()],
        watch: spineWatch,
      ),
    )
    ..add(
      stacked(
        spine,
        {
          'kind': 'chainIk',
          'root': 's0',
          'tip': 's4',
          'target': {'none': true},
        },
        [frame()],
        watch: spineWatch,
      ),
    );

  // a spline
  final tail = run('t', [0, 0, 0], [0.5, 0, 0], 5);
  for (final detail in const [12, 1, 4]) {
    cases.add(
      stacked(
        tail,
        {
          'kind': 'splineIk',
          'root': 't0',
          'tip': 't4',
          'points': [
            [0, 0, 0],
            [0.8, 0.6, 0],
            [1.6, 0.2, 0.5],
            [2.4, 1.0, 0.3],
          ],
          'detail': detail,
        },
        [frame()],
        watch: ['t0', 't1', 't2', 't3', 't4'],
      ),
    );
  }
  final unit = run('u', [0, 0, 0], [0, 1, 0], 2);
  cases
    ..add(
      stacked(
        unit,
        {
          'kind': 'splineIk',
          'root': 'u0',
          'tip': 'u1',
          'points': [
            [0, 0, 0],
            [0, 1, 0],
            [0, -2, 0],
          ],
          'detail': 1,
        },
        [frame()],
        watch: ['u0', 'u1'],
      ),
    )
    ..add(
      stacked(
        tail,
        {
          'kind': 'splineIk',
          'root': 't0',
          'tip': 't4',
          'points': [
            [0, 0, 0],
            [0.1, 0.1, 0],
          ],
        },
        [frame()],
        watch: ['t0', 't1', 't2', 't3', 't4'],
      ),
    )
    ..add(
      stacked(
        tail,
        {'kind': 'splineIk', 'root': 't0', 'tip': 't4', 'points': null},
        [frame()],
        watch: ['t0', 't4'],
      ),
    );

  // joint limits
  final joint = run('j', [0, 0, 0], [0, 1, 0], 3);
  for (final (swing, twist) in const [
    (0.3, 0.2),
    (0.0, 0.0),
    (1.0, math.pi),
    (0.5, 1.5),
  ]) {
    cases.add(
      stacked(
        joint,
        {'kind': 'coneLimit', 'bone': 'j1', 'swing': swing, 'twist': twist},
        [
          frame(
            edits: [
              edit('j1', turn: turnAbout([1, 0, 0], 0.9)),
            ],
          ),
          frame(
            edits: [
              edit('j1', turn: turnAbout([0, 1, 0], 1.4)),
            ],
          ),
          frame(
            edits: [
              edit('j1', turn: turnAbout([1, 1, 1], 2.2)),
            ],
          ),
          frame(
            edits: [
              edit('j1', turn: turnAbout([0, 0, 1], 0.1)),
            ],
          ),
          frame(
            edits: [
              edit('j1', turn: turnAbout([0, 1, 0], -2.0)),
            ],
          ),
        ],
        watch: ['j1', 'j2'],
      ),
    );
  }
  cases.add(
    stacked(
      joint,
      {'kind': 'coneLimit', 'bone': 'j1', 'swing': 0.4},
      [
        for (var i = 0; i < 8; i++)
          frame(edits: [edit('j1', turn: randomTurn(random))]),
      ],
      watch: ['j1', 'j2'],
    ),
  );
  for (final (min, max) in const [(-0.5, 0.5), (0.0, 1.0), (-1.0, -0.2)]) {
    for (final axis in const [
      null,
      [0.0, 0.0, 1.0],
      [1.0, 1.0, 0.0],
    ]) {
      cases.add(
        stacked(
          joint,
          {
            'kind': 'hingeLimit',
            'bone': 'j1',
            'min': min,
            'max': max,
            'axis': axis,
          },
          [
            frame(
              edits: [
                edit('j1', turn: turnAbout([1, 0, 0], 0.9)),
              ],
            ),
            frame(
              edits: [
                edit('j1', turn: turnAbout([0, 0, 1], -1.4)),
              ],
            ),
            frame(
              edits: [
                edit('j1', turn: turnAbout([1, 1, 0], 0.3)),
              ],
            ),
            frame(
              edits: [
                edit('j1', turn: turnAbout([0, 1, 1], 2.5)),
              ],
            ),
          ],
          watch: ['j1', 'j2'],
        ),
      );
    }
  }
  cases
    ..add(
      stacked(
        joint,
        {'kind': 'hingeLimit', 'bone': 'j1', 'min': 1.0, 'max': -1.0},
        [frame()],
        watch: ['j1'],
      ),
    )
    ..add(
      stacked(
        joint,
        {
          'kind': 'hingeLimit',
          'bone': 'j1',
          'min': -1.0,
          'max': 1.0,
          'axis': [0.0, 0.0, 0.0],
        },
        [frame()],
        watch: ['j1'],
      ),
    )
    ..add(
      stacked(
        joint,
        {'kind': 'coneLimit', 'bone': 'nowhere', 'swing': 1.0},
        [frame()],
        watch: ['j1'],
      ),
    );

  // twist
  final forearm = [
    boneSpec('upper', [0, 0, 0], [0, 1, 0]),
    ...run(
      'twist',
      [0, 1, 0],
      [0, 0.4, 0],
      3,
      parent: 'upper',
      connectFirst: true,
    ),
    boneSpec(
      'hand',
      [0, 2.2, 0],
      [0, 2.5, 0],
      parent: 'twist2',
      connected: true,
    ),
  ];
  for (final shares in const [
    null,
    [0.25, 0.5, 1.0],
    [1.0, 1.0, 1.0],
    [0.0, 0.5, -1.0],
  ]) {
    cases.add(
      stacked(
        forearm,
        {
          'kind': 'twistSpread',
          'source': 'hand',
          'bones': ['twist0', 'twist1', 'twist2'],
          'shares': shares,
        },
        [
          frame(
            edits: [
              edit('hand', turn: turnAbout([0, 1, 0], 1.2)),
            ],
          ),
          frame(
            edits: [
              edit('hand', turn: turnAbout([0, 1, 0], -2.4)),
            ],
          ),
          frame(
            edits: [
              edit('hand', turn: turnAbout([1, 1, 0], 1.0)),
            ],
          ),
          frame(),
        ],
        watch: ['twist0', 'twist1', 'twist2', 'hand'],
      ),
    );
  }
  cases.add(
    stacked(
      forearm,
      {
        'kind': 'twistSpread',
        'source': 'hand',
        'bones': ['twist0', 'twist1'],
        'shares': [0.5],
      },
      [frame()],
      watch: ['twist0'],
    ),
  );

  // look at
  final head = [
    ...run('neck', [0, 1.4, 0], [0, 0.15, 0], 2),
    boneSpec('goal', [0, 1.8, 2], [0, 2.0, 2]),
  ];
  for (final (axis, limit, ease) in const [
    ('y', math.pi / 2, 0.0),
    ('z', 0.5, 0.0),
    ('negativeZ', 3.0, 0.0),
    ('y', math.pi / 2, 0.2),
    ('x', 1.0, 0.05),
  ]) {
    cases.add(
      stacked(
        head,
        {
          'kind': 'lookAt',
          'bone': 'neck1',
          'target': {'head': 'goal'},
          'axis': axis,
          'limit': limit,
          'ease': ease,
        },
        [
          frame(
            dt: 1 / 60,
            edits: [
              edit('goal', at: [0, 0, 0]),
            ],
          ),
          frame(
            dt: 1 / 60,
            edits: [
              edit('goal', at: [2, 0, 0]),
            ],
          ),
          frame(
            dt: 1 / 60,
            repeat: 10,
            edits: [
              edit('goal', at: [2, 0.5, -3]),
            ],
          ),
          frame(
            dt: 0.5,
            edits: [
              edit('goal', at: [-1, 0, 0]),
            ],
          ),
          frame(
            edits: [
              edit('goal', at: [0, 0, -4]),
            ],
          ),
        ],
        watch: ['neck0', 'neck1'],
      ),
    );
  }
  cases
    ..add(
      stacked(
        head,
        {
          'kind': 'lookAt',
          'bone': 'neck1',
          'target': {'none': true},
        },
        [frame(dt: 0.1)],
        watch: ['neck1'],
      ),
    )
    ..add(
      stacked(
        head,
        {
          'kind': 'lookAt',
          'bone': 'neck1',
          'target': {
            'at': [0, 1.55, 0],
          },
        },
        [frame(dt: 0.1)],
        watch: ['neck1'],
      ),
    )
    ..add(
      stacked(
        head,
        {
          'kind': 'lookAt',
          'bone': 'neck1',
          'target': {'head': 'goal'},
          'ease': 0.3,
        },
        [frame(dt: 0.1, toWorld: turnedAbout(1.0, 1, 2, 3))],
        watch: ['neck1'],
        influence: 0.5,
      ),
    );

  // speed limits
  final limbs = [
    boneSpec('a', [0, 0, 0], [0, 1, 0]),
    boneSpec('b', [1, 0, 0], [1, 1, 0], parent: 'a'),
  ];
  for (final (turn, move) in const [
    (1.0, null),
    (null, 0.5),
    (2.0, 1.0),
    (0.0, 0.0),
  ]) {
    cases.add(
      stacked(
        limbs,
        {
          'kind': 'speedLimit',
          'bones': ['a', 'b'],
          'turnSpeed': turn,
          'moveSpeed': move,
        },
        [
          frame(dt: 1 / 60),
          frame(
            dt: 1 / 60,
            edits: [
              edit('a', turn: turnAbout([0, 0, 1], 1.5), at: [2, 0, 0]),
              edit('b', turn: turnAbout([1, 0, 0], -2.0), at: [0, 3, 0]),
            ],
          ),
          frame(
            dt: 0.5,
            edits: [
              edit('a', turn: turnAbout([0, 1, 0], 2.8), at: [-3, 1, 0]),
            ],
          ),
          frame(
            dt: 0,
            edits: [
              edit('a', turn: turnAbout([0, 1, 0], 0.3)),
            ],
          ),
          frame(
            dt: 1 / 30,
            repeat: 6,
            edits: [
              edit('b', turn: turnAbout([1, 1, 0], 1.9)),
            ],
          ),
        ],
        watch: ['a', 'b'],
      ),
    );
  }

  // attachments and constraints as modifiers
  cases
    ..add(
      stacked(
        limb,
        {
          'kind': 'boneAttachment',
          'bone': 'hand.L',
          'offset': translation(0.1, 0.2, 0.3),
        },
        [
          frame(
            edits: [
              edit('hand.L', turn: turnAbout([0, 0, 1], 0.4)),
            ],
          ),
          frame(toWorld: turnedAbout(0.6, 1, 2, 3)),
        ],
        watch: ['hand.L'],
      ),
    )
    ..add(
      stacked(
        limb,
        {'kind': 'boneAttachment', 'bone': 'hand.L'},
        [frame(toWorld: translation(5, 0, 0))],
        watch: ['hand.L'],
      ),
    )
    ..add(
      stacked(
        limb,
        {'kind': 'boneAttachment', 'bone': 'hand.L'},
        [frame(toWorld: translation(5, 0, 0))],
        watch: ['hand.L'],
        influence: 0,
      ),
    )
    ..add(
      stacked(
        limb,
        {'kind': 'boneAttachment', 'bone': 'elbow'},
        [frame()],
        watch: ['hand.L'],
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'constrain',
          'bone': 'forearm.L',
          'constraint': {'kind': 'copyRotation', 'source': 'goal'},
        },
        [
          frame(
            edits: [
              edit('goal', turn: turnAbout([0, 1, 1], 0.9)),
            ],
          ),
          frame(
            edits: [
              edit('goal', turn: turnAbout([1, 0, 1], -0.5)),
            ],
          ),
        ],
        watch: armWatch,
        influence: 0.7,
      ),
    )
    ..add(
      stacked(
        limb,
        {
          'kind': 'constrain',
          'bone': 'forearm.L',
          'constraint': {'kind': 'limitRotation', 'maximumAngle': 0.2},
        },
        [
          frame(
            edits: [
              edit('forearm.L', turn: turnAbout([0, 0, 1], 1.0)),
            ],
          ),
        ],
        watch: armWatch,
        influenceProperty: 'amount',
        properties: {'amount': 0.5},
      ),
    );

  // two modifiers in one stack
  cases.add(
    stacked(
      limb,
      {
        'kind': 'twoBoneIk',
        'root': 'upper_arm.L',
        'mid': 'forearm.L',
        'target': {'head': 'goal'},
        'pole': {'head': 'pole'},
      },
      goals(const [
        [0.3, 0.2, -0.4],
        [0.9, 0.1, 0.1],
      ]),
      watch: armWatch,
      more: [
        {'kind': 'coneLimit', 'bone': 'forearm.L', 'swing': 0.3},
      ],
    ),
  );

  // spring bones
  final hair = [
    boneSpec('anchor', [0, 0, 0], [1, 0, 0]),
    boneSpec('h0', [1, 0, 0], [2, 0, 0], parent: 'anchor', connected: true),
    boneSpec('h1', [2, 0, 0], [3, 0, 0], parent: 'h0', connected: true),
  ];
  Case spring({
    double stiffness = 0,
    double drag = 4,
    List<double>? gravity,
    double margin = 0,
    List<Case> colliders = const [],
    double snapDistance = 2,
    List<String> bones = const ['h0', 'h1'],
  }) => {
    'kind': 'springBones',
    'bones': bones,
    'stiffness': stiffness,
    'drag': drag,
    'gravity': gravity,
    'margin': margin,
    'colliders': colliders,
    'snapDistance': snapDistance,
  };
  const dt = 1 / 60;
  final hairWatch = ['h0', 'h1'];
  // The head moves by the snap distance to the last bit, so the comparison is on the bound.
  final snapProbe = Pose(armatureFrom(hair))..evaluate();
  final snapFrom = Vector3.copy(snapProbe.headOf('h0'));
  applyEdits(snapProbe, [
    edit('anchor', at: [1, 0, 0]),
  ]);
  snapProbe.evaluate();
  final snapStep = snapFrom.distanceTo(snapProbe.headOf('h0'));
  List<Case> hang({List<double>? at}) => [
    frame(
      dt: dt,
      edits: [
        edit('anchor', at: at ?? [0, 0, 0]),
      ],
    ),
    frame(
      dt: dt,
      repeat: 30,
      edits: [
        edit('anchor', at: at ?? [0, 0, 0]),
      ],
    ),
    frame(
      dt: dt,
      repeat: 200,
      edits: [
        edit('anchor', at: at ?? [0, 0, 0]),
      ],
    ),
  ];
  cases
    ..add(stacked(hair, spring(), hang(), watch: hairWatch))
    ..add(
      stacked(
        hair,
        spring(stiffness: 80, drag: 6),
        hang(at: [0, 0, 1]),
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(hair, spring(gravity: [0, 0, 0], stiffness: 40), [
        frame(
          dt: dt,
          edits: [
            edit('anchor', at: [0, 0, 0]),
          ],
        ),
        frame(
          dt: dt,
          edits: [
            edit('anchor', at: [0.5, 0, 0]),
          ],
        ),
        frame(
          dt: dt,
          repeat: 40,
          edits: [
            edit('anchor', at: [0.5, 0, 0]),
          ],
        ),
        frame(
          dt: dt,
          edits: [
            edit('anchor', at: [100, 0, 0]),
          ],
        ),
        frame(
          dt: 0,
          edits: [
            edit('anchor', at: [100, 0, 0]),
          ],
        ),
        frame(
          dt: 1,
          edits: [
            edit('anchor', at: [100.5, 0, 0]),
          ],
        ),
      ], watch: hairWatch),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'plane',
              'point': [0, -0.5, 0],
              'normal': [0.0, 1.0, 0.0],
            },
          ],
          margin: 0.1,
        ),
        hang(),
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'sphere',
              'centre': [1.3, -0.9, 0],
              'radius': 0.6,
            },
          ],
        ),
        hang(),
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'sphere',
              'centre': [1.3, -0.9, 0],
              'radius': 0.6,
            },
          ],
          margin: 0.15,
        ),
        hang(),
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(bones: ['h0'], stiffness: 20, snapDistance: snapStep),
        [
          frame(
            dt: dt,
            edits: [
              edit('anchor', at: [0, 0, 0]),
            ],
          ),
          frame(
            dt: dt,
            edits: [
              edit('anchor', at: [1, 0, 0]),
            ],
          ),
        ],
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'capsule',
              'start': [1.3, -0.9, -1],
              'end': [1.3, -0.9, 1],
              'radius': 0.6,
            },
          ],
        ),
        hang(),
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'sphere',
              'bone': 'anchor',
              'centre': [1.3, -0.9, 0],
              'radius': 0.6,
            },
          ],
        ),
        hang(at: [0, 0, 4]),
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(hair, spring(), [
        frame(
          dt: dt,
          edits: [
            edit('anchor', turn: turnAbout([0, 0, 1], 0.8)),
          ],
        ),
        frame(
          dt: dt,
          repeat: 60,
          edits: [
            edit('anchor', turn: turnAbout([0, 0, 1], 0.8)),
          ],
        ),
      ], watch: hairWatch),
    )
    ..add(
      stacked(hair, spring(bones: ['h1', 'h0']), [
        frame(dt: dt),
      ], watch: hairWatch),
    )
    ..add(
      stacked(hair, spring(bones: const []), [frame(dt: dt)], watch: hairWatch),
    )
    ..add(
      stacked(hair, spring(bones: ['h0', 'h0']), [
        frame(dt: dt, repeat: 3),
      ], watch: hairWatch),
    )
    ..add(
      stacked(hair, spring(bones: ['h0', 'h1', 'h0']), [
        frame(dt: dt, repeat: 3),
      ], watch: hairWatch),
    )
    ..add(
      stacked(hair, spring(stiffness: -1), [frame(dt: dt)], watch: hairWatch),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'sphere',
              'bone': 'skull',
              'centre': [0, 0, 0],
              'radius': 1.0,
            },
          ],
        ),
        [frame(dt: dt)],
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'sphere',
              'centre': [0, 0, 0],
              'radius': -1.0,
            },
          ],
        ),
        [frame(dt: dt)],
        watch: hairWatch,
      ),
    )
    ..add(
      stacked(
        hair,
        spring(
          colliders: [
            {
              'kind': 'plane',
              'point': [0, 0, 0],
              'normal': [0.0, 0.0, 0.0],
            },
          ],
        ),
        [frame(dt: dt)],
        watch: hairWatch,
      ),
    );

  // foot placement
  final body = [
    boneSpec('pelvis', [0, 1, 0], [0, 1.2, 0]),
    boneSpec('thigh.L', [0.15, 1, 0], [0.15, 0.5, 0.05], parent: 'pelvis'),
    boneSpec(
      'shin.L',
      [0.15, 0.5, 0.05],
      [0.15, 0.1, 0],
      parent: 'thigh.L',
      connected: true,
    ),
    boneSpec(
      'foot.L',
      [0.15, 0.1, 0],
      [0.15, 0.05, 0.2],
      parent: 'shin.L',
      connected: true,
    ),
    boneSpec('thigh.R', [-0.15, 1, 0], [-0.15, 0.5, 0.05], parent: 'pelvis'),
    boneSpec(
      'shin.R',
      [-0.15, 0.5, 0.05],
      [-0.15, 0.1, 0],
      parent: 'thigh.R',
      connected: true,
    ),
    boneSpec(
      'foot.R',
      [-0.15, 0.1, 0],
      [-0.15, 0.05, 0.2],
      parent: 'shin.R',
      connected: true,
    ),
  ];
  Case feet({
    Case? probe,
    String? pelvis = 'pelvis',
    double ease = 0.1,
    double rise = 0.5,
    double depth = 0.5,
    double maxTilt = math.pi / 4,
    double airborne = 0.1,
    bool withFoot = true,
    List<double>? up,
  }) => {
    'kind': 'footPlacement',
    'legs': [
      for (final side in const ['L', 'R'])
        {
          'thigh': 'thigh.$side',
          'shin': 'shin.$side',
          'foot': withFoot ? 'foot.$side' : null,
          'ankleHeight': 0.05,
        },
    ],
    'probe':
        probe ??
        {
          'height': 0.0,
          'normal': [0.0, 1.0, 0.0],
        },
    'pelvis': pelvis,
    'ease': ease,
    'rise': rise,
    'depth': depth,
    'maxTilt': maxTilt,
    'airborne': airborne,
    'up': up,
  };
  final footWatch = [
    'pelvis',
    'thigh.L',
    'shin.L',
    'foot.L',
    'thigh.R',
    'shin.R',
    'foot.R',
  ];
  for (final probe in <Case>[
    {
      'height': 0.0,
      'normal': [0.0, 1.0, 0.0],
    },
    {
      'height': 0.15,
      'normal': [0.0, 1.0, 0.0],
    },
    {
      'height': 0.1,
      'normal': [0.3, 1.0, 0.1],
    },
    {
      'height': -0.3,
      'normal': [0.0, 1.0, 0.0],
    },
    {
      'height': 0.2,
      'normal': [1.0, 0.2, 0.0],
    },
  ]) {
    cases.add(
      stacked(body, feet(probe: probe), [
        frame(dt: dt),
        frame(
          dt: dt,
          repeat: 5,
          edits: [
            edit('pelvis', at: [0, -0.1, 0]),
          ],
        ),
        frame(dt: dt, repeat: 60),
        frame(
          dt: 0.3,
          edits: [
            edit('pelvis', at: [0.2, 0.3, 0]),
          ],
        ),
      ], watch: footWatch),
    );
  }
  cases
    ..add(
      stacked(body, feet(withFoot: false, pelvis: null), [
        frame(dt: dt, repeat: 30),
      ], watch: footWatch),
    )
    ..add(
      stacked(body, feet(ease: 0), [
        frame(dt: dt),
        frame(
          dt: dt,
          edits: [
            edit('pelvis', at: [0, 0.4, 0]),
          ],
        ),
      ], watch: footWatch),
    )
    ..add(
      stacked(
        body,
        feet(
          up: [0.0, 0.0, 1.0],
          probe: {
            'height': 0.0,
            'normal': [0.0, 0.0, 1.0],
          },
        ),
        [frame(dt: dt, repeat: 10)],
        watch: footWatch,
      ),
    )
    ..add(
      stacked(
        body,
        feet(
          maxTilt: 0.1,
          probe: {
            'height': 0.05,
            'normal': [0.5, 1.0, 0.0],
          },
        ),
        [frame(dt: dt, repeat: 20)],
        watch: footWatch,
      ),
    )
    ..add(
      stacked(body, feet(rise: 0, depth: 0), [
        frame(dt: dt, repeat: 3),
      ], watch: footWatch),
    )
    ..add(
      stacked(body, feet(airborne: 0), [
        frame(dt: dt, repeat: 3),
      ], watch: footWatch),
    )
    ..add(
      stacked(
        body,
        feet(
          probe: {
            'height': 5.0,
            'normal': [0.0, 1.0, 0.0],
          },
        ),
        [frame(dt: dt, repeat: 3)],
        watch: footWatch,
      ),
    )
    ..add(
      stacked(
        body,
        {...feet(), 'legs': <Object?>[]},
        [frame(dt: dt)],
        watch: footWatch,
      ),
    )
    ..add(
      stacked(
        body,
        {
          ...feet(),
          'up': [0.0, 0.0, 0.0],
        },
        [frame(dt: dt)],
        watch: footWatch,
      ),
    )
    ..add(
      stacked(body, feet(ease: 0), [
        frame(dt: dt),
        frame(
          dt: 0,
          edits: [
            edit('pelvis', at: [0, 0.4, 0]),
          ],
        ),
      ], watch: footWatch),
    )
    ..add(stacked(body, feet(ease: -1), [frame(dt: dt)], watch: footWatch));

  return [for (final s in cases) make(s, modifierRun)];
}

// --- generated rigs ----------------------------------------------------------

Object widgetJson(BoneWidget w) => {
  'shape': w.shape.name,
  'size': number(w.size),
  'offset': vec(w.offset),
  'roll': number(w.roll),
};

Object? generateRun(Case s) {
  final meta = MetaRig(armatureFrom(s['bones']));
  for (final a in list(s['assign'])) {
    final pair = list(a);
    meta.assign(pair[0] as String, rigTypeFrom(map(pair[1])));
  }
  final rig = const RigGenerator().generate(meta);

  Object skins() => [
    for (final b in rig.armature.bones)
      {'name': b.name, 'skin': mat(rig.pose.skinningOf(b.name))},
  ];

  rig.pose.evaluate();
  final atRest = skins();

  final names = rig.controls.toList()..sort();
  for (var i = 0; i < names.length; i++) {
    final t = rig.pose[names[i]];
    t.location = Vector3(0.02 * (i + 1), 0.01 * (i % 3), -0.015 * i);
    t.rotation = Quaternion.axisAngle(
      Vector3(1, 2, 3).normalized(),
      0.1 * (i % 5 + 1),
    );
  }
  rig.pose.evaluate();

  return {
    'hasProblems': rig.hasProblems,
    'bones': [
      for (final b in rig.armature.bones)
        {
          'name': b.name,
          'head': vec(b.head),
          'tail': vec(b.tail),
          'roll': number(b.roll),
          'parent': b.parent,
          'connected': b.connected,
          'deform': b.deform,
          'constraints': [
            for (final c in rig.pose.constraintsOn(b.name))
              {
                'kind': c.runtimeType.toString(),
                'influence': number(c.influence),
                'influenceProperty': c.influenceProperty,
                'invertInfluence': c.invertInfluence,
                'dependencies': c.dependencies.toList(),
              },
          ],
        },
    ],
    'controls': rig.controls.toList(),
    'deformBones': rig.deformBones.toList(),
    'properties': {
      for (final e in rig.properties.entries) e.key: number(e.value),
    },
    'poseProperties': {
      for (final e in rig.pose.properties.entries) e.key: number(e.value),
    },
    'widgets': {
      for (final e in rig.widgets.entries) e.key: widgetJson(e.value),
    },
    'collections': [
      for (final c in rig.collections.all)
        {
          'name': c.name,
          'visible': c.visible,
          'colour': c.colour,
          'members': rig.collections.membersOf(c.name).toList()..sort(),
        },
    ],
    'sorted': [for (final c in rig.collections.sorted) c.name],
    'collectionsOf': {
      for (final b in rig.armature.bones)
        b.name: rig.collections.collectionsOf(b.name),
    },
    'unassigned': rig.unassigned.toList(),
    'problems': [
      for (final p in rig.problems) [p.bone, p.message],
    ],
    'chains': [
      for (final c in rig.pose.ikChains)
        {
          'root': c.root,
          'mid': c.mid,
          'target': c.target,
          'pole': c.pole,
          'influence': number(c.influence),
          'influenceProperty': c.influenceProperty,
          'stretch': number(c.stretch),
          'stretchProperty': c.stretchProperty,
        },
    ],
    'atRest': atRest,
    'driven': skins(),
  };
}

List<Case> generateCases() {
  Case one(List<Object?> bones, List<List<Object?>> assign) => {
    'op': 'generate',
    'bones': bones,
    'assign': assign,
  };
  Case type(String kind, [Case extra = const {}]) => {'kind': kind, ...extra};

  final spine = [
    boneSpec('hips', [0, 1, 0], [0, 1.2, 0]),
    ...run(
      'spine',
      [0, 1.2, 0],
      [0, 0.2, 0],
      4,
      parent: 'hips',
      connectFirst: true,
    ),
  ];
  final finger = run('finger', [1.2, 1.5, 0], [0.1, 0, 0], 3, parent: null);
  final face = [
    boneSpec('head', [0, 1.6, 0], [0, 1.9, 0]),
    boneSpec('jaw', [0, 1.65, 0.05], [0, 1.6, 0.15], parent: 'head'),
    boneSpec('eye.L', [0.03, 1.75, 0.1], [0.03, 1.75, 0.12], parent: 'head'),
    boneSpec('eye.R', [-0.03, 1.75, 0.1], [-0.03, 1.75, 0.12], parent: 'head'),
    boneSpec('lid_up.L', [0.03, 1.77, 0.1], [0.03, 1.77, 0.12], parent: 'head'),
    boneSpec(
      'lid_down.L',
      [0.03, 1.73, 0.1],
      [0.03, 1.73, 0.12],
      parent: 'head',
    ),
  ];
  final cases = <Case>[
    one(arm(), [
      ['upper_arm.L', type('limb')],
    ]),
    one(arm(), [
      [
        'upper_arm.L',
        type('limb', {'stretch': 0.5}),
      ],
    ]),
    one(arm(), [
      [
        'upper_arm.L',
        type('limb', {'stretch': 1}),
      ],
    ]),
    one(
      [...arm(), ...arm(suffix: '.R', side: -1)],
      [
        ['upper_arm.L', type('limb')],
        ['upper_arm.R', type('limb')],
      ],
    ),
    one(arm(), [
      ['upper_arm.L', type('limb')],
      ['hand.L', type('copy')],
    ]),
    one(spine, [
      ['hips', type('copy')],
      ['spine0', type('spine')],
    ]),
    one(spine, [
      ['spine0', type('spine')],
    ]),
    one(finger, [
      ['finger0', type('finger')],
    ]),
    one(run('f', [0, 0, 0], [0.1, 0, 0], 2), [
      ['f0', type('finger')],
    ]),
    one(run('f', [0, 0, 0], [0.1, 0, 0], 1), [
      ['f0', type('finger')],
    ]),
    one(
      [
        boneSpec('root', [0, 0, 0], [0, 1, 0]),
      ],
      [
        ['root', type('copy')],
      ],
    ),
    one(
      [
        boneSpec('root', [0, 0, 0], [0, 1, 0], deform: false),
      ],
      [
        ['root', type('copy')],
      ],
    ),
    one(
      [
        boneSpec('root.L', [0, 0, 0], [0, 1, 0], roll: 0.3),
      ],
      [
        ['root.L', type('copy')],
      ],
    ),
    one(face, [
      ['head', type('copy')],
      ['jaw', type('jaw')],
    ]),
    one(face, [
      [
        'jaw',
        type('jaw', {'maximumAngle': 0.3}),
      ],
    ]),
    one(face, [
      ['eye.L', type('eye')],
      [
        'eye.R',
        type('eye', {'distance': 3}),
      ],
      [
        'lid_up.L',
        type('eyelid', {'eye': 'eye.L'}),
      ],
      [
        'lid_down.L',
        type('eyelid', {'eye': 'eye.L', 'follow': 0.6}),
      ],
    ]),
    one(face, [
      [
        'lid_up.L',
        type('eyelid', {'eye': 'eye.L'}),
      ],
    ]),
    one(face, [
      [
        'lid_up.L',
        type('eyelid', {'eye': 'nowhere'}),
      ],
    ]),
    one(arm(), [
      ['nowhere', type('limb')],
    ]),
    one(arm(), [
      ['upper_arm.L', type('limb')],
      ['forearm.L', type('copy')],
    ]),
    one(
      [
        boneSpec('Upper.L', [0.2, 1.5, 0], [0.6, 1.5, -0.1]),
        boneSpec(
          'lower.L',
          [0.6, 1.5, -0.1],
          [1.0, 1.5, 0],
          parent: 'Upper.L',
          connected: true,
        ),
        boneSpec(
          'Hand.L',
          [1.0, 1.5, 0],
          [1.2, 1.5, 0],
          parent: 'lower.L',
          connected: true,
        ),
      ],
      [
        ['Upper.L', type('limb')],
      ],
    ),
    one(arm(), []),
    one(<Object?>[], []),
    one(
      [
        ...arm(),
        ...spine,
        ...finger,
        boneSpec('extra', [3, 3, 3], [3, 4, 3]),
      ],
      [
        ['upper_arm.L', type('limb')],
        ['spine0', type('spine')],
        ['finger0', type('finger')],
      ],
    ),
    one(arm(), [
      ['upper_arm.L', type('splines')],
    ]),
  ];
  return [for (final s in cases) make(s, generateRun)];
}

// --- collections --------------------------------------------------------------

Object? collectionsRun(Case s) {
  final collections = BoneCollections();
  final bones = <String>{'nobody'};
  for (final add in list(s['adds'])) {
    final a = list(add);
    collections.add(
      a[0] as String,
      a[1] as String,
      visible: a[2] as bool,
      colour: a[3] as int?,
    );
    bones.add(a[0] as String);
  }
  return {
    'all': [
      for (final c in collections.all)
        {
          'name': c.name,
          'visible': c.visible,
          'colour': c.colour,
          'members': collections.membersOf(c.name).toList(),
        },
    ],
    'sorted': [for (final c in collections.sorted) c.name],
    'of': {for (final b in bones) b: collections.collectionsOf(b)},
  };
}

List<Case> collectionsCases() {
  Case with_(List<List<Object?>> adds) => {'op': 'collections', 'adds': adds};
  return [
    for (final adds in <List<List<Object?>>>[
      [],
      [
        ['a', 'MCH', true, null],
        ['a', 'forearm.L', true, null],
        ['a', 'DEF', true, 255],
        ['b', 'forearm.L', false, null],
        ['a', 'MCH', true, null],
      ],
      [
        ['a', 'X', false, null],
        ['b', 'X', true, 5],
      ],
      [
        ['c', 'b', true, null],
        ['c', 'B', true, null],
        ['c', 'a', true, null],
        ['c', 'A', true, null],
        ['d', 'a', true, null],
      ],
    ])
      make(with_(adds), collectionsRun),
  ];
}

void main(List<String> args) {
  final root = args.isEmpty ? '.' : args.first;
  writeFixture(
    root: root,
    package: 'orblit_rig',
    name: 'rig',
    cases: [
      ...boneCases(),
      ...namingCases(),
      ...twoBoneCases(),
      ...fabrikCases(),
      ...curveCases(),
      ...widgetCases(),
      ...poseCases(),
      ...modifierCases(),
      ...generateCases(),
      ...collectionsCases(),
    ],
  );
}
