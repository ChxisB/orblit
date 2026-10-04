import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

const positions = [
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

Uint8List model({
  List<Map<String, Object?>>? nodes,
  int mode = 4,
  List<int> indices = const [0, 2, 1, 0, 3, 2],
  bool skin = false,
}) {
  final binary = Uint8List(positions.length * 4 + indices.length * 2);
  final data = ByteData.sublistView(binary);
  for (var i = 0; i < positions.length; i++) {
    data.setFloat32(i * 4, positions[i], Endian.little);
  }
  for (var i = 0; i < indices.length; i++) {
    data.setUint16(positions.length * 4 + i * 2, indices[i], Endian.little);
  }
  return Uint8List.fromList(
    utf8.encode(
      jsonEncode({
        'asset': {'version': '2.0'},
        'buffers': [
          {
            'byteLength': binary.length,
            'uri':
                'data:application/octet-stream;base64,${base64Encode(binary)}',
          },
        ],
        'bufferViews': [
          {'buffer': 0, 'byteLength': positions.length * 4},
          {
            'buffer': 0,
            'byteOffset': positions.length * 4,
            'byteLength': indices.length * 2,
          },
        ],
        'accessors': [
          {'bufferView': 0, 'componentType': 5126, 'count': 4, 'type': 'VEC3'},
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
                'indices': 1,
                'mode': mode,
              },
            ],
          },
        ],
        'nodes':
            nodes ??
            [
              {'mesh': 0, if (skin) 'skin': 0},
            ],
        'scenes': [
          {
            'nodes': [0],
          },
        ],
        'scene': 0,
      }),
    ),
  );
}

Future<CollisionMesh> read(Uint8List bytes) => CollisionMesh.fromGltf(
  AssetId.parse('floor.gltf'),
  bytes,
  MemoryAssetSource({}),
);

void main() {
  test(
    'geometry round trips with a version and rejects invalid indices',
    () async {
      final mesh = await read(model());
      expect(mesh.vertices, positions);
      expect(CollisionMesh.decode(mesh.encode()).indices, [0, 2, 1, 0, 3, 2]);
      expect(
        () => CollisionMesh(vertices: positions, indices: [0, 1, 4]),
        throwsArgumentError,
      );
      expect(
        () => CollisionMesh.decode(utf8.encode('{"version":2}')),
        throwsFormatException,
      );
    },
  );

  test(
    'active scene node transforms and repeated instances are baked',
    () async {
      final mesh = await read(
        model(
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
      );
      expect(mesh.vertices.take(3), [1, 2, -1]);
      expect(mesh.vertices.skip(12).take(3), [2, 4, -1]);
      expect(mesh.vertices.length, 24);
      expect(mesh.indices.skip(6), [4, 6, 5, 4, 7, 6]);
    },
  );

  test(
    'triangle strips, fans and mirrored transforms preserve winding',
    () async {
      final strip = await read(model(mode: 5, indices: [0, 1, 3, 2]));
      expect(strip.indices, [0, 1, 3, 3, 1, 2]);
      final fan = await read(model(mode: 6, indices: [0, 1, 2, 3]));
      expect(fan.indices, [0, 1, 2, 0, 2, 3]);
      final mirrored = await read(
        model(
          nodes: [
            {
              'mesh': 0,
              'scale': [-1, 1, 1],
            },
          ],
        ),
      );
      expect(mirrored.indices, [0, 1, 2, 0, 2, 3]);
    },
  );

  test('a cook includes the collision sidecar only when requested', () async {
    final importer = GltfImporter();
    final id = AssetId.parse('floor.gltf');
    final bytes = model();
    Future<ImportResult> cook(bool collision) => importer.import(
      ImportRequest(
        id: id,
        bytes: bytes,
        settings: importer.resolveSettings(
          ImportSettings(values: {'collision': collision}),
        ),
        target: CookTarget.any,
        source: MemoryAssetSource({}),
      ),
    );
    expect((await cook(false)).outputs.keys, ['gltf']);
    final cooked = await cook(true);
    expect(cooked.outputs['gltf'], bytes);
    expect(
      CollisionMesh.decode(cooked.outputs['collision.json']!).vertices,
      positions,
    );
  });

  test(
    'malformed indexed arrays and compressed geometry are refused',
    () async {
      final json = jsonDecode(utf8.decode(model())) as Map<String, Object?>;
      final nodes = json['nodes'] as List<Object?>;
      nodes.insert(0, null);
      await expectLater(
        read(Uint8List.fromList(utf8.encode(jsonEncode(json)))),
        throwsFormatException,
      );
      nodes.removeAt(0);
      json['scene'] = 'zero';
      await expectLater(
        read(Uint8List.fromList(utf8.encode(jsonEncode(json)))),
        throwsFormatException,
      );
      json['scene'] = 0;
      final meshes = json['meshes'] as List<Object?>;
      final mesh = meshes.first as Map<String, Object?>;
      final primitives = mesh['primitives'] as List<Object?>;
      final primitive = primitives.first as Map<String, Object?>;
      primitive['extensions'] = {
        'KHR_draco_mesh_compression': <String, Object?>{},
      };
      await expectLater(
        read(Uint8List.fromList(utf8.encode(jsonEncode(json)))),
        throwsFormatException,
      );
    },
  );

  test('skins and cyclic hierarchies fail before a collider is made', () async {
    await expectLater(read(model(skin: true)), throwsFormatException);
    await expectLater(
      read(
        model(
          nodes: [
            {
              'mesh': 0,
              'children': [0],
            },
          ],
        ),
      ),
      throwsFormatException,
    );
  });
}
