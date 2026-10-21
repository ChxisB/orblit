import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_parity_tool/fixture.dart';

const _square = [
  -1.0,
  0.0,
  -1.0,
  1.0,
  0.0,
  -1.0,
  1.0,
  0.0,
  1.0,
  -1.0,
  0.0,
  1.0,
];

const _quad = [0, 2, 1, 0, 3, 2];

// A number JSON can write and Dart's encoder cannot, swapped in after encoding.
const _infinity = '__infinity__';

Map<String, Object?> _out(CollisionMesh mesh) => {
  'vertices': [for (final v in mesh.vertices) number(v)],
  'indices': mesh.indices,
  'encoded': utf8.decode(mesh.encode()),
};

Future<Map<String, Object?>> _attemptAsync(
  Future<Object?> Function() body,
) async {
  try {
    return {'value': await body()};
  } on Object {
    return {'throws': true};
  }
}

Map<String, Object?> _case(String op, String text, Object? Function() body) => {
  'op': op,
  'in': [text],
  'expect': attempt(body),
};

// A model with one mesh of one primitive, in a data URI. `edit` changes the
// document before it is written, for the shapes a plain call cannot make.
String _model({
  List<Object?>? nodes,
  int mode = 4,
  List<int> indices = _quad,
  void Function(Map<String, Object?> doc)? edit,
}) {
  final binary = Uint8List(_square.length * 4 + indices.length * 2);
  final data = ByteData.sublistView(binary);
  for (var i = 0; i < _square.length; i++) {
    data.setFloat32(i * 4, _square[i], Endian.little);
  }
  for (var i = 0; i < indices.length; i++) {
    data.setUint16(_square.length * 4 + i * 2, indices[i], Endian.little);
  }
  // Through JSON, so every map and list is the loose kind an edit can put anything in.
  final doc =
      jsonDecode(
            jsonEncode(<String, Object?>{
              'asset': {'version': '2.0'},
              'buffers': <Object?>[
                {
                  'byteLength': binary.length,
                  'uri':
                      'data:application/octet-stream;base64,${base64Encode(binary)}',
                },
              ],
              'bufferViews': <Object?>[
                {'buffer': 0, 'byteLength': _square.length * 4},
                {
                  'buffer': 0,
                  'byteOffset': _square.length * 4,
                  'byteLength': indices.length * 2,
                },
              ],
              'accessors': <Object?>[
                {
                  'bufferView': 0,
                  'componentType': 5126,
                  'count': 4,
                  'type': 'VEC3',
                },
                {
                  'bufferView': 1,
                  'componentType': 5123,
                  'count': indices.length,
                  'type': 'SCALAR',
                },
              ],
              'meshes': <Object?>[
                {
                  'primitives': <Object?>[
                    {
                      'attributes': {'POSITION': 0},
                      'indices': 1,
                      'mode': mode,
                    },
                  ],
                },
              ],
              'nodes':
                  nodes ??
                  <Object?>[
                    {'mesh': 0},
                  ],
              'scenes': <Object?>[
                {
                  'nodes': [0],
                },
              ],
              'scene': 0,
            }),
          )
          as Map<String, Object?>;
  edit?.call(doc);
  return jsonEncode(doc).replaceAll('"$_infinity"', '1e999');
}

Map<String, Object?> _primitive(Map<String, Object?> doc) =>
    ((((doc['meshes']! as List)[0] as Map)['primitives']! as List)[0]
        as Map<String, Object?>);

List<Object?> _list(Map<String, Object?> doc, String key) =>
    doc[key]! as List<Object?>;

// Nodes that put the mesh through one transform each.
List<String> _transforms() {
  Object? node(String key, Object? value) => {'mesh': 0, key: value};
  return [
    for (final (key, value) in <(String, Object?)>[
      ('translation', [3, -2, 0.5]),
      ('translation', [1e300, 0, 0]),
      ('translation', [1, 2]),
      ('translation', [1, 2, 3, 4]),
      ('translation', [1, 'a', 3]),
      ('translation', [1, true, 3]),
      ('translation', [1, null, 3]),
      ('translation', [1, 2, _infinity]),
      ('translation', 'abc'),
      ('translation', {}),
      ('translation', null),
      ('rotation', [0, 0.7071067811865476, 0, 0.7071067811865476]),
      ('rotation', [0.5, 0.5, 0.5, 0.5]),
      ('rotation', [1, 2, 3, 4]),
      ('rotation', [0, 0, 0, 2]),
      ('rotation', [0, 0, 0, 0]),
      ('rotation', [0, 0, 0]),
      ('rotation', [0.1, 0.2, 0.3, 0.4]),
      ('rotation', [1e-200, 0, 0, 0]),
      ('rotation', [-0.3, 0.2, 0.9, -0.1]),
      ('scale', [2, 3, 4]),
      ('scale', [-1, 1, 1]),
      ('scale', [-1, -1, 1]),
      ('scale', [-1, -1, -1]),
      ('scale', [0, 1, 1]),
      ('scale', [1e200, 1e200, 1e200]),
      ('scale', [1]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 5, 6, 7, 1]),
      ('matrix', [-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]),
      ('matrix', [0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1]),
      ('matrix', [2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 'x']),
      ('matrix', null),
      ('matrix', {}),
      ('skin', 0),
      ('skin', null),
      ('mesh', 5),
      ('mesh', -1),
      ('mesh', '0'),
      ('mesh', 0.5),
      ('mesh', 0.0),
      ('children', [0]),
      ('children', 'a'),
      ('children', [0.5]),
      ('children', [null]),
      ('children', [9]),
      ('children', [-1]),
      ('children', <Object?>[]),
    ])
      _model(
        nodes: [
          if (key == 'mesh') {'mesh': value} else node(key, value),
        ],
      ),
  ];
}

List<String> _hierarchies() => [
  // The test of the Dart package: a parent, two instances and a stray node.
  _model(
    nodes: [
      {
        'translation': [3, 0, 0],
        'children': [1, 2],
      },
      {
        'mesh': 0,
        'translation': [0, 2, 0],
        'scale': [2, 1, 1],
      },
      {
        'mesh': 0,
        'translation': [0, 4, 0],
      },
      {
        'mesh': 0,
        'translation': [999, 0, 0],
      },
    ],
  ),
  // A rotated parent over a scaled child: the order of the product.
  _model(
    nodes: [
      {
        'rotation': [0, 0.7071067811865476, 0, 0.7071067811865476],
        'translation': [1, 0, 0],
        'children': [1],
      },
      {
        'mesh': 0,
        'scale': [2, 1, 3],
        'translation': [0, 1, 0],
      },
    ],
  ),
  // A node held by two parents is two instances, not a cycle.
  _model(
    nodes: [
      {
        'children': [1, 2],
      },
      {
        'translation': [5, 0, 0],
        'children': [3],
      },
      {
        'translation': [0, 5, 0],
        'children': [3],
      },
      {'mesh': 0},
    ],
  ),
  // A mirror in a parent turns a child inside out, and a second one turns it back.
  _model(
    nodes: [
      {
        'scale': [-1, 1, 1],
        'children': [1],
      },
      {'mesh': 0},
    ],
  ),
  _model(
    nodes: [
      {
        'scale': [-1, 1, 1],
        'children': [1],
      },
      {
        'scale': [1, -1, 1],
        'mesh': 0,
      },
    ],
  ),
  // Cycles.
  _model(
    nodes: [
      {
        'mesh': 0,
        'children': [0],
      },
    ],
  ),
  _model(
    nodes: [
      {
        'children': [1],
      },
      {
        'mesh': 0,
        'children': [0],
      },
    ],
  ),
  _model(
    nodes: [
      {
        'children': [1],
      },
      {
        'children': [2],
      },
      {
        'children': [0],
        'mesh': 0,
      },
    ],
  ),
  // A skin on a node the scene never reaches is not met.
  _model(
    nodes: [
      {'mesh': 0},
      {'mesh': 0, 'skin': 0},
    ],
  ),
  // Nodes held by no scene, and a scene that reaches none of them.
  _model(edit: (doc) => doc.remove('scenes')),
  _model(edit: (doc) => doc.remove('scene')),
  _model(
    nodes: [
      {
        'mesh': 0,
        'children': [1],
      },
      {
        'mesh': 0,
        'translation': [0, 1, 0],
      },
      {
        'mesh': 0,
        'translation': [0, 2, 0],
      },
    ],
    edit: (doc) => doc.remove('scenes'),
  ),
  _model(
    nodes: [
      {
        'children': [1],
      },
      {
        'children': [0],
      },
      {'mesh': 0},
    ],
    edit: (doc) => doc.remove('scenes'),
  ),
  _model(
    nodes: [
      {'skin': 0},
    ],
    edit: (doc) => doc.remove('scenes'),
  ),
  // No scenes and no nodes: every mesh as it is.
  _model(
    edit: (doc) {
      doc
        ..remove('scenes')
        ..remove('nodes');
    },
  ),
  _model(
    edit: (doc) {
      doc
        ..remove('scenes')
        ..['nodes'] = <Object?>[];
    },
  ),
  _model(
    edit: (doc) {
      doc
        ..remove('scenes')
        ..remove('nodes');
      final meshes = _list(doc, 'meshes');
      meshes.add(
        Map<String, Object?>.of(meshes.first! as Map<String, Object?>),
      );
    },
  ),
  // Scenes: the one asked for, one past the end, below zero, not a number.
  for (final scene in <Object?>[0, 1, 2, -1, 1.0, '0', null, true, 3000000000])
    _model(
      edit: (doc) {
        _list(doc, 'scenes').add({
          'nodes': [1],
        });
        _list(doc, 'nodes').add({
          'mesh': 0,
          'translation': [0, 9, 0],
        });
        doc['scene'] = scene;
      },
    ),
  _model(
    edit: (doc) => _list(doc, 'scenes')
      ..clear()
      ..add(<String, Object?>{}),
  ),
  _model(
    edit: (doc) => _list(doc, 'scenes')
      ..clear()
      ..add({'nodes': <Object?>[]}),
  ),
  for (final nodes in <Object?>[
    [5],
    [-1],
    ['a'],
    [0.5],
    [0, 0],
    'zero',
    null,
  ])
    _model(
      edit: (doc) => _list(doc, 'scenes')
        ..clear()
        ..add({'nodes': nodes}),
    ),
  // Lists that are not lists of objects.
  _model(edit: (doc) => _list(doc, 'nodes').insert(0, null)),
  _model(edit: (doc) => _list(doc, 'nodes').add('a')),
  _model(edit: (doc) => _list(doc, 'nodes').add(3)),
  _model(edit: (doc) => doc['nodes'] = {'mesh': 0}),
  _model(edit: (doc) => doc['nodes'] = 'a'),
  _model(edit: (doc) => doc['nodes'] = null),
  _model(
    edit: (doc) => doc['scenes'] = {
      'nodes': [0],
    },
  ),
  _model(edit: (doc) => _list(doc, 'scenes').add([1])),
  _model(edit: (doc) => doc['scenes'] = null),
  _model(edit: (doc) => doc['meshes'] = 'a'),
  _model(edit: (doc) => _list(doc, 'meshes').add('a')),
  _model(edit: (doc) => doc['meshes'] = null),
  _model(edit: (doc) => doc['meshes'] = <Object?>[]),
  _model(edit: (doc) => _list(doc, 'bufferViews').add('a')),
  _model(edit: (doc) => doc['bufferViews'] = 5),
  _model(edit: (doc) => doc['bufferViews'] = null),
];

List<String> _primitives() => [
  // Modes: a list, a strip, a fan, and the ones a collider skips.
  for (final mode in [-1, 0, 1, 2, 3, 4, 5, 6, 7, 8]) _model(mode: mode),
  for (final indices in <List<int>>[
    [0, 1, 2],
    [0, 1, 2, 3],
    [0, 1, 2, 3, 0, 1],
    [0, 1, 2, 3, 2, 1, 0],
    [0, 1],
    [],
  ]) ...[
    _model(mode: 5, indices: indices),
    _model(mode: 6, indices: indices),
    _model(indices: indices),
  ],
  _model(indices: [0, 1, 2, 2, 1, 3, 3, 1, 0]),
  _model(indices: [0, 1, 4]),
  _model(indices: [0, 1, 65535]),
  _model(mode: 5, indices: [0, 1, 2, 7]),
  _model(
    mode: 5,
    indices: [0, 1, 2, 3, 0],
    nodes: [
      {
        'mesh': 0,
        'scale': [-1, 1, 1],
      },
    ],
  ),
  _model(
    mode: 6,
    indices: [0, 1, 2, 3, 0],
    nodes: [
      {
        'mesh': 0,
        'scale': [1, -2, 1],
      },
    ],
  ),
  // Mode written in the ways JSON allows.
  for (final mode in <Object?>[null, '4', 4.0, true, <Object?>[], 3000000000])
    _model(edit: (doc) => _primitive(doc)['mode'] = mode),
  // No indices: the points in order.
  _model(mode: 6, edit: (doc) => _primitive(doc).remove('indices')),
  _model(mode: 5, edit: (doc) => _primitive(doc).remove('indices')),
  _model(edit: (doc) => _primitive(doc).remove('indices')),
  _model(edit: (doc) => _primitive(doc)['indices'] = null),
  _model(edit: (doc) => _primitive(doc)['indices'] = 'a'),
  _model(edit: (doc) => _primitive(doc)['indices'] = 0),
  _model(edit: (doc) => _primitive(doc)['indices'] = 7),
  _model(edit: (doc) => _primitive(doc)['indices'] = -1),
  // Refusals and what stands in front of them.
  _model(
    edit: (doc) => _primitive(doc)['extensions'] = {
      'KHR_draco_mesh_compression': <String, Object?>{},
    },
  ),
  _model(
    mode: 1,
    edit: (doc) => _primitive(doc)['extensions'] = {
      'KHR_draco_mesh_compression': <String, Object?>{},
    },
  ),
  _model(edit: (doc) => _primitive(doc)['extensions'] = {'OTHER': 1}),
  _model(edit: (doc) => _primitive(doc)['extensions'] = 'a'),
  _model(
    edit: (doc) => _primitive(doc)['targets'] = [
      {'POSITION': 0},
    ],
  ),
  _model(edit: (doc) => _primitive(doc)['targets'] = null),
  _model(mode: 2, edit: (doc) => _primitive(doc)['targets'] = <Object?>[]),
  _model(edit: (doc) => _primitive(doc).remove('attributes')),
  _model(edit: (doc) => _primitive(doc)['attributes'] = 'a'),
  _model(edit: (doc) => _primitive(doc)['attributes'] = <String, Object?>{}),
  _model(edit: (doc) => _primitive(doc)['attributes'] = {'NORMAL': 0}),
  for (final position in <Object?>[1.5, '0', null, 9, -1, 1, 0.0])
    _model(
      edit: (doc) => _primitive(doc)['attributes'] = {'POSITION': position},
    ),
  _model(
    edit: (doc) => (doc['meshes']! as List<Object?>)[0] = {'primitives': 'a'},
  ),
  _model(
    edit: (doc) => (doc['meshes']! as List<Object?>)[0] = <String, Object?>{},
  ),
  _model(
    edit: (doc) =>
        (doc['meshes']! as List<Object?>)[0] = {'primitives': <Object?>[]},
  ),
  _model(
    edit: (doc) => (doc['meshes']! as List<Object?>)[0] = {
      'primitives': ['a'],
    },
  ),
  _model(
    edit: (doc) => (doc['meshes']! as List<Object?>)[0] = {
      'primitives': [
        {
          'attributes': {'POSITION': 0},
          'indices': 1,
        },
        null,
      ],
    },
  ),
  // Two primitives in one mesh: the second one's indices start after the first's points.
  _model(
    edit: (doc) {
      final first = _primitive(doc);
      (((doc['meshes']! as List)[0] as Map)['primitives']! as List).add({
        ...first,
        'mode': 6,
      });
    },
  ),
  _model(
    edit: (doc) {
      final first = _primitive(doc);
      (((doc['meshes']! as List)[0] as Map)['primitives']! as List)
        ..insert(0, {...first, 'mode': 1})
        ..add({...first, 'mode': 5});
    },
  ),
  // Compressed buffer views, by either name, and extensions that mean nothing.
  for (final name in [
    'EXT_meshopt_compression',
    'KHR_meshopt_compression',
    'KHR_other',
  ])
    _model(
      edit: (doc) =>
          (_list(doc, 'bufferViews')[0]!
              as Map<String, Object?>)['extensions'] = {
            name: <String, Object?>{},
          },
    ),
  _model(
    edit: (doc) =>
        (_list(doc, 'bufferViews')[0]! as Map<String, Object?>)['extensions'] =
            'EXT_meshopt_compression',
  ),
  // A point carried out of range by its node.
  _model(
    nodes: [
      {
        'mesh': 0,
        'scale': [1e308, 1e308, 1e308],
        'children': [1],
      },
      {
        'mesh': 0,
        'scale': [1e308, 1e308, 1e308],
      },
    ],
  ),
  '{"asset":{"version":"2.0"}}',
  '{}',
  '[]',
  'null',
  '',
  '{"meshes":[{"primitives":[]}]}',
];

List<String> _decodes() {
  const vertices = '[0,0,0,1,0,0,0,1,0]';
  return [
    '{"version":1,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":1.0,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":1,"vertices":[0.5,-0.25,1e-7,1e21,0,0,0,1.5,0],"indices":[2,1,0]}',
    '{"version":1,"vertices":[0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9],"indices":[0,1,2,2,1,0]}',
    '{"indices":[0,1,2],"vertices":$vertices,"version":1,"extra":[1,2,3]}',
    '\u{feff}{"version":1,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,3]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,-1]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,3000000000]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,1e30]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,99999999999999999999]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,2.0]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,"2"]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,null]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1,true]}',
    '{"version":1,"vertices":$vertices,"indices":[0,1]}',
    '{"version":1,"vertices":$vertices,"indices":[]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,0,0],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,"a"],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,null],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,true],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,1e999],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,-1e999],"indices":[0,1,2]}',
    '{"version":1,"vertices":[0,0,0,1,0,0,0,1,[0]],"indices":[0,1,2]}',
    '{"version":1,"vertices":{},"indices":[0,1,2]}',
    '{"version":1,"vertices":"abc","indices":[0,1,2]}',
    '{"version":1,"vertices":$vertices,"indices":{}}',
    '{"version":1,"vertices":$vertices}',
    '{"version":1,"indices":[0,1,2]}',
    '{"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":2,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":0,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":"1","vertices":$vertices,"indices":[0,1,2]}',
    '{"version":null,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":true,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":[1],"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":1.0000000000000002,"vertices":$vertices,"indices":[0,1,2]}',
    '{"version":1}',
    '{"version":2}',
    '{}',
    '[]',
    '[1,2,3]',
    'null',
    '1',
    '"a"',
    '',
    ' ',
    '{',
    '{"version":1,',
    '{"version":1,"vertices":$vertices,"indices":[0,1,2]} x',
  ];
}

List<String> _objs() => [
  'v 0 0 0\nv 2 0 0\nv 2 0 2\nv 0 0 2\nv 0 1 0\n'
      'o floor\nf 1 2 3 4\no wall\nf 1 2 5\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv -1 0.5 0\nf 1 2 3 4 5\n',
  'v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf -4 -3 -2 -1\n',
  'v 0.1 0.2 0.3\nv 1.7 0.3 0.1\nv 0.3 1.9 0.7\nf 1 2 3\n',
  'v 1e39 0 0\nv 0 1e39 0\nv 0 0 1e39\nf 1 2 3\n',
  'v 1e38 0 0\nv 0 1e38 0\nv 0 0 1e38\nf 1 2 3\n',
  'v 1e400 0 0\nv 0 1 0\nv 0 0 1\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\nf 1/1/1 2/2/1 3/3/1\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1//1 2//1 3//1\n',
  'mtllib none.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl red\nf 1 2 3\nusemtl blue\nf 3 2 1\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nl 1 2\np 3\nf 1 2 3\ns 1\nf 2 4 3\n',
  '# a comment\r\nv 0 0 0\r\nv 1 0 0\r\nv 0 1 0\r\nf 1 2 3\r\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\nf 1 2 9\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 1 1\n',
  'v 0 0 0\nv 1 0 0\nv 2 0 0\nf 1 2 3\n',
  'v 0 0 0\n',
  '',
  '\n\n',
  'f 1 2 3\n',
  'garbage\n',
];

Future<void> main(List<String> args) async {
  final gltf = _transforms() + _hierarchies() + _primitives();
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'collision',
    cases: [
      for (final text in _decodes())
        _case(
          'decode',
          text,
          () => _out(CollisionMesh.decode(utf8.encode(text))),
        ),
      for (final text in _objs())
        _case(
          'obj',
          text,
          () => _out(
            CollisionMesh.fromObj(Uint8List.fromList(utf8.encode(text))),
          ),
        ),
      for (final text in gltf)
        {
          'op': 'gltf',
          'in': [text],
          'expect': await _attemptAsync(
            () async => _out(
              await CollisionMesh.fromGltf(
                AssetId.parse('floor.gltf'),
                Uint8List.fromList(utf8.encode(text)),
                MemoryAssetSource({}),
              ),
            ),
          ),
        },
    ],
  );
}
