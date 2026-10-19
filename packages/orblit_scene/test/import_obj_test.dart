import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

Uint8List _bytes(String text) => Uint8List.fromList(utf8.encode(text));

MeshComponent _mesh(SceneEntity entity) =>
    entity.components[SceneComponents.mesh]! as MeshComponent;

const _two = '''
mtllib two.mtl
v 0 0 0
v 1 0 0
v 1 1 0
v 0 1 0
o floor
usemtl red
f 1 2 3 4
o wall
usemtl blue
f 1 2 3
f 1 3 4
''';

const _mtl = 'newmtl red\nKd 1 0 0\nnewmtl blue\nKd 0 0 1\n';

void main() {
  group('an OBJ', () {
    test('opens as one entity for each group, with its faces and colour', () {
      final read = readSceneFrom(
        _bytes(_two),
        name: 'Level',
        files: {'two.mtl': _bytes(_mtl)},
      );
      final doc = read.document;
      expect(doc.name, 'Level');
      expect(doc.entities.map((e) => e.name), ['floor', 'wall']);
      expect(doc.entities.map((e) => _mesh(e).geometry!.faceCount), [1, 2]);
      expect(_mesh(doc.entities.first).authored, isTrue);
      final red = _mesh(doc.entities.first).colour;
      expect(red.red, closeTo(1, 1e-9));
      expect(red.blue, 0);
      final blue = _mesh(doc.entities.last).colour;
      expect(blue.blue, closeTo(1, 1e-9));
      expect(blue.red, 0);
      expect(read.wasWrittenHere, isFalse);
      expect(read.problems, isEmpty);
    });

    test('says so when one group wears two materials', () {
      final read = readSceneFrom(
        _bytes(_two.replaceAll('o wall', 'o floor')),
        files: {'two.mtl': _bytes(_mtl)},
      );
      expect(read.document.entities, hasLength(1));
      expect(read.problems.single, contains('2 materials'));
    });

    test('carries what the reader could not read into the problems', () {
      final read = readSceneFrom(_bytes(_two));
      expect(read.problems.single, contains('two.mtl'));
      expect(_mesh(read.document.entities.first).geometry, isNotNull);
    });

    test('a mesh written by this package comes back as one entity', () {
      final cube = Shape.of(ShapeKind.cube).build();
      final files = cube.toObj(name: 'box');
      final read = readSceneFrom(
        files.first.bytes,
        files: {for (final file in files) file.name: file.bytes},
      );
      expect(read.document.entities.single.name, 'box');
      expect(_mesh(read.document.entities.single).geometry!.faceCount, 6);
    });

    test('with no faces is refused as a scene', () {
      expect(
        () => readSceneFrom(_bytes('v 0 0 0\n')),
        throwsA(isA<SceneFormatException>()),
      );
    });
  });

  group('the sniff', () {
    test('a glTF with a byte order mark still goes to the glTF reader', () {
      final bytes = Uint8List.fromList([0xEF, 0xBB, 0xBF, ..._bytes('[1]')]);
      expect(
        () => readSceneFrom(bytes),
        throwsA(
          isA<SceneFormatException>().having(
            (e) => e.message,
            'message',
            contains('glTF'),
          ),
        ),
      );
    });

    test('JSON, even an array, still goes to the glTF reader', () {
      expect(
        () => readSceneFrom(_bytes('  [1, 2]')),
        throwsA(
          isA<SceneFormatException>().having(
            (e) => e.message,
            'message',
            contains('glTF'),
          ),
        ),
      );
    });
  });
}
