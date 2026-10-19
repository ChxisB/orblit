import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:test/test.dart';

Uint8List _bytes(String text) => Uint8List.fromList(utf8.encode(text));

Mesh _cube() => Shape.of(ShapeKind.cube).build();

void _sameBox(Mesh read, Mesh written) {
  expect(read.bounds.min.distanceTo(written.bounds.min), closeTo(0, 1e-6));
  expect(read.bounds.max.distanceTo(written.bounds.max), closeTo(0, 1e-6));
}

/// A PLY with [header] lines and then [body], the way a file is laid out.
Uint8List _ply(List<String> header, [List<int> body = const []]) =>
    Uint8List.fromList([
      ...utf8.encode('${['ply', ...header, 'end_header'].join('\n')}\n'),
      ...body,
    ]);

void main() {
  group('STL', () {
    test('a binary file comes back with its corners welded', () {
      final cube = _cube();
      final mesh = readStl(cube.toStl());
      expect(mesh.faceCount, 12);
      expect(mesh.vertexCount, 8);
      expect(mesh.faces.every((f) => f.isTriangle), isTrue);
      _sameBox(mesh, cube);
    });

    test('a text file reads the same as a binary one', () {
      final cube = _cube();
      final mesh = readStl(cube.toStl(binary: false));
      expect(mesh.faceCount, 12);
      expect(mesh.vertexCount, 8);
      _sameBox(mesh, cube);
    });

    test('a binary header that begins with solid is still binary', () {
      final binary = _cube().toStl();
      binary.setRange(0, 5, utf8.encode('solid'));
      expect(readStl(binary).faceCount, 12);
    });

    test('a file cut short, or with a number that is not one, is refused', () {
      final binary = _cube().toStl();
      expect(
        () => readStl(binary.sublist(0, binary.length - 10)),
        throwsFormatException,
      );
      final broken = Uint8List.fromList(binary);
      ByteData.view(broken.buffer).setFloat32(96, double.nan, Endian.little);
      expect(() => readStl(broken), throwsFormatException);
      expect(
        () => readStl(_bytes('solid a\nvertex 0 0 0\nvertex 1 0 0\n')),
        throwsFormatException,
      );
      expect(() => readStl(Uint8List(0)), throwsFormatException);
      expect(
        () => readStl(Uint8List.fromList([1, 2, 3])),
        throwsFormatException,
      );
    });

    test('a count that promises more than the file holds is refused', () {
      final lie = Uint8List(84);
      ByteData.view(lie.buffer).setUint32(80, 0xFFFFFFFF, Endian.little);
      expect(() => readStl(lie), throwsFormatException);
    });

    test('a triangle with two equal corners has no area and is dropped', () {
      final mesh = readStl(
        _bytes(
          'solid a\n'
          'vertex 0 0 0\nvertex 1 0 0\nvertex 0 0 0\n'
          'vertex 0 0 0\nvertex 1 0 0\nvertex 0 1 -0\n'
          'endsolid a\n',
        ),
      );
      expect(mesh.faceCount, 1);
      expect(mesh.vertexCount, 3);
    });

    test('a file over the byte limit is refused', () {
      expect(
        () => readStl(_cube().toStl(), limits: const ReadLimits(bytes: 100)),
        throwsFormatException,
      );
    });
  });

  group('PLY', () {
    test('a binary file keeps its faces and its coordinates', () {
      final cube = _cube();
      final mesh = readPly(cube.toPly());
      expect(mesh.faceCount, 12);
      expect(mesh.vertexCount, 24);
      expect(mesh.faces.first.uv.manual, hasLength(3));
      _sameBox(mesh, cube);
    });

    test('a text file reads the same as a binary one', () {
      final cube = _cube();
      final mesh = readPly(cube.toPly(binary: false));
      expect(mesh.faceCount, 12);
      _sameBox(mesh, cube);
    });

    test('big endian, other elements and other properties are read past', () {
      final body = ByteData(3 * 13 + 8 + 13);
      var at = 0;
      for (final point in [
        [0.0, 0.0, 0.0],
        [2.0, 0.0, 0.0],
        [0.0, 3.0, 0.0],
      ]) {
        for (final value in point) {
          body.setFloat32(at, value);
          at += 4;
        }
        body.setUint8(at++, 200);
      }
      for (final value in [7, 8]) {
        body.setInt32(at, value);
        at += 4;
      }
      body.setUint8(at++, 3);
      for (final index in [0, 1, 2]) {
        body.setInt32(at, index);
        at += 4;
      }
      final mesh = readPly(
        _ply([
          'format binary_big_endian 1.0',
          'element vertex 3',
          'property float x',
          'property float y',
          'property float z',
          'property uchar red',
          'element edge 1',
          'property int a',
          'property int b',
          'element face 1',
          'property list uchar int vertex_indices',
        ], body.buffer.asUint8List()),
      );
      expect(mesh.faces.single.vertices, [0, 1, 2]);
      expect(mesh.bounds.max.y, 3);
    });

    test('a quad stays a quad', () {
      final mesh = readPly(
        _ply([
          'format ascii 1.0',
          'element vertex 4',
          'property float x',
          'property float y',
          'property float z',
          'element face 1',
          'property list uchar int vertex_index',
        ], _bytes('0 0 0\n1 0 0\n1 1 0\n0 1 0\n4 0 1 2 3\n')),
      );
      expect(mesh.faces.single.length, 4);
    });

    test('splats are the splat importer\'s, and the message says so', () {
      expect(
        () => readPly(
          _ply([
            'format ascii 1.0',
            'element vertex 1',
            'property float x',
            'property float y',
            'property float z',
            'property float f_dc_0',
            'property float opacity',
          ], _bytes('0 0 0 1 1\n')),
        ),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('splat'),
          ),
        ),
      );
    });

    test('points with no faces are not a model', () {
      expect(
        () => readPly(
          _ply([
            'format ascii 1.0',
            'element vertex 1',
            'property float x',
            'property float y',
            'property float z',
          ], _bytes('0 0 0\n')),
        ),
        throwsFormatException,
      );
    });

    test('a face naming a point that is not there is refused', () {
      expect(
        () => readPly(
          _ply([
            'format ascii 1.0',
            'element vertex 3',
            'property float x',
            'property float y',
            'property float z',
            'element face 1',
            'property list uchar int vertex_indices',
          ], _bytes('0 0 0\n1 0 0\n0 1 0\n3 0 1 9\n')),
        ),
        throwsFormatException,
      );
    });

    test('a header that promises more than the limits allow is refused', () {
      for (final count in ['4000000000', '99999999999999999999', '-1']) {
        expect(
          () => readPly(
            _ply([
              'format binary_little_endian 1.0',
              'element vertex $count',
              'property float x',
              'property float y',
              'property float z',
              'element face 1',
              'property list uchar int vertex_indices',
            ]),
          ),
          throwsFormatException,
        );
      }
    });

    test('a file with no header, or a body cut short, is refused', () {
      expect(
        () => readPly(_bytes('ply\nformat ascii 1.0\n')),
        throwsFormatException,
      );
      expect(() => readPly(_bytes('hello')), throwsFormatException);
      final binary = _cube().toPly();
      expect(
        () => readPly(binary.sublist(0, binary.length - 20)),
        throwsFormatException,
      );
    });
  });

  group('fuzz', () {
    void survives(Uint8List bytes, Mesh Function(Uint8List) read) {
      try {
        read(bytes);
      } on FormatException {
        // A refusal is an answer. Anything else is the bug.
      }
    }

    final sources = <String, (Uint8List, Mesh Function(Uint8List))>{
      'binary STL': (_cube().toStl(), readStl),
      'text STL': (_cube().toStl(binary: false), readStl),
      'binary PLY': (_cube().toPly(), readPly),
      'text PLY': (_cube().toPly(binary: false), readPly),
    };

    for (final entry in sources.entries) {
      test(
        '${entry.key}: random bytes, and a real file with bytes changed',
        () {
          final random = math.Random(21);
          final (source, read) = entry.value;
          for (var round = 0; round < 400; round++) {
            survives(
              Uint8List.fromList([
                for (var i = 0; i < random.nextInt(600); i++)
                  random.nextInt(256),
              ]),
              read,
            );
            final changed = Uint8List.fromList(source);
            for (var n = 0; n < 1 + random.nextInt(8); n++) {
              changed[random.nextInt(changed.length)] = random.nextInt(256);
            }
            survives(changed, read);
            survives(changed.sublist(0, random.nextInt(changed.length)), read);
          }
        },
      );
    }
  });
}
