import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:test/test.dart';

Uint8List _bytes(String text) => Uint8List.fromList(utf8.encode(text));

ObjModel _read(
  String text, {
  Map<String, String> files = const {},
  ReadLimits limits = const ReadLimits(),
}) => readObj(
  _bytes(text),
  files: {for (final entry in files.entries) entry.key: _bytes(entry.value)},
  limits: limits,
);

const _square = '''
v 0 0 0
v 1 0 0
v 1 1 0
v 0 1 0
''';

void main() {
  group('faces', () {
    test('a quad and a pentagon keep their corners', () {
      final model = _read('''
v 0 0 0
v 1 0 0
v 1 1 0
v 0 1 0
v -1 0.5 0
f 1 2 3 4
f 1 4 5
f 1 2 3 4 5
''');
      expect(model.mesh.faces.map((f) => f.length), [4, 3, 5]);
      expect(model.mesh.vertexCount, 5);
    });

    test('a negative index counts back from the last point read', () {
      final model = _read('$_square\nf -4 -3 -2 -1\n');
      expect(model.mesh.faces.single.vertices, [0, 1, 2, 3]);
    });

    test('a corner may carry a coordinate and a normal, or only a normal', () {
      final model = _read('''
$_square
vt 0 0
vt 1 0
vt 1 1
vt 0 1
vn 0 0 1
f 1/1/1 2/2/1 3/3/1 4/4/1
f 1//1 2//1 3//1
''');
      final first = model.mesh.faces.first.uv;
      expect(first.isManual, isTrue);
      expect(first.manual!.map((uv) => uv.y), [0, 0, 1, 1]);
      expect(model.mesh.faces.last.uv.isManual, isFalse);
    });

    test('a face with a corner that is not there is dropped, with a note', () {
      final model = _read('$_square\nf 1 2 3 4\nf 1 2 9\nf 1 2\n');
      expect(model.mesh.faceCount, 1);
      expect(model.problems, hasLength(2));
      expect(model.problems.first, contains('Line 7'));
    });

    test('a line ending in a backslash goes on to the next', () {
      final model = _read('$_square\nf 1 2 \\\n 3 4\n');
      expect(model.mesh.faces.single.length, 4);
    });

    test('a smoothing group turns smoothing on, and off turns it off', () {
      final model = _read('$_square\ns 1\nf 1 2 3\ns off\nf 1 3 4\n');
      expect(model.mesh.faces.map((f) => f.smooth), [true, false]);
    });
  });

  group('groups and materials', () {
    const mtl = '''
newmtl red
Kd 1 0 0
Pr 0.25
Pm 0.75
newmtl glass
Kd 0.2 0.4 0.6
d 0.5
Ke 0.1 0.2 0.3
map_Kd glass.png
''';

    test('each object or group is its own group of faces', () {
      final model = _read('''
$_square
o left
f 1 2 3
g right extra
f 1 3 4
o left
f 1 2 4
''');
      expect(model.groups.map((g) => g.name), ['left', 'right extra']);
      expect(model.groups.first.faces, [0, 2]);
      expect(model.meshOf(model.groups.first).faceCount, 2);
    });

    test('faces before any name are in a group called default', () {
      final model = _read('$_square\nf 1 2 3\n');
      expect(model.groups.single.name, 'default');
    });

    test('a group carries only the points its faces use', () {
      final model = _read('''
$_square
v 5 5 5
o far
f 1 2 3
o near
f 1 3 4
''');
      final near = model.meshOf(model.groups.last);
      expect(near.vertexCount, 3);
      expect(near.faces.single.vertices.every((i) => i < 3), isTrue);
    });

    test('usemtl picks a material from the library mtllib names', () {
      final model = _read(
        '''
mtllib one.mtl
$_square
usemtl glass
f 1 2 3
usemtl red
f 1 3 4
''',
        files: {'one.mtl': mtl},
      );
      expect(model.materials.map((m) => m.name), ['red', 'glass']);
      expect(model.mesh.faces.map((f) => f.material), [1, 0]);
      final red = model.materials.first;
      expect(red.colour.take(3), [1, 0, 0]);
      expect(red.roughness, 0.25);
      expect(red.metallic, 0.75);
      final glass = model.materials.last;
      expect(glass.colour[3], 0.5);
      expect(glass.emissive, [0.1, 0.2, 0.3]);
      expect(model.problems.single, contains('glass'));
      expect(model.problems.single, contains('pictures'));
    });

    test('a library that was not given is a note, and the name is kept', () {
      final model = _read('mtllib gone.mtl\n$_square\nusemtl wood\nf 1 2 3\n');
      expect(model.materials.single.name, 'wood');
      expect(model.problems.single, contains('gone.mtl'));
    });

    test('a library is found by its file name whatever folder it names', () {
      final model = _read(
        'mtllib models/one.mtl\n$_square\nusemtl red\nf 1 2 3\n',
        files: {'one.mtl': 'newmtl red\nKd 1 0 0\n'},
      );
      expect(model.materials.first.name, 'red');
      expect(model.problems, isEmpty);
    });

    test('roughness comes from the exponent when there is no Pr', () {
      final model = _read(
        'mtllib a.mtl\n$_square\nusemtl a\nf 1 2 3\n',
        files: {'a.mtl': 'newmtl a\nNs 901\n'},
      );
      expect(model.materials.single.roughness, closeTo(0, 1e-9));
    });
  });

  group('limits', () {
    test('a file over the byte limit is refused before it is read', () {
      expect(
        () => _read(_square, limits: const ReadLimits(bytes: 10)),
        throwsFormatException,
      );
    });

    test('too many points, or too many faces, stop the read', () {
      expect(
        () => _read('$_square\nf 1 2 3', limits: const ReadLimits(vertices: 3)),
        throwsFormatException,
      );
      expect(
        () => _read(
          '$_square\nf 1 2 3\nf 1 3 4',
          limits: const ReadLimits(faces: 1),
        ),
        throwsFormatException,
      );
    });

    test('a face with too many corners is dropped, not the file', () {
      final model = _read(
        '$_square\nf 1 2 3 4 1\nf 1 2 3',
        limits: const ReadLimits(corners: 4),
      );
      expect(model.mesh.faceCount, 1);
      expect(model.problems.single, contains('corners'));
    });

    test('a thousand bad lines make a short list of notes', () {
      final model = _read('$_square\nf 1 2 3\n${'f 1 2 99\n' * 1000}');
      expect(model.problems.length, lessThan(60));
      expect(model.problems.last, contains('more'));
    });

    test('numbers that are not numbers, or not finite, are notes', () {
      final model = _read('v 1e999 0 0\nv a b c\nv 1 2\n$_square\nf 1 2 3');
      expect(model.mesh.vertexCount, 4);
      expect(model.problems, hasLength(3));
    });

    test('no faces at all is not a model', () {
      expect(() => _read(_square), throwsFormatException);
      expect(() => _read(''), throwsFormatException);
      expect(
        () => readObj(Uint8List.fromList([7, 7, 7])),
        throwsFormatException,
      );
    });
  });

  group('written by this package', () {
    test('a mesh written as OBJ is read back as the same geometry', () {
      final cube = Shape.of(ShapeKind.cube).build();
      final files = cube.toObj(
        name: 'box',
        materials: const [GlbMaterial(name: 'paint', metallic: 0.5)],
      );
      final model = readObj(
        files.first.bytes,
        files: {for (final file in files) file.name: file.bytes},
      );

      expect(model.problems, isEmpty);
      expect(model.mesh.vertexCount, cube.vertexCount);
      expect(model.mesh.faces.map((f) => f.length), [4, 4, 4, 4, 4, 4]);
      expect(model.groups.single.name, 'box');
      expect(model.materials.single.name, 'paint');
      expect(model.materials.single.metallic, closeTo(0.5, 1e-6));
      for (var i = 0; i < cube.faceCount; i++) {
        expect(model.mesh.faces[i].vertices, cube.faces[i].vertices);
        expect(model.mesh.faces[i].uv.manual, hasLength(4));
      }
      final a = cube.bounds;
      final b = model.mesh.bounds;
      expect(b.min.distanceTo(a.min), closeTo(0, 1e-6));
      expect(b.max.distanceTo(a.max), closeTo(0, 1e-6));
    });
  });

  group('bounds', () {
    test('boundsOfObj gives the box of the points', () {
      final box = boundsOfObj(_bytes('v -1 0 2\nv 3 4 5\nv 0 0 0\nf 1 2 3\n'))!;
      expect([box.min.x, box.min.y, box.min.z], [-1, 0, 0]);
      expect([box.max.x, box.max.y, box.max.z], [3, 4, 5]);
    });

    test('anything that is not a model has none', () {
      expect(boundsOfObj(Uint8List(0)), isNull);
      expect(boundsOfObj(_bytes('v 1 2 3\n')), isNull);
      expect(boundsOfObj(Uint8List.fromList([0, 255, 3])), isNull);
    });
  });

  group('fuzz', () {
    const cube = '''
mtllib a.mtl
v -1 -1 -1
v 1 -1 -1
v 1 1 -1
v -1 1 -1
vt 0 0
vt 1 0
vt 1 1
o box
usemtl a
s 1
f 1/1 2/2 3/3 4/1
f -1 -2 -3
''';

    void survives(Uint8List bytes) {
      try {
        readObj(bytes, files: {'a.mtl': _bytes('newmtl a\nKd 1 1 1\n')});
      } on FormatException {
        // A refusal is an answer. Anything else is the bug.
      }
    }

    test('random bytes are read or refused, never a crash', () {
      final random = math.Random(19);
      for (var round = 0; round < 400; round++) {
        survives(
          Uint8List.fromList([
            for (var i = 0; i < random.nextInt(2000); i++) random.nextInt(256),
          ]),
        );
      }
    });

    test('a valid file with bytes changed is read or refused', () {
      final random = math.Random(20);
      final source = _bytes(cube);
      for (var round = 0; round < 600; round++) {
        final changed = Uint8List.fromList(source);
        for (var n = 0; n < 1 + random.nextInt(8); n++) {
          changed[random.nextInt(changed.length)] = random.nextInt(256);
        }
        survives(changed);
      }
    });

    test('numbers at the edge of what an int or a double holds', () {
      for (final edge in [
        '99999999999999999999',
        '-99999999999999999999',
        '9223372036854775807',
        '-9223372036854775808',
        '0',
        '-0',
        '1e308',
        '-1e308',
        'nan',
        'inf',
      ]) {
        survives(
          _bytes('v $edge $edge $edge\n$_square\nf $edge 1 2 3\nf 1 2 3'),
        );
      }
    });
  });
}
