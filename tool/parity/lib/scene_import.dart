import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_scene/orblit_scene.dart';

import 'fixture.dart';
import 'scene_export.dart';

/// The cases for scene import: the bytes of a document, a name, the files
/// beside it, and the scene that comes out or that it threw.
List<Map<String, Object?>> importCases() => [
  ..._writtenHere(),
  ..._trees(),
  ..._ours(),
  ..._transforms(),
  ..._lights(),
  ..._cameras(),
  ..._geometry(),
  ..._models(),
  ..._notAScene(),
  ..._objs(),
];

// What reading gives, with the scene as the tree it encodes, so a number is
// compared as a number and not as the text it printed.
Map<String, Object?> _result(SceneImported read) => {
  'document': tree(read.document.toJson()),
  'problems': read.problems,
  'wasWrittenHere': read.wasWrittenHere,
};

Map<String, Object?> _read(
  List<int> bytes,
  String name,
  Map<String, List<int>> files,
) => attempt(
  () => _result(
    readSceneFrom(
      Uint8List.fromList(bytes),
      name: name,
      files: {
        for (final e in files.entries) e.key: Uint8List.fromList(e.value),
      },
    ),
  ),
);

Map<String, Object?> _case(
  List<int> bytes, {
  String name = 'Scene',
  Map<String, List<int>> files = const {},
}) => {
  'op': 'import',
  'in': {
    'bytes': b64(bytes),
    'name': name,
    'files': {for (final e in files.entries) e.key: b64(e.value)},
  },
  'expect': _read(bytes, name, files),
};

// Every scene the export cases write, read back. The scenario is not repeated
// here: it is named by its place in the export cases.
List<Map<String, Object?>> _writtenHere() {
  final scenarios = exportScenarios();
  return [
    for (var i = 0; i < scenarios.length; i++)
      {
        'op': 'import',
        'in': {'export': i},
        'expect': attempt(() {
          final written = writeScenario(scenarios[i]);
          return _result(
            readSceneFrom(
              written.first.bytes,
              files: {
                for (final file in written.files.skip(1)) file.name: file.bytes,
              },
            ),
          );
        }),
      },
  ];
}

Map<String, Object?> _node(
  String name, {
  Object? light,
  Map<String, Object?> more = const {},
}) => {
  'name': name,
  if (light != null)
    'extensions': {
      'KHR_lights_punctual': {'light': light},
    },
  ...more,
};

List<int> _json(Map<String, Object?> json) => utf8.encode(
  jsonEncode({
    'asset': {'version': '2.0'},
    ...json,
  }),
);

List<int> _text(String text) => utf8.encode(text);

Map<String, Object?> _lights2(List<Object?> lights) => {
  'extensions': {
    'KHR_lights_punctual': {'lights': lights},
  },
};

List<Map<String, Object?>> _trees() => [
  _case(_json({})),
  _case(_json({'nodes': []})),
  _case(_json({'nodes': 5})),
  _case(
    _json({
      'nodes': [
        1,
        _node('a'),
        'x',
        _node('b'),
        null,
      ],
    }),
  ),
  _case(
    _json({
      'scene': 0,
      'scenes': [
        {
          'name': 'Imported',
          'nodes': [0],
        },
      ],
      'nodes': [
        _node(
          'Root',
          more: {
            'children': [1],
          },
        ),
        _node(
          'Child',
          more: {
            'translation': [1.0, 2.0, 3.0],
          },
        ),
      ],
    }),
    name: 'Given',
  ),
  _case(
    _json({
      'nodes': [
        _node(
          'A',
          more: {
            'children': [-1, 99, 0, 'x', 1.5, 1, 1],
          },
        ),
        _node('B'),
      ],
    }),
  ),
  // 4294967297 wraps to 1 in 32 bits.
  _case(
    _json({
      'nodes': [
        _node(
          'A',
          more: {
            'children': [4294967297],
          },
        ),
        _node('B'),
      ],
    }),
  ),
  _case(
    _json({
      'nodes': [
        _node(
          'A',
          more: {
            'children': [2],
          },
        ),
        _node(
          'B',
          more: {
            'children': [2],
          },
        ),
        _node('C'),
      ],
    }),
  ),
  _case(
    _json({
      'nodes': [
        _node(
          'A',
          more: {
            'children': [1],
          },
        ),
        _node(
          'B',
          more: {
            'children': [0],
          },
        ),
      ],
    }),
  ),
  _case(
    _json({
      'nodes': [
        {'children': 'x'},
        {'name': 7},
        {'name': ''},
      ],
    }),
  ),
  for (final scene in [5, -1, '1', 1.0, null, 0, 1, 2])
    _case(
      _json({
        'scene': scene,
        'scenes': [
          {'name': 'First'},
          {'name': 'Second'},
        ],
        'nodes': [_node('A')],
      }),
    ),
  _case(
    _json({
      'scenes': [
        'x',
        {'name': 'Late'},
      ],
      'nodes': [_node('A')],
    }),
  ),
  _case(
    _json({
      'scenes': [
        {'name': 4},
      ],
      'nodes': [_node('A')],
    }),
    name: 'Fallback',
  ),
];

Map<String, Object?> _ourNode(
  String name, {
  Object? id,
  Object? components = const <String, Object?>{},
  Object? visible,
  List<int>? children,
}) => {
  'name': name,
  if (children != null) 'children': children,
  'extras': {
    'orblit': {
      if (id != null) 'id': id,
      'components': components,
      if (visible != null) 'visible': visible,
    },
  },
};

Map<String, Object?> _ourScene(Map<String, Object?> orblit) => {
  'scenes': [
    {
      'name': 'Ours',
      'nodes': [0],
      'extras': {'orblit': orblit},
    },
  ],
};

List<Map<String, Object?>> _ours() => [
  _case(
    _json({
      'nodes': [
        _ourNode('A', id: 'a'),
        _ourNode('B'),
        _ourNode('C', id: ''),
        _ourNode('D', id: 4),
      ],
    }),
  ),
  _case(
    _json({
      'nodes': [
        _ourNode('A', id: 'a', components: 5),
        _ourNode(
          'B',
          id: 'b',
          components: {
            'transform': 3,
            'camera': {'fieldOfView': 30},
            'mystery': {'x': 1},
            'mesh': <Object?>[],
          },
        ),
      ],
    }),
  ),
  for (final visible in [false, true, 0, 'no', null])
    _case(
      _json({
        'nodes': [_ourNode('A', id: 'a', visible: visible)],
      }),
    ),
  _case(
    _json({
      'nodes': [_ourNode('A', id: 'same'), _ourNode('B', id: 'same')],
    }),
  ),
  // A node that is not an entity between two that are.
  _case(
    _json({
      'nodes': [
        _ourNode('Top', id: 'top', children: [1]),
        {
          'name': 'Wrapper',
          'children': [2],
        },
        _ourNode('Under', id: 'under'),
      ],
    }),
  ),
  // Nodes between two entities that are a loop, and not entities.
  _case(
    _json({
      'nodes': [
        _ourNode('Under', id: 'under'),
        {
          'name': 'Loop1',
          'children': [2, 0],
        },
        {
          'name': 'Loop2',
          'children': [1],
        },
      ],
    }),
  ),
  // Versions below four are left out on purpose: the object arrives without
  // the kind they need, and the two sides word that note differently.
  for (final version in [4, 5, 99, 'x', null])
    _case(
      _json({
        ..._ourScene({
          if (version != null) 'formatVersion': version,
          'settings': {'ambient': 100},
        }),
        'nodes': [
          _ourNode(
            'A',
            id: 'a',
            components: {
              'transform': {
                'position': [1, 2, 3],
              },
              'prefab': {'asset': 'props/a.oprefab'},
            },
          ),
        ],
      }),
    ),
  for (final settings in [5, 'x', <Object?>[], <String, Object?>{}, null])
    _case(
      _json({
        ..._ourScene({'settings': settings}),
        'nodes': [_ourNode('A', id: 'a')],
      }),
    ),
];

List<Map<String, Object?>> _transforms() => [
  for (final more in <Map<String, Object?>>[
    {
      'matrix': [
        // A turn about y and a scale, then a move: the columns of
        // [translate 5 6 7] * [turn 40] * [scale 2 3 4].
        1.5320888862379562, 0, -1.2855752193730787, 0,
        0, 3, 0, 0,
        2.5711504387461574, 0, 3.0641777724759125, 0,
        5, 6, 7, 1,
      ],
    },
    {
      'matrix': [0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 2, 3, 1],
    },
    {
      'matrix': [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 5, 6, 1],
    },
    {
      'matrix': [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 2, 3],
      'translation': [9, 9, 9],
    },
    {
      'matrix': [1, 0, 0, 0, 0, 'x', 0, 0, 0, 0, 1, 0, 1, 2, 3, 1],
      'translation': [9.0, 9.0, 9.0],
    },
    {
      'matrix': [for (var i = 0; i < 16; i++) i == 0 || i == 5 || i == 10 || i == 15 ? 1 : 0],
      'translation': [9, 9, 9],
    },
    {
      'translation': [1, 2],
      'rotation': [0, 0, 0],
      'scale': [1, 2, 3, 4],
    },
    {
      'rotation': [0.0, 0.0, 0.0, 0.0],
    },
    {
      'rotation': [0.0, 0.0, 0.0, 2.0],
    },
    {
      'rotation': [0.5, 0.5, 0.5, 0.5],
    },
    {
      'rotation': [0.0, 0.7071067811865476, 0.0, 0.7071067811865476],
    },
    {
      'rotation': [0.0, 1.0, 0.0, 0.0],
    },
    {
      'scale': [2.0, 3.0, 4.0],
    },
    {
      'translation': [1.0, 'x', 3.0],
    },
    {
      'translation': [1.0, 2.0, 3.0, 4.0],
    },
    {'matrix': 'x', 'rotation': 7},
  ])
    _case(
      _json({
        'nodes': [_node('N', more: more)],
      }),
    ),
];

Map<String, Object?> _light(
  String? type, {
  Object? intensity = 1,
  Object? color = const [1.0, 1.0, 1.0],
  Map<String, Object?>? spot,
  Map<String, Object?>? ours,
}) => {
  'type': ?type,
  'color': ?color,
  'intensity': ?intensity,
  'spot': ?spot,
  if (ours != null) 'extras': {'orblit': ours},
};

List<Map<String, Object?>> _lights() => [
  _case(
    _json({
      ..._lights2([
        _light('directional', intensity: 819.6),
        _light('point', intensity: 54.4),
        _light(
          'spot',
          intensity: 400,
          spot: {'innerConeAngle': 0.4, 'outerConeAngle': 0.6},
        ),
        _light('spot', intensity: 400),
        _light('spot', spot: {'outerConeAngle': 0.0}),
        _light('spot', spot: {'innerConeAngle': 1.0, 'outerConeAngle': 0.5}),
        _light('spot', spot: {'innerConeAngle': -1.0, 'outerConeAngle': 0.5}),
        _light('spot', spot: {'innerConeAngle': 'x', 'outerConeAngle': 'y'}),
        _light('spot', spot: {}),
        _light('weird'),
        _light(null),
        _light('point', intensity: null),
        _light('point', intensity: 'bright'),
        _light('point', color: [1.0, 0.5]),
        _light('point', color: [0.5, 0.25, 0.1]),
        _light('point', color: 'red'),
        _light('point', ours: {'kind': 'area', 'sourceRadius': 0.75}),
        _light('point', ours: {'kind': 'area'}),
        _light('point', ours: {'kind': 'disc', 'sourceRadius': 'x'}),
        _light('directional', ours: {'kind': 'area', 'sourceRadius': 2}),
      ]),
      'nodes': [
        for (var i = 0; i < 20; i++) _node('L$i', light: i),
      ],
    }),
  ),
  _case(
    _json({
      ..._lights2([_light('point')]),
      'nodes': [
        for (final at in [0, 1, 5, -1, 0.5, '0', null, 4294967296])
          _node('N', light: at),
      ],
    }),
  ),
  _case(
    _json({
      'extensions': {'KHR_lights_punctual': 5},
      'nodes': [_node('N', light: 0)],
    }),
  ),
  _case(
    _json({
      'extensions': {
        'KHR_lights_punctual': {'lights': 'x'},
      },
      'nodes': [_node('N', light: 0)],
    }),
  ),
  _case(
    _json({
      ..._lights2([
        'x',
        _light('spot'),
      ]),
      'nodes': [_node('N', light: 0), _node('M', light: 1)],
    }),
  ),
];

List<Map<String, Object?>> _cameras() => [
  _case(
    _json({
      'cameras': [
        {
          'type': 'perspective',
          'perspective': {'yfov': 1.1344640137963142, 'znear': 0.2, 'zfar': 800},
        },
        {
          'type': 'perspective',
          'perspective': {'yfov': 0.5},
        },
        {'type': 'perspective'},
        {
          'type': 'orthographic',
          'orthographic': {'xmag': 2, 'ymag': 2, 'znear': 0, 'zfar': 9},
        },
        {
          'type': 'perspective',
          'perspective': {'yfov': 1.0, 'znear': 'x', 'zfar': null},
          'extras': {
            'orblit': {'fieldOfView': 33.5},
          },
        },
        {
          'perspective': {'yfov': 1.0},
          'extras': {
            'orblit': {'fieldOfView': 'wide'},
          },
        },
        7,
      ],
      'nodes': [
        for (final at in [0, 1, 2, 3, 4, 5, 6, 7, -1, 0.5, 'x'])
          _node('C', more: {'camera': at}),
      ],
    }),
  ),
];

// The corners and, optionally, the order of a set of triangles, as the parts
// of a document that point at a buffer.
Map<String, Object?> _buffer(
  List<double> corners, {
  List<int>? order,
  Map<String, Object?> primitive = const {},
  int count = -1,
}) {
  final positions = Float32List.fromList(corners);
  final indices = order == null ? null : Uint16List.fromList(order);
  final bytes = (BytesBuilder()
        ..add(positions.buffer.asUint8List())
        ..add(indices?.buffer.asUint8List() ?? const <int>[]))
      .toBytes();
  return {
    'buffers': [
      {
        'byteLength': bytes.length,
        'uri': 'data:application/octet-stream;base64,${base64Encode(bytes)}',
      },
    ],
    'bufferViews': [
      {'buffer': 0, 'byteOffset': 0, 'byteLength': positions.lengthInBytes},
      if (indices != null)
        {
          'buffer': 0,
          'byteOffset': positions.lengthInBytes,
          'byteLength': indices.lengthInBytes,
        },
    ],
    'accessors': [
      {
        'bufferView': 0,
        'componentType': 5126,
        'count': count < 0 ? corners.length ~/ 3 : count,
        'type': 'VEC3',
      },
      if (indices != null)
        {
          'bufferView': 1,
          'componentType': 5123,
          'count': indices.length,
          'type': 'SCALAR',
        },
    ],
    'meshes': [
      {
        'primitives': [
          {
            'attributes': {'POSITION': 0},
            'indices': ?(indices == null ? null : 1),
            ...primitive,
          },
        ],
      },
    ],
  };
}

const _triangle = [0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0];
const _quad = [0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 0.0, 1.0, 0.0];

Map<String, Object?> _meshNode(Map<String, Object?> buffer) => {
  ...buffer,
  'nodes': [_node('M', more: {'mesh': 0})],
};

List<Map<String, Object?>> _geometry() => [
  _case(_json(_meshNode(_buffer(_triangle, order: [0, 1, 2])))),
  _case(_json(_meshNode(_buffer(_triangle)))),
  _case(_json(_meshNode(_buffer(_quad, order: [0, 1, 2, 0, 2, 3])))),
  _case(_json(_meshNode(_buffer(_quad)))),
  _case(_json(_meshNode(_buffer(_quad, order: [0, 1, 2, 0])))),
  _case(_json(_meshNode(_buffer(_triangle, order: [0, 1, 7])))),
  _case(_json(_meshNode(_buffer(_triangle, order: [])))),
  _case(_json(_meshNode(_buffer(_triangle, count: 9)))),
  _case(_json(_meshNode(_buffer(_triangle, count: 2)))),
  _case(_json(_meshNode(_buffer([])))),
  for (final mode in [0, 1, 3, 4, 5, 6, 'x', -1, 4.0, null])
    _case(_json(_meshNode(_buffer(_triangle, primitive: {'mode': mode})))),
  for (final material in [0, 1, -1, 'x'])
    _case(
      _json({
        ..._meshNode(_buffer(_triangle, primitive: {'material': material})),
        'materials': [
          {
            'pbrMetallicRoughness': {
              'baseColorFactor': [0.5, 0.25, 0.125, 1.0],
            },
          },
        ],
      }),
    ),
  for (final factor in [
    [1.0, 0.0],
    [1.0, 0.0, 0.0],
    [1.0, 'x', 0.0],
    'red',
    null,
  ])
    _case(
      _json({
        ..._meshNode(_buffer(_triangle, primitive: {'material': 0})),
        'materials': [
          {
            'pbrMetallicRoughness': {'baseColorFactor': factor},
          },
          5,
        ],
      }),
    ),
  for (final at in [-1, 5, 1.5, 'x', 4294967296, 9999999999, null])
    _case(
      _json(
        _meshNode(
          _buffer(
            _triangle,
            order: [0, 1, 2],
            primitive: {
              'attributes': {'POSITION': at},
            },
          ),
        ),
      ),
    ),
  for (final at in [-1, 5, 1.5, 'x', 4294967296, 0])
    _case(
      _json(
        _meshNode(
          _buffer(_triangle, order: [0, 1, 2], primitive: {'indices': at}),
        ),
      ),
    ),
  _case(
    _json({
      ..._buffer(_triangle, order: [0, 1, 2]),
      'meshes': [
        {
          'primitives': [
            {
              'attributes': {'POSITION': 0},
              'indices': 1,
            },
            {
              'attributes': {'POSITION': 0},
              'indices': 1,
              'mode': 1,
            },
            {
              'attributes': {'POSITION': 0},
              'indices': 1,
              'mode': 5,
            },
            {
              'attributes': {'POSITION': 0},
            },
            5,
          ],
        },
        {'primitives': 'x'},
        7,
      ],
      'nodes': [
        _node('Both', more: {'mesh': 0}),
        _node('Odd', more: {'mesh': 1}),
        _node('Past', more: {'mesh': 2}),
        _node('Nowhere', more: {'mesh': 3}),
      ],
    }),
  ),
];

// Models written by the mesh package, which have normals, texture coordinates
// and materials, and are not scenes.
List<Map<String, Object?>> _models() => [
  for (final kind in [
    ShapeKind.cube,
    ShapeKind.sphere,
    ShapeKind.cylinder,
    ShapeKind.torus,
  ])
    _case(
      Shape(kind: kind).build().toGlb(
        name: kind.name,
        materials: const [GlbMaterial(name: 'Paint')],
      ),
      name: kind.name,
    ),
];

List<Map<String, Object?>> _notAScene() => [
  _case([]),
  _case([7, 7, 7, 7, 7, 7]),
  _case(_text('[1, 2, 3]')),
  _case(_text('  [1, 2]')),
  _case(_text('{')),
  _case(_text('{"nodes": [}')),
  _case(_text('"glTF"')),
  _case([0xEF, 0xBB, 0xBF, ..._text('[1]')]),
  _case([0xEF, 0xBB, 0xBF, ..._text('{"nodes": [{"name": "A"}]}')]),
  _case([0xEF, 0xBB, 0xBF]),
  _case([0xEF, 0xBB]),
  _case(_text('glTF')),
  _case([..._text('glTF'), 2, 0, 0, 0, 12, 0, 0, 0]),
  _case([..._text('glTF'), 2, 0, 0, 0, 99, 0, 0, 0]),
  _case(_text('\n\t {"nodes": [{"name": "A"}]}')),
  _case(_text('{"nodes": [{"name": "A"}]} trailing')),
  _case(_text('{"buffers": [{"uri": "missing.bin", "byteLength": 4}]}')),
  _case(
    _json({
      'accessors': [
        {'componentType': 5126, 'count': 3},
      ],
      'meshes': [
        {
          'primitives': [
            {
              'attributes': {'POSITION': 0},
            },
          ],
        },
      ],
      'nodes': [_node('Hollow', more: {'mesh': 0})],
    }),
  ),
];

const _two =
    'mtllib two.mtl\n'
    'v 0 0 0\n'
    'v 1 0 0\n'
    'v 1 1 0\n'
    'v 0 1 0\n'
    'o floor\n'
    'usemtl red\n'
    'f 1 2 3 4\n'
    'o wall\n'
    'usemtl blue\n'
    'f 1 2 3\n'
    'f 1 3 4\n';

const _mtl = 'newmtl red\nKd 1 0 0\nnewmtl blue\nKd 0 0 1\n';

Map<String, List<int>> _beside(String text) => {'two.mtl': _text(text)};

List<Map<String, Object?>> _objs() => [
  _case(_text(_two), name: 'Level', files: _beside(_mtl)),
  _case(_text(_two)),
  _case(
    _text(_two.replaceAll('o wall', 'o floor')),
    files: _beside(_mtl),
  ),
  _case(_text(_two.replaceAll('o wall', 'g wall')), files: _beside(_mtl)),
  _case(_text(_two.replaceAll('\n', '\r\n')), files: _beside(_mtl)),
  _case(_text(_two), files: _beside('')),
  _case(_text(_two), files: _beside('newmtl red\nnewmtl blue\nKd 1 0\n')),
  _case(
    _text(_two),
    files: _beside('newmtl red\nKd 0.5 0.25 0.125\nnewmtl red\nKd 1 1 1\n'),
  ),
  _case(_text(_two.replaceAll('usemtl blue', 'usemtl green')), files: _beside(_mtl)),
  _case(_text(_two.replaceAll('usemtl red\n', '')), files: _beside(_mtl)),
  _case(_text('v 0 0 0\n')),
  _case(_text('')),
  _case(_text('# only a comment\n')),
  _case(_text('o a\no b\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\nf -3 -2 -1\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/1/1 2/2/2 3/3/3\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 9\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2\n')),
  _case(_text('v 0 0 0 1\nv 1 0 0 1\nv 0 1 0 1\nf 1 2 3\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\no a\nf 1 2 3\no b\no c\nf 3 2 1\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\ng a b\nf 1 2 3\n')),
  _case(_text('v 0 0 0\nv 1 0 0\nv 0 1 0\no\nf 1 2 3\n')),
  _case(
    _text('mtllib sub/other.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl m\nf 1 2 3\n'),
    files: {'sub/other.mtl': _text('newmtl m\nKd 0.2 0.4 0.6\n')},
  ),
  _case(
    _box.first.bytes,
    files: {for (final file in _box) file.name: file.bytes},
  ),
];

final _box = const Shape(kind: ShapeKind.cube).build().toObj(name: 'box');
