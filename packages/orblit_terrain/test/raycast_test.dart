import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  test('meets flat ground straight down', () {
    final terrain = Terrain(regionSize: 64)..addRegion(const RegionKey(0, 0));
    final hit = terrain.raycast(Vector3(20, 50, 30), Vector3(0, -1, 0))!;
    expect(hit.x, closeTo(20, 1e-9));
    expect(hit.y, closeTo(0, 1e-6));
    expect(hit.z, closeTo(30, 1e-9));
  });

  test('meets a slope where heightAt says it is, at an angle', () {
    final terrain = Terrain(regionSize: 64)
      ..fillHeights(const RegionKey(0, 0), (x, z) => x * 0.5);
    final hit = terrain.raycast(Vector3(0, 40, 10), Vector3(1, -1, 0.2))!;
    expect(hit.y, closeTo(terrain.heightAt(hit.x, hit.z)!, 1e-6));
    // Along the ray: y = 40 - t, x = t, and the ground is x / 2.
    expect(hit.x, closeTo(80 / 3, 1e-3));
  });

  test('stops at the first ground, not the ground behind it', () {
    final terrain = Terrain(
      regionSize: 64,
    )..fillHeights(const RegionKey(0, 0), (x, z) => x >= 30 && x < 34 ? 20 : 0);
    final hit = terrain.raycast(Vector3(0, 10, 5), Vector3(1, 0, 0))!;
    expect(hit.x, inInclusiveRange(29, 30.5));
  });

  test('misses through a hole, off the edge and into the sky', () {
    final terrain = Terrain(regionSize: 64)..addRegion(const RegionKey(0, 0));
    terrain.regionAt(const RegionKey(0, 0))!
      ..setCover(20, 20, Cover.auto.withHole(true))
      ..setCover(21, 20, Cover.auto.withHole(true))
      ..setCover(20, 21, Cover.auto.withHole(true))
      ..setCover(21, 21, Cover.auto.withHole(true));
    expect(terrain.raycast(Vector3(20.5, 10, 20.5), Vector3(0, -1, 0)), isNull);
    expect(terrain.raycast(Vector3(100, 10, 10), Vector3(0, -1, 0)), isNull);
    expect(terrain.raycast(Vector3(10, 10, 10), Vector3(0, 1, 0)), isNull);
    expect(
      terrain.raycast(Vector3(10, 10, 10), Vector3(0, -1, 0), maxDistance: 5),
      isNull,
    );
  });

  test('meets nothing on a terrain with no regions', () {
    expect(Terrain().raycast(Vector3(0, 10, 0), Vector3(0, -1, 0)), isNull);
  });
}
