import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_motion/src/pass.dart';
import 'package:orblit_motion/src/sync.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

ClipDocument _read(String text) => ClipDocument.decode(text).clip;

WhenDone? _ending(String? name) =>
    name == null ? null : WhenDone.values.byName(name);

Object _marks(List<Mark> marks) => [
  for (final mark in marks) [number(mark.at), mark.name],
];

/// Every clip the motion fixture uses, and one with unique marks to line up.
Map<String, String> _texts() => {
  ...clipTexts(),
  'gait': ClipDocument(
    name: 'Gait',
    duration: 1.2,
    marks: const [Mark(0.12, 'a'), Mark(0.48, 'b'), Mark(0.96, 'c')],
  ).encode(),
};

/// Clips with marks at the very start, at the very end and twice at one time,
/// and one with no length at all.
Map<String, String> _edges() => {
  'edge': ClipDocument(
    name: 'Edge',
    duration: 1,
    rootMotion: RootMotion(bone: 'hips', turns: true),
    channels: [
      _moves('hips', [(0, Vector3.zero()), (1, Vector3(1, 0, 2))]),
      _turns('hips', [(0, Quaternion.identity()), (1, _about(0, 1, 0, 1.2))]),
    ],
    marks: const [
      Mark(0, 'start'),
      Mark(0.3, 'a'),
      Mark(0.6, 'b'),
      Mark(0.6, 'c'),
      Mark(1, 'end'),
    ],
  ).encode(),
  'zero': ClipDocument(
    name: 'Zero',
    duration: 0,
    marks: const [Mark(0, 'a'), Mark(0, 'b')],
  ).encode(),
};

List<String> _played() => [..._texts().values, ..._edges().values];

const List<String?> _endings = [null, 'hold', 'loop', 'bounce', 'release'];

const List<List<double>> _plays = [
  [0.25, 0.5, 1.0],
  [2.5],
  [0.0, 0.1, 3.7],
  [1000.25],
  [-0.5, 0.3],
  [double.nan, 0.4],
  [double.infinity],
  [0.5, double.infinity],
];

Case _playCase(String text, String? ending, double speed, List<double> steps) {
  return {
    'op': 'play',
    'in': [text, ending, number(speed), [for (final s in steps) number(s)]],
    'expect': attempt(() {
      final clip = _read(text);
      final player = ClipPlayer(clip, whenDone: _ending(ending), speed: speed);
      if (speed < 0) player.seek(clip.duration);
      player.play();
      final out = <Object>[];
      for (var i = 0; i < steps.length; i++) {
        final step = player.advance(steps[i]);
        out.add({
          'marks': _marks(step.marks),
          'moved': stepDump(step.moved),
          'at': number(player.at),
          'playing': player.playing,
          'finished': player.finished,
          'released': player.released,
          if (i == steps.length - 1) 'frame': frameDump(step.frame),
        });
      }
      return out;
    }),
  };
}

List<Case> playCases() => [
  for (final text in _played())
    for (final ending in _endings)
      for (final speed in [1.0, -1.0])
        for (final steps in _plays) _playCase(text, ending, speed, steps),
];

const List<String> _passEndings = ['hold', 'release', 'loop', 'bounce'];

const List<double> _laps = [
  -1,
  0,
  0.25,
  1,
  1.5,
  2,
  2.75,
  3,
  1000.5,
  double.nan,
  double.infinity,
  double.negativeInfinity,
];

const List<(double, double)> _passSpans = [
  (0, 1),
  (0.5, 1.5),
  (1, 0),
  (0, 3.5),
  (0.2, 7.3),
  (0, 1000.5),
  (2, 2),
  (-1, 2.5),
  (3, 3.25),
  (0.9, 1.1),
  (0, double.infinity),
  (double.negativeInfinity, 1),
  (double.infinity, 0),
  (-2, -1),
  (1.25, 1.75),
  (0, 256),
  (2, 1000),
];

List<Case> passCases() => [
  for (final text in _played())
    for (final ending in _passEndings) ...[
      for (final lap in _laps)
        {
          'op': 'timeAt',
          'in': [text, ending, number(lap)],
          'expect': attempt(
            () => number(timeAt(_read(text), _ending(ending)!, lap)),
          ),
        },
      for (final (from, to) in _passSpans)
        for (final marks in [true, false])
          {
            'op': 'passOver',
            'in': [text, ending, number(from), number(to), marks],
            'expect': attempt(() {
              final passed = passOver(
                _read(text),
                _ending(ending)!,
                from,
                to,
                marks: marks,
              );
              return {
                'moved': stepDump(passed.moved),
                'marks': _marks(passed.marks),
              };
            }),
          },
    ],
];

const List<List<String>> _names = [
  ['spark', 'buzz'],
  ['buzz', 'spark'],
  ['left', 'right'],
  ['spark'],
  [],
  ['spark', 'nope'],
  ['buzz', 'spark', 'spark'],
  ['a', 'b', 'c'],
  ['c', 'a', 'b'],
  ['b', 'a'],
  ['a', 'c', 'b'],
  ['c', 'b', 'a'],
  ['a', 'b'],
  ['start', 'a'],
  ['a', 'end'],
  ['b', 'c'],
  ['c', 'b'],
  ['start', 'a', 'b'],
];

const List<double> _syncLaps = [
  0,
  0.1,
  0.25,
  0.5,
  0.75,
  0.999,
  1,
  1.3,
  2.9,
  -0.4,
  7.5,
  double.nan,
  double.infinity,
  double.negativeInfinity,
];

List<Case> syncCases() => [
  for (final text in _played())
    for (final names in _names)
      for (final lap in _syncLaps)
        {
          'op': 'sync',
          'in': [text, names, number(lap)],
          'expect': attempt(() => number(syncedLap(_read(text), lap, names))),
        },
];

ClipChannel<Vector3> _moves(String bone, List<(double, Vector3)> keys) =>
    ClipChannel<Vector3>(
      target: '',
      bone: bone,
      property: 'position',
      kind: ChannelKind.vector,
      keys: [for (final (at, v) in keys) Key(at, v, hold: Hold.linear)],
    );

ClipChannel<Quaternion> _turns(String bone, List<(double, Quaternion)> keys) =>
    ClipChannel<Quaternion>(
      target: '',
      bone: bone,
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [for (final (at, q) in keys) Key(at, q, hold: Hold.linear)],
    );

ClipChannel<double> _numbers(String property, List<(double, double)> keys) =>
    ClipChannel<double>(
      target: 'bulb',
      property: property,
      kind: ChannelKind.number,
      keys: [for (final (at, v) in keys) Key(at, v, hold: Hold.linear)],
    );

Quaternion _about(double x, double y, double z, double radians) =>
    Quaternion.axisAngle(Vector3(x, y, z).normalized(), radians);

ClipDocument _clipOf(List<ClipChannel<Object>> channels) =>
    ClipDocument(name: 'Reduced', duration: 2, channels: channels);

/// Dense channels of each kind a clip can carry, and the ones reduction
/// must leave alone.
Map<String, ClipDocument> _dense() {
  final noise = math.Random(7);
  double wobble() => noise.nextDouble() - 0.5;
  return {
    'sine': _clipOf([
      _moves('hips', [
        for (var i = 0; i <= 200; i++)
          (i / 100, Vector3(math.sin(i / 100 * math.pi), 0, 0.2 * i / 100)),
      ]),
      _turns('spine', [
        for (var i = 0; i <= 120; i++)
          (i / 60, _about(0.2, 1, 0, 0.7 * math.sin(i / 60 * math.pi))),
      ]),
      _numbers('light.power', [
        for (var i = 0; i <= 100; i++) (i / 50, 30 + 20 * math.sin(i / 8)),
      ]),
    ]),
    'ragged': _clipOf([
      _moves('hips', [
        for (var i = 0; i <= 300; i++)
          (i / 150, Vector3(wobble() * 0.01, i * 0.01, wobble() * 0.002)),
      ]),
      _turns('neck', [
        for (var i = 0; i <= 300; i++)
          (i / 150, _about(wobble(), 1, wobble(), 0.002 * wobble() * i)),
      ]),
      _numbers('light.power', [
        for (var i = 0; i <= 300; i++) (i / 150, wobble() * 0.01),
      ]),
    ]),
    'straight': _clipOf([
      _moves('hips', [
        for (var i = 0; i <= 10; i++) (i / 5, Vector3(i * 0.1, 0, 0)),
      ]),
      _numbers('light.power', [for (var i = 0; i <= 10; i++) (i / 5, i * 3.0)]),
    ]),
    'eased': _clipOf([
      ClipChannel<Vector3>(
        target: '',
        bone: 'hips',
        property: 'position',
        kind: ChannelKind.vector,
        keys: [
          Key(0, Vector3.zero(), hold: Hold.smooth),
          Key(1, Vector3(0.5, 0, 0), hold: Hold.smooth),
          Key(2, Vector3(1, 0, 0), hold: Hold.smooth),
        ],
      ),
    ]),
    'cut': _clipOf([
      _moves('hips', [
        (0, Vector3.zero()),
        (1, Vector3(1, 0, 0)),
        (1, Vector3(1, 1, 0)),
        (2, Vector3(1, 2, 0)),
      ]),
    ]),
    'flipped': _clipOf([
      _turns('spine', [
        (0, _about(0, 1, 0, 0.4)),
        (1, _about(0, 1, 0, 0.4).scaled(-1)),
        (2, _about(0, 1, 0, 0.4)),
      ]),
    ]),
    'sloped': _clipOf([
      ClipChannel<double>(
        target: 'bulb',
        property: 'light.power',
        kind: ChannelKind.number,
        keys: const [
          Key(0.0, 0.0, hold: Hold.linear),
          Key(1.0, 1.0, hold: Hold.linear, slopeOut: 0.3),
          Key(2.0, 2.0, hold: Hold.linear),
        ],
      ),
    ]),
    'long': _clipOf([
      _numbers('light.power', [
        for (var i = 0; i <= 2400; i++) (i / 1200, math.exp(i / 200)),
      ]),
    ]),
  };
}

const List<(double, double)> _tolerances = [
  (5e-4, 5e-4),
  (1e-3, 0.05),
  (0.1, 0.1),
  (0.0, 0.0),
  (double.nan, double.nan),
  (-1.0, -1.0),
];

List<Case> reduceCases() => [
  for (final text in [
    ...clipTexts().values,
    for (final entry in _dense().entries)
      if (entry.key != 'long') entry.value.encode(),
  ])
    for (final (angle, distance) in _tolerances)
      {
        'op': 'reduce',
        'in': [text, number(angle), number(distance)],
        'expect': attempt(
          () =>
              reduceKeys(_read(text), angle: angle, distance: distance).encode(),
        ),
      },
  {
    'op': 'reduce',
    'in': [_dense()['long']!.encode(), 5e-4, 5e-4],
    'expect': attempt(() => reduceKeys(_dense()['long']!).encode()),
  },
];

Matrix4 _compose(List<Object?> parts) => Matrix4.compose(
  _vector(parts[0]!),
  _turn(parts[1]!),
  _vector(parts[2]!),
);

Vector3 _vector(Object parts) {
  final list = parts as List<Object?>;
  return Vector3(list[0]! as double, list[1]! as double, list[2]! as double);
}

Quaternion _turn(Object parts) {
  final list = parts as List<Object?>;
  return Quaternion(
    list[0]! as double,
    list[1]! as double,
    list[2]! as double,
    list[3]! as double,
  );
}

List<Object?> _rest(double x, double y, double z) =>
    partDump(Vector3(x, y, z), Quaternion.identity(), Vector3.all(1));

/// Skeletons as a fixture holds them: names, parents, and a position, a
/// rotation and a scale for each bone in its parent.
final Map<String, Map<String, Object?>> _skeletons = {
  'arm': {
    'names': ['root', 'upper', 'lower'],
    'parents': [-1, 0, 1],
    'local': [_rest(0, 1, 0), _rest(1, 0, 0), _rest(1, 0, 0)],
    'above': null,
  },
  'lifted': {
    'names': ['root', 'upper'],
    'parents': [-1, 0],
    'local': [_rest(0, 0, 0), _rest(1, 0, 0)],
    'above': _rest(0, 5, 0),
  },
  'biped': {
    'names': ['hips', 'spine', 'head', 'armL', 'handL', 'legL', 'footL'],
    'parents': [-1, 0, 1, 1, 3, 0, 5],
    'local': [
      _rest(0, 90, 0),
      partDump(
        Vector3(0, 25, 2),
        _about(1, 0, 0, 0.1),
        Vector3(1, 1.1, 1),
      ),
      _rest(0, 30, 0),
      partDump(Vector3(12, 20, 0), _about(0, 0, 1, -1.4), Vector3.all(1)),
      partDump(Vector3(25, 0, 0), _about(0, 1, 0, 0.3), Vector3(1, 1, 1.5)),
      partDump(Vector3(-8, -5, 0), _about(0, 0, 1, 3.1), Vector3.all(1)),
      _rest(0, -40, 0),
    ],
    'above': partDump(
      Vector3(0, 0, 0),
      _about(1, 0, 0, -math.pi / 2),
      Vector3.all(0.01),
    ),
  },
  'single': {
    'names': ['only'],
    'parents': [-1],
    'local': [_rest(2, 0, 0)],
    'above': null,
  },
  'turned': {
    'names': ['root', 'child', 'tip'],
    'parents': [-1, 0, 1],
    'local': [
      partDump(Vector3(0, 1, 0), _about(0, 0, 1, 0.8), Vector3.all(1)),
      _rest(1, 0, 0),
      _rest(1, 0, 0),
    ],
    'above': partDump(Vector3(0, 0, 0), _about(1, 0, 0, 0.3), Vector3.all(1)),
  },
  'none': {'names': [], 'parents': [], 'local': [], 'above': null},
  'twice': {
    'names': ['a', 'a'],
    'parents': [-1, 0],
    'local': [_rest(0, 0, 0), _rest(1, 0, 0)],
    'above': null,
  },
  'forward': {
    'names': ['a', 'b'],
    'parents': [1, -1],
    'local': [_rest(0, 0, 0), _rest(1, 0, 0)],
    'above': null,
  },
  'below': {
    'names': ['a', 'b'],
    'parents': [-1, -2],
    'local': [_rest(0, 0, 0), _rest(1, 0, 0)],
    'above': null,
  },
  'short': {
    'names': ['a', 'b', 'c'],
    'parents': [-1, 0],
    'local': [_rest(0, 0, 0), _rest(1, 0, 0), _rest(1, 0, 0)],
    'above': null,
  },
};

List<Object?> _bone(Vector3? p, Quaternion? q, Vector3? s) => [
  p == null ? null : vecDump(p),
  q == null ? null : quatDump(q),
  s == null ? null : vecDump(s),
];

final List<Map<String, Object?>> _poses = [
  {},
  {'upper': _bone(null, _about(0, 0, 1, math.pi / 2), null)},
  {'root': _bone(Vector3(0, 0, 2), null, Vector3.all(2))},
  {'lower': _bone(null, null, null), 'spine': _bone(null, null, null)},
  {'tail': _bone(Vector3(9, 9, 9), null, null)},
  {
    'hips': _bone(Vector3(0, 80, 5), _about(0, 1, 0, 0.6), null),
    'spine': _bone(null, _about(1, 0, 0, 0.3), Vector3(1, 2, 1)),
    'armL': _bone(null, _about(0, 0, 1, -0.8), null),
    'handL': _bone(Vector3(30, 1, 0), null, null),
    'footL': _bone(null, _about(0, 1, 1, 2.2), Vector3.all(0.5)),
    'upper': _bone(Vector3(2, 3, 4), _about(1, 1, 1, 0.4), null),
    'lower': _bone(null, _about(0, 0, 1, 1.2), Vector3(1, 1, 3)),
  },
];

Map<String, Object?> _restDump(
  Map<String, Object?> spec,
  Map<String, Object?> pose,
) {
  final above = spec['above'];
  final named = spec['names']! as List<Object?>;
  final skeleton = RestSkeleton(
    names: [for (final n in spec['names']! as List<Object?>) n! as String],
    parents: [for (final p in spec['parents']! as List<Object?>) p! as int],
    local: [
      for (final l in spec['local']! as List<Object?>)
        _compose(l! as List<Object?>),
    ],
    above: above == null ? null : _compose(above as List<Object?>),
  );
  final bones = {
    for (final entry in pose.entries)
      entry.key: () {
        final parts = entry.value! as List<Object?>;
        return BoneLocal(
          position: parts[0] == null ? null : _vector(parts[0]!),
          rotation: parts[1] == null ? null : _turn(parts[1]!),
          scale: parts[2] == null ? null : _vector(parts[2]!),
        );
      }(),
  };
  return {
    'length': skeleton.length,
    'reach': number(skeleton.reach),
    'index': [
      skeleton.indexOf(named.isEmpty ? '' : named.first! as String),
      skeleton.indexOf('upper'),
      skeleton.indexOf('nope'),
    ],
    'origins': [
      for (var i = 0; i < skeleton.length; i++) vecDump(skeleton.originOf(i)),
    ],
    'posed': [for (final v in skeleton.posedOrigins(bones)) vecDump(v)],
    'positions': [
      for (var i = 0; i < skeleton.length; i++) vecDump(skeleton.positionOf(i)),
    ],
    'rotations': [
      for (var i = 0; i < skeleton.length; i++) quatDump(skeleton.rotationOf(i)),
    ],
    'scales': [
      for (var i = 0; i < skeleton.length; i++) vecDump(skeleton.scaleOf(i)),
    ],
    'worlds': [
      for (var i = 0; i < skeleton.length; i++)
        quatDump(skeleton.worldRotationOf(i)),
    ],
    'parentWorlds': [
      for (var i = 0; i < skeleton.length; i++)
        quatDump(skeleton.parentWorldRotationOf(i)),
    ],
    'parentBodies': [
      for (var i = 0; i < skeleton.length; i++)
        [for (final v in skeleton.parentWorldOf(i).storage) number(v)],
    ],
  };
}

List<Case> restCases() => [
  for (final spec in _skeletons.values)
    for (final pose in _poses)
      {
        'op': 'rest',
        'in': [spec, pose],
        'expect': attempt(() => _restDump(spec, pose)),
      },
];

SceneEntity _entity(
  String id, {
  String? parent,
  bool prefab = false,
  Map<String, SceneComponent> more = const {},
}) => SceneEntity(
  id: id,
  name: id,
  parent: parent,
  components: {
    SceneComponents.transform: TransformComponent(),
    if (prefab) SceneComponents.prefab: const PrefabComponent(asset: 'lamp.oscene'),
    ...more,
  },
);

String _street() => SceneDocument(
  entities: [
    _entity('street1', prefab: true),
    _entity('street1/lamp3', parent: 'street1', prefab: true),
    _entity(
      'street1/lamp3/bulb',
      parent: 'street1/lamp3',
      more: {
        SceneComponents.light: const LightComponent(power: 5),
      },
    ),
    _entity(
      'street1/lamp3/bulb/glow',
      parent: 'street1/lamp3/bulb',
      more: {SceneComponents.mesh: const MeshComponent()},
    ),
    _entity('door'),
  ],
).encode();

const List<String> _owners = [
  'street1/lamp3',
  'street1/lamp3/bulb',
  'street1',
  'door',
  'nobody',
];

List<Case> sceneCases() {
  final scene = _street();
  return [
    for (final text in _texts().values)
      for (final owner in _owners)
        for (final at in [0.0, 0.3, 0.75, 1.2, 2.0])
          {
            'op': 'scene',
            'in': [scene, owner, text, number(at)],
            'expect': attempt(() {
              final document = SceneDocument.decode(scene).document;
              final frame = _read(text).sampleAt(at);
              final ops = sceneOpsFor(
                frame,
                document,
                ClipScope.inScene(document, owner),
              );
              return [
                for (final op in ops)
                  [op.id, op.type, op.field, tree(op.from), tree(op.to)],
              ];
            }),
          },
  ];
}
