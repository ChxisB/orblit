import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/src/asset_id.dart';
import 'package:orblit_asset/src/asset_source.dart';
import 'package:orblit_asset/src/gltf/accessor.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/importer.dart';
import 'package:test/test.dart';

import 'gltf_fixtures.dart';

void main() {
  final id = AssetId.parse('models/thing.glb');
  final empty = MemoryAssetSource(const {});

  group('GltfDocument', () {
    test('reads a GLB and gives back the bytes its views cover', () async {
      final values = Float32List.fromList([1, 2, 3, 4, 5, 6]);
      final document = await GltfDocument.read(
        id,
        glb({
          'asset': {'version': '2.0'},
          'buffers': [
            {'byteLength': values.lengthInBytes},
          ],
          'bufferViews': [
            {'buffer': 0, 'byteOffset': 0, 'byteLength': values.lengthInBytes},
          ],
        }, Uint8List.sublistView(values)),
        empty,
      );

      expect(
        Float32List.sublistView(document.viewBytes(0)),
        [1, 2, 3, 4, 5, 6],
      );
    });

    test('reads a .gltf, its data URIs and the files beside it', () async {
      final beside = Uint8List.fromList([9, 9, 9, 9]);
      final document = await GltfDocument.read(
        AssetId.parse('models/thing.gltf'),
        Uint8List.fromList(utf8.encode(jsonEncode({
          'asset': {'version': '2.0'},
          'buffers': [
            {'uri': 'geometry.bin', 'byteLength': 4},
            {
              'uri': 'data:application/octet-stream;base64,'
                  '${base64Encode(const [1, 2, 3, 4])}',
              'byteLength': 4,
            },
          ],
          'bufferViews': [
            {'buffer': 0, 'byteOffset': 0, 'byteLength': 4},
            {'buffer': 1, 'byteOffset': 0, 'byteLength': 4},
          ],
        }))),
        MemoryAssetSource({AssetId.parse('models/geometry.bin'): beside}),
      );

      expect(document.viewBytes(0), [9, 9, 9, 9]);
      expect(document.viewBytes(1), [1, 2, 3, 4]);
    });

    test('flattens every buffer into one when it writes a GLB', () async {
      // Two buffers of a length that is not a multiple of four, so that a
      // rewrite which forgot to align the second would be read at the wrong
      // offset rather than merely being untidy.
      final document = await GltfDocument.read(
        AssetId.parse('models/thing.gltf'),
        Uint8List.fromList(utf8.encode(jsonEncode({
          'asset': {'version': '2.0'},
          'buffers': [
            {'uri': 'a.bin', 'byteLength': 3},
            {'uri': 'b.bin', 'byteLength': 3},
          ],
          'bufferViews': [
            {'buffer': 0, 'byteOffset': 0, 'byteLength': 3},
            {'buffer': 1, 'byteOffset': 0, 'byteLength': 3},
          ],
        }))),
        MemoryAssetSource({
          AssetId.parse('models/a.bin'): Uint8List.fromList([1, 2, 3]),
          AssetId.parse('models/b.bin'): Uint8List.fromList([4, 5, 6]),
        }),
      );
      final added = document.addView(const [7, 7]);

      final again = await GltfDocument.read(id, document.toGlb(), empty);
      expect((again.json['buffers'] as List).length, 1);
      expect(again.viewBytes(0), [1, 2, 3]);
      expect(again.viewBytes(1), [4, 5, 6]);
      expect(again.viewBytes(added), [7, 7]);
      for (final view in again.list('bufferViews')) {
        expect(view['byteOffset'] as int, isA<int>());
        expect((view['byteOffset'] as int) % 4, 0,
            reason: 'a view that starts off a four-byte boundary cannot hold '
                'a float accessor');
      }
    });

    test('keeps every key it was not asked about', () async {
      final document = await GltfDocument.read(
        id,
        glb({
          'asset': {'version': '2.0', 'generator': 'somebody else'},
          'extensionsUsed': ['KHR_materials_variants'],
          'extensions': {
            'KHR_materials_variants': {
              'variants': [
                {'name': 'red'},
              ],
            },
          },
          'extras': {'anything': 42},
          'nodes': [
            {'name': 'root', 'extras': {'mine': true}},
          ],
        }, Uint8List(0)),
        empty,
      );

      final again = await GltfDocument.read(id, document.toGlb(), empty);
      expect(again.json['extras'], {'anything': 42});
      expect(again.json['extensionsUsed'], ['KHR_materials_variants']);
      expect(again.json['extensions'], isNotNull);
      expect((again.list('nodes').first['extras'] as Map)['mine'], true);
      expect((again.json['asset'] as Map)['generator'], 'somebody else');
    });

    test('says which file is wrong rather than reading past the end',
        () async {
      final bytes = glb({'asset': {'version': '2.0'}}, Uint8List(0));
      final truncated = Uint8List.sublistView(bytes, 0, bytes.length - 4);
      expect(
        () => GltfDocument.read(id, truncated, empty),
        throwsA(isA<ImportFailure>()
            .having((e) => e.reason, 'reason', contains('truncated'))),
      );
    });

    test('refuses a GLB that is not version 2', () {
      final bytes = glb({'asset': {'version': '2.0'}}, Uint8List(0));
      ByteData.sublistView(bytes).setUint32(4, 1, Endian.little);
      expect(
        () => GltfDocument.read(id, bytes, empty),
        throwsA(isA<ImportFailure>()
            .having((e) => e.reason, 'reason', contains('version 1'))),
      );
    });
  });

  group('accessors', () {
    Future<GltfDocument> of(
      Map<String, Object?> json,
      Uint8List binary,
    ) =>
        GltfDocument.read(id, glb(json, binary), empty);

    test('reads texture coordinates however they are stored', () async {
      final floats = Float32List.fromList([0.0, 0.5, 1.0, 0.25]);
      final shorts = Uint16List.fromList([0, 32768, 65535, 16384]);
      final binary = BytesBuilder()
        ..add(Uint8List.sublistView(floats))
        ..add(Uint8List.sublistView(shorts));
      final bytes = binary.toBytes();

      final document = await of({
        'asset': {'version': '2.0'},
        'buffers': [
          {'byteLength': bytes.length},
        ],
        'bufferViews': [
          {'buffer': 0, 'byteOffset': 0, 'byteLength': 16},
          {'buffer': 0, 'byteOffset': 16, 'byteLength': 8},
        ],
        'accessors': [
          {
            'bufferView': 0,
            'componentType': 5126,
            'count': 2,
            'type': 'VEC2',
          },
          {
            'bufferView': 1,
            'componentType': 5123,
            'normalized': true,
            'count': 2,
            'type': 'VEC2',
          },
        ],
      }, bytes);

      expect(document.readVec2(0), [0.0, 0.5, 1.0, 0.25]);
      final quantized = document.readVec2(1);
      expect(quantized[0], 0.0);
      expect(quantized[1], closeTo(0.5, 0.001));
      expect(quantized[2], 1.0);
      expect(quantized[3], closeTo(0.25, 0.001));
    });

    test('reads an interleaved view at its stride', () async {
      // Position and UV in one buffer, sixteen bytes apart: the layout a
      // well-optimised exporter writes and the one a naive reader gets wrong.
      final values = Float32List.fromList([
        1, 2, 3, 0.25, // vertex 0: xyz, then u,v starts
        0.75, 0, 0, 0,
        4, 5, 6, 0.5,
        0.125, 0, 0, 0,
      ]);
      final bytes = Uint8List.sublistView(values);
      final document = await of({
        'asset': {'version': '2.0'},
        'buffers': [
          {'byteLength': bytes.length},
        ],
        'bufferViews': [
          {
            'buffer': 0,
            'byteOffset': 0,
            'byteLength': bytes.length,
            'byteStride': 32,
          },
        ],
        'accessors': [
          {
            'bufferView': 0,
            'byteOffset': 0,
            'componentType': 5126,
            'count': 2,
            'type': 'VEC3',
          },
          {
            'bufferView': 0,
            'byteOffset': 12,
            'componentType': 5126,
            'count': 2,
            'type': 'VEC2',
          },
        ],
      }, bytes);

      expect(document.readVec3(0), [1, 2, 3, 4, 5, 6]);
      expect(document.readVec2(1), [0.25, 0.75, 0.5, 0.125]);
      // Tightly packed on the way out, because it is about to be joined to
      // another accessor's bytes and two strides cannot be concatenated.
      expect(document.accessorBytes(1).length, 16);
      expect(Float32List.sublistView(document.accessorBytes(1)),
          [0.25, 0.75, 0.5, 0.125]);
    });

    test('narrows new indices to the smallest width that holds them',
        () async {
      final document = await of({'asset': {'version': '2.0'}}, Uint8List(0));
      expect(document.accessor(document.addIndices([0, 1, 2]))['componentType'],
          5121);
      expect(document.accessor(document.addIndices([0, 300]))['componentType'],
          5123);
      expect(
          document.accessor(document.addIndices([0, 70000]))['componentType'],
          5125);
    });

    test('refuses a sparse accessor instead of reading its base', () async {
      final document = await of({
        'asset': {'version': '2.0'},
        'accessors': [
          {
            'componentType': 5126,
            'count': 1,
            'type': 'VEC2',
            'sparse': {'count': 1},
          },
        ],
      }, Uint8List(0));
      expect(() => document.readVec2(0),
          throwsA(isA<FormatException>()
              .having((e) => e.message, 'message', contains('sparse'))));
    });
  });
}
