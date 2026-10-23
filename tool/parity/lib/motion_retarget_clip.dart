import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:orblit_parity_tool/motion_retarget_names.dart';
import 'package:vector_math/vector_math_64.dart';

/// The skeletons are the library of `retarget_names`, by name. The clips the
/// cases name are made here, one per skeleton and flavour, and held in this
/// fixture's own library case.
final Map<String, String> _texts = {};

Quaternion _turn(int bone, double amount) => Quaternion.axisAngle(
  Vector3(1, 0.5 + (bone % 3) * 0.25, 0.3 - (bone % 2) * 0.6).normalized(),
  0.2 + 0.1 * bone + amount,
);

ClipChannel<Quaternion> _rotation(
  String target,
  String bone,
  int index, {
  required bool curved,
}) {
  final times = index.isEven ? [0.0, 0.5, 1.0] : [0.0, 0.3, 1.0];
  final cubic = curved && index % 3 == 0;
  return ClipChannel<Quaternion>(
    target: target,
    bone: bone,
    property: 'rotation',
    kind: ChannelKind.rotation,
    keys: [
      for (final (k, at) in times.indexed)
        Key(
          at,
          _turn(index, k * 0.45),
          hold: cubic
              ? Hold.curve
              : k == 1
              ? Hold.linear
              : Hold.smooth,
          slopeIn: cubic && k > 0
              ? Quaternion(-0.4 + index * 0.01, 0.1, 0.2, 0.3)
              : null,
          slopeOut: cubic && k < 2
              ? Quaternion(0.3, -0.2 + index * 0.01, 0.5, 0.1)
              : null,
        ),
    ],
  );
}

ClipChannel<Vector3> _vector(
  String target,
  String bone,
  String property,
  List<Vector3> values, {
  bool curved = false,
}) => ClipChannel<Vector3>(
  target: target,
  bone: bone,
  property: property,
  kind: ChannelKind.vector,
  keys: [
    for (final (k, value) in values.indexed)
      Key(
        k.toDouble(),
        value,
        hold: curved ? Hold.curve : Hold.linear,
        slopeIn: curved && k > 0 ? Vector3(0.2, -0.1, 0.4) : null,
        slopeOut: curved && k < values.length - 1
            ? Vector3(0.5, 0.25, -0.3)
            : null,
      ),
  ],
);

/// A clip that moves every bone of [skeleton], with a position that leaves
/// rest, one that stays at it, a scale that leaves it, one that stays, a
/// channel that is not a bone's, one for a bone nothing has, root motion and a
/// mark.
ClipDocument _moving(
  RestSkeleton skeleton, {
  String target = '',
  bool curved = false,
  bool leafRoot = false,
}) {
  final names = skeleton.names;
  final n = names.length;
  final channels = <ClipChannel<Object>>[
    for (var i = 0; i < n; i++) _rotation(target, names[i], i, curved: curved),
  ];
  if (n > 0) {
    final rest = skeleton.positionOf(0);
    channels.add(
      _vector(target, names[0], 'position', [
        rest + Vector3(0, 0.1, 0),
        rest + Vector3(0.5, 0, 0.3),
      ], curved: curved),
    );
  }
  if (n > 1) {
    final rest = skeleton.positionOf(1);
    channels.add(_vector(target, names[1], 'position', [rest, rest]));
    final scale = skeleton.scaleOf(1);
    channels.add(_vector(target, names[1], 'scale', [scale, scale]));
  }
  if (n > 2) {
    final scale = skeleton.scaleOf(n ~/ 2);
    channels.add(_vector(target, names[n ~/ 2], 'scale', [scale, scale * 1.5]));
  }
  channels
    ..add(
      ClipChannel<double>(
        target: target,
        property: 'light.power',
        kind: ChannelKind.number,
        keys: [const Key(0.0, 1.0), const Key(1.0, 2.0)],
      ),
    )
    ..add(_rotation(target, 'ghost', 1, curved: false));
  return ClipDocument(
    name: 'Move',
    duration: 1,
    channels: channels,
    marks: const [Mark(0.5, 'step')],
    rootMotion: n == 0
        ? null
        : RootMotion(
            target: target,
            bone: leafRoot ? names.last : names.first,
            turns: true,
            rises: true,
            up: leafRoot ? Vector3(0, 0, 1) : null,
          ),
  );
}

/// A clip whose positions stay put by the margin the retarget allows or a
/// hair past it. Bone 0 rests at nought and moves by exactly the margin, bone
/// 1 rests a unit up and moves by a bit under the margin, and bone 2 rests at
/// nought and has slopes of exactly the size that counts as still.
ClipDocument _staying(RestSkeleton skeleton) {
  final names = skeleton.names;
  final rest = [for (var i = 0; i < 3; i++) skeleton.positionOf(i)];
  return ClipDocument(
    name: 'Stay',
    duration: 1,
    channels: [
      _vector('', names[0], 'position', [
        rest[0] + Vector3(1e-6, 0, 0),
        rest[0],
      ]),
      _vector('', names[1], 'position', [
        rest[1] + Vector3(0, 1.5e-6, 0),
        rest[1],
      ]),
      ClipChannel<Vector3>(
        target: '',
        bone: names[2],
        property: 'position',
        kind: ChannelKind.vector,
        keys: [
          Key(0.0, rest[2], hold: Hold.curve, slopeOut: Vector3(1e-9, 0, 0)),
          Key(1.0, rest[2], hold: Hold.curve, slopeIn: Vector3(1e-9, 0, 0)),
        ],
      ),
    ],
  );
}

ClipDocument _bare() => ClipDocument(
  name: 'Bare',
  duration: 1,
  marks: const [Mark(0.5, 'step')],
  rootMotion: RootMotion(bone: 'ghost'),
  channels: [
    ClipChannel<double>(
      target: '',
      property: 'light.power',
      kind: ChannelKind.number,
      keys: [const Key(0.0, 1.0)],
    ),
  ],
);

/// Channels for the target '' and for 'other', so a retarget of one leaves
/// the other as it was.
ClipDocument _twoTargets(RestSkeleton skeleton) {
  final here = _moving(skeleton);
  final other = _moving(skeleton, target: 'other', curved: true);
  return ClipDocument(
    name: 'Two',
    duration: 1,
    channels: [...here.channels, ...other.channels],
    marks: here.marks,
    rootMotion: other.rootMotion,
  );
}

/// The name a case gives a clip, made for [skeleton] the way [flavour] says.
String _clipFor(String skeleton, String flavour) {
  final name = '$skeleton|$flavour';
  if (_texts.containsKey(name)) return name;
  final built = skeletonNamed(skeleton);
  final clip = switch (flavour) {
    'moving' => _moving(built),
    'curved' => _moving(built, curved: true),
    'leafRoot' => _moving(built, leafRoot: true),
    'twoTargets' => _twoTargets(built),
    'staying' => _staying(built),
    'bare' => _bare(),
    _ => throw StateError('no flavour "$flavour"'),
  };
  _texts[name] = clip.encode();
  return name;
}

ClipDocument _clipNamed(Object? name) =>
    ClipDocument.decode(_texts[name]!).clip;

Map<String, Object?> _optionsOf({
  bool humanoid = false,
  bool fixRest = false,
  String target = '',
  Map<String, String> bones = const {},
  List<String> unpaired = const [],
}) => {
  'humanoid': humanoid,
  'fixRest': fixRest,
  'target': target,
  'bones': bones,
  'unpaired': unpaired,
};

Map<String, String> _stringMap(Object? raw) => {
  for (final MapEntry(:key, :value) in (raw! as Map<String, Object?>).entries)
    key: value! as String,
};

Set<String> _stringSet(Object? raw) => {
  for (final one in raw! as List<Object?>) one! as String,
};

Object? _dumpRetargeted(ClipRetargeted result) => {
  'clip': tree(jsonDecode(result.clip.encode())),
  'problems': result.problems,
};

Object? _retarget(List<Object?> input) {
  final options = input[3]! as Map<String, Object?>;
  return _dumpRetargeted(
    retargetClip(
      _clipNamed(input[2]),
      from: skeletonNamed(input[0]! as String),
      to: skeletonNamed(input[1]! as String),
      bones: _stringMap(options['bones']),
      unpaired: _stringSet(options['unpaired']),
      target: options['target']! as String,
      humanoid: options['humanoid']! as bool,
      fixRest: options['fixRest']! as bool,
    ),
  );
}

Case _retargetCase(
  String from,
  String to,
  String flavour,
  Map<String, Object?> options,
) {
  for (final name in [from, to]) {
    if (!hasSkeleton(name)) throw StateError('no skeleton "$name"');
  }
  return caseOf('retarget', [
    from,
    to,
    _clipFor(from, flavour),
    options,
  ], _retarget);
}

const _core = ['plain', 'mannequin', 'capture', 'blender', 'bip'];

/// Pairs of skeletons that are not each other's twin: different spines,
/// poses, units, armatures above and bones missing.
const _shaped = [
  ('capture', 'capture.a60'),
  ('capture.a60', 'capture'),
  ('capture.a60', 'mannequin'),
  ('plain', 'plain.s3.rf'),
  ('plain.s3.rf', 'plain'),
  ('plain.s1', 'capture.s3'),
  ('capture.s2.bare.end', 'mannequin.s1.rf'),
  ('mannequin.s3.end', 'bip.s2.bare'),
  ('blender.s1.rf.end', 'def.s3'),
  ('joint', 'capture'),
  ('def', 'joint.s2'),
  ('plain', 'v.plain.zUpAbove'),
  ('v.plain.zUpAbove', 'capture'),
  ('capture', 'v.capture.centimetres'),
  ('v.capture.centimetres', 'v.capture.zUpAbove'),
  ('capture', 'v.capture.oneLeg'),
  ('v.capture.noLegs', 'capture'),
  ('plain', 'v.plain.spine0'),
  ('v.capture.rooted', 'mannequin'),
  ('mannequin', 'v.capture.rooted'),
  ('v.mannequin.halfNamed', 'mannequin'),
  ('capture', 'v.mannequin.halfNamed'),
  ('n.a', 'n.b'),
  ('plain', 'v.single'),
  ('v.single', 'plain'),
  ('v.single', 'v.single'),
  ('v.empty', 'plain'),
  ('plain', 'v.empty'),
];

final List<Map<String, Object?>> _optionSets = [
  _optionsOf(),
  _optionsOf(humanoid: true),
  _optionsOf(fixRest: true),
  _optionsOf(humanoid: true, fixRest: true),
];

String _flavourOf(String from) => from == 'v.empty' ? 'bare' : 'moving';

List<(String, String)> _pairs() => [
  for (final from in _core)
    for (final to in _core)
      if (from != to) (from, to),
  ..._shaped,
];

List<Case> _everyPair() => [
  for (final (from, to) in _pairs())
    for (final options in _optionSets)
      _retargetCase(from, to, _flavourOf(from), options),
];

/// The other kinds of clip over the core pairs, with the plain options and
/// with both flags on.
List<Case> _otherClips() => [
  for (final flavour in ['curved', 'leafRoot', 'bare'])
    for (final (from, to) in _pairs().take(20))
      for (final options in [_optionSets.first, _optionSets.last])
        _retargetCase(from, to, flavour, options),
];

/// Bones named by hand: the target bone at [at] of [to] driven by the source
/// bone at [from].
({String bone, String source}) _hand(
  String fromName,
  String toName,
  int to,
  int from,
) {
  final source = skeletonNamed(fromName).names;
  final target = skeletonNamed(toName).names;
  return (
    bone: target[to % target.length],
    source: source[from % source.length],
  );
}

List<Case> _handCases() {
  final cases = <Case>[];
  for (final (from, to) in [
    ('capture', 'mannequin'),
    ('plain', 'plain.s3.rf'),
    ('capture', 'v.capture.oneLeg'),
  ]) {
    final one = _hand(from, to, 2, 0);
    final two = _hand(from, to, 4, 3);
    final three = _hand(from, to, 1, 3);
    final left = skeletonNamed(to).names[3];
    for (final options in [
      _optionsOf(bones: {one.bone: one.source}),
      _optionsOf(bones: {two.bone: two.source, three.bone: three.source}),
      _optionsOf(unpaired: [left]),
      _optionsOf(bones: {one.bone: one.source}, unpaired: [left]),
      _optionsOf(
        humanoid: true,
        fixRest: true,
        bones: {one.bone: one.source},
        unpaired: [left],
      ),
      _optionsOf(unpaired: skeletonNamed(to).names.take(6).toList()),
    ]) {
      cases.add(_retargetCase(from, to, 'moving', options));
    }
    cases
      ..add(_retargetCase(from, to, 'twoTargets', _optionsOf(target: 'other')))
      ..add(
        _retargetCase(
          from,
          to,
          'twoTargets',
          _optionsOf(target: 'other', humanoid: true, fixRest: true),
        ),
      )
      ..add(_retargetCase(from, to, 'twoTargets', _optionsOf()))
      ..add(_retargetCase(from, to, 'moving', _optionsOf(target: 'absent')));
  }
  return cases;
}

/// Names the retarget refuses: a bone neither skeleton has, and bones both
/// named and left unpaired.
List<Case> _refusedCases() {
  const from = 'capture';
  const to = 'mannequin';
  final one = _hand(from, to, 2, 0);
  return [
    for (final options in [
      _optionsOf(bones: {'nowhere': one.source}),
      _optionsOf(bones: {one.bone: 'nowhere'}),
      _optionsOf(unpaired: ['nowhere']),
      _optionsOf(bones: {one.bone: one.source}, unpaired: [one.bone]),
    ])
      _retargetCase(from, to, 'moving', options),
  ];
}

/// Skeletons that stand on a number the retarget compares with, each against
/// one that does not, in both directions.
List<Case> _edgeCases() {
  _checkEdges();
  return [
    for (final (from, to, options) in [
      ('rt.flat', 'bent', _optionsOf()),
      ('bent', 'rt.flat', _optionsOf()),
      ('rt.hair', 'bent', _optionsOf()),
      ('bent', 'rt.hair', _optionsOf()),
      ('rt.scales', 'rt.plain4', _optionsOf()),
      ('rt.plain4', 'rt.scales', _optionsOf()),
      ('rt.bendA', 'rt.bendB', _optionsOf()),
      ('rt.bendA', 'rt.bendB', _optionsOf(fixRest: true)),
      ('rt.bendB', 'rt.bendA', _optionsOf(fixRest: true)),
      ('rt.bendA', 'rt.zeroTo', _optionsOf(fixRest: true)),
      ('rt.zeroTo', 'rt.bendA', _optionsOf(fixRest: true)),
      ('rt.bendB', 'rt.tinyTo', _optionsOf(fixRest: true)),
      ('rt.tinyTo', 'rt.bendB', _optionsOf(fixRest: true)),
      ('rt.dotFrom', 'rt.dotTo', _optionsOf(fixRest: true)),
      ('rt.dotTo', 'rt.dotFrom', _optionsOf(fixRest: true)),
      ('rt.stays', 'rt.stays', _optionsOf()),
      ('rt.pairX', 'rt.pairZ', _optionsOf(fixRest: true)),
      ('rt.pairZ', 'rt.pairX', _optionsOf(fixRest: true)),
      ('rt.wrap', 'rt.wrap', _optionsOf(fixRest: true, unpaired: ['x', 'y'])),
    ])
      _retargetCase(from, to, _edgeFlavour(from), options),
  ];
}

/// The clip each edge skeleton needs: positions that stay put, a root motion
/// on the last bone, or every bone moving.
String _edgeFlavour(String from) => switch (from) {
  'rt.stays' || 'rt.wrap' => 'staying',
  'rt.pairX' || 'rt.pairZ' => 'leafRoot',
  _ => 'moving',
};

/// Each edge skeleton must stand on its number to the last bit, or the cases
/// that use it prove nothing.
void _checkEdges() {
  final stands = {
    'reach of rt.hair': skeletonNamed('rt.hair').reach == 1e-9,
    'reach of rt.flat': skeletonNamed('rt.flat').reach == 0,
    'scale of rt.scales': skeletonNamed('rt.scales').scaleOf(2).y == 1e-12,
    'turned scale of rt.scales': skeletonNamed('rt.scales').scaleOf(2).x == -1,
    'aim of rt.tinyTo':
        (skeletonNamed('rt.tinyTo').originOf(1) -
                skeletonNamed('rt.tinyTo').originOf(0))
            .length ==
        1e-9,
    'dot of rt.dotFrom':
        (skeletonNamed('rt.dotFrom').originOf(1) -
                skeletonNamed('rt.dotFrom').originOf(0))
            .normalized()
            .dot(Vector3(1, 0, 0)) ==
        -0.7,
  };
  for (final MapEntry(:key, :value) in stands.entries) {
    if (!value) throw StateError('$key is not on its edge.');
  }
}

/// A skeleton against its own twin with the rest fixed, which should add
/// nothing wherever the twin's quaternions come out the other way round.
List<Case> _twinCases() => [
  for (final name in [
    'rod',
    'rod.above',
    'bent',
    'scaled',
    'folded',
    'zero',
    'edge.span',
    'edge.tiny',
    'edge.bend1',
    'edge.bend2',
    'rt.bendA',
    'rt.bendB',
  ])
    _retargetCase(name, name, 'moving', _optionsOf(fixRest: true)),
];

List<Case> retargetCases() => [
  ..._everyPair(),
  ..._otherClips(),
  ..._handCases(),
  ..._refusedCases(),
  ..._edgeCases(),
  ..._twinCases(),
];

/// The clips the retarget cases name.
List<Case> clipLibraryCases() => [
  {
    'op': 'library',
    'in': <Object?>[],
    'expect': {'clips': _texts},
  },
];

Object? _dumpMap(BoneMap map, List<String> problems) => {
  'map': tree(map.toJson()),
  'text': map.encode(),
  'problems': problems,
};

BoneMap _readMap(Object? text) => BoneMap.decode(text! as String).map;

Case _decodeCase(String text) => caseOf('boneMapDecode', [text], (input) {
  final load = BoneMap.decode(input[0]! as String);
  return _dumpMap(load.map, load.problems);
});

String _head(Map<String, Object?> extra, {int version = 1}) =>
    jsonEncode({'kind': 'orblit.bonemap', 'formatVersion': version, ...extra});

List<String> _mapTexts() => [
  BoneMap().encode(),
  BoneMap(
    fromAsset: 'models/a.glb',
    toAsset: 'models/b.glb',
    bones: {'zed': 'one', 'arm': 'two', 'hand': 'three'},
    unpaired: {'toe', 'eye'},
    humanoid: true,
    fixRest: true,
  ).encode(),
  BoneMap(humanoid: true).encode(),
  BoneMap(fixRest: true).encode(),
  _head({}),
  _head({
    'bones': {'a': 'b'},
    'unpaired': ['c'],
    'from': 'x',
    'to': 'y',
  }),
  _head({'humanoid': true, 'fixRest': true}),
  _head({'humanoid': 'yes', 'fixRest': 1}),
  _head({
    'bones': {
      'a': 3,
      'b': '',
      '': 'c',
      'd': null,
      'e': 'f',
      'g': ['h'],
    },
    'unpaired': [
      1,
      '',
      null,
      'k',
      'k',
      ['m'],
    ],
  }),
  _head({
    'bones': {'a': 'b', 'c': 'd'},
    'unpaired': ['a', 'c', 'e'],
  }),
  _head({
    'bones': ['a', 'b'],
    'unpaired': 'a',
  }),
  _head({
    'bones': 'a',
    'unpaired': {'a': 1},
  }),
  _head({'from': 5, 'to': null}),
  _head({'from': '', 'to': ''}),
  _head({
    'extra': 1,
    'bones': {'a': 'b'},
  }),
  _head({}, version: 2),
  _head({}, version: 0),
  _head({}, version: 1000000),
  jsonEncode({'kind': 'orblit.clip', 'formatVersion': 1}),
  jsonEncode({'formatVersion': 1}),
  jsonEncode(['a']),
  '',
  'not json',
  '{',
  'null',
  '3',
  '{"kind":"orblit.bonemap","formatVersion":"1"}',
  '{"kind":"orblit.bonemap"}',
  '{"kind":"orblit.bonemap","formatVersion":1.5}',
];

List<Case> boneMapDecodeCases() => [
  for (final text in _mapTexts()) _decodeCase(text),
];

const _edits = [
  ['withPair', 'a', 'z'],
  ['withPair', 'c', 'y'],
  ['withPair', 'new', 'z'],
  ['withPair', 'e', 'z'],
  ['withUnpaired', 'a'],
  ['withUnpaired', 'c'],
  ['withUnpaired', 'new'],
  ['withUnpaired', 'e'],
  ['withAuto', 'a'],
  ['withAuto', 'c'],
  ['withAuto', 'new'],
  ['withAuto', 'e'],
];

String _editBase() => BoneMap(
  fromAsset: 'f',
  toAsset: 't',
  bones: {'zz': 'q', 'a': 'b', 'mm': 'r'},
  unpaired: {'e', 'c'},
  humanoid: true,
).encode();

Object? _edit(List<Object?> input) {
  final map = _readMap(input[0]);
  final [op, ...args] = (input[1]! as List<Object?>).cast<String>();
  final edited = switch (op) {
    'withPair' => map.withPair(args[0], args[1]),
    'withUnpaired' => map.withUnpaired(args[0]),
    'withAuto' => map.withAuto(args[0]),
    _ => throw StateError('no edit "$op"'),
  };
  return {'text': edited.encode()};
}

List<Case> boneMapEditCases() => [
  for (final edit in _edits) caseOf('boneMapEdit', [_editBase(), edit], _edit),
  // Several edits in a row on one map, from an empty one.
  caseOf('boneMapEdit', [
    BoneMap().encode(),
    ['withPair', 'b', 'a'],
  ], _edit),
];

/// Maps that name bones of [from] and [to], some of which they lack.
String _fitText(
  String fromName,
  String toName, {
  bool stale = true,
  bool humanoid = false,
  bool fixRest = false,
}) {
  final from = skeletonNamed(fromName).names;
  final to = skeletonNamed(toName).names;
  return BoneMap(
    fromAsset: fromName,
    toAsset: toName,
    bones: {
      to[1]: from[0],
      to[4]: from[3],
      to[2]: from[3],
      if (stale) 'ghost': from[0],
      if (stale) to[3]: 'ghost',
    },
    unpaired: {to[5], to[6], if (stale) 'phantom'},
    humanoid: humanoid,
    fixRest: fixRest,
  ).encode();
}

const _fits = [
  ('capture', 'mannequin'),
  ('plain', 'plain.s3.rf'),
  ('capture', 'capture.a60'),
];

List<Case> boneMapFitCases() => [
  for (final (from, to) in _fits)
    for (final stale in [true, false])
      for (final op in ['boneMapStale', 'boneMapFitted'])
        caseOf(op, [_fitText(from, to, stale: stale), from, to], (input) {
          final map = _readMap(input[0]);
          final one = skeletonNamed(input[1]! as String);
          final two = skeletonNamed(input[2]! as String);
          return op == 'boneMapStale'
              ? map.stale(from: one, to: two)
              : {'text': map.fitted(from: one, to: two).encode()};
        }),
];

List<Case> boneMapPairingCases() => [
  for (final (from, to) in _fits)
    for (final stale in [true, false])
      for (final (humanoid, fixRest) in [
        (false, false),
        (true, false),
        (true, true),
      ])
        caseOf(
          'boneMapPairing',
          [
            _fitText(
              from,
              to,
              stale: stale,
              humanoid: humanoid,
              fixRest: fixRest,
            ),
            from,
            to,
          ],
          (input) {
            final pairing = _readMap(input[0]).pairing(
              from: skeletonNamed(input[1]! as String),
              to: skeletonNamed(input[2]! as String),
            );
            return {
              'drivers': [
                for (final MapEntry(:key, :value) in pairing.drivers.entries)
                  [key, value],
              ],
              'problems': pairing.problems,
            };
          },
        ),
];

List<Case> boneMapApplyCases() => [
  for (final (from, to) in _fits)
    for (final stale in [true, false])
      for (final (humanoid, fixRest) in [(false, false), (true, true)])
        caseOf(
          'boneMapApply',
          [
            _fitText(
              from,
              to,
              stale: stale,
              humanoid: humanoid,
              fixRest: fixRest,
            ),
            from,
            to,
            _clipFor(from, 'twoTargets'),
            if (humanoid) 'other' else '',
          ],
          (input) => _dumpRetargeted(
            _readMap(input[0]).apply(
              _clipNamed(input[3]),
              from: skeletonNamed(input[1]! as String),
              to: skeletonNamed(input[2]! as String),
              target: input[4]! as String,
            ),
          ),
        ),
];
