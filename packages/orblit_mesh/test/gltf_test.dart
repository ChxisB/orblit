import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:test/test.dart';

void main() {
  group('the buffer it builds', () {
    test('every view starts on a four-byte boundary', () {
      final buffer = GltfBuffer()
        // Three bytes, so whatever follows has to be pushed along.
        ..addView(Uint8List.fromList([1, 2, 3]))
        ..addFloats(Float32List.fromList([1, 2, 3]), 'VEC3');

      expect(buffer.views[0]['byteOffset'], 0);
      expect(buffer.views[1]['byteOffset'], 4);
      expect(buffer.bytes.length % 4, 0);
    });

    test('a view says how long it is, not how long it was padded to', () {
      final buffer = GltfBuffer()..addView(Uint8List.fromList([1, 2, 3]));
      expect(buffer.views.single['byteLength'], 3);
    });

    test('positions carry the bounds glTF requires of them', () {
      final buffer = GltfBuffer()
        ..addPositions(Float32List.fromList([-1, 0, 2, 3, -4, 0, 0, 5, -6]));

      final accessor = buffer.accessors.single;
      expect(accessor['count'], 3);
      expect(accessor['min'], [-1, -4, -6]);
      expect(accessor['max'], [3, 5, 2]);
    });

    test('no positions means nought rather than infinity', () {
      // Whichever way round the sweep starts, an empty one leaves the bounds
      // at the extremes it started from, and glTF takes neither.
      final buffer = GltfBuffer()..addPositions(Float32List(0));
      expect(buffer.accessors.single['min'], [0, 0, 0]);
      expect(buffer.accessors.single['max'], [0, 0, 0]);
    });

    test('indices take the narrowest width that holds them', () {
      int widthOf(List<int> indices) {
        final buffer = GltfBuffer()..addIndices(indices);
        return buffer.accessors.single['componentType']! as int;
      }

      expect(widthOf([0, 1, 255]), GltfComponent.unsignedByte);
      expect(widthOf([0, 1, 256]), GltfComponent.unsignedShort);
      expect(widthOf([0, 1, 65535]), GltfComponent.unsignedShort);
      expect(widthOf([0, 1, 65536]), GltfComponent.unsignedInt);
    });

    test('a narrower index is a smaller buffer', () {
      final small = GltfBuffer()..addIndices([for (var i = 0; i < 300; i++) 1]);
      final large = GltfBuffer()
        ..addIndices([for (var i = 0; i < 300; i++) 70000]);

      expect(small.length, 300);
      expect(large.length, 1200);
    });

    test('an offset of nought is left out, since that is what it means', () {
      final buffer = GltfBuffer()
        ..addView(Float32List.fromList([1, 2, 3, 4]))
        ..addAccessor(
          view: 0,
          componentType: GltfComponent.float,
          count: 2,
          type: 'VEC2',
        );
      expect(buffer.accessors.single.containsKey('byteOffset'), isFalse);
    });

    test('a buffer nobody wrote to is no buffer at all', () {
      // A buffer of nought length is one of the few things the validator
      // calls an error outright.
      expect(GltfBuffer().buffers, isEmpty);
    });
  });

  group('the glb container', () {
    test('reads back what it wrote', () {
      final bytes = glbBytes({
        'asset': {'version': '2.0'},
      }, Uint8List.fromList([1, 2, 3, 4, 5]));

      final chunks = glbChunks(bytes)!;
      expect((chunks.json['asset']! as Map)['version'], '2.0');
      expect(chunks.binary.sublist(0, 5), [1, 2, 3, 4, 5]);
    });

    test('every chunk is a whole number of words', () {
      final bytes = glbBytes({
        'asset': {'version': '2.0'},
      }, Uint8List.fromList([1, 2, 3]));

      final view = ByteData.sublistView(bytes);
      expect(bytes.length % 4, 0);
      expect(view.getUint32(8, Endian.little), bytes.length);
      expect(view.getUint32(12, Endian.little) % 4, 0);
    });

    test('the json chunk is padded with spaces, so it stays json', () {
      // The specification asks for spaces rather than zeros precisely so a
      // reader taking the chunk at its stated length still finds json.
      final bytes = glbBytes({'asset': {}}, Uint8List(0));
      final length = ByteData.sublistView(bytes).getUint32(12, Endian.little);
      final text = utf8.decode(bytes.sublist(20, 20 + length));

      expect(text.trimRight(), '{"asset":{}}');
      expect(jsonDecode(text), isA<Map<String, Object?>>());
    });

    test('a document with no bytes writes no binary chunk', () {
      final bytes = glbBytes({'asset': {}}, Uint8List(0));
      final length = ByteData.sublistView(bytes).getUint32(12, Endian.little);
      expect(bytes.length, 20 + length);
      expect(glbChunks(bytes)!.binary, isEmpty);
    });

    test('anything that is not a glb reads as nothing', () {
      expect(glbChunks(Uint8List(0)), isNull);
      expect(glbChunks(Uint8List(64)), isNull, reason: 'no magic word');
      expect(
        glbChunks(Uint8List.fromList([1, 2, 3, 4, 5])),
        isNull,
        reason: 'too short to hold a header',
      );

      // The magic word and then nothing that follows it.
      final cut = glbBytes({'asset': {}}, Uint8List(0)).sublist(0, 24);
      expect(glbChunks(cut), isNull);
    });

    test('a name with an accent in it survives the trip', () {
      // The length in the header counts bytes, and utf-8 says a character is
      // not one of them. Writing the length in characters is a file that is
      // truncated by exactly as many accents as the name has.
      final bytes = glbBytes({
        'nodes': [
          {'name': 'Façade — étage'},
        ],
      }, Uint8List(0));

      final nodes = glbChunks(bytes)!.json['nodes']! as List;
      expect((nodes.single as Map)['name'], 'Façade — étage');
    });
  });

  group('the numbers it writes', () {
    test('a whole number goes out without its point', () {
      // A count written as 12.0 is a file some loaders refuse.
      final bytes = glbBytes({
        'metallicFactor': 1.0,
        'nested': [
          {'count': 12.0},
        ],
      }, Uint8List(0));
      final length = ByteData.sublistView(bytes).getUint32(12, Endian.little);
      final text = utf8.decode(bytes.sublist(20, 20 + length));

      expect(text, contains('"metallicFactor":1,'));
      expect(text, contains('"count":12'));
      expect(text, isNot(contains('.0')));
    });

    test('a number that is not whole keeps every bit of itself', () {
      final bytes = glbBytes({'roughness': 0.05}, Uint8List(0));
      expect(glbChunks(bytes)!.json['roughness'], 0.05);
    });
  });

  group('the gltf text it writes', () {
    test('names the buffer beside it', () {
      final json = gltfText(
        {
          'asset': {'version': '2.0'},
        },
        buffer: 'model.bin',
        byteLength: 64,
      );

      final buffers = (jsonDecode(json) as Map)['buffers'] as List;
      expect((buffers.single as Map)['uri'], 'model.bin');
      expect((buffers.single as Map)['byteLength'], 64);
    });

    test('a document with no bytes names no buffer', () {
      final json = gltfText({
        'asset': {'version': '2.0'},
        'buffers': [
          {'byteLength': 8},
        ],
      }, byteLength: 0);
      expect((jsonDecode(json) as Map).containsKey('buffers'), isFalse);
    });
  });
}
