import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_terrain/orblit_terrain.dart';

void main() {
  Terrain ground() => Terrain(
    regionSize: 16,
    spacing: 0.5,
    sets: const [
      TerrainSet(name: 'grass', albedo: 'grass.png', normal: 'grass_n.png'),
      TerrainSet(
        name: 'rock',
        albedo: 'missing.png',
        tileSize: 9,
        triplanar: true,
      ),
    ],
    autoCover: const AutoCover(steep: 1, flat: 0, slope: 2, heightFalloff: 0.3),
    blendSharpness: 0.5,
  );

  test('carries the regions as they are, and the settings', () {
    final terrain = ground();
    final region = terrain.addRegion(const RegionKey(-2, 3));
    final drawn = terrainFrom(terrain, key: 9);

    expect(drawn.key, 9);
    expect(drawn.regionSize, 16);
    expect(drawn.spacing, 0.5);
    expect(drawn.blendSharpness, 0.5);
    expect(
      [
        drawn.autoSteep,
        drawn.autoFlat,
        drawn.autoSlope,
        drawn.autoHeightFalloff,
      ],
      [1, 0, 2, 0.3],
    );
    final only = drawn.regions.single;
    expect([only.x, only.z, only.revision], [-2, 3, region.revision]);
    expect(identical(only.heights, region.heights), isTrue);
    expect(identical(only.cover, region.cover), isTrue);
    expect(identical(only.colour, region.colour), isTrue);
  });

  test('an edit moves the revision, so the region travels again', () {
    final terrain = ground();
    final region = terrain.addRegion(const RegionKey(0, 0));
    final before = terrainFrom(terrain, key: 1).regions.single.revision;
    region.setHeight(3, 4, 12);
    expect(terrainFrom(terrain, key: 1).regions.single.revision, isNot(before));
  });

  test('images come from the caller by path, and a missing one is plain', () {
    final grass = Uint8List(2 * 2 * 4)..fillRange(0, 16, 7);
    final normal = Uint8List(2 * 2 * 4)..fillRange(0, 16, 9);
    final asked = <String>[];
    final drawn = terrainFrom(
      ground(),
      key: 1,
      pixels: (path) {
        asked.add(path);
        return {'grass.png': grass, 'grass_n.png': normal}[path];
      },
      picturesRevision: 4,
    );

    expect(asked, ['grass.png', 'grass_n.png', 'missing.png']);
    expect(drawn.picturesRevision, 4);
    expect(drawn.textureSize, 2);
    expect(identical(drawn.sets[0].albedo, grass), isTrue);
    expect(identical(drawn.sets[0].normal, normal), isTrue);
    expect(drawn.sets[1].albedo, isNull);
    expect([drawn.sets[1].tileSize, drawn.sets[1].triplanar], [9, true]);
  });

  test('with no images at all, every set is plain', () {
    final drawn = terrainFrom(ground(), key: 1);
    expect(
      drawn.sets.every((set) => set.albedo == null && set.normal == null),
      isTrue,
    );
    expect(drawn.textureSize, 1);
  });

  test('ground the renderer cannot draw says so', () {
    expect(
      () => terrainFrom(Terrain(regionSize: 8), key: 1),
      throwsArgumentError,
    );
  });

  test('what it adds of its own starts where the renderer does', () {
    final drawn = terrainFrom(Terrain(regionSize: 16), key: 1);
    final plain = OrblitTerrain(key: 1, regionSize: 16);
    expect(
      [drawn.meshSize, drawn.levels, drawn.castShadows, drawn.receiveShadows],
      [plain.meshSize, plain.levels, plain.castShadows, plain.receiveShadows],
    );
  });
}
