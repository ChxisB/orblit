import 'dart:math' as math;

import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  Terrain small({double spacing = 1}) =>
      Terrain(regionSize: 4, spacing: spacing);

  group('heightAt', () {
    test('is the texel height on a texel', () {
      final terrain = small()..addRegion(const RegionKey(0, 0));
      terrain.regionAt(const RegionKey(0, 0))!.setHeight(2, 1, 3.5);
      expect(terrain.heightAt(2, 1), 3.5);
      expect(terrain.heightAt(1, 1), 0);
    });

    test('follows the two triangles, split low corner to high', () {
      final terrain = small();
      final region = terrain.addRegion(const RegionKey(0, 0))
        ..setHeight(1, 1, 1);
      // Only the high corner is raised. Bilinear would give 0.125 at both;
      // on the triangles each point is a quarter of the way up.
      expect(terrain.heightAt(0.5, 0.25), closeTo(0.25, 1e-9));
      expect(terrain.heightAt(0.25, 0.5), closeTo(0.25, 1e-9));
      region
        ..setHeight(1, 1, 0)
        ..setHeight(1, 0, 1);
      // Only the +x corner is raised: the triangle that holds it slopes,
      // the other is flat.
      expect(terrain.heightAt(0.75, 0.25), closeTo(0.5, 1e-9));
      expect(terrain.heightAt(0.25, 0.75), closeTo(0, 1e-9));
    });

    test('reads across a region edge', () {
      final terrain = small()
        ..fillHeights(const RegionKey(0, 0), (x, z) => x)
        ..fillHeights(const RegionKey(1, 0), (x, z) => x);
      expect(terrain.heightAt(3.5, 2), closeTo(3.5, 1e-9));
      expect(terrain.heightAt(4, 2), closeTo(4, 1e-9));
    });

    test('works below zero', () {
      final terrain = small()
        ..fillHeights(const RegionKey(-1, -1), (x, z) => x + 2 * z);
      expect(terrain.heightAt(-1.5, -2.25), closeTo(-6, 1e-9));
    });

    test('scales with spacing', () {
      final terrain = small(spacing: 2)
        ..fillHeights(const RegionKey(0, 0), (x, z) => x * 10);
      expect(terrain.heightAt(2, 0), closeTo(20, 1e-9));
      expect(terrain.heightAt(3, 0), closeTo(30, 1e-9));
    });

    test('is null where there is no region', () {
      final terrain = small()..addRegion(const RegionKey(0, 0));
      expect(terrain.heightAt(1, 1), 0);
      expect(terrain.heightAt(-0.5, 1), isNull);
      expect(terrain.heightAt(3.5, 1), isNull, reason: 'the +x corner is gone');
      expect(terrain.heightAt(double.nan, 0), isNull);
    });

    test('is null on a triangle touching a hole, not its neighbour', () {
      final terrain = small();
      terrain
          .addRegion(const RegionKey(0, 0))
          .setCover(1, 0, Cover.plain.withHole(true));
      expect(terrain.heightAt(0.75, 0.25), isNull);
      expect(terrain.heightAt(0.25, 0.75), 0);
    });
  });

  group('normalAt', () {
    test('points up on level ground', () {
      final terrain = small()..addRegion(const RegionKey(0, 0));
      final normal = terrain.normalAt(1.3, 2.6)!;
      expect(normal.x, closeTo(0, 1e-9));
      expect(normal.y, closeTo(1, 1e-9));
      expect(normal.z, closeTo(0, 1e-9));
    });

    test('leans away from the rise on a plane', () {
      final terrain = Terrain(regionSize: 8, spacing: 2)
        ..fillHeights(const RegionKey(0, 0), (x, z) => 0.5 * x - 0.25 * z);
      final normal = terrain.normalAt(5.2, 7.9)!;
      final expected = Vector3(-0.5, 1, 0.25)..normalize();
      expect(normal.x, closeTo(expected.x, 1e-6));
      expect(normal.y, closeTo(expected.y, 1e-6));
      expect(normal.z, closeTo(expected.z, 1e-6));
    });

    test('ignores holes and is null where there is nothing', () {
      final terrain = small();
      terrain
          .addRegion(const RegionKey(0, 0))
          .setCover(1, 1, Cover.plain.withHole(true));
      expect(terrain.normalAt(1, 1), isNotNull);
      expect(terrain.normalAt(-3, -3), isNull);
    });
  });

  group('cover', () {
    test('coverAt reads the nearest texel as stored', () {
      final terrain = small();
      terrain.addRegion(const RegionKey(0, 0)).setCover(2, 1, Cover.plain);
      expect(terrain.coverAt(1.6, 0.6), Cover.plain);
      expect(terrain.coverAt(0.2, 0.2), Cover.auto);
      expect(terrain.coverAt(-5, 0), isNull);
    });

    test('automatic ground turns steep with slope and height', () {
      const auto = AutoCover();
      expect(auto.flatness(1, 0), 1);
      expect(auto.flatness(0.5, 0), 0);
      expect(auto.flatness(1, 1000), 0);
      expect(auto.flatness(math.cos(math.pi / 6), 0), closeTo(0.732, 1e-3));
      final cover = auto.coverFor(0.75, 0);
      expect((cover.base, cover.overlay), (0, 1));
      expect(cover.blend, closeTo(0.5, 1 / 255));
    });
  });

  group('settings files', () {
    Terrain sample() {
      final terrain = Terrain(
        regionSize: 64,
        spacing: 0.5,
        sets: [
          const TerrainSet(
            name: 'grass',
            albedo: 'terrain/grass_albedo.png',
            normal: 'terrain/grass_normal.png',
          ),
          const TerrainSet(name: 'rock', tileSize: 8, triplanar: true),
        ],
        autoCover: const AutoCover(steep: 1, flat: 0, slope: 2),
        blendSharpness: 0.5,
      );
      terrain
        ..addRegion(const RegionKey(1, 0))
        ..addRegion(const RegionKey(-1, 0))
        ..addRegion(const RegionKey(0, -1));
      return terrain;
    }

    test('come back as they went, regions as keys', () {
      final load = Terrain.decode(sample().encode());
      expect(load.problems, isEmpty);
      final terrain = load.terrain;
      expect(terrain.regionSize, 64);
      expect(terrain.spacing, 0.5);
      expect(terrain.blendSharpness, 0.5);
      expect(terrain.sets.map((set) => set.name), ['grass', 'rock']);
      expect(terrain.sets[0].albedo, 'terrain/grass_albedo.png');
      expect(terrain.sets[1].triplanar, isTrue);
      expect(terrain.sets[1].tileSize, 8);
      expect(terrain.autoCover.steep, 1);
      expect(terrain.autoCover.slope, 2);
      expect(terrain.regions, isEmpty);
      expect(load.regions, [
        const RegionKey(0, -1),
        const RegionKey(-1, 0),
        const RegionKey(1, 0),
      ]);
    });

    test('put a key to a line and a set to a line', () {
      final lines = sample().encode().split('\n');
      expect(lines.first, '{');
      expect(lines, contains('  "kind": "orblit.terrain",'));
      expect(lines, contains('  "regionSize": 64,'));
      expect(
        lines.where((line) => line.startsWith('    {"name":')),
        hasLength(2),
      );
    });

    test('refuse what is not a terrain, or one from a newer Orblit', () {
      expect(
        () => Terrain.decode('nope'),
        throwsA(isA<TerrainFormatException>()),
      );
      expect(
        () => Terrain.decode(
          '{"kind":"orblit.terrain","formatVersion":2,'
          '"regionSize":64}',
        ),
        throwsA(isA<TerrainFormatException>()),
      );
      expect(
        () => Terrain.decode('{"kind":"orblit.terrain","regionSize":100}'),
        throwsA(isA<TerrainFormatException>()),
      );
    });

    test('drop what they cannot read, with a note', () {
      final load = Terrain.decode(
        '{"kind":"orblit.terrain","formatVersion":1,"regionSize":64,'
        '"spacing":-1,"sets":[{"tileSize":2},{"name":"mud","tileSize":0}],'
        '"regions":[[0,0],[0,0],["a"]]}',
      );
      expect(load.terrain.spacing, 1);
      expect(load.terrain.sets.single.name, 'mud');
      expect(load.terrain.sets.single.tileSize, 4);
      expect(load.regions, [const RegionKey(0, 0)]);
      expect(load.problems, hasLength(5));
    });
  });

  test('keeps its own list of sets, which can be changed', () {
    const given = [TerrainSet(name: 'grass')];
    final terrain = Terrain(regionSize: 16, sets: given)
      ..sets.add(const TerrainSet(name: 'rock'));
    terrain.sets[0] = const TerrainSet(name: 'moss');
    expect(terrain.sets.map((set) => set.name), ['moss', 'rock']);
    expect(given.single.name, 'grass');
  });

  test('a region of the wrong size is refused', () {
    expect(
      () => small().putRegion(TerrainRegion(const RegionKey(0, 0), 8)),
      throwsArgumentError,
    );
  });
}
