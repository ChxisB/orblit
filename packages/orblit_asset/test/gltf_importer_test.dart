import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

Uint8List gltf(Map<String, Object?> document) =>
    Uint8List.fromList(utf8.encode(jsonEncode(document)));

/// A GLB whose JSON chunk holds [document]: a 12-byte header, then a chunk
/// header, then the JSON padded to four bytes as the format requires.
Uint8List glb(Map<String, Object?> document) {
  final json = utf8.encode(jsonEncode(document));
  final padding = (4 - json.length % 4) % 4;
  final chunk = [...json, ...List.filled(padding, 0x20)];
  final bytes = Uint8List(20 + chunk.length);
  final view = ByteData.view(bytes.buffer);
  view.setUint32(0, 0x46546C67, Endian.little);
  view.setUint32(4, 2, Endian.little);
  view.setUint32(8, bytes.length, Endian.little);
  view.setUint32(12, chunk.length, Endian.little);
  view.setUint32(16, 0x4E4F534A, Endian.little);
  bytes.setRange(20, bytes.length, chunk);
  return bytes;
}

Future<Set<AssetId>> depsOf(Importer importer, AssetId of, Uint8List bytes) =>
    importer.dependenciesOf(of, bytes, const {});

void main() {
  const gltfImporter = GltfImporter();

  group('GltfImporter', () {
    test('claims both container formats', () {
      expect(gltfImporter.handles(id('a.gltf')), isTrue);
      expect(gltfImporter.handles(id('a.glb')), isTrue);
      expect(gltfImporter.handles(id('a.fbx')), isFalse);
    });

    test('finds the buffers and images beside a .gltf', () async {
      final bytes = gltf({
        'buffers': [
          {'uri': 'helmet.bin'},
        ],
        'images': [
          {'uri': 'textures/albedo.png'},
          {'uri': 'textures/normal.png'},
        ],
      });
      expect(await depsOf(gltfImporter, id('models/helmet.gltf'), bytes), {
        id('models/helmet.bin'),
        id('models/textures/albedo.png'),
        id('models/textures/normal.png'),
      });
    });

    test('resolves a URI that climbs out of the model\'s folder', () async {
      final bytes = gltf({
        'images': [
          {'uri': '../shared/wood.png'},
        ],
      });
      expect(await depsOf(gltfImporter, id('models/chair/chair.gltf'), bytes), {
        id('models/shared/wood.png'),
      });
    });

    test('decodes a percent-encoded URI, because that is how a space '
        'survives a glTF', () async {
      final bytes = gltf({
        'images': [
          {'uri': 'base%20colour.png'},
        ],
      });
      expect(await depsOf(gltfImporter, id('m.gltf'), bytes), {
        id('base colour.png'),
      });
    });

    test('bytes already inside the file are not dependencies', () async {
      final bytes = gltf({
        'buffers': [
          {'uri': 'data:application/octet-stream;base64,AAAA'},
          {'byteLength': 4},
        ],
        'images': [
          {'bufferView': 0},
        ],
      });
      expect(await depsOf(gltfImporter, id('m.gltf'), bytes), isEmpty);
    });

    test('a URI with a scheme is left alone rather than guessed at', () async {
      final bytes = gltf({
        'images': [
          {'uri': 'https://example.invalid/a.png'},
        ],
      });
      expect(await depsOf(gltfImporter, id('m.gltf'), bytes), isEmpty);
    });

    test('a URI climbing out of the project fails, naming it', () async {
      final bytes = gltf({
        'images': [
          {'uri': '../../outside.png'},
        ],
      });
      expect(
        () => depsOf(gltfImporter, id('m.gltf'), bytes),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('outside.png'),
          ),
        ),
      );
    });

    test('a GLB is read through its JSON chunk', () async {
      final bytes = glb({
        'images': [
          {'uri': 'beside.png'},
        ],
      });
      expect(await depsOf(gltfImporter, id('m.glb'), bytes), {
        id('beside.png'),
      });
    });

    test('a self-contained GLB depends on nothing', () async {
      expect(
        await depsOf(gltfImporter, id('m.glb'), glb({'images': []})),
        isEmpty,
      );
    });

    test('a .gltf saved under a .glb name is an error, not an empty '
        'dependency list', () async {
      expect(
        () => depsOf(
          gltfImporter,
          id('m.glb'),
          gltf({
            'images': [
              {'uri': 'a.png'},
              {'uri': 'b.png'},
            ],
          }),
        ),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('magic'),
          ),
        ),
      );
    });

    test('a truncated GLB is an error', () async {
      final bytes = glb({'images': []});
      final short = Uint8List.sublistView(bytes, 0, bytes.length - 4);
      expect(
        () => depsOf(gltfImporter, id('m.glb'), short),
        throwsA(isA<ImportFailure>()),
      );
    });

    test('a GLB too short to have a header is an error', () async {
      expect(
        () => depsOf(gltfImporter, id('m.glb'), Uint8List(8)),
        throwsA(isA<ImportFailure>()),
      );
    });

    test('malformed JSON is an error', () async {
      expect(
        () => depsOf(
          gltfImporter,
          id('m.gltf'),
          Uint8List.fromList(utf8.encode('{nope')),
        ),
        throwsA(isA<ImportFailure>()),
      );
    });

    test('passes the bytes through under the container\'s name', () async {
      final bytes = gltf({'images': []});
      final result = await gltfImporter.import(
        ImportRequest(
          id: id('m.gltf'),
          bytes: bytes,
          settings: const {},
          target: CookTarget.any,
          source: MemoryAssetSource(),
        ),
      );
      expect(result.outputs.keys, ['gltf']);
      expect(result.outputs['gltf'], bytes);
    });
  });

  group('SceneImporter', () {
    const scene = SceneImporter();

    Future<Set<AssetId>> depsIn(Map<String, Object?> document) => depsOf(
      scene,
      id('scenes/level.oscene'),
      Uint8List.fromList(utf8.encode(jsonEncode(document))),
    );

    test('finds what a scene places, however deep', () async {
      expect(
        await depsIn({
          'nodes': [
            {
              'name': 'floor',
              'model': 'models/floor.glb',
              'children': [
                {'name': 'lamp', 'model': '../props/lamp.glb'},
              ],
            },
          ],
          'environment': 'sky/dusk.hdr',
        }),
        {id('models/floor.glb'), id('props/lamp.glb'), id('sky/dusk.hdr')},
      );
    });

    test('finds a field nobody thought to name here, because it matches the '
        'shape rather than a list', () async {
      expect(
        await depsIn({
          'nodes': [
            {'emissiveTexture': 'lights/glow.png'},
          ],
        }),
        {id('lights/glow.png')},
      );
    });

    test(
      'a reference is an asset id, so moving the scene does not break it',
      () async {
        expect(
          await depsIn({
            'nodes': [
              {'model': 'models/floor.glb'},
            ],
          }),
          {id('models/floor.glb')},
          reason: 'not scenes/models/floor.glb',
        );
      },
    );

    test(
      'a reference spelled ./ is read against the scene\'s own folder',
      () async {
        expect(
          await depsIn({
            'nodes': [
              {'model': './beside.glb'},
            ],
          }),
          {id('scenes/beside.glb')},
        );
      },
    );

    test('a reference key holding something that is not a name at all is not '
        'a dependency', () async {
      expect(
        await depsIn({
          'nodes': [
            {'material': 'matte white'},
          ],
        }),
        isEmpty,
      );
    });

    test('a string under a key that names nothing is left alone', () async {
      expect(await depsIn({'name': 'level one', 'author': 'someone'}), isEmpty);
    });

    test('malformed JSON is an error', () async {
      expect(
        () => depsOf(
          scene,
          id('a.oscene'),
          Uint8List.fromList(utf8.encode('nope')),
        ),
        throwsA(isA<ImportFailure>()),
      );
    });
  });
}
