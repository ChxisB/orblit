@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

/// A region of [size] texels whose heights are `height + texel index`, so
/// two regions differ and a texel read back says which file it came from.
Uint8List regionFile(int x, int z, {int size = 4, double height = 1}) {
  final region = TerrainRegion(RegionKey(x, z), size);
  for (var i = 0; i < region.heights.length; i++) {
    region.heights[i] = height + i;
  }
  return region.encode();
}

/// A terrain's settings file listing [regions], 4 texels to a region.
Uint8List terrainFile({
  List<(int, int)> regions = const [],
  List<TerrainSet> sets = const [],
  List<ScatterLayer> scatter = const [],
  int regionSize = 4,
}) {
  final terrain = Terrain(regionSize: regionSize, sets: sets, scatter: scatter);
  for (final (x, z) in regions) {
    terrain.addRegion(RegionKey(x, z));
  }
  return utf8.encode(terrain.encode());
}

void main() {
  const importer = TerrainImporter();

  ImportRequest requestFor(
    String asset,
    List<int> bytes, [
    Map<AssetId, List<int>> others = const {},
  ]) => ImportRequest(
    id: id(asset),
    bytes: Uint8List.fromList(bytes),
    settings: const {},
    target: CookTarget.any,
    source: MemoryAssetSource(others),
  );

  Matcher failsWith(String text) => throwsA(
    isA<ImportFailure>().having((f) => f.reason, 'reason', contains(text)),
  );

  group('a region', () {
    test('is carried as written', () async {
      final bytes = regionFile(2, -1);
      final result = await importer.import(
        requestFor('terrain/x2_z-1.oregion', bytes),
      );
      expect(result.outputs.keys, ['oregion']);
      expect(result.outputs['oregion'], bytes);
      expect(result.notes, isEmpty);
    });

    test('has no dependencies', () async {
      expect(
        await importer.dependenciesOf(
          id('terrain/x0_z0.oregion'),
          regionFile(0, 0),
          const {},
        ),
        isEmpty,
      );
    });

    test('named for another place is a failure', () {
      expect(
        importer.import(requestFor('terrain/x1_z0.oregion', regionFile(0, 0))),
        failsWith('named for region 1, 0 and holds region 0, 0'),
      );
    });

    test('not named for its place is cooked with a note', () async {
      final result = await importer.import(
        requestFor('terrain/hills.oregion', regionFile(3, 4)),
      );
      expect(result.outputs.keys, ['oregion']);
      expect(result.notes.single, contains('x3_z4.oregion'));
    });

    test('that is not a region is a failure that says so', () {
      expect(
        importer.import(
          requestFor('terrain/x0_z0.oregion', utf8.encode('hello')),
        ),
        failsWith('not a region file'),
      );
    });

    test('from a newer Orblit is a failure', () {
      final text = utf8.encode(
        '{"kind":"orblit.region","formatVersion":99,"x":0,"z":0,"size":4}\n',
      );
      expect(
        importer.import(requestFor('terrain/x0_z0.oregion', text)),
        failsWith('newer Orblit'),
      );
    });

    test('with a map it does not know is cooked with a note', () async {
      final header = utf8.encode(
        '{"kind":"orblit.region","formatVersion":1,"x":0,"z":0,"size":4,'
        '"maps":[{"name":"wetness","bytes":4}]}\n',
      );
      // The maps start on a four byte boundary after the header line.
      final start = (header.length + 3) & ~3;
      final bytes = Uint8List(start + 4)..setAll(0, header);
      final result = await importer.import(
        requestFor('terrain/x0_z0.oregion', bytes),
      );
      expect(result.notes.single, contains('wetness'));
    });
  });

  group('a terrain', () {
    final world = id('terrain/world.oterrain');

    test('depends on the regions it lists', () async {
      final deps = await importer.dependenciesOf(
        world,
        terrainFile(regions: [(0, 0), (-1, 2)]),
        const {},
      );
      expect(deps, {id('terrain/x0_z0.oregion'), id('terrain/x-1_z2.oregion')});
    });

    test('depends on the images, models and materials it names', () async {
      final deps = await importer.dependenciesOf(
        world,
        terrainFile(
          sets: const [
            TerrainSet(
              name: 'grass',
              albedo: 'textures/grass.png',
              normal: 'textures/grass_n.png',
            ),
            TerrainSet(name: 'bare'),
          ],
          scatter: const [
            ScatterLayer(
              name: 'pines',
              mesh: 'models/pine.glb',
              material: 'materials/pine.omat',
            ),
          ],
        ),
        const {},
      );
      expect(deps, {
        id('textures/grass.png'),
        id('textures/grass_n.png'),
        id('models/pine.glb'),
        id('materials/pine.omat'),
      });
    });

    test('at the project root keeps its regions at the root', () async {
      final deps = await importer.dependenciesOf(
        id('world.oterrain'),
        terrainFile(regions: [(0, 0)]),
        const {},
      );
      expect(deps, {id('x0_z0.oregion')});
    });

    test('naming a path that is not one is a failure', () {
      expect(
        importer.dependenciesOf(
          world,
          terrainFile(
            sets: const [TerrainSet(name: 'grass', albedo: '/abs/grass.png')],
          ),
          const {},
        ),
        failsWith('"/abs/grass.png", which is not a path within the project'),
      );
    });

    test('that is not a terrain is a failure that says so', () {
      expect(
        importer.dependenciesOf(world, utf8.encode('[]'), const {}),
        failsWith('not a terrain file'),
      );
      expect(
        importer.dependenciesOf(world, Uint8List.fromList([0xff]), const {}),
        failsWith('not text'),
      );
    });

    test('is carried as written, with a count in its notes', () async {
      final bytes = terrainFile(regions: [(0, 0), (1, 0)]);
      final result = await importer.import(
        requestFor('terrain/world.oterrain', bytes, {
          id('terrain/x0_z0.oregion'): regionFile(0, 0),
          id('terrain/x1_z0.oregion'): regionFile(1, 0),
        }),
      );
      expect(result.outputs.keys, ['oterrain']);
      expect(result.outputs['oterrain'], bytes);
      expect(result.notes.first, '2 regions of 4 texels, 1.0 m apart');
    });

    test('with one region says region and not regions', () async {
      final result = await importer.import(
        requestFor('terrain/world.oterrain', terrainFile(regions: [(0, 0)]), {
          id('terrain/x0_z0.oregion'): regionFile(0, 0),
        }),
      );
      expect(result.notes.first, startsWith('1 region of'));
    });

    test('listing a region of another size is a failure', () {
      expect(
        importer.import(
          requestFor('terrain/world.oterrain', terrainFile(regions: [(0, 0)]), {
            id('terrain/x0_z0.oregion'): regionFile(0, 0, size: 8),
          }),
        ),
        failsWith('x0_z0.oregion is 8 texels across and this terrain\'s'),
      );
    });

    test('listing a region whose file holds another is a failure', () {
      expect(
        importer.import(
          requestFor('terrain/world.oterrain', terrainFile(regions: [(1, 0)]), {
            id('terrain/x1_z0.oregion'): regionFile(0, 0),
          }),
        ),
        failsWith('lists region 1, 0, and x1_z0.oregion holds region 0, 0'),
      );
    });

    test('listing a region that cannot be read names the file', () {
      expect(
        importer.import(
          requestFor('terrain/world.oterrain', terrainFile(regions: [(0, 0)]), {
            id('terrain/x0_z0.oregion'): utf8.encode('nope'),
          }),
        ),
        failsWith('not a region file'),
      );
    });
  });

  group('cooked', () {
    late MemoryAssetSource source;
    late MemoryCookCache cache;

    Cook cook() => Cook(
      source: source,
      cache: cache,
      importers: ImporterRegistry([importer]),
      target: CookTarget.any,
    );

    void project({List<int>? region, bool texture = true}) {
      source = MemoryAssetSource({
        id('terrain/world.oterrain'): terrainFile(
          regions: [(0, 0)],
          sets: const [TerrainSet(name: 'grass', albedo: 'textures/grass.png')],
        ),
        id('terrain/x0_z0.oregion'): region ?? regionFile(0, 0),
        if (texture) id('textures/grass.png'): [1, 2, 3],
      });
    }

    setUp(() {
      cache = MemoryCookCache();
      project();
    });

    test('a terrain is cooked, then served from the cache', () async {
      final first = await cook().cookOne(id('terrain/world.oterrain'));
      expect(first.status, CookStatus.cooked);
      final second = await cook().cookOne(id('terrain/world.oterrain'));
      expect(second.status, CookStatus.cached);
    });

    test('editing a region cooks the terrain again', () async {
      await cook().cookOne(id('terrain/world.oterrain'));
      project(region: regionFile(0, 0, height: 50));
      final again = await cook().cookOne(id('terrain/world.oterrain'));
      expect(again.status, CookStatus.cooked);
    });

    test('a missing region fails the terrain', () async {
      source = MemoryAssetSource({
        id('terrain/world.oterrain'): terrainFile(regions: [(0, 0)]),
      });
      final result = await cook().cookOne(id('terrain/world.oterrain'));
      expect(result.status, CookStatus.failed);
      expect('${result.error}', contains('x0_z0.oregion'));
    });

    test('a missing image fails the terrain', () async {
      project(texture: false);
      final result = await cook().cookOne(id('terrain/world.oterrain'));
      expect(result.status, CookStatus.failed);
      expect('${result.error}', contains('textures/grass.png'));
    });
  });

  group('the default importers', () {
    test('a project cook claims both files', () {
      final registry = CookProject.defaultImporters();
      expect(registry.byName('terrain'), isA<TerrainImporter>());
      for (final file in ['a.oterrain', 'x0_z0.oregion']) {
        expect(registry.forAsset(id(file), const ImportSettings()), isNotNull);
      }
    });

    test('a running app claims them too', () {
      final registry = RuntimeImport.defaultImporters();
      expect(registry.byName('terrain'), isA<TerrainImporter>());
    });
  });

  group('a cooked terrain', () {
    late Directory work;

    String at(String relative) =>
        [work.path, ...relative.split('/')].join(Platform.pathSeparator);

    void write(String relative, List<int> bytes) {
      final file = File(at('assets/$relative'));
      file.parent.createSync(recursive: true);
      file.writeAsBytesSync(bytes);
    }

    Future<MountedPacks> cookAndMount() async {
      final target = CookTargets.find('android')!;
      final project = CookProject(
        assetsPath: at('assets'),
        outPath: at('out'),
        target: target,
        cachePath: at('cache'),
        importers: ImporterRegistry([importer]),
      );
      final report = await project.run();
      expect(report.ok, isTrue, reason: '${report.results}');
      await packBundle(
        bundlePath: project.bundlePath,
        outPath: at('game.opack'),
        target: target,
      );
      final bytes = await File(at('game.opack')).readAsBytes();
      return MountedPacks(target: target)
        ..start(await AssetPack.open(MemoryPackBytes(bytes)));
    }

    setUp(() {
      work = Directory.systemTemp.createTempSync('orblit_terrain_cook_test');
      write('terrain/world.oterrain', terrainFile(regions: [(0, 0), (1, 0)]));
      write('terrain/x0_z0.oregion', regionFile(0, 0, height: 10));
      write('terrain/x1_z0.oregion', regionFile(1, 0, height: 100));
    });

    tearDown(() => work.deleteSync(recursive: true));

    test('opens from a pack with its settings and its regions', () async {
      final packs = await cookAndMount();
      final cooked = await CookedTerrain.open(
        packs,
        id('terrain/world.oterrain'),
      );
      expect(cooked.terrain.regionSize, 4);
      expect(cooked.terrain.regions, isEmpty);
      expect(cooked.regions.keys, [
        const RegionKey(0, 0),
        const RegionKey(1, 0),
      ]);
      expect(cooked.problems, isEmpty);
    });

    test('streams the heights that were authored', () async {
      final packs = await cookAndMount();
      final cooked = await CookedTerrain.open(
        packs,
        id('terrain/world.oterrain'),
      );
      final streamer = TerrainStreamer(
        terrain: cooked.terrain,
        source: cooked.regions,
        loadRadius: 1,
        keepRadius: 1,
      );
      await streamer.follow(0.5, 0.5);
      expect(streamer.resident, {const RegionKey(0, 0)});
      expect(cooked.terrain.heightAt(0, 0), 10);

      await streamer.follow(6.5, 0.5);
      expect(streamer.resident, {const RegionKey(1, 0)});
      expect(cooked.terrain.heightAt(4, 0), 100);
      expect(streamer.failures, isEmpty);
    });

    test('names a listed region the pack does not hold', () async {
      final packs = MountedPacks(target: CookTargets.find('android')!);
      final bundle = Directory(at('hand'))..createSync();
      final terrain = terrainFile(regions: [(0, 0), (1, 0)]);
      final region = regionFile(0, 0);
      File('${bundle.path}/world.oterrain.oterrain').writeAsBytesSync(terrain);
      File('${bundle.path}/x0_z0.oregion.oregion').writeAsBytesSync(region);
      File('${bundle.path}/manifest.json').writeAsStringSync(
        AssetManifest(
          entries: {
            id('world.oterrain.oterrain'): AssetEntry(
              ContentHash.of(terrain),
              terrain.length,
            ),
            id('x0_z0.oregion.oregion'): AssetEntry(
              ContentHash.of(region),
              region.length,
            ),
          },
        ).encode(),
      );
      await packBundle(
        bundlePath: bundle.path,
        outPath: at('hand.opack'),
        target: CookTargets.find('android')!,
      );
      packs.start(
        await AssetPack.open(
          MemoryPackBytes(await File(at('hand.opack')).readAsBytes()),
        ),
      );

      final cooked = await CookedTerrain.open(packs, id('world.oterrain'));
      expect(cooked.regions.keys, [const RegionKey(0, 0)]);
      expect(cooked.problems.single, contains('x1_z0.oregion'));
    });

    test('is not found when the pack has no such terrain', () async {
      final packs = await cookAndMount();
      expect(
        CookedTerrain.open(packs, id('terrain/other.oterrain')),
        throwsA(isA<AssetNotFound>()),
      );
    });
  });
}
