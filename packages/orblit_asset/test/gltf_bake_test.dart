import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/gltf/accessor.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'gltf_fixtures.dart';

/// A model of triangles. Each has a normal along +z and a tangent along +x
/// with sign +1, so a placement that turns or mirrors it is easy to read.
class TriangleModel {
  final Map<String, Object?> json = {
    'asset': {'version': '2.0'},
    'materials': <Object?>[<String, Object?>{}],
    'meshes': [
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
  int _at = 0;

  List<Object?> get primitives =>
      ((json['meshes'] as List).first as Map)['primitives'] as List<Object?>;

  int _accessor(
    List<int> bytes,
    int componentType,
    int count,
    String type, [
    Map<String, Object?> extra = const {},
  ]) {
    final views = json['bufferViews'] as List;
    views.add({'buffer': 0, 'byteOffset': _at, 'byteLength': bytes.length});
    _bin.add(bytes);
    _at += bytes.length;
    final pad = (4 - _at % 4) % 4;
    _bin.add(Uint8List(pad));
    _at += pad;
    final accessors = json['accessors'] as List;
    accessors.add({
      'bufferView': views.length - 1,
      'componentType': componentType,
      'count': count,
      'type': type,
      ...extra,
    });
    return accessors.length - 1;
  }

  int material(Map<String, Object?> value) {
    (json['materials'] as List).add(value);
    return (json['materials'] as List).length - 1;
  }

  /// A triangle whose corners are (0,0,0), (1,0,0) and (0,1,0) plus [at].
  void triangle({
    int material = 0,
    List<double> at = const [0, 0, 0],
    List<double> normal = const [0, 0, 1],
  }) {
    final positions = Float32List.fromList([
      at[0], at[1], at[2], //
      at[0] + 1, at[1], at[2],
      at[0], at[1] + 1, at[2],
    ]);
    final normals = Float32List.fromList([...normal, ...normal, ...normal]);
    final tangents = Float32List.fromList([1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1]);
    final coords = Float32List.fromList([0, 0, 1, 0, 0, 1]);
    primitives.add(<String, Object?>{
      'attributes': <String, Object?>{
        'POSITION': _accessor(
          Uint8List.sublistView(positions),
          5126,
          3,
          'VEC3',
        ),
        'NORMAL': _accessor(Uint8List.sublistView(normals), 5126, 3, 'VEC3'),
        'TANGENT': _accessor(Uint8List.sublistView(tangents), 5126, 3, 'VEC4'),
        'TEXCOORD_0': _accessor(Uint8List.sublistView(coords), 5126, 3, 'VEC2'),
      },
      'indices': _accessor(
        Uint8List.sublistView(Uint16List.fromList([0, 1, 2])),
        5123,
        3,
        'SCALAR',
      ),
      'material': material,
    });
  }

  Uint8List toGlb() {
    final bytes = _bin.toBytes();
    json['buffers'] = [
      {'byteLength': bytes.length},
    ];
    return glb(json, bytes);
  }
}

Future<GltfDocument> open(Uint8List bytes) => GltfDocument.read(
  AssetId.parse('baked.glb'),
  bytes,
  MemoryAssetSource(const {}),
);

Future<GltfDocument> baked(
  TriangleModel model,
  List<Matrix4> placements, {
  int maxVertices = 65536,
}) async {
  final out = await bakeInstances(
    model.toGlb(),
    placements,
    maxVertices: maxVertices,
  );
  expect(out, isNotNull);
  return open(out!);
}

List<Map<String, Object?>> primitivesOf(GltfDocument document) => [
  for (final p in document.list('meshes').first['primitives'] as List)
    p as Map<String, Object?>,
];

int attribute(Map<String, Object?> primitive, String name) =>
    (primitive['attributes'] as Map<String, Object?>)[name]! as int;

Matcher near(List<double> expected) => pairwiseCompare<double, double>(
  expected,
  (want, got) => (want - got).abs() < 1e-5,
  'within 1e-5 of',
);

void main() {
  group('bakeInstances', () {
    test('puts the model where each matrix says', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [
        Matrix4.translation(Vector3(10, 0, 0)),
        Matrix4.translation(Vector3(0, 5, 0))..rotateZ(math.pi / 2),
      ]);

      final primitive = primitivesOf(document).single;
      expect(
        document.readVec3(attribute(primitive, 'POSITION')),
        near([
          10, 0, 0, 11, 0, 0, 10, 1, 0, //
          0, 5, 0, 0, 6, 0, -1, 5, 0,
        ]),
      );
      expect(document.readIndices(primitive['indices']! as int), [
        0,
        1,
        2,
        3,
        4,
        5,
      ]);
    });

    test('turns normals and tangents with the placement', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [Matrix4.rotationX(math.pi / 2)]);

      final primitive = primitivesOf(document).single;
      final normal = document.readVec3(attribute(primitive, 'NORMAL'));
      expect(normal.sublist(0, 3), near([0, -1, 0]));
      final tangent = Float32List.sublistView(
        document.accessorBytes(attribute(primitive, 'TANGENT')),
      );
      expect(tangent.sublist(0, 4), near([1, 0, 0, 1]));
    });

    test('keeps a normal square to the surface under a stretch', () async {
      // A tilted normal on a surface stretched along x leans away from x. The
      // plain matrix would lean it towards x and light it wrongly.
      final lean = math.sqrt1_2;
      final model = TriangleModel()..triangle(normal: [lean, 0, lean]);
      final document = await baked(model, [Matrix4.diagonal3Values(4, 1, 1)]);

      final normal = document.readVec3(
        attribute(primitivesOf(document).single, 'NORMAL'),
      );
      final length = math.sqrt(0.25 * 0.25 + 1);
      expect(normal.sublist(0, 3), near([0.25 / length, 0, 1 / length]));
    });

    test('a mirrored placement keeps the front face front', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [Matrix4.diagonal3Values(-1, 1, 1)]);

      final primitive = primitivesOf(document).single;
      expect(document.readIndices(primitive['indices']! as int), [0, 2, 1]);
      final tangent = Float32List.sublistView(
        document.accessorBytes(attribute(primitive, 'TANGENT')),
      );
      expect(tangent[3], -1);
    });

    test('keeps the model nodes in the baked positions', () async {
      final model = TriangleModel()..triangle();
      model.json['nodes'] = [
        <String, Object?>{
          'scale': [2, 2, 2],
          'children': [1],
        },
        <String, Object?>{
          'mesh': 0,
          'translation': [1, 0, 0],
        },
      ];
      (model.json['scenes'] as List)[0] = {
        'nodes': [0],
      };
      final document = await baked(model, [Matrix4.identity()]);

      final positions = document.readVec3(
        attribute(primitivesOf(document).single, 'POSITION'),
      );
      // The parent scales what its child translated.
      expect(positions.sublist(0, 6), near([2, 0, 0, 4, 0, 0]));
    });

    test('draws as many primitives as the model has materials', () async {
      final model = TriangleModel();
      final a = model.material({'name': 'a'});
      final b = model.material({'name': 'b'});
      for (var i = 0; i < 3; i++) {
        model
          ..triangle(material: a)
          ..triangle(material: b);
      }
      final document = await baked(model, [
        for (var i = 0; i < 50; i++) Matrix4.translation(Vector3(i * 2, 0, 0)),
      ]);

      final primitives = primitivesOf(document);
      expect(primitives.map((p) => p['material']), [a, b]);
      expect(document.accessorCount(attribute(primitives[0], 'POSITION')), 450);
    });

    test('starts another primitive before the index width changes', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [
        for (var i = 0; i < 5; i++) Matrix4.translation(Vector3(i * 2, 0, 0)),
      ], maxVertices: 6);

      final counts = [
        for (final p in primitivesOf(document))
          document.accessorCount(attribute(p, 'POSITION')),
      ];
      expect(counts, [6, 6, 3]);
      final last = primitivesOf(document).last;
      expect(document.readIndices(last['indices']! as int), [0, 1, 2]);
    });

    test('writes one node holding one mesh', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [Matrix4.identity()]);

      expect(document.list('nodes'), hasLength(1));
      expect(document.list('meshes'), hasLength(1));
      expect(document.json['scene'], 0);
    });

    test('bounds the positions so culling believes them', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [
        Matrix4.translation(Vector3(100, 0, 0)),
      ]);

      final accessor = document.accessor(
        attribute(primitivesOf(document).single, 'POSITION'),
      );
      expect(accessor['min'], [100.0, 0.0, 0.0]);
      expect(accessor['max'], [101.0, 1.0, 0.0]);
    });

    test('skips a placement with no volume', () async {
      final model = TriangleModel()..triangle();
      final document = await baked(model, [
        Matrix4.diagonal3Values(0, 1, 1),
        Matrix4.identity(),
      ]);

      final primitive = primitivesOf(document).single;
      expect(document.accessorCount(attribute(primitive, 'POSITION')), 3);
    });
  });

  group('bakeInstances leaves alone what it cannot place', () {
    final one = [Matrix4.identity()];

    test('nothing to place', () async {
      final model = TriangleModel()..triangle();
      expect(await bakeInstances(model.toGlb(), const []), isNull);
      expect(
        await bakeInstances(model.toGlb(), [Matrix4.diagonal3Values(0, 1, 1)]),
        isNull,
      );
    });

    for (final (name, edit) in <(String, void Function(TriangleModel))>[
      (
        'a skin',
        (m) => m.json['skins'] = [
          {
            'joints': [0],
          },
        ],
      ),
      (
        'an animation',
        (m) => m.json['animations'] = [
          {'channels': <Object?>[], 'samplers': <Object?>[]},
        ],
      ),
      (
        'a morph target',
        (m) => (m.primitives.first! as Map<String, Object?>)['targets'] = [
          <String, Object?>{},
        ],
      ),
      (
        'a compressed buffer',
        (m) => m.json['extensionsRequired'] = ['EXT_meshopt_compression'],
      ),
      (
        'a node that is instanced already',
        (m) => (m.json['nodes'] as List)[0] = {
          'mesh': 0,
          'extensions': {'KHR_mesh_gpu_instancing': <String, Object?>{}},
        },
      ),
      (
        'a position stored in shorts',
        (m) {
          final primitive = m.primitives.first! as Map<String, Object?>;
          final at = (primitive['attributes'] as Map)['POSITION'] as int;
          ((m.json['accessors'] as List)[at] as Map)['componentType'] = 5122;
        },
      ),
    ]) {
      test(name, () async {
        final model = TriangleModel()..triangle();
        edit(model);
        expect(await bakeInstances(model.toGlb(), one), isNull);
      });
    }

    test('a buffer it cannot read', () async {
      final model = TriangleModel()..triangle();
      model.json['buffers'] = [
        {'uri': 'beside.bin', 'byteLength': 10},
      ];
      final bytes = glb(model.json, Uint8List(0));
      expect(await bakeInstances(bytes, one), isNull);
    });
  });
}
