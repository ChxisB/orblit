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

  /// What a game holds. A patch is built from the whole of what the game
  /// should hold after it, so each one here names every asset.
  Map<String, String> state({String wall = 'wall v1', String tree = 'tree'}) =>
      {
        'textures/wall.ktx2': wall,
        'models/tree.glb': tree,
        'scenes/town.oscene': 'town',
      };

  final game = state();

  Future<String> text(AssetSource source, String id) async =>
      utf8.decode(await source.read(AssetId.parse(id)));

  void mount(MountedPacks packs, PatchChain chain) {
    chain.ordered.forEach(packs.patch);
    chain.standalone.forEach(packs.addMod);
  }

  test('puts patches in the order each names the one before', () async {
    final shipped = await build('game', game);
    final one = await build('one', state(wall: 'wall v2'), base: [shipped]);
    final two = await build(
      'two',
      state(wall: 'wall v3'),
      base: [shipped, one],
    );
    final three = await build(
      'three',
      state(wall: 'wall v4', tree: 'tree v2'),
      base: [shipped, one, two],
    );

    for (final listing in [
      [one, two, three],
      [three, two, one],
      [two, three, one],
      [three, one, two],
    ]) {
      final chain = PatchChain.of(shipped, listing);

      expect(chain.ordered.map((p) => p.hash), [
        one.hash,
        two.hash,
        three.hash,
      ]);
      expect(chain.skipped, isEmpty);
      expect(chain.standalone, isEmpty);
    }
  });

  test('mounts three patches in chain order and reads the last', () async {
    final shipped = await build('game', game);
    final one = await build('one', state(wall: 'wall v2'), base: [shipped]);
    final two = await build(
      'two',
      state(wall: 'wall v2', tree: 'tree v2'),
      base: [shipped, one],
    );
    final three = await build(
      'three',
      state(wall: 'wall v4', tree: 'tree v2'),
      base: [shipped, one, two],
    );
    final packs = MountedPacks(target: CookTargets.find('android')!)
      ..start(shipped);

    mount(packs, PatchChain.of(shipped, [three, one, two]));

    expect(await text(packs, 'textures/wall.ktx2'), 'wall v4');
    expect(await text(packs, 'models/tree.glb'), 'tree v2');
    expect(await text(packs, 'scenes/town.oscene'), 'town');
  });

  test('skips a patch whose base is not here, and says which', () async {
    final shipped = await build('game', game);
    final one = await build('one', state(wall: 'wall v2'), base: [shipped]);
    final two = await build(
      'two',
      state(wall: 'wall v2', tree: 'tree v2'),
      base: [shipped, one],
    );
    final three = await build(
      'three',
      state(wall: 'wall v4', tree: 'tree v2'),
      base: [shipped, one, two],
    );

    final chain = PatchChain.of(shipped, [three, one]);

    expect(chain.ordered.map((p) => p.hash), [one.hash]);
    expect(chain.skipped.map((s) => s.pack.hash), [three.hash]);
    expect(
      chain.skipped.single.reason,
      contains(two.hash.hex.substring(0, 12)),
    );
    expect(
      chain.skipped.single.reason,
      contains('shipped pack is ${shipped.hash.hex.substring(0, 12)}'),
    );
  });

  test('skips a patch built on the shipped pack of an older version', () async {
    final older = await build('older', game);
    final patch = await build('patch', state(wall: 'wall v2'), base: [older]);
    final shipped = await build('game', {...game, 'models/rock.glb': 'rock'});

    final chain = PatchChain.of(shipped, [patch]);

    expect(chain.ordered, isEmpty);
    expect(chain.skipped.single.pack.hash, patch.hash);
  });

  test('stops at a fork and skips both sides of it', () async {
    final shipped = await build('game', game);
    final left = await build('left', state(wall: 'left'), base: [shipped]);
    final right = await build('right', state(wall: 'right'), base: [shipped]);
    final after = await build(
      'after',
      state(wall: 'wall v2', tree: 'tree v2'),
      base: [shipped, left],
    );

    final chain = PatchChain.of(shipped, [left, right, after]);

    expect(chain.ordered, isEmpty);
    expect(chain.skipped.map((s) => s.pack.hash).toSet(), {
      left.hash,
      right.hash,
      after.hash,
    });
    final forked = chain.skipped.firstWhere((s) => s.pack.hash == left.hash);
    expect(forked.reason, contains('2 patches were built on'));
    final stranded = chain.skipped.firstWhere((s) => s.pack.hash == after.hash);
    expect(stranded.reason, contains('not in the chain'));
  });

  test('keeps a patch that comes before a fork lower down', () async {
    final shipped = await build('game', game);
    final one = await build('one', state(wall: 'wall v2'), base: [shipped]);
    final left = await build(
      'left',
      state(wall: 'wall v2', tree: 'left'),
      base: [shipped, one],
    );
    final right = await build(
      'right',
      state(wall: 'wall v2', tree: 'right'),
      base: [shipped, one],
    );

    final chain = PatchChain.of(shipped, [left, one, right]);

    expect(chain.ordered.map((p) => p.hash), [one.hash]);
    expect(chain.skipped, hasLength(2));
  });

  test('skips a pack for another target', () async {
    final shipped = await build('game', game);
    final other = await build('web', game, target: 'web');

    final chain = PatchChain.of(shipped, [other]);

    expect(chain.standalone, isEmpty);
    expect(chain.skipped.single.reason, contains('for web'));
    expect(chain.skipped.single.reason, contains('for android'));
  });

  test(
    'offers packs with no base as mods and leaves the shipped one',
    () async {
      final shipped = await build('game', game);
      final mod = await build('mod', state(wall: 'mod wall'));

      final chain = PatchChain.of(shipped, [shipped, mod, mod]);

      expect(chain.standalone.map((p) => p.hash), [mod.hash]);
      expect(chain.ordered, isEmpty);
      expect(chain.skipped, isEmpty);
    },
  );
}
