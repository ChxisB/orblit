import 'dart:io';

import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_test/orblit_test.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'support.dart';

void main() {
  final before = SceneDocument(entities: [thing('crate', at: Vector3.zero())]);

  group('describeChange', () {
    test('is an empty list for a scene that did not change', () {
      expect(describeChange(before, before), '[]\n');
    });

    test('names the entity and the field that moved', () {
      final after = before.withEntity(
        'crate',
        thing('crate', at: Vector3(0, 1, 0)),
      );

      final text = describeChange(before, after);

      expect(text, contains('"crate"'));
      expect(text, contains('position'));
    });
  });

  group('matchesTextFile', () {
    late Directory directory;
    late String path;

    setUp(() {
      directory = Directory.systemTemp.createTempSync('orblit_test_');
      path = '${directory.path}/frames/one.txt';
    });

    tearDown(() => directory.deleteSync(recursive: true));

    test('writes the file on request and then matches it', () {
      expect('a\nb\n', matchesTextFile(path, update: true));

      expect(File(path).readAsStringSync(), 'a\nb\n');
      expect('a\nb\n', matchesTextFile(path, update: false));
    });

    test('says there is no file, and how to make one', () {
      expect(
        refusal(matchesTextFile(path, update: false), 'a'),
        contains('ORBLIT_UPDATE_GOLDENS=1'),
      );
    });

    test('says which line differs', () {
      expect('a\nb\n', matchesTextFile(path, update: true));

      expect(
        refusal(matchesTextFile(path, update: false), 'a\nc\n'),
        allOf(contains('line 2'), contains('"b"'), contains('"c"')),
      );
    });

    test('says when this is longer than the file', () {
      expect('a\n', matchesTextFile(path, update: true));

      expect(
        refusal(matchesTextFile(path, update: false), 'a\nb\n'),
        allOf(contains('line 2'), contains('nothing')),
      );
    });
  });
}
