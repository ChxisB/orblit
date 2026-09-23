import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:test/test.dart';

void main() {
  group('reading accessors', () {
    test('scales a normalised short into minus one to one', () {
      final bytes = ByteData(8)
        ..setInt16(0, 32767, Endian.little)
        ..setInt16(2, -32768, Endian.little)
        ..setInt16(4, 0, Endian.little)
        ..setInt16(6, 16384, Endian.little);
      final read = GltfAccessors({
        'bufferViews': [
          {'buffer': 0, 'byteLength': 8},
        ],
        'accessors': [
          {
            'bufferView': 0,
            'componentType': 5122,
            'normalized': true,
            'count': 1,
            'type': 'VEC4',
          },
        ],
      }, bytes.buffer.asUint8List());
      final numbers = read.floats(0, 4);
      expect(numbers[0], 1);
      expect(numbers[1], -1, reason: 'clamped, as the format says');
      expect(numbers[2], 0);
      expect(numbers[3], closeTo(0.5, 1e-4));
      expect(read.problems, isEmpty);
    });

    test('minds the stride of an interleaved view', () {
      final bytes = ByteData(16)
        ..setFloat32(0, 1, Endian.little)
        ..setFloat32(4, 99, Endian.little)
        ..setFloat32(8, 2, Endian.little)
        ..setFloat32(12, 99, Endian.little);
      final read = GltfAccessors({
        'bufferViews': [
          {'buffer': 0, 'byteLength': 16, 'byteStride': 8},
        ],
        'accessors': [
          {
            'bufferView': 0,
            'componentType': 5126,
            'count': 2,
            'type': 'SCALAR',
          },
        ],
      }, bytes.buffer.asUint8List());
      expect(read.floats(0, 1), [1, 2]);
      expect(read.count(0), 2);
    });

    test('notes what it cannot read rather than throwing', () {
      final read = GltfAccessors({
        'accessors': [
          {'bufferView': 0, 'componentType': 5126, 'count': 3},
        ],
      }, null);
      expect(read.floats(0, 3), isEmpty);
      expect(read.floats(7, 3), isEmpty, reason: 'no such accessor');
      expect(read.problems, hasLength(1));
    });
  });

  group('the parts of a document', () {
    test('come out of a GLB', () {
      final glb = glbBytes({
        'asset': {'version': '2.0'},
      }, Uint8List(4));
      final parts = gltfParts(glb);
      expect(parts.json['asset'], {'version': '2.0'});
      expect(parts.binary, hasLength(4));
    });

    test('come out of JSON with its buffer in a data URI', () {
      final json = {
        'asset': {'version': '2.0'},
        'buffers': [
          {
            'byteLength': 3,
            'uri':
                'data:application/octet-stream;base64,${base64Encode([1, 2, 3])}',
          },
        ],
      };
      final parts = gltfParts(utf8.encode(jsonEncode(json)));
      expect(parts.binary, [1, 2, 3]);
    });

    test('come out of JSON with its buffer beside it', () {
      final json = {
        'buffers': [
          {'byteLength': 2, 'uri': 'old%20town.bin'},
        ],
      };
      final parts = gltfParts(
        utf8.encode(jsonEncode(json)),
        files: {
          'old town.bin': Uint8List.fromList([7, 8]),
        },
      );
      expect(parts.binary, [7, 8]);
    });

    test('are refused when the bytes are neither', () {
      expect(
        () => gltfParts(Uint8List.fromList(utf8.encode('not a model'))),
        throwsFormatException,
      );
    });
  });
}
