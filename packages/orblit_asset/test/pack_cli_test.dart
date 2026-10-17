@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';

import 'package:test/test.dart';

import 'pack_fixture.dart';

void main() {
  late PackFixture fixture;

  setUp(() async => fixture = await PackFixture.create());
  tearDown(() => fixture.dispose());

  Future<ProcessResult> pack(List<String> arguments) => Process.run(
    Platform.resolvedExecutable,
    ['run', 'bin/pack.dart', ...arguments],
    workingDirectory: Directory.current.path,
  );

  String file(String name) => fixture.at('$name.opack');

  final scene = jsonEncode({'model': 'textures/wall.png'});

  group('pack diff', () {
    test('prints the sections and exits 0 for a clean pair', () async {
      await fixture.build('before', {'models/tree.glb': 'tree'});
      await fixture.build('after', {
        'models/tree.glb': 'tree two',
        'models/fresh.glb': 'fresh',
      });

      final result = await pack(['diff', file('before'), file('after')]);

      expect(result.exitCode, 0, reason: '${result.stderr}');
      expect(result.stdout, contains('added      1\n  models/fresh.glb'));
      expect(result.stdout, contains('removed    0'));
      expect(result.stdout, contains('models/tree.glb  4 to 8 bytes'));
      expect(result.stdout, contains('dangling   0'));
      expect(result.stdout, isNot(contains('unreadable')));
    });

    test('exits 1 and names the document when a removed name is still '
        'used', () async {
      await fixture.build('before', {
        'textures/wall.png': 'wall',
        'scenes/town.oscene': scene,
      });
      await fixture.build('after', {'scenes/town.oscene': scene});

      final result = await pack(['diff', file('before'), file('after')]);

      expect(result.exitCode, 1);
      expect(
        result.stdout,
        contains('scenes/town.oscene names textures/wall.png'),
      );
      expect(
        result.stderr,
        contains('missing names 1, unreadable documents 0'),
      );
    });

    test('reads a patch with the packs it stands on', () async {
      final game = await fixture.build('game', {
        'textures/wall.png': 'wall',
        'scenes/town.oscene': scene,
      });
      final first = await fixture.build(
        'first',
        {'textures/wall.png': 'wall two', 'scenes/town.oscene': scene},
        base: [game.pack],
      );
      await fixture.build(
        'second',
        {'textures/wall.png': 'wall three', 'scenes/town.oscene': scene},
        base: [game.pack, first.pack],
      );

      final lost = await pack(['diff', file('first'), file('second')]);
      final found = await pack([
        'diff',
        file('first'),
        file('second'),
        '--base',
        file('game'),
      ]);

      expect(lost.exitCode, 1);
      expect(lost.stderr, contains(game.hash.hex.substring(0, 12)));
      expect(found.exitCode, 0, reason: '${found.stderr}');
      expect(found.stdout, contains('textures/wall.png  8 to 10 bytes'));
    });

    test('exits 1 for packs of two targets', () async {
      await fixture.build('before', {'a/x.glb': 'one'});
      await fixture.build('after', {'a/x.glb': 'one'}, target: 'web');

      final result = await pack(['diff', file('before'), file('after')]);

      expect(result.exitCode, 1);
      expect(result.stderr, contains('android'));
      expect(result.stderr, contains('web'));
    });

    test('exits 1 for a pack that is not there', () async {
      await fixture.build('before', {'a/x.glb': 'one'});

      final result = await pack(['diff', file('before'), file('nothing')]);

      expect(result.exitCode, 1);
      expect(result.stderr, contains('nothing.opack'));
    });

    test('exits 2 when it is not given two packs', () async {
      await fixture.build('before', {'a/x.glb': 'one'});

      final result = await pack(['diff', file('before')]);

      expect(result.exitCode, 2);
      expect(result.stderr, contains('diff takes an old pack and a new one'));
    });
  });
}
