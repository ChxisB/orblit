@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  late Directory temp;

  setUp(() async {
    temp = await Directory.systemTemp.createTemp('orblit_mount_');
  });

  tearDown(() => temp.delete(recursive: true));

  String at(String relative) =>
      [temp.path, ...relative.split('/')].join(Platform.pathSeparator);

  /// Writes a bundle folder of [files], each a path and its text, with the
  /// manifest that goes with it.
  Future<String> bundle(String folder, Map<String, String> files) async {
    final entries = <AssetId, AssetEntry>{};
    for (final MapEntry(:key, :value) in files.entries) {
      final bytes = utf8.encode(value);
      final file = File(at('$folder/$key'));
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes);
      entries[AssetId.parse(key)] = AssetEntry(
        ContentHash.of(bytes),
        bytes.length,
      );
    }
    await File(
      at('$folder/manifest.json'),
    ).writeAsString(AssetManifest(entries: entries).encode());
    return at(folder);
  }

  /// Packs [files] as `<name>.opack` over [base], and opens it.
  Future<AssetPack> build(
    String name,
    Map<String, String> files, {
    List<AssetPack> base = const [],
    String target = 'android',
  }) async {
    final folder = await bundle('bundle_$name', files);
    await packBundle(
      bundlePath: folder,
      outPath: at('$name.opack'),
      target: CookTargets.find(target)!,
      base: base,
    );
    final bytes = await File(at('$name.opack')).readAsBytes();
    return AssetPack.open(MemoryPackBytes(bytes));
  }

  MountedPacks mounted([String target = 'android']) =>
      MountedPacks(target: CookTargets.find(target)!);

  Future<String> text(AssetSource source, String id) async =>
      utf8.decode(await source.read(AssetId.parse(id)));

  final game = {
    'textures/wall.ktx2': 'wall v1',
    'models/tree.glb': 'tree',
    'scenes/town.oscene': 'town',
  };

  Matcher refused(String text) => throwsA(
    isA<PackMountRefused>().having(
      (error) => error.message,
      'message',
      contains(text),
    ),
  );

  group('a patch', () {
    test('holds what changed, what is new, and names what went', () async {
      final base = await build('game', game);
      final patch = await build(
        'patch',
        {
          'textures/wall.ktx2': 'wall v2',
          'models/tree.glb': 'tree',
          'models/rock.glb': 'rock',
        },
        base: [base],
      );

      expect(patch.manifest.entries.length, 2);
      expect(patch.manifest.entries.keys.map((id) => '$id').toList()..sort(), [
        'models/rock.glb',
        'textures/wall.ktx2',
      ]);
      expect(patch.removed.map((id) => '$id'), ['scenes/town.oscene']);
      expect(patch.base, base.hash);

      final packs = mounted()
        ..start(base)
        ..patch(patch);
      expect(await text(packs, 'textures/wall.ktx2'), 'wall v2');
      expect(await text(packs, 'models/rock.glb'), 'rock');
      expect(await text(packs, 'models/tree.glb'), 'tree');
      await expectLater(
        packs.read(AssetId.parse('scenes/town.oscene')),
        throwsA(isA<AssetNotFound>()),
      );
      expect(await packs.exists(AssetId.parse('scenes/town.oscene')), isFalse);
    });

    test('reports two blobs in its summary', () async {
      final base = await build('game', game);
      final folder = await bundle('next', {
        'textures/wall.ktx2': 'wall v2',
        'models/tree.glb': 'tree',
        'models/rock.glb': 'rock',
      });
      final summary = await packBundle(
        bundlePath: folder,
        outPath: at('next.opack'),
        target: CookTargets.android,
        base: [base],
      );
      expect(summary.blobs, 2);
      expect(summary.assets, 2);
      expect(summary.removed, 1);
    });

    test('counts what is new apart from what it replaces', () async {
      final base = await build('game', game);
      final folder = await bundle('next', {
        'textures/wall.ktx2': 'wall v2',
        'models/tree.glb': 'tree',
        'models/rock.glb': 'rock',
        'models/twin.glb': 'tree',
      });
      final summary = await packBundle(
        bundlePath: folder,
        outPath: at('next.opack'),
        target: CookTargets.android,
        base: [base],
      );
      expect(summary.assets, 3);
      expect(summary.replaced, 1);
      expect(summary.added, 2);
      expect(summary.removed, 1);
    });

    test('of nothing is all added when it has no base', () async {
      final folder = await bundle('whole', game);
      final summary = await packBundle(
        bundlePath: folder,
        outPath: at('whole.opack'),
        target: CookTargets.android,
      );
      expect(summary.added, 3);
      expect(summary.replaced, 0);
    });

    test('is the same file every time', () async {
      final base = await build('game', game);
      final folder = await bundle('next', {'textures/wall.ktx2': 'wall v2'});
      for (final name in ['a', 'b']) {
        await packBundle(
          bundlePath: folder,
          outPath: at('$name.opack'),
          target: CookTargets.android,
          base: [base],
        );
      }
      expect(
        await File(at('a.opack')).readAsBytes(),
        await File(at('b.opack')).readAsBytes(),
      );
    });

    test('of an unchanged bundle holds nothing and still mounts', () async {
      final base = await build('game', game);
      final patch = await build('patch', game, base: [base]);
      expect(patch.manifest.entries, isEmpty);
      expect(patch.removed, isEmpty);

      final packs = mounted()
        ..start(base)
        ..patch(patch);
      expect(packs.manifest.entries.length, 3);
      expect(packs.chainHash, patch.hash);
    });

    test('holds a new id even when its bytes are in the base', () async {
      final base = await build('game', game);
      final patch = await build(
        'patch',
        {...game, 'models/oak.glb': 'tree'},
        base: [base],
      );
      expect(patch.manifest.entries.keys.map((id) => '$id'), [
        'models/oak.glb',
      ]);
      final packs = mounted()
        ..start(base)
        ..patch(patch);
      expect(await text(packs, 'models/oak.glb'), 'tree');
    });

    test('builds against a chain and names the top of it', () async {
      final base = await build('game', game);
      final one = await build(
        'one',
        {'textures/wall.ktx2': 'wall v2', 'models/tree.glb': 'tree'},
        base: [base],
      );
      final two = await build(
        'two',
        {
          'textures/wall.ktx2': 'wall v2',
          'models/tree.glb': 'tree',
          'scenes/town.oscene': 'town again',
        },
        base: [base, one],
      );

      expect(one.removed.map((id) => '$id'), ['scenes/town.oscene']);
      expect(two.base, one.hash);
      expect(two.removed, isEmpty);
      expect(two.manifest.entries.keys.map((id) => '$id'), [
        'scenes/town.oscene',
      ]);

      final packs = mounted()
        ..start(base)
        ..patch(one)
        ..patch(two);
      expect(await text(packs, 'scenes/town.oscene'), 'town again');
      expect(await text(packs, 'textures/wall.ktx2'), 'wall v2');
    });

    test(
      'built against the wrong target is refused before it is written',
      () async {
        final macos = await build('game', game, target: 'macos');
        final folder = await bundle('next', game);
        await expectLater(
          packBundle(
            bundlePath: folder,
            outPath: at('next.opack'),
            target: CookTargets.android,
            base: [macos],
          ),
          throwsA(isA<PackMountRefused>()),
        );
        expect(File(at('next.opack')).existsSync(), isFalse);
      },
    );

    test('built against a chain out of order is refused', () async {
      final base = await build('game', game);
      final one = await build('one', {'models/tree.glb': 'oak'}, base: [base]);
      final folder = await bundle('next', game);
      await expectLater(
        packBundle(
          bundlePath: folder,
          outPath: at('next.opack'),
          target: CookTargets.android,
          base: [one, base],
        ),
        throwsA(isA<PackMountRefused>()),
      );
    });
  });

  group('mounting', () {
    test('refuses a patch built against a different pack', () async {
      final base = await build('game', game);
      final patch = await build(
        'patch',
        {...game, 'models/rock.glb': 'rock'},
        base: [base],
      );
      final other = await build('other', {...game, 'models/other.glb': 'x'});

      final packs = mounted()..start(other);
      expect(() => packs.patch(patch), throwsA(isA<PackMountRefused>()));
      expect(() => packs.patch(patch), refused('was built against'));
    });

    test('refuses a pack made for another target', () async {
      final macos = await build('mac', game, target: 'macos');
      final macosMod = await build('modmac', {'a.txt': 'x'}, target: 'macos');
      final android = await build('droid', game);
      final androidPatch = await build(
        'patch',
        {...game, 'a.txt': 'x'},
        base: [android],
      );
      final macosPatch = await build(
        'macpatch',
        {...game, 'a.txt': 'x'},
        base: [macos],
        target: 'macos',
      );

      final packs = mounted();
      expect(() => packs.start(macos), refused('A pack for macos'));
      packs.start(android);
      expect(() => packs.patch(macosPatch), refused('A pack for macos'));
      expect(() => packs.addMod(macosMod), refused('A pack for macos'));
      packs.patch(androidPatch);
    });

    test('keeps start, patch and addMod to their own kind of pack', () async {
      final base = await build('game', game);
      final patch = await build('patch', {...game, 'a.txt': 'x'}, base: [base]);
      final mod = await build('mod', {'a.txt': 'mod'});

      final fresh = mounted();
      expect(() => fresh.start(patch), refused('is a patch'));
      expect(() => fresh.patch(patch), refused('Start the chain'));
      fresh.start(base);
      expect(() => fresh.start(mod), refused('already started'));
      expect(() => fresh.patch(mod), refused('no base'));
      expect(() => fresh.addMod(patch), refused('is a patch'));
    });

    test('refuses the same mod twice', () async {
      final base = await build('game', game);
      final mod = await build('mod', {'a.txt': 'mod'});
      final packs = mounted()
        ..start(base)
        ..addMod(mod);
      expect(() => packs.addMod(mod), refused('already mounted'));
    });

    test('leaves the chain alone when a mount is refused', () async {
      final base = await build('game', game);
      final macos = await build('mac', game, target: 'macos');
      final packs = mounted()..start(base);
      expect(() => packs.addMod(macos), throwsA(isA<PackMountRefused>()));
      expect(packs.chainHash, base.hash);
      expect(packs.manifest.entries.length, 3);
    });
  });

  group('a mod', () {
    test('wins over a patch whichever was mounted first', () async {
      final base = await build('game', game);
      final patch = await build(
        'patch',
        {...game, 'textures/wall.ktx2': 'wall patched'},
        base: [base],
      );
      final mod = await build('mod', {'textures/wall.ktx2': 'wall modded'});

      final patchFirst = mounted()
        ..start(base)
        ..patch(patch)
        ..addMod(mod);
      final modFirst = mounted()
        ..start(base)
        ..addMod(mod)
        ..patch(patch);
      final modBeforeAnything = mounted()
        ..addMod(mod)
        ..start(base)
        ..patch(patch);

      for (final packs in [patchFirst, modFirst, modBeforeAnything]) {
        expect(await text(packs, 'textures/wall.ktx2'), 'wall modded');
        expect(await text(packs, 'models/tree.glb'), 'tree');
      }
    });

    test('mounted later sits above one mounted earlier', () async {
      final base = await build('game', game);
      final first = await build('first', {'textures/wall.ktx2': 'first'});
      final second = await build('second', {'textures/wall.ktx2': 'second'});
      final packs = mounted()
        ..start(base)
        ..addMod(first)
        ..addMod(second);
      expect(await text(packs, 'textures/wall.ktx2'), 'second');
    });

    test('can add back an id a patch removed', () async {
      final base = await build('game', game);
      final patch = await build(
        'patch',
        {'textures/wall.ktx2': 'wall v1', 'models/tree.glb': 'tree'},
        base: [base],
      );
      final mod = await build('mod', {'scenes/town.oscene': 'my town'});
      final packs = mounted()
        ..start(base)
        ..patch(patch);
      expect(await packs.exists(AssetId.parse('scenes/town.oscene')), isFalse);
      packs.addMod(mod);
      expect(await text(packs, 'scenes/town.oscene'), 'my town');
    });
  });

  group('ownership', () {
    test('names the pack that answers for each id', () async {
      final base = await build('game', game);
      final patch = await build(
        'one',
        {'textures/wall.ktx2': 'wall v2', 'models/tree.glb': 'tree'},
        base: [base],
      );
      final mod = await build('mod', {'models/tree.glb': 'oak'});
      final packs = mounted()
        ..start(base)
        ..patch(patch)
        ..addMod(mod);

      expect(packs.ownerOf(AssetId.parse('textures/wall.ktx2')), same(patch));
      expect(packs.ownerOf(AssetId.parse('models/tree.glb')), same(mod));
    });

    test('names the base for an id no patch touched', () async {
      final base = await build('game', game);
      final patch = await build(
        'one',
        {'textures/wall.ktx2': 'wall v2', 'models/tree.glb': 'tree'},
        base: [base],
      );
      final packs = mounted()
        ..start(base)
        ..patch(patch);

      expect(packs.ownerOf(AssetId.parse('models/tree.glb')), same(base));
    });

    test('is null for an id that is removed or never was', () async {
      final base = await build('game', game);
      final patch = await build(
        'one',
        {'textures/wall.ktx2': 'wall v1', 'models/tree.glb': 'tree'},
        base: [base],
      );
      final packs = mounted()
        ..start(base)
        ..patch(patch);

      expect(packs.ownerOf(AssetId.parse('scenes/town.oscene')), isNull);
      expect(packs.ownerOf(AssetId.parse('models/ghost.glb')), isNull);
    });
  });

  group('reading through the chain', () {
    test('says why an asset is missing', () async {
      final empty = mounted();
      await expectLater(
        empty.read(AssetId.parse('a.txt')),
        throwsA(
          isA<AssetNotFound>().having(
            (error) => error.message,
            'message',
            contains('no pack is mounted'),
          ),
        ),
      );
      final base = await build('game', game);
      final packs = mounted()..start(base);
      await expectLater(
        packs.read(AssetId.parse('a.txt')),
        throwsA(
          isA<AssetNotFound>().having(
            (error) => error.message,
            'message',
            contains('none of the 1 mounted packs'),
          ),
        ),
      );
    });

    test('layers under a directory like any other source', () async {
      final base = await build('game', game);
      final layered = LayeredAssetSource([
        MemoryAssetSource({
          AssetId.parse('models/tree.glb'): utf8.encode('dev'),
        }),
        mounted()..start(base),
      ]);
      expect(await text(layered, 'models/tree.glb'), 'dev');
      expect(await text(layered, 'scenes/town.oscene'), 'town');
    });
  });
}
