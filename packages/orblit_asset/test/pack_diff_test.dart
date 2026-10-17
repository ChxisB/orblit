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

  Future<PackDiff> diff(
    AssetPack before,
    AssetPack after, [
    List<AssetPack> pool = const [],
  ]) async => PackDiff.between(
    before: MountedPacks.withBases(before, pool),
    after: MountedPacks.withBases(after, pool),
  );

  /// The older pack of a pair, holding every asset the newer one drops.
  Future<AssetPack> older(Map<String, String> files) => build('before', {
    'textures/gone.png': 'gone',
    'models/tree.glb': 'tree',
    'anim/gone.oclip': '{}',
    ...files,
  });

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

    test('every kind of document is read, by name', () async {
      final names = jsonEncode({
        'clips': ['anim/gone.oclip'],
      });
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'a/rig.oprefab': names,
        'a/walk.oclip': names,
        'a/move.oblend': jsonEncode({
          'states': [
            {'clip': 'anim/gone.oclip'},
          ],
        }),
        'a/look.omat': jsonEncode({'albedo': 'textures/gone.png'}),
      });

      final result = await diff(await older({}), after);

      expect(result.dangling.map((d) => d.document.toString()), [
        'a/look.omat',
        'a/move.oblend',
        'a/rig.oprefab',
        'a/walk.oclip',
      ]);
    });

    test('a name used as a key counts', () async {
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'scenes/town.oscene': jsonEncode({
          'overrides': {'textures/gone.png': 1},
        }),
      });

      final result = await diff(await older({}), after);

      expect(result.dangling.single.missing, id('textures/gone.png'));
    });

    test('a reference that starts "./" is read against its document', () async {
      final after = await build('after', {
        'scenes/town.oscene': scene([
          '../models/tree.glb',
          './tree.glb',
          './models/tree.glb',
        ]),
        'scenes/tree.glb': 'tree',
        'textures/gone.png': 'kept',
      });

      final result = await diff(
        await older({'scenes/tree.glb': 'tree'}),
        after,
      );

      expect(
        result.dangling.map((d) => d.missing.toString()),
        ['models/tree.glb'],
        reason:
            '"./tree.glb" is still there and "./models/tree.glb" is '
            'another file',
      );
    });

    test('free text that ends in a removed name is not a reference', () async {
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'scenes/town.oscene': jsonEncode({
          'name': 'Hero.1',
          'note': 'see textures/gone.png',
          'looks': 'matte white',
          'other': 'readme.txt',
        }),
      });

      final result = await diff(await older({}), after);

      expect(result.dangling, isEmpty);
    });

    test('a name that never was in the older pack is not dangling', () async {
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'scenes/town.oscene': scene(['textures/never.png', 'models/tree.glb']),
      });

      final result = await diff(await older({}), after);

      expect(result.removed, [id('anim/gone.oclip'), id('textures/gone.png')]);
      expect(result.dangling, isEmpty);
    });

    test('a name that is removed and still present is not dangling', () async {
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'textures/gone.png': 'back again',
        'scenes/town.oscene': scene(['textures/gone.png']),
      });

      final result = await diff(await older({}), after);

      expect(result.dangling, isEmpty);
    });

    test('a document that is not JSON is unreadable once something is '
        'removed', () async {
      final after = await build('after', {
        'models/tree.glb': 'tree',
        'scenes/town.oscene': 'not json',
      });

      final result = await diff(await older({}), after);

      expect(result.unreadable, [id('scenes/town.oscene')]);
      expect(result.isBroken, isTrue);
    });

    test('documents are not read when nothing was removed', () async {
      final files = {'scenes/town.oscene': 'not json'};

      final result = await diff(
        await build('before', files),
        await build('after', {...files, 'models/fresh.glb': 'fresh'}),
      );

      expect(result.unreadable, isEmpty);
      expect(result.isBroken, isFalse);
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

      final result = await diff(game, patch, [game]);

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

      final result = await diff(game, patch, [game]);

      expect(result.dangling, isEmpty);
      expect(result.changed, hasLength(1));
    });

    test('packs for different targets are refused', () async {
      final android = await build('android', {'a/x.glb': 'one'});
      final web = await build('web', {'a/x.glb': 'one'}, target: 'web');

      await expectLater(
        diff(android, web),
        throwsA(
          isA<PackMountRefused>().having(
            (e) => e.message,
            'message',
            allOf(contains('android'), contains('web')),
          ),
        ),
      );
    });

    test('the lists cannot be changed by the caller', () async {
      final result = await diff(
        await older({}),
        await build('after', {'models/tree.glb': 'tree'}),
      );

      expect(() => result.added.add(id('a/b.glb')), throwsUnsupportedError);
      expect(() => result.removed.clear(), throwsUnsupportedError);
      expect(() => result.changed.clear(), throwsUnsupportedError);
      expect(() => result.dangling.clear(), throwsUnsupportedError);
      expect(() => result.unreadable.clear(), throwsUnsupportedError);
    });
  });

  group('MountedPacks.withBases', () {
    test('finds the packs under a patch by hash, in any order', () async {
      final game = await build('game', {'a/x.glb': 'one'});
      final first = await build('first', {'a/x.glb': 'two'}, base: [game]);
      final second = await build(
        'second',
        {'a/x.glb': 'three'},
        base: [game, first],
      );

      final packs = MountedPacks.withBases(second, [first, game]);

      expect(packs.chainHash, second.hash);
      expect(packs.target, 'android');
      expect(utf8.decode(await packs.read(id('a/x.glb'))), 'three');
      expect(
        MountedPacks.withBases(second, [game, first]).chainHash,
        second.hash,
      );
    });

    test('names the pack that is missing', () async {
      final game = await build('game', {'a/x.glb': 'one'});
      final patch = await build('patch', {'a/x.glb': 'two'}, base: [game]);

      expect(
        () => MountedPacks.withBases(patch, const []),
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

      expect(MountedPacks.withBases(game, const []).chainHash, game.hash);
    });
  });
}
