import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/gltf/merge.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

// A number or a string JSON can write and Dart's encoder cannot, swapped in
// after encoding.
String _raw(String text) => '__raw__${text}__raw__';

final _rawMark = RegExp(r'"__raw__(.*?)__raw__"');

Uint8List _glb(Map<String, Object?> json, List<int> binary) {
  final text = utf8.encode(
    jsonEncode(json).replaceAllMapped(_rawMark, (m) => m[1]!),
  );
  final jsonLength = text.length + (4 - text.length % 4) % 4;
  final binLength = binary.length + (4 - binary.length % 4) % 4;
  final out = Uint8List(12 + 8 + jsonLength + 8 + binLength);
  final data = ByteData.sublistView(out)
    ..setUint32(0, 0x46546C67, Endian.little)
    ..setUint32(4, 2, Endian.little)
    ..setUint32(8, out.length, Endian.little)
    ..setUint32(12, jsonLength, Endian.little)
    ..setUint32(16, 0x4E4F534A, Endian.little);
  out
    ..fillRange(20, 20 + jsonLength, 0x20)
    ..setRange(20, 20 + text.length, text);
  final at = 20 + jsonLength;
  data
    ..setUint32(at, binLength, Endian.little)
    ..setUint32(at + 4, 0x004E4942, Endian.little);
  out.setRange(at + 8, at + 8 + binary.length, binary);
  return out;
}

/// A model of quads and triangles, each drawn with a material the case picks.
class _Model {
  final Map<String, Object?> json = {
    'asset': {'version': '2.0'},
    'materials': <Object?>[<String, Object?>{}],
    'meshes': <Object?>[
      {'primitives': <Object?>[]},
    ],
    'nodes': <Object?>[
      <String, Object?>{'mesh': 0},
    ],
    'scenes': <Object?>[
      <String, Object?>{
        'nodes': [0],
      },
    ],
    'scene': 0,
    'accessors': <Object?>[],
    'bufferViews': <Object?>[],
  };
  final BytesBuilder _bin = BytesBuilder(copy: false);

  List<Object?> get materials => json['materials']! as List<Object?>;

  List<Object?> primitivesOf(int mesh) =>
      ((json['meshes']! as List)[mesh] as Map)['primitives']! as List<Object?>;

  List<Object?> get primitives => primitivesOf(0);

  List<Object?> get accessors => json['accessors']! as List<Object?>;

  Map<String, Object?> accessorAt(int index) =>
      accessors[index]! as Map<String, Object?>;

  int view(List<int> bytes, {int? stride}) {
    final views = json['bufferViews']! as List;
    views.add({
      'buffer': 0,
      'byteOffset': _bin.length,
      'byteLength': bytes.length,
      if (stride != null) 'byteStride': stride,
    });
    _bin
      ..add(bytes)
      ..add(Uint8List((4 - _bin.length % 4) % 4));
    return views.length - 1;
  }

  int accessor(
    List<int> bytes, {
    required int count,
    required String type,
    int componentType = 5126,
    Map<String, Object?> extra = const {},
  }) => addAccessor({
    'bufferView': view(bytes),
    'componentType': componentType,
    'count': count,
    'type': type,
    ...extra,
  });

  int addAccessor(Map<String, Object?> value) {
    accessors.add(value);
    return accessors.length - 1;
  }

  int indices(List<int> values, int componentType) => accessor(
    switch (componentType) {
      5121 => Uint8List.fromList(values),
      5125 => Uint8List.sublistView(Uint32List.fromList(values)),
      _ => Uint8List.sublistView(Uint16List.fromList(values)),
    },
    componentType: componentType,
    count: values.length,
    type: 'SCALAR',
  );

  int material(Map<String, Object?> value) {
    materials.add(value);
    return materials.length - 1;
  }

  /// A quad from [x] to x + 1, with a UV set.
  Map<String, Object?> quad(
    Object? material, {
    double x = 0,
    int index = 5123,
    bool indexed = true,
    int mesh = 0,
  }) {
    final positions = Float32List.fromList([
      x, 0, 0, //
      x + 1, 0, 0,
      x + 1, 1, 0,
      x, 1, 0,
    ]);
    final coords = Float32List.fromList([0, 0, 1, 0, 1, 1, 0, 1]);
    final primitive = <String, Object?>{
      'attributes': <String, Object?>{
        'POSITION': accessor(
          Uint8List.sublistView(positions),
          count: 4,
          type: 'VEC3',
          extra: {
            'min': [x, 0, 0],
            'max': [x + 1, 1, 0],
          },
        ),
        'TEXCOORD_0': accessor(
          Uint8List.sublistView(coords),
          count: 4,
          type: 'VEC2',
        ),
      },
      if (indexed) 'indices': indices(const [0, 1, 2, 0, 2, 3], index),
      if (material != null) 'material': material,
    };
    primitivesOf(mesh).add(primitive);
    return primitive;
  }

  /// A triangle from (x, 0, 0), with a normal, a tangent and a UV set.
  Map<String, Object?> triangle({
    Object? material,
    double x = 0,
    List<double> normal = const [0, 0, 1],
    List<int> index = const [0, 1, 2],
    int indexType = 5123,
    bool indexed = true,
  }) {
    final positions = Float32List.fromList([
      x, 0, 0, //
      x + 1, 0, 0,
      x, 1, 0,
    ]);
    final normals = Float32List.fromList([...normal, ...normal, ...normal]);
    final tangents = Float32List.fromList([1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1]);
    final coords = Float32List.fromList([0, 0, 1, 0, 0, 1]);
    final primitive = <String, Object?>{
      'attributes': <String, Object?>{
        'POSITION': accessor(
          Uint8List.sublistView(positions),
          count: 3,
          type: 'VEC3',
        ),
        'NORMAL': accessor(
          Uint8List.sublistView(normals),
          count: 3,
          type: 'VEC3',
        ),
        'TANGENT': accessor(
          Uint8List.sublistView(tangents),
          count: 3,
          type: 'VEC4',
        ),
        'TEXCOORD_0': accessor(
          Uint8List.sublistView(coords),
          count: 3,
          type: 'VEC2',
        ),
      },
      if (indexed) 'indices': indices(index, indexType),
      if (material != null) 'material': material,
    };
    primitives.add(primitive);
    return primitive;
  }

  /// A pyramid of two triangles with depth, leaning normals and tangents of both signs.
  Map<String, Object?> solid({Object? material}) {
    final positions = Float32List.fromList([
      0, 0, 1, //
      1, 0, 2,
      0, 1, 3,
      1, 1, -1,
    ]);
    final normals = Float32List.fromList([
      0, 0.6, 0.8, //
      0.6, 0, 0.8,
      0, 0, 1,
      1, 0, 0,
    ]);
    final tangents = Float32List.fromList([
      1, 0, 0, -1, //
      0, 1, 0, 1,
      0, 0, 1, -1,
      1, 0, 0, 1,
    ]);
    final primitive = <String, Object?>{
      'attributes': <String, Object?>{
        'POSITION': accessor(
          Uint8List.sublistView(positions),
          count: 4,
          type: 'VEC3',
        ),
        'NORMAL': accessor(
          Uint8List.sublistView(normals),
          count: 4,
          type: 'VEC3',
        ),
        'TANGENT': accessor(
          Uint8List.sublistView(tangents),
          count: 4,
          type: 'VEC4',
        ),
      },
      'indices': indices(const [0, 1, 2, 1, 3, 2], 5123),
      if (material != null) 'material': material,
    };
    primitives.add(primitive);
    return primitive;
  }

  /// A triangle whose position and normal share one view, 24 bytes a vertex.
  Map<String, Object?> interleaved({Object? material}) {
    final both = Float32List.fromList([
      0, 0, 0, 0, 0, 1, //
      1, 0, 0, 0, 0, 1,
      0, 1, 0, 0, 0, 1,
    ]);
    final shared = view(Uint8List.sublistView(both), stride: 24);
    final primitive = <String, Object?>{
      'attributes': <String, Object?>{
        'POSITION': addAccessor({
          'bufferView': shared,
          'componentType': 5126,
          'count': 3,
          'type': 'VEC3',
        }),
        'NORMAL': addAccessor({
          'bufferView': shared,
          'byteOffset': 12,
          'componentType': 5126,
          'count': 3,
          'type': 'VEC3',
        }),
      },
      'indices': indices(const [0, 1, 2], 5123),
      if (material != null) 'material': material,
    };
    primitives.add(primitive);
    return primitive;
  }

  Uint8List toGlb() {
    final bytes = _bin.toBytes();
    json['buffers'] = [
      {'byteLength': bytes.length},
    ];
    return _glb(json, bytes);
  }
}

Uint8List _build(void Function(_Model model) body) {
  final model = _Model();
  body(model);
  return model.toGlb();
}

Map<String, Object?> _attributes(Map<String, Object?> primitive) =>
    primitive['attributes']! as Map<String, Object?>;

Map<String, Object?> _decode(String text) =>
    jsonDecode(text) as Map<String, Object?>;

Future<GltfDocument> _open(Uint8List bytes) => GltfDocument.read(
  AssetId.parse('models/thing.glb'),
  bytes,
  MemoryAssetSource(const {}),
);

Future<Map<String, Object?>> _attempt(Future<Object?> Function() body) async {
  try {
    return {'value': await body()};
  } on Object {
    return {'throws': true};
  }
}

// A glb as its JSON text and its binary chunk, so a case holds each once.
Map<String, Object?> _parts(Uint8List glb) {
  final data = ByteData.sublistView(glb);
  final at = 20 + data.getUint32(12, Endian.little);
  final end = at + 8 + data.getUint32(at, Endian.little);
  return {
    'json': utf8.decode(glb.sublist(20, at)),
    'bin': b64(glb.sublist(at + 8, end)),
  };
}

// What a merge did, as the count it returned and the document it left.
Map<String, Object?> _merged(GltfDocument document, int count) => {
  'count': count,
  ..._parts(document.toGlb()),
};

// Models for mergeMaterials.

Uint8List _materials(
  List<Object?> list, {
  void Function(_Model model)? edit,
  int quads = 0,
}) => _build((model) {
  model.json['materials'] = list;
  for (var i = 0; i < (quads == 0 ? list.length : quads); i++) {
    model.quad(list.isEmpty ? null : i % list.length, x: i * 2.0);
  }
  edit?.call(model);
});

List<Uint8List> _materialModels() => [
  _materials([
    {'doubleSided': true, 'name': 'left'},
    {'doubleSided': true, 'name': 'right'},
    {'doubleSided': false},
  ]),
  _materials([
    {
      'pbrMetallicRoughness': {
        'baseColorFactor': [1.0, 0.0, 0.0, 1.0],
      },
    },
    {
      'pbrMetallicRoughness': {
        'baseColorFactor': [1.0, 0.0, 0.0, 0.9],
      },
    },
  ]),
  // 1 and 1.0 are one number in a glTF.
  _materials([
    {
      'pbrMetallicRoughness': {
        'baseColorFactor': [1.0, 0.0, 0.0, 1.0],
      },
    },
    {
      'pbrMetallicRoughness': {
        'baseColorFactor': [1, 0, 0, 1],
      },
    },
  ]),
  _materials([
    {'v': -0.0},
    {'v': 0.0},
  ]),
  _materials([
    {'v': -0.0},
    {'v': 0},
  ]),
  _materials([
    {'v': 0},
    {'v': 0.0},
    {'v': -0.0},
  ]),
  _materials([
    {'a': 1, 'b': 2},
    {'b': 2, 'a': 1},
  ]),
  _materials([
    {
      'pbr': {'name': 'x'},
    },
    {
      'pbr': {'name': 'y'},
    },
  ]),
  _materials([
    {'name': 'a'},
    <String, Object?>{},
    {'name': 3},
    {'name': null},
  ]),
  _materials([
    {
      'v': [1, 2],
    },
    {
      'v': [2, 1],
    },
  ]),
  _materials([
    {'v': '1'},
    {'v': 1},
    {'v': true},
    {'v': null},
    <String, Object?>{},
  ]),
  _materials([
    {'v': false},
    {'v': 0},
    {'v': ''},
    {'v': <Object?>[]},
    {'v': <String, Object?>{}},
  ]),
  _materials([
    {'v': 1e21},
    {'v': _raw('1E21')},
    {'v': _raw('1e+21')},
    {'v': _raw('1000000000000000000000.0')},
  ]),
  _materials([
    {'v': _raw('9007199254740993')},
    {'v': _raw('9007199254740992.0')},
    {'v': _raw('9007199254740992')},
  ]),
  _materials([
    {'v': _raw('99999999999999999999')},
    {'v': _raw('1e20')},
    {'v': _raw('100000000000000000000')},
  ]),
  _materials([
    {'v': 0.1 + 0.2},
    {'v': _raw('0.30000000000000004')},
    {'v': 0.3},
  ]),
  _materials([
    {'v': _raw('1e999')},
    {'v': _raw('1e999')},
  ]),
  _materials([
    {'v': _raw('-1e999')},
    {'v': 1},
  ]),
  _materials([
    {'a': 'é', 'b': '\u{1f600}'},
    {'b': '\u{1f600}', 'a': 'é'},
  ]),
  _materials([
    {'é': 1, 'z': 2, 'a': 3, 'B': 4, '\u{1f600}': 5, '10': 6, '2': 7},
    {'2': 7, '10': 6, '\u{1f600}': 5, 'B': 4, 'a': 3, 'z': 2, 'é': 1},
  ]),
  _materials([
    {'x': 1},
  ]),
  _materials(<Object?>[], quads: 1),
  _materials(
    [
      {'doubleSided': true},
      {'doubleSided': true},
    ],
    edit: (model) {
      // Variants name materials too.
      model.quad(0)['extensions'] = {
        'KHR_materials_variants': {
          'mappings': [
            {
              'material': 1,
              'variants': [0],
            },
            {
              'material': 1.0,
              'variants': [1],
            },
            {
              'material': '1',
              'variants': [2],
            },
            {
              'variants': [3],
            },
            'a',
            null,
            {'material': 7, 'variants': <Object?>[]},
          ],
        },
      };
    },
  ),
  _materials(
    [
      {'doubleSided': true},
      {'doubleSided': true},
      {'doubleSided': true},
    ],
    edit: (model) {
      model.quad(1)['extensions'] = {'OTHER': 1};
      model.quad(2)['extensions'] = 'a';
      model.quad(2)['extensions'] = {'KHR_materials_variants': 'a'};
      model.quad(2)['extensions'] = {
        'KHR_materials_variants': {'mappings': 'a'},
      };
      model.quad(2)['extensions'] = {
        'KHR_materials_variants': <String, Object?>{},
      };
    },
  ),
  _materials(
    [
      {'doubleSided': true},
      {'doubleSided': true},
      {'doubleSided': true},
    ],
    edit: (model) {
      // A primitive that names a material the number does not give.
      model.quad(1.0);
      model.quad('2');
      model.quad(-1);
      model.quad(9);
      model.quad(null);
      model.quad(true);
      model.primitives
        ..add('a')
        ..add(null)
        ..add(4);
    },
  ),
  _materials(
    [
      {'doubleSided': true},
      {'doubleSided': true},
    ],
    edit: (model) {
      (model.json['meshes']! as List)
        ..add(<String, Object?>{})
        ..add({'primitives': 'a'})
        ..add({
          'primitives': [
            {'material': 1},
          ],
        });
    },
  ),
  _materials([
    {'doubleSided': true},
    {'doubleSided': true},
  ], edit: (model) => (model.json['meshes']! as List).add('a')),
  _materials([
    {'doubleSided': true},
    {'doubleSided': true},
  ], edit: (model) => model.json['meshes'] = 5),
  _materials([
    {'doubleSided': true},
    'a',
  ], quads: 1),
  _materials([
    {'doubleSided': true},
    null,
  ], quads: 1),
  _materials([
    {'doubleSided': true},
    {'doubleSided': true},
  ], edit: (model) => model.json.remove('materials')),
  _materials([
    {'doubleSided': true},
    {'doubleSided': true},
  ], edit: (model) => model.json['materials'] = 5),
  _materials([
    {'doubleSided': true},
    {'doubleSided': true},
  ], edit: (model) => model.json['materials'] = {'a': 1}),
];

// Models for mergePrimitives, with the vertex limit each is merged under.

typedef _Merge = (Uint8List, int);

const _wide = 65536;

_Merge _quads(
  List<Object?> materials, {
  int maxVertices = _wide,
  void Function(_Model model, List<Map<String, Object?>> made)? edit,
}) {
  final model = _Model()..materials.add(<String, Object?>{'doubleSided': true});
  final made = [
    for (var i = 0; i < materials.length; i++)
      model.quad(materials[i], x: i * 2.0),
  ];
  edit?.call(model, made);
  return (model.toGlb(), maxVertices);
}

List<_Merge> _primitiveModels() => [
  _quads([0, 0]),
  _quads([0, 0, 0]),
  _quads([0, 1, 0]),
  _quads([0, 1, 0, 1, 0, 1]),
  _quads([0]),
  _quads([]),
  for (final limit in [-1, 0, 1, 3, 4, 5, 7, 8, 9, 11, 12, 13, 100])
    _quads([0, 0, 0, 0, 0], maxVertices: limit),
  _quads([0, 0, 0, 0, 0, 0, 0], maxVertices: 12),
  // The material is part of what a draw is, written however it was.
  _quads([null, null]),
  _quads([null, 0]),
  _quads([1, 1.0]),
  _quads([1.0, 1.0]),
  _quads(['1', '1']),
  _quads(['1', 1]),
  _quads([true, true]),
  _quads([-1, -1]),
  _quads([0, 0], edit: (model, made) => made[0]['material'] = null),
  // Three index widths and none at all.
  _quads(
    [0, 0, 0],
    edit: (model, made) {
      final first = made[0];
      first['indices'] = model.indices(const [0, 1, 2, 0, 2, 3], 5121);
      made[2]['indices'] = model.indices(const [0, 1, 2, 0, 2, 3], 5125);
    },
  ),
  _quads([0, 0], edit: (model, made) => made[0].remove('indices')),
  _quads([0, 0], edit: (model, made) => made[1].remove('indices')),
  _quads(
    [0, 0, 0],
    edit: (model, made) {
      made[0].remove('indices');
      made[1].remove('indices');
    },
  ),
  _quads([0, 0], edit: (model, made) => made[1]['indices'] = null),
  _quads([0, 0], edit: (model, made) => made[1]['indices'] = 1.0),
  _quads([0, 0], edit: (model, made) => made[1]['indices'] = '3'),
  _quads([0, 0], edit: (model, made) => made[1]['indices'] = 99),
  _quads([0, 0], edit: (model, made) => made[1]['indices'] = -1),
  _quads(
    [0, 0],
    edit: (model, made) => made[1]['indices'] = model.indices(const [
      0,
      1,
      2,
      0,
      2,
      3,
      1,
      2,
      3,
    ], 5123),
  ),
  _quads(
    [0, 0],
    edit: (model, made) =>
        made[1]['indices'] = model.indices(const [0, 1, 2, 0], 5123),
  ),
  // One attribute fewer, or one more.
  _quads([
    0,
    0,
  ], edit: (model, made) => _attributes(made[1]).remove('TEXCOORD_0')),
  _quads(
    [0, 0],
    edit: (model, made) =>
        _attributes(made[1])['TEXCOORD_1'] = _attributes(made[1])['TEXCOORD_0'],
  ),
  _quads(
    [0, 0, 0],
    edit: (model, made) {
      for (final primitive in made) {
        _attributes(primitive)['TEXCOORD_1'] = _attributes(
          primitive,
        )['TEXCOORD_0'];
      }
    },
  ),
  _quads(
    [0, 0],
    edit: (model, made) {
      for (final primitive in made) {
        final attributes = _attributes(primitive);
        final reordered = {
          'TEXCOORD_0': attributes['TEXCOORD_0'],
          'POSITION': attributes['POSITION'],
        };
        attributes
          ..clear()
          ..addAll(reordered);
      }
    },
  ),
  _quads(
    [0, 0],
    edit: (model, made) {
      for (final primitive in made) {
        _attributes(primitive)['COLOR_0'] = model.accessor(
          Uint8List.fromList(List.filled(16, 200)),
          componentType: 5121,
          count: 4,
          type: 'VEC4',
          extra: {'normalized': true},
        );
      }
    },
  ),
  // Accessors that differ in how they are stored.
  _quads(
    [0, 0],
    edit: (model, made) => model.accessorAt(
      _attributes(made[1])['TEXCOORD_0']! as int,
    )['normalized'] = true,
  ),
  _quads(
    [0, 0],
    edit: (model, made) => model.accessorAt(
      _attributes(made[1])['TEXCOORD_0']! as int,
    )['normalized'] = false,
  ),
  _quads(
    [0, 0],
    edit: (model, made) => model.accessorAt(
      _attributes(made[1])['TEXCOORD_0']! as int,
    )['normalized'] = 'true',
  ),
  _quads(
    [0, 0],
    edit: (model, made) => model.accessorAt(
      _attributes(made[1])['TEXCOORD_0']! as int,
    )['normalized'] = 1,
  ),
  _quads(
    [0, 0],
    edit: (model, made) => model.accessorAt(
      _attributes(made[1])['TEXCOORD_0']! as int,
    )['componentType'] = 5126.0,
  ),
  _quads(
    [0, 0],
    edit: (model, made) => model.accessorAt(
      _attributes(made[1])['POSITION']! as int,
    )['componentType'] = 5126.0,
  ),
  _quads(
    [0, 0],
    edit: (model, made) =>
        model.accessorAt(_attributes(made[1])['TEXCOORD_0']! as int)['type'] =
            'VEC3',
  ),
  _quads(
    [0, 0],
    edit: (model, made) =>
        model.accessorAt(_attributes(made[1])['TEXCOORD_0']! as int)['type'] =
            null,
  ),
  _quads(
    [0, 0],
    edit: (model, made) =>
        model.accessorAt(_attributes(made[1])['TEXCOORD_0']! as int)['sparse'] =
            <String, Object?>{},
  ),
  _quads(
    [0, 0],
    edit: (model, made) =>
        model.accessorAt(_attributes(made[1])['POSITION']! as int)['count'] = 0,
  ),
  _quads(
    [0, 0],
    edit: (model, made) {
      for (final primitive in made) {
        model.accessorAt(_attributes(primitive)['POSITION']! as int)['count'] =
            0;
      }
    },
  ),
  _quads([0, 0], edit: (model, made) => model.accessorAt(0).remove('min')),
  // Primitives that are never joined.
  for (final spoiler in <Map<String, Object?>>[
    {
      'targets': [
        {'POSITION': 0},
      ],
    },
    {'targets': null},
    {'targets': <Object?>[]},
    {
      'extensions': {
        'KHR_materials_variants': {'mappings': <Object?>[]},
      },
    },
    {
      'extensions': {
        'KHR_draco_mesh_compression': {'bufferView': 0},
      },
    },
    {
      'extensions': {'OTHER': 1},
    },
    {'extensions': 'a'},
    {'mode': 5},
    {'mode': 6},
    {'mode': 0},
    {'mode': 1},
    {'mode': 4},
    {'mode': 4.0},
    {'mode': '4'},
    {'mode': null},
    {'mode': true},
    {'mode': 3000000000},
    {'extras': 'last'},
    {
      'extras': {'a': 1},
    },
  ])
    _quads([0, 0], edit: (model, made) => made[1].addAll(spoiler)),
  _quads([0, 0], edit: (model, made) => made[0]['extras'] = 'first'),
  _quads(
    [0, 0],
    edit: (model, made) {
      made[0]['extras'] = 'first';
      made[1]['extras'] = 'second';
    },
  ),
  _quads([0, 0], edit: (model, made) => made[0]['mode'] = 5),
  _quads([0, 0, 0], edit: (model, made) => made[1]['mode'] = 5),
  _quads([0, 0, 0], edit: (model, made) => made[0]['mode'] = 6),
  _quads([0, 0], edit: (model, made) => _attributes(made[1])['JOINTS_0'] = 0),
  _quads([0, 0], edit: (model, made) => _attributes(made[1])['WEIGHTS_0'] = 0),
  _quads(
    [0, 0],
    edit: (model, made) {
      for (final primitive in made) {
        _attributes(primitive)['JOINTS_0'] = 0;
        _attributes(primitive)['WEIGHTS_0'] = 0;
      }
    },
  ),
  // Attributes that point nowhere.
  _quads([0, 0], edit: (model, made) => made[1].remove('attributes')),
  _quads([0, 0], edit: (model, made) => made[1]['attributes'] = 'a'),
  _quads([
    0,
    0,
  ], edit: (model, made) => made[1]['attributes'] = <String, Object?>{}),
  _quads([
    0,
    0,
  ], edit: (model, made) => _attributes(made[1]).remove('POSITION')),
  _quads([0, 0], edit: (model, made) => _attributes(made[1])['POSITION'] = 1.0),
  _quads([0, 0], edit: (model, made) => _attributes(made[1])['POSITION'] = '0'),
  _quads([
    0,
    0,
  ], edit: (model, made) => _attributes(made[1])['POSITION'] = null),
  _quads([0, 0], edit: (model, made) => _attributes(made[1])['POSITION'] = 99),
  _quads([0, 0], edit: (model, made) => _attributes(made[1])['POSITION'] = -1),
  _quads([
    0,
    0,
  ], edit: (model, made) => _attributes(made[1])['TEXCOORD_0'] = 99),
  _quads([
    0,
    0,
  ], edit: (model, made) => _attributes(made[1])['TEXCOORD_0'] = 1.0),
  _quads([
    0,
    0,
  ], edit: (model, made) => _attributes(made[1])['TEXCOORD_0'] = null),
  // Primitives that are not objects sit where they are.
  _quads(
    [0, 0],
    edit: (model, made) => model.primitives
      ..insert(1, null)
      ..insert(0, 'a')
      ..add(3),
  ),
  _quads([
    0,
    1,
    0,
  ], edit: (model, made) => model.primitives.insert(2, <String, Object?>{})),
  // More than one mesh, and meshes with nothing to join.
  _quads(
    [0, 0],
    edit: (model, made) {
      (model.json['meshes']! as List)
        ..add({'primitives': <Object?>[]})
        ..add(<String, Object?>{})
        ..add({'primitives': 'a'});
      model
        ..quad(0, x: 6, mesh: 1)
        ..quad(0, x: 8, mesh: 1)
        ..quad(1, x: 10, mesh: 1);
    },
  ),
  _quads(
    [0],
    edit: (model, made) {
      (model.json['meshes']! as List).add({'primitives': <Object?>[]});
      model
        ..quad(0, x: 6, mesh: 1)
        ..quad(0, x: 8, mesh: 1);
    },
  ),
  _quads([0, 0], edit: (model, made) => model.json['meshes'] = 5),
  _quads([
    0,
    0,
  ], edit: (model, made) => (model.json['meshes']! as List).add('a')),
  _quads([0, 0], edit: (model, made) => model.json.remove('meshes')),
  // Positions that share a view with normals.
  _quads(
    [0, 0],
    edit: (model, made) {
      final first = model.interleaved();
      final second = model.interleaved();
      model.primitives
        ..clear()
        ..addAll([first, second]);
    },
  ),
  _quads(
    [0, 0],
    edit: (model, made) {
      model.primitives
        ..clear()
        ..addAll([model.interleaved(), model.interleaved()]);
    },
  ),
  // A model that is only some bytes.
  (_build((model) => model.triangle()), _wide),
  (
    _build(
      (model) => model
        ..triangle()
        ..triangle(x: 2),
    ),
    _wide,
  ),
  (
    _build(
      (model) => model
        ..triangle()
        ..triangle(x: 2, normal: const [0, 1, 0])
        ..triangle(x: 4, indexed: false),
    ),
    _wide,
  ),
];

// Models and placements for bakeInstances.

typedef _Bake = (Uint8List, List<Matrix4>, int);

Matrix4 _at(double x, double y, double z) => Matrix4.translationValues(x, y, z);

Matrix4 _scale(double x, double y, double z) =>
    Matrix4.diagonal3Values(x, y, z);

Matrix4 _entry(int row, int column, double value) =>
    Matrix4.identity()..setEntry(row, column, value);

final _turned = Matrix4.compose(
  Vector3(1, 2, 3),
  Quaternion(0.1, 0.2, 0.3, 0.9)..normalize(),
  Vector3(2, 3, 0.5),
);

final _sheared = Matrix4.fromList([
  1, 0, 0, 0, //
  0.5, 1, 0, 0,
  0, 0.25, 1, 0,
  1, 2, 3, 1,
]);

final _projected = Matrix4.fromList([
  1, 0, 0, 0.5, //
  0, 1, 0, 0.25,
  0, 0, 1, 0.125,
  1, 2, 3, 2,
]);

final _all = [
  Matrix4.identity(),
  _at(10, 0, 0),
  _at(0, 5, 0)..rotateZ(3.141592653589793 / 2),
  Matrix4.rotationX(3.141592653589793 / 2),
  Matrix4.rotationY(1),
  Matrix4.rotationZ(-2),
  _scale(4, 1, 1),
  _scale(-1, 1, 1),
  _scale(1, -1, 1),
  _scale(1, 1, -1),
  _scale(-1, -1, 1),
  _scale(-1, -1, -1),
  _scale(2, 3, 4),
  _scale(0.001, 0.001, 0.001),
  _turned,
  _turned.clone()..scaleByDouble(-1, 1, 1, 1),
  _sheared,
  _projected,
  Matrix4.zero()..setEntry(3, 3, 1),
];

List<Object?> _spec(Matrix4 m) => [for (final v in m.storage) number(v)];

List<_Bake> _bakeModels() {
  final one = _build((model) => model.triangle());
  final lean = Float64List.fromList([
    0.7071067811865476,
    0,
    0.7071067811865476,
  ]);
  final tilted = _build((model) => model.triangle(normal: lean.toList()));
  final two = _build((model) {
    final a = model.material({'name': 'a'});
    final b = model.material({'name': 'b'});
    for (var i = 0; i < 3; i++) {
      model
        ..triangle(material: a, x: i * 2.0)
        ..triangle(material: b, x: i * 2.0 + 1);
    }
  });
  final each = [for (var i = 0; i < 5; i++) _at(i * 2.0, 0, 0)];
  final solid = _build((model) => model.solid());
  // A primitive with no vertices, alone and beside a triangle.
  void empty(_Model model) => model.primitives.add({
    'attributes': {
      'POSITION': model.accessor(Uint8List(0), count: 0, type: 'VEC3'),
    },
  });
  return [
    (one, [Matrix4.identity()], _wide),
    for (final placement in _all) (solid, [placement], _wide),
    (solid, [_turned, _scale(-1, 1, 1), _at(1, 2, 3)], _wide),
    (solid, [_turned, _scale(1, 1, -1), _at(1, 2, 3)], 5),
    (_build(empty), [Matrix4.identity(), _at(1, 0, 0)], _wide),
    (
      _build((model) => empty(model..triangle())),
      [Matrix4.identity(), _at(1, 0, 0)],
      _wide,
    ),
    (
      _build(
        (model) => model
          ..triangle()
          ..primitives.add({'attributes': <String, Object?>{}}),
      ),
      [Matrix4.identity()],
      _wide,
    ),
    (one, [_at(10, 0, 0), _at(0, 5, 0)..rotateZ(3.141592653589793 / 2)], _wide),
    for (final placement in _all) (one, [placement], _wide),
    for (final placement in _all) (tilted, [placement], _wide),
    for (final placement in _all) (two, [placement, _at(3, 0, 0)], _wide),
    (one, _all, _wide),
    (two, _all, _wide),
    (two, [for (var i = 0; i < 50; i++) _at(i * 2.0, 0, 0)], _wide),
    for (final limit in [-1, 0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 15, 16])
      (one, each, limit),
    (two, each, 10),
    (two, each, 4),
    (two, each, 1),
    (one, const [], _wide),
    (one, [_scale(0, 1, 1)], _wide),
    (one, [_scale(1e-200, 1e-200, 1e-200)], _wide),
    (one, [_scale(1e300, 1, 1)], _wide),
    (one, [_scale(1e38, 1, 1)], _wide),
    (one, [_scale(1e40, 1, 1)], _wide),
    (one, [_entry(0, 3, double.nan), _entry(1, 3, double.infinity)], _wide),
    (
      one,
      [
        _entry(0, 3, double.nan),
        Matrix4.identity(),
        _entry(1, 3, double.negativeInfinity),
      ],
      _wide,
    ),
    (one, [_entry(3, 3, double.nan)], _wide),
    (one, [_entry(3, 0, double.infinity), _at(1, 0, 0)], _wide),
    // Normals that are not unit, are zero, or point along an axis.
    for (final normal in <List<double>>[
      [0, 0, 0],
      [0, 0, 2],
      [3, 4, 0],
      [1, 1, 1],
      [0, 0, -1],
      [1e-30, 0, 0],
    ])
      for (final placement in [Matrix4.identity(), _scale(4, 1, 1), _turned])
        (_build((model) => model.triangle(normal: normal)), [placement], _wide),
    // Index lists and widths.
    for (final (indices, type) in <(List<int>, int)>[
      ([0, 1, 2], 5121),
      ([0, 1, 2], 5125),
      ([0, 1, 2, 0], 5123),
      ([0, 1, 2, 2, 1, 0], 5123),
      ([0, 1], 5123),
      ([2, 1, 0], 5123),
      ([0, 1, 7], 5123),
    ])
      (
        _build((model) => model.triangle(index: indices, indexType: type)),
        [_at(1, 0, 0), _scale(-1, 1, 1)],
        _wide,
      ),
    (
      _build((model) => model.triangle(indexed: false)),
      [_at(1, 0, 0), _scale(-1, 1, 1)],
      _wide,
    ),
    (
      _build((model) {
        model.triangle(indexed: false);
        model.triangle();
      }),
      [Matrix4.identity()],
      _wide,
    ),
    // Material.
    for (final material in <Object?>[null, 1.0, '1', -1, 4, true])
      (
        _build((model) => model.triangle(material: material)),
        [_at(1, 0, 0)],
        _wide,
      ),
    (
      _build((model) {
        model.triangle(material: 0)['extras'] = {'a': 1};
        model.triangle(material: 0, x: 2)['extras'] = 'second';
      }),
      [Matrix4.identity()],
      _wide,
    ),
    // A batch that closes early leaves the next one the first template, extras and all.
    (
      _build((model) {
        model.triangle(material: 0)['extras'] = {'a': 1};
        model.triangle(material: 0, x: 2)['extras'] = 'second';
      }),
      [Matrix4.identity()],
      3,
    ),
    // What an attribute may be.
    for (final (_, edit)
        in <(String, void Function(_Model, Map<String, Object?>))>[
          ('no normal', (m, p) => _attributes(p).remove('NORMAL')),
          ('no tangent', (m, p) => _attributes(p).remove('TANGENT')),
          ('no uv', (m, p) => _attributes(p).remove('TEXCOORD_0')),
          ('no position', (m, p) => _attributes(p).remove('POSITION')),
          (
            'normal in shorts',
            (m, p) => m.accessorAt(
              _attributes(p)['NORMAL']! as int,
            )['componentType'] = 5122,
          ),
          (
            'tangent in shorts',
            (m, p) => m.accessorAt(
              _attributes(p)['TANGENT']! as int,
            )['componentType'] = 5122,
          ),
          (
            'position in shorts',
            (m, p) => m.accessorAt(
              _attributes(p)['POSITION']! as int,
            )['componentType'] = 5122,
          ),
          (
            'uv in shorts',
            (m, p) => m.accessorAt(
              _attributes(p)['TEXCOORD_0']! as int,
            )['componentType'] = 5123,
          ),
          (
            'float written as a double',
            (m, p) => m.accessorAt(
              _attributes(p)['POSITION']! as int,
            )['componentType'] = 5126.0,
          ),
          (
            'second uv',
            (m, p) =>
                _attributes(p)['TEXCOORD_1'] = _attributes(p)['TEXCOORD_0'],
          ),
          (
            'colour',
            (m, p) => _attributes(p)['COLOR_0'] = m.accessor(
              Uint8List.fromList(List.filled(12, 90)),
              componentType: 5121,
              count: 3,
              type: 'VEC4',
              extra: {'normalized': true},
            ),
          ),
          ('joints', (m, p) => _attributes(p)['JOINTS_0'] = 0),
          ('mode strip', (m, p) => p['mode'] = 5),
          ('mode list', (m, p) => p['mode'] = 4),
          ('mode list as double', (m, p) => p['mode'] = 4.0),
          ('targets', (m, p) => p['targets'] = <Object?>[]),
          (
            'draco',
            (m, p) => p['extensions'] = {
              'KHR_draco_mesh_compression': <String, Object?>{},
            },
          ),
          (
            'variants',
            (m, p) => p['extensions'] = {
              'KHR_materials_variants': <String, Object?>{},
            },
          ),
          (
            'other extension',
            (m, p) => p['extensions'] = {'OTHER': <String, Object?>{}},
          ),
          (
            'sparse',
            (m, p) =>
                m.accessorAt(_attributes(p)['NORMAL']! as int)['sparse'] = {},
          ),
          ('position by name', (m, p) => _attributes(p)['POSITION'] = 1.0),
          ('position out of range', (m, p) => _attributes(p)['POSITION'] = 99),
          ('indices out of range', (m, p) => p['indices'] = 99),
          ('indices as a double', (m, p) => p['indices'] = 1.0),
        ])
      (
        _build((model) => edit(model, model.triangle())),
        [_at(1, 0, 0), _scale(-1, 1, 1)],
        _wide,
      ),
    (_build((model) => model.interleaved()), [_at(1, 0, 0), _turned], _wide),
    (
      _build((model) {
        model
          ..interleaved()
          ..triangle(x: 3)
          ..interleaved();
      }),
      [_at(1, 0, 0), _scale(-1, 1, 1)],
      _wide,
    ),
    ..._nodeModels(),
    ..._refusedModels(one, each),
  ];
}

// The nodes that put the model's meshes in the scene.
List<_Bake> _nodeModels() {
  Uint8List nodes(
    List<Object?> list, {
    void Function(_Model model)? edit,
    int triangles = 1,
  }) => _build((model) {
    for (var i = 0; i < triangles; i++) {
      model.triangle(x: i * 2.0);
    }
    model.json['nodes'] = list;
    edit?.call(model);
  });
  final place = [_at(1, 0, 0), _scale(-1, 1, 1)];
  Object? node(String key, Object? value) => {'mesh': 0, key: value};
  return [
    for (final (key, value) in <(String, Object?)>[
      ('translation', [3, -2, 0.5]),
      ('translation', [1, 2]),
      ('translation', [1, 2, 3, 4]),
      ('translation', [1, 'a', 3]),
      ('translation', [1, true, 3]),
      ('translation', [1, null, 3]),
      ('translation', [1, 2, _raw('1e999')]),
      ('translation', 'abc'),
      ('translation', <String, Object?>{}),
      ('translation', null),
      ('rotation', [0, 0.7071067811865476, 0, 0.7071067811865476]),
      ('rotation', [0.5, 0.5, 0.5, 0.5]),
      ('rotation', [1, 2, 3, 4]),
      ('rotation', [0, 0, 0, 2]),
      ('rotation', [0, 0.7071067811865476, 0, 0.7071067811865476, 9]),
      ('rotation', [0, 0, 0, 0]),
      ('rotation', [0, 0, 0]),
      ('rotation', [0, 0, 0, 'a']),
      ('rotation', [-0.3, 0.2, 0.9, -0.1]),
      ('scale', [2, 3, 4]),
      ('scale', [-1, 1, 1]),
      ('scale', [-1, -1, 1]),
      ('scale', [0, 1, 1]),
      ('scale', [1]),
      ('scale', [1, 2, 3, 4]),
      ('scale', 'abc'),
      ('scale', [1, 2, 'a']),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 5, 6, 7, 1]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 5, 6, 7, 1, 9]),
      ('matrix', [-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]),
      ('matrix', [0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1]),
      ('matrix', [2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 'x']),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, null]),
      ('matrix', [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0]),
      ('matrix', null),
      ('matrix', <String, Object?>{}),
      ('matrix', 'abc'),
    ])
      (nodes([node(key, value)]), place, _wide),
    // The tree.
    (
      nodes([
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
      ]),
      place,
      _wide,
    ),
    (
      nodes([
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
      ]),
      place,
      _wide,
    ),
    (
      nodes([
        {
          'scale': [-1, 1, 1],
          'children': [1],
        },
        {
          'scale': [1, -1, 1],
          'mesh': 0,
        },
      ]),
      place,
      _wide,
    ),
    // Two nodes drawing two meshes, and one mesh drawn twice.
    (
      nodes(
        [
          {'mesh': 0},
          {
            'mesh': 1,
            'translation': [0, 3, 0],
          },
        ],
        triangles: 2,
        edit: (model) {
          final drawn = [...model.primitives];
          (model.json['meshes']! as List)
            ..clear()
            ..addAll([
              {
                'primitives': [drawn[0]],
              },
              {
                'primitives': [drawn[1]],
              },
            ]);
          model.json['scenes'] = [
            {
              'nodes': [0, 1],
            },
          ];
        },
      ),
      place,
      _wide,
    ),
    (
      nodes(
        [
          {'mesh': 0},
          {
            'mesh': 0,
            'translation': [0, 3, 0],
          },
        ],
        edit: (model) => model.json['scenes'] = [
          {
            'nodes': [0, 1],
          },
        ],
      ),
      place,
      _wide,
    ),
    // Walks that fail.
    for (final list in <List<Object?>>[
      [
        {
          'mesh': 0,
          'children': [0],
        },
      ],
      [
        {
          'children': [1],
        },
        {
          'mesh': 0,
          'children': [0],
        },
      ],
      [
        {
          'children': [1, 2],
        },
        {
          'children': [3],
        },
        {
          'children': [3],
        },
        {'mesh': 0},
      ],
      [
        {
          'mesh': 0,
          'children': [9],
        },
      ],
      [
        {
          'mesh': 0,
          'children': [-1],
        },
      ],
      [
        {
          'mesh': 0,
          'children': ['a'],
        },
      ],
      [
        {
          'mesh': 0,
          'children': [0.5],
        },
      ],
      [
        {
          'mesh': 0,
          'children': [null],
        },
      ],
      [
        {'mesh': 0, 'children': 'a'},
      ],
      [
        {'mesh': 0, 'children': <Object?>[]},
      ],
      [
        {'mesh': 5},
      ],
      [
        {'mesh': -1},
      ],
      [
        {'mesh': '0'},
      ],
      [
        {'mesh': 0.5},
      ],
      [
        {'mesh': 0.0},
      ],
      [
        {'mesh': null},
      ],
      [<String, Object?>{}],
      [
        {
          'mesh': 0,
          'extensions': {'KHR_mesh_gpu_instancing': <String, Object?>{}},
        },
      ],
      [
        {
          'mesh': 0,
          'extensions': {'KHR_mesh_gpu_instancing': null},
        },
      ],
      [
        {
          'mesh': 0,
          'extensions': {'OTHER': <String, Object?>{}},
        },
      ],
      [
        {'mesh': 0, 'extensions': 'KHR_mesh_gpu_instancing'},
      ],
      [
        {
          'children': [1],
          'extensions': {'KHR_mesh_gpu_instancing': <String, Object?>{}},
        },
        {'mesh': 0},
      ],
      [
        {'mesh': 0, 'skin': 0},
      ],
      [
        {'mesh': 0, 'camera': 0},
      ],
      [
        {
          'mesh': 0,
          'extensions': {
            'KHR_lights_punctual': {'light': 0},
          },
        },
      ],
      ['a'],
      [null],
      [3],
      <Object?>[],
    ])
      (nodes(list), place, _wide),
    // The scene that holds them.
    for (final scene in <Object?>[
      0,
      1,
      2,
      -1,
      1.0,
      '0',
      null,
      true,
      3000000000,
    ])
      (
        nodes(
          [
            {'mesh': 0},
            {
              'mesh': 0,
              'translation': [0, 9, 0],
            },
          ],
          edit: (model) {
            model.json['scenes'] = [
              {
                'nodes': [0],
              },
              {
                'nodes': [1],
              },
            ];
            model.json['scene'] = scene;
          },
        ),
        place,
        _wide,
      ),
    for (final scenes in <Object?>[
      <Object?>[],
      [<String, Object?>{}],
      [
        {'nodes': <Object?>[]},
      ],
      [
        {
          'nodes': [5],
        },
      ],
      [
        {
          'nodes': [-1],
        },
      ],
      [
        {
          'nodes': ['a', 0],
        },
      ],
      [
        {
          'nodes': [0.5, 0],
        },
      ],
      [
        {
          'nodes': [0, 0],
        },
      ],
      [
        {'nodes': 'zero'},
      ],
      [
        {'nodes': null},
      ],
      [
        {
          'nodes': [null, 0],
        },
      ],
      ['a'],
      [null],
      5,
      'a',
      {
        'nodes': [0],
      },
      null,
    ])
      (
        nodes([
          {'mesh': 0},
        ], edit: (model) => model.json['scenes'] = scenes),
        place,
        _wide,
      ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json.remove('scenes')),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json.remove('scene')),
      place,
      _wide,
    ),
    (
      nodes([
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
      ], edit: (model) => model.json.remove('scenes')),
      place,
      _wide,
    ),
    (
      nodes([
        {
          'children': [1],
        },
        {
          'children': [0],
        },
        {'mesh': 0},
      ], edit: (model) => model.json.remove('scenes')),
      place,
      _wide,
    ),
    (
      nodes([
        {
          'children': [1],
        },
        {'mesh': 0},
      ], edit: (model) => model.json.remove('scenes')),
      place,
      _wide,
    ),
    (
      nodes([
        {
          'mesh': 0,
          'children': [0.5],
        },
      ], edit: (model) => model.json.remove('scenes')),
      place,
      _wide,
    ),
    (
      nodes(<Object?>[], edit: (model) => model.json.remove('scenes')),
      place,
      _wide,
    ),
    (
      nodes(
        [
          {'mesh': 0},
        ],
        edit: (model) {
          model.json
            ..remove('scenes')
            ..remove('nodes');
        },
      ),
      place,
      _wide,
    ),
    // Lists that are not lists of objects.
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json['nodes'] = 'a'),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json['nodes'] = {'mesh': 0}),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json['meshes'] = 'a'),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json['meshes'] = <Object?>[]),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json['meshes'] = ['a']),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.json['meshes'] = [<String, Object?>{}]),
      place,
      _wide,
    ),
    (
      nodes(
        [
          {'mesh': 0},
        ],
        edit: (model) => model.json['meshes'] = [
          {'primitives': 'a'},
        ],
      ),
      place,
      _wide,
    ),
    (
      nodes(
        [
          {'mesh': 0},
        ],
        edit: (model) => model.json['meshes'] = [
          {'primitives': <Object?>[]},
        ],
      ),
      place,
      _wide,
    ),
    (
      nodes(
        [
          {'mesh': 0},
        ],
        edit: (model) => model.json['meshes'] = [
          {
            'primitives': [null],
          },
        ],
      ),
      place,
      _wide,
    ),
    (
      nodes(
        [
          {'mesh': 0},
        ],
        edit: (model) => model.json['meshes'] = [
          {
            'primitives': ['a'],
          },
        ],
      ),
      place,
      _wide,
    ),
    (
      nodes([
        {'mesh': 0},
      ], edit: (model) => model.primitives.add('a')),
      place,
      _wide,
    ),
  ];
}

// Models the bake leaves alone, and the documents around them.
List<_Bake> _refusedModels(Uint8List one, List<Matrix4> each) {
  Uint8List edited(void Function(_Model model) edit) =>
      _build((model) => edit(model..triangle()));
  return [
    for (final edit in <void Function(_Model)>[
      (m) => m.json['skins'] = [
        {
          'joints': [0],
        },
      ],
      (m) => m.json['skins'] = <Object?>[],
      (m) => m.json['skins'] = 5,
      (m) => m.json['skins'] = 'a',
      (m) => m.json['skins'] = ['a'],
      (m) => m.json['skins'] = null,
      (m) => m.json['animations'] = [
        {'channels': <Object?>[], 'samplers': <Object?>[]},
      ],
      (m) => m.json['animations'] = <Object?>[],
      (m) => m.json['animations'] = 5,
      (m) => m.json['extensionsRequired'] = ['EXT_meshopt_compression'],
      (m) => m.json['extensionsRequired'] = ['KHR_draco_mesh_compression'],
      (m) =>
          m.json['extensionsRequired'] = ['OTHER', 'EXT_meshopt_compression'],
      (m) => m.json['extensionsRequired'] = ['OTHER'],
      (m) => m.json['extensionsRequired'] = [1, null, 'a'],
      (m) => m.json['extensionsRequired'] = 'EXT_meshopt_compression',
      (m) => m.json['extensionsRequired'] = <Object?>[],
      (m) => m.json['extensionsUsed'] = ['EXT_meshopt_compression'],
      (m) => m.json['cameras'] = [
        {'type': 'perspective'},
      ],
      (m) => m.json['extensions'] = {
        'KHR_lights_punctual': {
          'lights': [
            {'type': 'point'},
          ],
        },
      },
      (m) => m.json['buffers'] = [
        {'uri': 'beside.bin', 'byteLength': 10},
      ],
    ])
      (edited(edit), [Matrix4.identity()], _wide),
    // Bytes that are not a model.
    (Uint8List.fromList([1, 2, 3]), [Matrix4.identity()], _wide),
    (Uint8List(0), [Matrix4.identity()], _wide),
    (Uint8List.fromList(utf8.encode('{}')), [Matrix4.identity()], _wide),
    (Uint8List.fromList(utf8.encode('[]')), [Matrix4.identity()], _wide),
    (Uint8List.fromList(utf8.encode('null')), [Matrix4.identity()], _wide),
    (
      Uint8List.fromList(utf8.encode('{"asset":{"version":"2.0"}}')),
      [Matrix4.identity()],
      _wide,
    ),
    (Uint8List.fromList(one.sublist(0, 30)), [Matrix4.identity()], _wide),
    (
      Uint8List.fromList(one.sublist(0, one.length - 3)),
      [Matrix4.identity()],
      _wide,
    ),
    // A glTF written as text, with its buffer in a data URI.
    (_withDataUri(one), [Matrix4.identity(), ...each], _wide),
  ];
}

// The same triangle model, as glTF text over a data URI, so the buffer is not a GLB chunk.
Uint8List _withDataUri(Uint8List glb) {
  final data = ByteData.sublistView(glb);
  final jsonLength = data.getUint32(12, Endian.little);
  final json = _decode(utf8.decode(glb.sublist(20, 20 + jsonLength)));
  final binaryAt = 20 + jsonLength;
  final binaryLength = data.getUint32(binaryAt, Endian.little);
  final binary = glb.sublist(binaryAt + 8, binaryAt + 8 + binaryLength);
  json['buffers'] = [
    {
      'byteLength': binary.length,
      'uri': 'data:application/octet-stream;base64,${base64Encode(binary)}',
    },
  ];
  return Uint8List.fromList(utf8.encode(jsonEncode(json)));
}

Future<Map<String, Object?>> _mergeCase(
  String op,
  Uint8List model,
  int maxVertices,
) async => {
  'op': op,
  'in': [b64(model), maxVertices],
  'expect': await _attempt(() async {
    final document = await _open(model);
    return switch (op) {
      'mergeMaterials' => {
        'count': mergeMaterials(document),
        'json': jsonEncode(document.json),
      },
      'mergePrimitives' => _merged(
        document,
        mergePrimitives(document, maxVertices: maxVertices),
      ),
      _ => {
        'keys': [
          for (final mesh in document.list('meshes'))
            if (mesh['primitives'] case final List<Object?> list)
              for (final primitive in list)
                if (primitive is Map<String, Object?>)
                  primitiveGroupKey(document, primitive),
        ],
      },
    };
  }),
};

Future<Map<String, Object?>> _bakeCase(_Bake bake) async {
  final (model, placements, maxVertices) = bake;
  return {
    'op': 'bake',
    'in': [
      b64(model),
      [for (final placement in placements) _spec(placement)],
      maxVertices,
    ],
    'expect': await _attempt(() async {
      final out = await bakeInstances(
        model,
        placements,
        maxVertices: maxVertices,
      );
      if (out == null) return null;
      return _parts(out);
    }),
  };
}

Future<void> main(List<String> args) async {
  final primitives = _primitiveModels();
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'gltf_edit',
    cases: [
      for (final model in _materialModels())
        await _mergeCase('mergeMaterials', model, _wide),
      for (final (model, limit) in primitives) ...[
        await _mergeCase('mergePrimitives', model, limit),
        await _mergeCase('groupKey', model, limit),
      ],
      for (final bake in _bakeModels()) await _bakeCase(bake),
    ],
  );
}
