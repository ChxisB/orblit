import 'dart:math' as math;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/src/examples/terrain.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  final camera = OrblitCamera(
    position: Vector3(0, 40, 120),
    target: Vector3.zero(),
  );

  test('is ground the renderer takes, with both sets painted', () {
    final drawn = TerrainExample().scene(camera, 0).terrain.single;
    expect(drawn.regions, hasLength(16));
    expect(drawn.regionSize, 64);
    expect(drawn.sets, hasLength(2));
    expect(
      drawn.sets.every((set) => set.albedo != null && set.normal != null),
      isTrue,
    );
    expect(drawn.textureSize, 128);
  });

  test('has hills, and ends in a plain', () {
    final terrain = TerrainExample().terrain;
    final middle = [
      for (var x = -120.0; x <= 120; x += 8)
        for (var z = -120.0; z <= 120; z += 8) terrain.heightAt(x, z)!,
    ];
    expect(middle.reduce((a, b) => a > b ? a : b), greaterThan(20));
    final edge = TerrainExample.reach - 2;
    expect(terrain.heightAt(-edge, -edge), closeTo(0, 1e-9));
  });

  test('sends its regions once, however the settings move', () {
    final example = TerrainExample();
    List<int> revisions(double seconds) => [
      for (final region
          in example.scene(camera, seconds).terrain.single.regions)
        region.revision,
    ];
    final first = revisions(0);
    example
      ..steepness = 3
      ..altitude = 1.5
      ..sharpness = 0.2
      ..rings = 3
      ..triplanar = false;
    final drawn = example.scene(camera, 1).terrain.single;
    expect(revisions(2), first);
    expect(drawn.autoSlope, 3);
    expect(drawn.levels, 3);
    expect(drawn.sets.first.triplanar, isFalse);
    expect(drawn.picturesRevision, 0);
  });

  test('scatters grass, stones and trees, and places them once', () {
    final example = TerrainExample();
    final first = example.scene(camera, 0).populations;
    final placer = example.placer;
    expect(placer.layers.map((l) => l.name), [
      'grass',
      'stones',
      'trunks',
      'crowns',
    ]);
    int counted(String name) {
      final layer = placer.layers.indexWhere((l) => l.name == name);
      return placer.groups
          .where((g) => g.layer == layer)
          .fold(0, (total, g) => total + g.count);
    }

    expect(counted('grass'), greaterThan(10000));
    expect(counted('stones'), greaterThan(100));
    expect(counted('trunks'), greaterThan(50));
    // One seed: every trunk has its crown.
    expect(counted('crowns'), counted('trunks'));
    expect(first.map((p) => p.key).toSet(), hasLength(first.length));

    // Nothing moved, so nothing is placed or sent again.
    final revisions = [for (final p in first) p.revision];
    final again = example.scene(camera, 1).populations;
    expect([for (final p in again) p.revision], revisions);

    // A cover rule moves the grass off the steeper slopes.
    final grass = counted('grass');
    example.steepness = 3;
    example.scene(camera, 2);
    expect(counted('grass'), lessThan(grass));

    expect((example..scatter = false).scene(camera, 3).populations, isEmpty);
  });

  test('its trees stand on gentle, low ground', () {
    final example = TerrainExample()..scene(camera, 0);
    final terrain = example.terrain;
    final placer = example.placer;
    final trunks = placer.layers.indexWhere((l) => l.name == 'trunks');
    for (final group in placer.groups.where((g) => g.layer == trunks)) {
      for (var i = 0; i < group.count; i++) {
        final x = group.transforms[i * 16 + 12];
        final z = group.transforms[i * 16 + 14];
        final ground = terrain.heightAt(x, z)!;
        expect(ground, lessThanOrEqualTo(30));
        final up = terrain.normalAt(x, z)!;
        expect(up.y, greaterThanOrEqualTo(math.cos(22 * math.pi / 180) - 1e-6));
      }
    }
  });

  test('the rover stands on the ground and leans with it', () {
    final example = TerrainExample();
    final terrain = example.terrain;
    for (var seconds = 0.0; seconds < 60; seconds += 3.7) {
      final rover = example.scene(camera, seconds).objects.single;
      final up = rover.transform.getColumn(1).xyz..normalize();
      final centre = rover.transform.getTranslation();
      // Back down its own up to the ground it was put on.
      final foot = centre - up * (0.8 + 0.15);
      expect(foot.xz.length, closeTo(TerrainExample.roverRadius, 1e-9));
      expect(foot.y, closeTo(terrain.heightAt(foot.x, foot.z)!, 1e-9));
      final ground = terrain.normalAt(foot.x, foot.z)!;
      expect(up.dot(ground), closeTo(1, 1e-9), reason: 'at $seconds s');
    }
    expect((example..rover = false).scene(camera, 0).objects, isEmpty);
  });
}
