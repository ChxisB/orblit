@TestOn('vm')
library;

import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

import 'pack_fixture.dart';

void main() {
  late PackFixture fixture;

  setUp(() async => fixture = await PackFixture.create());
  tearDown(() => fixture.dispose());

  Future<AssetPack> build(
    String name,
    Map<String, String> files, {
    List<AssetPack> base = const [],
    String target = 'android',
  }) async =>
      (await fixture.build(name, files, base: base, target: target)).pack;

  AssetId id(String text) => AssetId.parse(text);

  /// A scene that places each of [paths] as a model.
  String scene(List<String> paths) => jsonEncode({
    'entities': [
      for (final path in paths) {'name': 'thing', 'model': path},
    ],
  });

  Future<PackDiff> diff(AssetPack before, AssetPack after) async =>
      PackDiff.between(
        before: mountChain(before, const []),
        after: mountChain(after, const []),
      );

  Future<PackDiff> diffPatch(
    AssetPack before,
    AssetPack after,
    List<AssetPack> pool,
  ) async => PackDiff.between(
    before: mountChain(before, pool),
    after: mountChain(after, pool),
  );

  group('PackDiff', () {
    test('lists what was added, removed and changed by name', () async {
      final before = await build('before', {
        'textures/wall.png': 'wall one',
        'models/tree.glb': 'tree',
        'textures/gone.png': 'gone',
      });
      final after = await build('after', {
        'textures/wall.png': 'wall two is longer',
        'models/tree.glb': 'tree',
        'models/fresh.glb': 'fresh',
      });

      final result = await diff(before, after);

      expect(result.added, [id('models/fresh.glb')]);
      expect(result.removed, [id('textures/gone.png')]);
      expect(result.changed, hasLength(1));
      expect(result.changed.single.id, id('textures/wall.png'));
      expect(result.changed.single.before, 'wall one'.length);
      expect(result.changed.single.after, 'wall two is longer'.length);
      expect(result.isBroken, isFalse);
    });

    test('says nothing about two packs that hold the same things', () async {
      final files = {'models/tree.glb': 'tree', 'a/scene.oscene': '{}'};
      final result = await diff(
        await build('one', files),
        await build('two', files),
      );

      expect(result.added, isEmpty);
      expect(result.removed, isEmpty);
      expect(result.changed, isEmpty);
      expect(result.dangling, isEmpty);
      expect(result.isBroken, isFalse);
    });

    test('a scene that names a removed texture is dangling', () async {
      final before = await build('before', {
        'textures/wall.png': 'wall',
        'models/tree.glb': 'tree',
        'scenes/town.oscene': scene(['models/tree.glb', 'textures/wall.png']),
      });
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'scenes/town.oscene': scene(['models/tree.glb', 'textures/wall.png']),
      });

      final result = await diff(before, after);

      expect(result.removed, [id('textures/wall.png')]);
      expect(result.dangling, hasLength(1));
      expect(result.dangling.single.document, id('scenes/town.oscene'));
      expect(result.dangling.single.missing, id('textures/wall.png'));
      expect(result.isBroken, isTrue);
    });

    test('a prefab, a clip and a blend are read as well', () async {
      final gone = jsonEncode({
        'clips': ['anim/gone.oclip'],
      });
      final after = await build('after', {
        'anim/walk.oclip': '{}',
        'a/rig.oprefab': gone,
        'a/walk.oclip': gone,
        'a/move.oblend': jsonEncode({
          'states': [
            {'clip': 'anim/gone.oclip'},
          ],
        }),
      });

      final result = await diff(await build('before', {'a': ''}), after);

      expect(result.dangling.map((d) => d.document.toString()), [
        'a/move.oblend',
        'a/rig.oprefab',
        'a/walk.oclip',
      ], reason: 'by name, and a list of strings counts');
    });

    test('a reference that starts "./" is read against its document', () async {
      final after = await build('after', {
        'scenes/town.oscene': scene(['./tree.glb', '../models/tree.glb']),
        'scenes/tree.glb': 'tree',
        'models/other.glb': 'other',
      });

      final result = await diff(await build('before', {'a': ''}), after);

      expect(result.dangling.map((d) => d.missing.toString()), [
        'models/tree.glb',
      ]);
    });

    test('text that is not the name of a kind of asset is not a '
        'reference', () async {
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'scenes/town.oscene': jsonEncode({
          'name': 'Hero.1',
          'note': 'see notes.txt',
          'looks': 'matte white',
          'model': 'models/tree.glb',
          'other': 'readme.txt',
        }),
      });

      final result = await diff(await build('before', {'a': ''}), after);

      expect(result.dangling, isEmpty);
    });

    test('a document that is not JSON is listed as unreadable', () async {
      final after = await build('after', {'scenes/town.oscene': 'not json'});

      final result = await diff(await build('before', {'a': ''}), after);

      expect(result.unreadable, [id('scenes/town.oscene')]);
      expect(result.isBroken, isTrue);
    });

    test('a patch is read with its base, so what it did not touch is '
        'present', () async {
      final game = await build('game', {
        'textures/wall.png': 'wall',
        'textures/roof.png': 'roof',
        'scenes/town.oscene': scene(['textures/wall.png', 'textures/roof.png']),
      });
      final patch = await build(
        'patch',
        {
          'textures/wall.png': 'wall repainted',
          'scenes/town.oscene': scene([
            'textures/wall.png',
            'textures/roof.png',
          ]),
        },
        base: [game],
      );

      final result = await diffPatch(game, patch, [game]);

      expect(result.changed.map((c) => c.id.toString()), ['textures/wall.png']);
      expect(result.removed, [id('textures/roof.png')]);
      expect(
        result.dangling.single.document,
        id('scenes/town.oscene'),
        reason: 'the scene is in the base, the removal is in the patch',
      );
      expect(result.dangling.single.missing, id('textures/roof.png'));
    });

    test('a patch that removes nothing leaves its base scene whole', () async {
      final game = await build('game', {
        'textures/wall.png': 'wall',
        'scenes/town.oscene': scene(['textures/wall.png']),
      });
      final patch = await build(
        'patch',
        {
          'textures/wall.png': 'wall repainted',
          'scenes/town.oscene': scene(['textures/wall.png']),
        },
        base: [game],
      );

      final result = await diffPatch(game, patch, [game]);

      expect(result.dangling, isEmpty);
      expect(result.changed, hasLength(1));
    });
  });

  group('mountChain', () {
    test('finds the packs under a patch by hash, in any order', () async {
      final game = await build('game', {'a/x.glb': 'one'});
      final first = await build('first', {'a/x.glb': 'two'}, base: [game]);
      final second = await build(
        'second',
        {'a/x.glb': 'three'},
        base: [game, first],
      );

      final packs = mountChain(second, [first, game]);

      expect(packs.chainHash, second.hash);
      expect(utf8.decode(await packs.read(id('a/x.glb'))), 'three');
      expect(mountChain(second, [game, first]).chainHash, second.hash);
    });

    test('names the pack that is missing', () async {
      final game = await build('game', {'a/x.glb': 'one'});
      final patch = await build('patch', {'a/x.glb': 'two'}, base: [game]);

      expect(
        () => mountChain(patch, const []),
        throwsA(
          isA<PackMountRefused>().having(
            (e) => e.message,
            'message',
            contains(game.hash.hex.substring(0, 12)),
          ),
        ),
      );
    });

    test('a pack with no base stands on its own', () async {
      final game = await build('game', {'a/x.glb': 'one'});

      expect(mountChain(game, const []).chainHash, game.hash);
    });
  });
}
