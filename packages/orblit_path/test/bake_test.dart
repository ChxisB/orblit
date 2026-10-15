import 'dart:math' as math;

import 'package:orblit_path/orblit_path.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A terrain of one 16 texel region at [key], one metre a texel, its ground
/// from [height] and every texel marked walkable by [mark].
Terrain ground({
  RegionKey key = const RegionKey(0, 0),
  double Function(double x, double z)? height,
  Cover Function(int i, int j)? mark,
}) {
  final terrain = Terrain(regionSize: 16);
  final region = terrain.fillHeights(key, height ?? (x, z) => 0);
  for (var j = 0; j < 16; j++) {
    for (var i = 0; i < 16; i++) {
      region.cover[j * 16 + i] =
          (mark?.call(i, j) ?? Cover.of(navigation: true)).word;
    }
  }
  return terrain;
}

/// A floor of two triangles from (0, 0) to (size, size) at height [y].
({List<double> positions, List<int> indices}) floor(double size, double y) => (
  positions: [0, y, 0, 0, y, size, size, y, size, size, y, 0],
  indices: [0, 1, 3, 1, 2, 3],
);

/// A vertical wall along z = [z] from x 0 to [width], [height] tall.
({List<double> positions, List<int> indices}) wall(
  double z,
  double width,
  double height,
) => (
  positions: [0, 0, z, width, 0, z, width, height, z, 0, height, z],
  indices: [0, 1, 2, 0, 2, 3],
);

MeshWalk meshOf(
  List<({List<double> positions, List<int> indices})> parts, {
  double agentHeight = 1.8,
  double stepHeight = 0.3,
  double maxSlope = math.pi / 4,
}) {
  final positions = <double>[];
  final indices = <int>[];
  for (final part in parts) {
    final base = positions.length ~/ 3;
    positions.addAll(part.positions);
    indices.addAll(part.indices.map((i) => i + base));
  }
  return (
    positions: positions,
    indices: indices,
    agentHeight: agentHeight,
    stepHeight: stepHeight,
    maxSlope: maxSlope,
  );
}

void main() {
  group('a terrain', () {
    test('is open where the cover is marked for walking', () {
      final grid = bakeTerrain(ground());
      expect(grid.width, 16);
      expect(grid.depth, 16);
      expect(grid.isOpen(5, 5), isTrue);
      expect(grid.heightOf(5, 5), closeTo(0, 1e-6));
    });

    test('is closed where nobody marked it', () {
      final terrain = ground(mark: (i, j) => Cover.of(navigation: i < 8));
      final grid = bakeTerrain(terrain);
      expect(grid.isOpen(3, 5), isTrue);
      expect(grid.isOpen(12, 5), isFalse);
    });

    test('can be told to walk anywhere', () {
      final terrain = ground(mark: (i, j) => Cover.plain);
      final grid = bakeTerrain(terrain, walkable: (cover) => true);
      expect(grid.isOpen(5, 5), isTrue);
    });

    test('is closed at a hole', () {
      final terrain = ground(
        mark: (i, j) => Cover.of(navigation: true, hole: i == 7 && j == 7),
      );
      final grid = bakeTerrain(terrain);
      expect(grid.isOpen(7, 7), isFalse);
      expect(grid.isOpen(3, 3), isTrue);
    });

    test('is closed where it is too steep and dearer where it climbs', () {
      final steep = bakeTerrain(ground(height: (x, z) => x * 2));
      expect(steep.isOpen(5, 5), isFalse);
      final gentle = bakeTerrain(ground(height: (x, z) => x * 0.5));
      expect(gentle.isOpen(5, 5), isTrue);
      expect(gentle.weightOf(5, 5), closeTo(math.sqrt(1.25), 1e-3));
      expect(gentle.heightOf(5, 5), closeTo(2.75, 1e-3));
    });

    test('lies where the regions are', () {
      final grid = bakeTerrain(ground(key: const RegionKey(2, -1)));
      expect(grid.originX, 32);
      expect(grid.originZ, -16);
      expect(grid.isOpen(5, 5), isTrue);
    });

    test('takes a smaller area and a larger cell', () {
      final grid = bakeTerrain(
        ground(),
        cell: 2,
        minX: 4,
        minZ: 4,
        maxX: 12,
        maxZ: 12,
      );
      expect(grid.width, 4);
      expect(grid.depth, 4);
      expect(grid.originX, 4);
    });

    test('a terrain with no regions is one closed cell', () {
      final grid = bakeTerrain(Terrain(regionSize: 16));
      expect(grid.width, 1);
      expect(grid.isOpen(0, 0), isFalse);
    });

    test('can be walked across after it is baked', () {
      final terrain = ground(
        mark: (i, j) => Cover.of(navigation: true, hole: i == 8 && j < 12),
      );
      final grid = bakeTerrain(terrain);
      final path = grid.findPath(Vector3(3.5, 0, 3.5), Vector3(12.5, 0, 3.5))!;
      expect(
        path.map((p) => p.z).reduce((a, b) => a > b ? a : b),
        greaterThan(11),
      );
    });
  });

  group('a mesh', () {
    test('is open over a floor, at its height', () {
      final grid = bakeMesh(meshOf([floor(10, 1.5)]), cell: 1);
      expect(grid.width, 10);
      expect(grid.isOpen(4, 4), isTrue);
      expect(grid.heightOf(4, 4), closeTo(1.5, 1e-9));
    });

    test('is closed at a wall and open either side', () {
      final grid = bakeMesh(meshOf([floor(10, 0), wall(5, 10, 3)]), cell: 1);
      expect(grid.isOpen(4, 3), isTrue);
      expect(grid.isOpen(4, 7), isTrue);
      expect(grid.isOpen(4, 5), isFalse);
    });

    test('walks over a kerb', () {
      final grid = bakeMesh(meshOf([floor(10, 0), wall(5, 10, 0.2)]), cell: 1);
      expect(grid.isOpen(4, 5), isTrue);
    });

    test('is closed under a low ceiling and open under a high one', () {
      // Facing down: wound the other way round to a floor.
      final low = (
        positions: <double>[0, 1.2, 0, 4, 1.2, 0, 4, 1.2, 10, 0, 1.2, 10],
        indices: <int>[0, 1, 2, 0, 2, 3],
      );
      final high = (
        positions: <double>[6, 2.5, 0, 10, 2.5, 0, 10, 2.5, 10, 6, 2.5, 10],
        indices: <int>[0, 1, 2, 0, 2, 3],
      );
      final grid = bakeMesh(meshOf([floor(10, 0), low, high]), cell: 1);
      expect(grid.isOpen(2, 5), isFalse);
      expect(grid.isOpen(8, 5), isTrue);
    });

    test('is closed beside a ramp that is too steep to climb', () {
      final ramp = (
        positions: <double>[0, 0, 0, 0, 0, 10, 4, 8, 10, 4, 8, 0],
        indices: <int>[0, 1, 2, 0, 2, 3],
      );
      final grid = bakeMesh(meshOf([floor(10, 0), ramp]), cell: 1);
      expect(grid.isOpen(0, 5), isFalse, reason: 'under it, head height');
      expect(grid.isOpen(7, 5), isTrue);
    });

    test('is open on a ramp that is gentle, at the height of it', () {
      final ramp = (
        positions: <double>[0, 0, 0, 0, 0, 10, 10, 2, 10, 10, 2, 0],
        indices: <int>[0, 1, 2, 0, 2, 3],
      );
      final grid = bakeMesh(meshOf([ramp]), cell: 1);
      expect(grid.isOpen(5, 5), isTrue);
      expect(grid.heightOf(5, 5), closeTo(1.1, 1e-6));
    });

    test('carves a wall out of a terrain grid', () {
      final grid = bakeTerrain(ground());
      carve(grid, meshOf([wall(8, 16, 3)]));
      expect(grid.isOpen(3, 8), isFalse);
      expect(grid.isOpen(3, 3), isTrue);
    });

    test('leaves a floor that is only seen from above', () {
      final grid = bakeTerrain(ground());
      carve(grid, meshOf([floor(16, 0)]));
      expect(grid.isOpen(3, 3), isTrue);
    });
  });
}
