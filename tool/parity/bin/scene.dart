import 'dart:convert';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/scene_documents.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

const _types = [
  SceneComponents.transform,
  SceneComponents.mesh,
  SceneComponents.material,
  SceneComponents.body,
  SceneComponents.joint,
  SceneComponents.motion,
  SceneComponents.zone,
  SceneComponents.vehicle,
  SceneComponents.wheel,
  SceneComponents.light,
  SceneComponents.camera,
  SceneComponents.splats,
  SceneComponents.terrain,
  SceneComponents.sprite,
  SceneComponents.tilemap,
  SceneComponents.parallax,
  SceneComponents.weather,
  SceneComponents.canvas,
  SceneComponents.particles,
  SceneComponents.soundSource,
  SceneComponents.soundListener,
  SceneComponents.soundSurface,
];

// One wrong value of each JSON kind, to put in each key in turn.
final List<Object?> _wrong = [
  'x',
  -3,
  2.5,
  <Object?>[],
  null,
  true,
  <String, Object?>{},
];

// Words that name a case of an enum, by the type and key that read them.
final Map<(String, String), List<String>> _words = {
  (SceneComponents.body, 'shape'): [for (final s in BodyShape.values) s.name],
  (SceneComponents.body, 'motion'): [for (final m in BodyMotion.values) m.name],
  (SceneComponents.joint, 'kind'): [for (final k in JointKind.values) k.name],
  (SceneComponents.particles, 'shape'): [
    for (final s in EmitterShape.values) s.name,
  ],
  (SceneComponents.particles, 'space'): [
    for (final s in EmitterSpace.values) s.name,
  ],
  (SceneComponents.soundSource, 'curve'): [
    for (final c in SoundCurve.values) c.name,
  ],
};

// Fields that hold a list or a map the plain probes would not fill in.
final Map<String, List<Map<String, Object?>>> _shaped = {
  SceneComponents.body: [
    {
      'locks': ['moveX', 'turnZ', 'nonsense', 4],
    },
    {
      'shape': 'hull',
      'hull': [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
    },
    {
      'shape': 'hull',
      'hull': [0, 0, 0, 1, 0],
    },
    {
      'shape': 'compound',
      'parts': <Object?>[
        {
          'shape': 'capsule',
          'radius': 0.25,
          'height': 1.5,
          'centre': [0, 1, 0],
          'rotation': [0, 0.7071067811865476, 0, 0.7071067811865476],
          'scale': [1, 2, 3],
        },
        {
          'shape': 'box',
          'size': [1, 2, 3],
          'rotation': [1, 2, 3],
        },
        7,
        {},
      ],
    },
    {
      'shape': 'mesh',
      'meshVertices': [0, 0, 0, 1, 0, 0, 0, 1, 0],
      'meshIndices': [0, 1, 2, 2, 1, 0],
      'shapeScale': [2, 2, 2],
    },
    {
      'mass': 12,
      'centreOfMass': [0, 0.5, 0],
      'inertia': [1, 2, 3],
      'surface': [0.5, 0, -0.5],
    },
  ],
  SceneComponents.joint: [
    {
      'kind': 'hinge',
      'limits': {
        'aboutX': [-1, 2],
        'alongY': [3, 1],
        'aboutZ': [0.5],
        'sideways': [0, 1],
      },
      'springs': {
        'alongX': [4],
        'aboutY': [4, 0.5, 0.25],
        'aboutZ': [-1, 2],
      },
    },
    {
      'kind': 'pulley',
      'over': [0, 5, 0, 1, 5, 0],
    },
    {
      'kind': 'path',
      'path': [0, 0, 0, 1, 0, 0, 1, 1, 0, 1],
      'follow': true,
    },
  ],
  SceneComponents.particles: [
    {
      'bursts': [
        {'time': 0, 'count': 20},
        {'time': 1.5, 'count': 3.6},
        {'time': -1, 'count': 5},
        {'time': 2, 'count': 70000},
        {'time': 2},
        3,
      ],
      'duration': 4,
      'lifetime': [3, 1],
      'speed': [1],
    },
    {
      'maxParticles': 100000000,
      'count': 5,
      'rate': -2,
      'extents': [1, 2, 3],
    },
  ],
  SceneComponents.vehicle: [
    {
      'ratios': [3.5, 2.1, 1.4, 1, 0.8],
      'reverse': -3.2,
    },
    {
      'ratios': [3.5, 'x', 0],
    },
  ],
  SceneComponents.wheel: [
    {'radius': 0.4, 'travel': 0.3, 'steers': true, 'drives': true},
  ],
  SceneComponents.motion: [
    {
      'clips': ['walk', 'run', 4, 'walk'],
      'autoplay': 'run',
    },
  ],
  SceneComponents.zone: [
    {
      'gravity': [0, -3, 0],
      'priority': 2.5,
    },
  ],
};

// Dart reads the input after a trip through JSON, as it would read a file:
// a map literal typed by inference is not the map a decoder makes.
Map<String, Object?> _read(String type, Map<String, Object?> json) {
  final decoded = jsonDecode(jsonEncode(json)) as Map<String, Object?>;
  return {
    'op': 'read',
    'in': [type, json],
    'expect': attempt(() => tree(SceneComponents.read(type, decoded).toJson())),
  };
}

List<Map<String, Object?>> _readCases() {
  final cases = <Map<String, Object?>>[
    _read('mystery', {
      'a': 1,
      'b': [2, 3.5],
    }),
  ];
  for (final type in _types) {
    final full = fullOf(type);
    cases
      ..add(_read(type, {}))
      ..add(_read(type, full))
      ..add(_read(type, changed(full)! as Map<String, Object?>));
    for (final key in full.keys) {
      for (final wrong in _wrong) {
        cases.add(_read(type, {...full, key: wrong}));
      }
      for (final word in _words[(type, key)] ?? const <String>[]) {
        cases.add(_read(type, {...full, key: word}));
      }
    }
    for (final shaped in _shaped[type] ?? const <Map<String, Object?>>[]) {
      cases
        ..add(_read(type, shaped))
        ..add(_read(type, {...full, ...shaped}));
    }
  }
  return cases;
}

// Angles, in degrees, that include the poles of the Euler decomposition
// (a quarter turn about y) and the wrap at half a turn.
const _degrees = [0.0, 30.0, 90.0, 135.0, 180.0, -90.0, -200.0, 370.0];

List<Map<String, Object?>> _rotationCases() => [
  for (final x in _degrees)
    for (final y in _degrees)
      for (final z in [0.0, 45.0, -135.0]) _rotation(Vector3(x, y, z)),
];

Map<String, Object?> _rotation(Vector3 degrees) {
  final turn = TransformComponent.rotationOf(degrees);
  final back = TransformComponent.anglesOf(turn);
  return {
    'op': 'rotation',
    'in': [number(degrees.x), number(degrees.y), number(degrees.z)],
    'expect': {
      'matrix': [for (final e in turn.storage) number(e)],
      'angles': [number(back.x), number(back.y), number(back.z)],
    },
  };
}

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_scene',
    name: 'scene',
    cases: [
      ..._readCases(),
      ..._rotationCases(),
      ...documentCases(),
      ...diffCases(),
      ...operationCases(),
      ...entityCases(),
    ],
  );
}
