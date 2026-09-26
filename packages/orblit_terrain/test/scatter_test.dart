import 'dart:math' as math;

import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  /// Flat ground, [across] regions each way, 16 texels a region.
  Terrain flat({
    int across = 2,
    int regionSize = 16,
    List<ScatterLayer> scatter = const [],
    double Function(double x, double z)? height,
  }) {
    final terrain = Terrain(regionSize: regionSize, scatter: scatter);
    for (var z = 0; z < across; z++) {
      for (var x = 0; x < across; x++) {
        terrain.fillHeights(RegionKey(x, z), height ?? (x, z) => 0);
      }
    }
    return terrain;
  }

  ScatterPlacer placed(Terrain terrain) => ScatterPlacer()..update(terrain);

  /// Where each one stands: the translation of each transform.
  List<Vector3> spots(ScatterGroup group) => [
    for (var n = 0; n < group.count; n++)
      Vector3(
        group.transforms[n * 16 + 12],
        group.transforms[n * 16 + 13],
        group.transforms[n * 16 + 14],
      ),
  ];

  List<Vector3> allSpots(ScatterPlacer placer) => [
    for (final group in placer.groups) ...spots(group),
  ];

  /// Column [c] of member [n]'s transform.
  Vector3 column(ScatterGroup group, int n, int c) => Vector3(
    group.transforms[n * 16 + c * 4],
    group.transforms[n * 16 + c * 4 + 1],
    group.transforms[n * 16 + c * 4 + 2],
  );

  const grass = ScatterLayer(name: 'grass', seed: 7, density: 2);

  group('placing', () {
    test('lays the same pattern every time', () {
      final a = placed(flat(scatter: [grass]));
      final b = placed(flat(scatter: [grass]));
      expect(a.count, greaterThan(0));
      expect(a.groups.length, b.groups.length);
      for (var g = 0; g < a.groups.length; g++) {
        expect(a.groups[g].transforms, b.groups[g].transforms);
        expect(a.groups[g].colours, b.groups[g].colours);
      }
    });

    test('keeps the pattern it has always laid', () {
      // A change here moves every stone in every saved world. Make it only
      // on purpose.
      final placer = placed(flat(across: 1, scatter: [grass]));
      final first = spots(placer.groups.single).take(3).toList();
      // The same on the web: the pattern is integer arithmetic that stays
      // exact in a double.
      expect(first.map((spot) => spot.x), [
        closeTo(0.03972, 1e-5),
        closeTo(0.92124, 1e-5),
        closeTo(1.71098, 1e-5),
      ]);
      expect(first.map((spot) => spot.z), [
        closeTo(0.58308, 1e-5),
        closeTo(0.70224, 1e-5),
        closeTo(0.50828, 1e-5),
      ]);
    });

    test('lays the pattern over the world, not over each region', () {
      double hills(double x, double z) => math.sin(x * 0.3) * 2 + z * 0.1;
      final small = placed(
        flat(across: 4, regionSize: 8, height: hills, scatter: [grass]),
      );
      final large = placed(
        flat(across: 1, regionSize: 32, height: hills, scatter: [grass]),
      );
      List<String> sorted(ScatterPlacer placer) => [
        for (final spot in allSpots(placer))
          '${spot.x.toStringAsFixed(4)},${spot.z.toStringAsFixed(4)}',
      ]..sort();
      // Everything but the last row and column of texels, which the large
      // region has ground beyond and the small ones do not.
      bool inside(String spot) {
        final [x, z] = spot.split(',').map(double.parse).toList();
        return x < 31 && z < 31;
      }

      expect(
        sorted(small).where(inside).toList(),
        sorted(large).where(inside).toList(),
      );
    });

    test('puts down about as many as the density says', () {
      final placer = placed(flat(across: 2, scatter: [grass]));
      // Four regions of 16 m square at 2 per square metre, less the far
      // edges, where there is no ground beyond the last texel to stand on.
      expect(placer.count, closeTo(2 * 31 * 31, 2 * 31 * 31 * 0.06));
    });

    test('stands only on ground that is there', () {
      final terrain = flat(across: 2, scatter: [grass])
        ..removeRegion(const RegionKey(1, 1));
      final placer = placed(terrain);
      expect(placer.groupsIn(const RegionKey(1, 1)), isEmpty);
      for (final spot in allSpots(placer)) {
        expect(terrain.heightAt(spot.x, spot.z), isNotNull);
      }
    });

    test('puts nothing in a hole', () {
      final terrain = flat(across: 1, scatter: [grass]);
      final region = terrain.regionAt(const RegionKey(0, 0))!;
      for (var j = 4; j < 10; j++) {
        for (var i = 4; i < 10; i++) {
          region.setCover(i, j, Cover.auto.withHole(true));
        }
      }
      expect(Cover(region.cover[region.indexOf(5, 5)]).hole, isTrue);
      final before = placed(flat(across: 1, scatter: [grass])).count;
      final placer = placed(terrain);
      expect(placer.count, lessThan(before - 2 * 25));
      for (final spot in allSpots(placer)) {
        expect(terrain.heightAt(spot.x, spot.z), isNotNull);
        final inHole = spot.x > 4 && spot.x < 9 && spot.z > 4 && spot.z < 9;
        expect(inHole, isFalse, reason: '$spot');
      }
    });

    test('grows only on its sets, as thick as they cover the ground', () {
      const onGrass = ScatterLayer(
        name: 'grass',
        seed: 7,
        density: 4,
        sets: [1],
      );
      Terrain painted(Cover cover) {
        final terrain = flat(across: 1, scatter: [onGrass]);
        final region = terrain.regionAt(const RegionKey(0, 0))!;
        for (var at = 0; at < region.cover.length; at++) {
          region.cover[at] = cover.word;
        }
        region.touch();
        return terrain;
      }

      final all = placed(painted(Cover.of(base: 1, overlay: 1))).count;
      final none = placed(painted(Cover.of(base: 0, overlay: 0))).count;
      final half = placed(
        painted(Cover.of(base: 0, overlay: 1, blend: 0.5)),
      ).count;
      final quarter = placed(
        painted(Cover.of(base: 1, overlay: 0, blend: 0.75)),
      ).count;
      expect(all, greaterThan(800));
      expect(none, 0);
      expect(half / all, closeTo(0.5, 0.06));
      expect(quarter / all, closeTo(0.25, 0.06));
    });

    test('reads automatic ground as the sets it chooses between', () {
      const onGrass = ScatterLayer(name: 'grass', density: 4, sets: [1]);
      const onScree = ScatterLayer(name: 'scree', density: 4, sets: [0]);
      // Level: all the flat set, which is 1 by default.
      final level = placed(flat(across: 1, scatter: [onGrass, onScree]));
      expect(level.groups[0].count, greaterThan(800));
      expect(level.groups[1].count, 0);
      // Steep: 70° is past where the flat set is gone. Asked of the middle
      // region, since the slope at the terrain's outer edge is read from
      // one side only and comes out gentler.
      final steep = placed(
        flat(
          across: 3,
          scatter: [onGrass, onScree],
          height: (x, z) => x * math.tan(70 * math.pi / 180),
        ),
      ).groupsIn(const RegionKey(1, 1));
      expect(steep[0].count, 0);
      expect(steep[1].count, greaterThan(800));
    });

    test('keeps to its slopes', () {
      // The middle region of three, away from the terrain's outer edge.
      int ramp(double degrees, ScatterLayer layer) => placed(
        flat(
          across: 3,
          scatter: [layer],
          height: (x, z) => x * math.tan(degrees * math.pi / 180),
        ),
      ).groupsIn(const RegionKey(1, 1)).single.count;
      const gentle = ScatterLayer(name: 'gentle', density: 2, maxSlope: 20);
      const steep = ScatterLayer(name: 'steep', density: 2, minSlope: 25);
      expect(ramp(30, gentle), 0);
      expect(ramp(10, gentle), greaterThan(400));
      expect(ramp(10, steep), 0);
      expect(ramp(30, steep), greaterThan(400));
    });

    test('keeps to its heights', () {
      const low = ScatterLayer(name: 'low', density: 2, maxHeight: 5);
      const high = ScatterLayer(name: 'high', density: 2, minHeight: 5);
      final terrain = flat(
        across: 1,
        scatter: [low, high],
        height: (x, z) => x,
      );
      final placer = placed(terrain);
      expect(placer.groups[0].count, greaterThan(0));
      expect(placer.groups[1].count, greaterThan(0));
      for (final spot in spots(placer.groups[0])) {
        expect(terrain.heightAt(spot.x, spot.z), lessThanOrEqualTo(5));
      }
      for (final spot in spots(placer.groups[1])) {
        expect(terrain.heightAt(spot.x, spot.z), greaterThanOrEqualTo(5));
      }
    });

    test('a trunk and a crown with one seed stand together', () {
      const trunk = ScatterLayer(
        name: 'trunk',
        seed: 3,
        density: 0.5,
        size: (0.3, 2, 0.3),
        minScale: 0.8,
        maxScale: 1.2,
      );
      const crown = ScatterLayer(
        name: 'crown',
        seed: 3,
        density: 0.5,
        size: (1.5, 1.5, 1.5),
        lift: 2,
        minScale: 0.8,
        maxScale: 1.2,
      );
      final placer = placed(flat(across: 1, scatter: [trunk, crown]));
      final trunks = spots(placer.groups[0]);
      final crowns = spots(placer.groups[1]);
      expect(trunks.length, crowns.length);
      for (var n = 0; n < trunks.length; n++) {
        expect(crowns[n].x, closeTo(trunks[n].x, 1e-5));
        expect(crowns[n].z, closeTo(trunks[n].z, 1e-5));
        // The crown's bottom is the trunk's top, whatever the size drawn.
        final trunkTop = trunks[n].y + column(placer.groups[0], n, 1).y;
        final crownBottom = crowns[n].y - column(placer.groups[1], n, 1).y;
        expect(crownBottom, closeTo(trunkTop, 1e-4));
      }
    });

    test('layers with their own seeds stand apart', () {
      const stones = ScatterLayer(name: 'stones', seed: 1, density: 2);
      const flowers = ScatterLayer(name: 'flowers', seed: 2, density: 2);
      final placer = placed(flat(across: 1, scatter: [stones, flowers]));
      final a = spots(placer.groups[0]).map((s) => '${s.x},${s.z}').toSet();
      final b = spots(placer.groups[1]).map((s) => '${s.x},${s.z}').toSet();
      expect(a.intersection(b), isEmpty);
    });
  });

  group('standing', () {
    test('a block stands on its bottom face, sunk by its lift', () {
      const block = ScatterLayer(
        name: 'block',
        density: 1,
        size: (0.2, 1, 0.4),
        lift: -0.1,
        turn: false,
      );
      final terrain = flat(across: 1, scatter: [block], height: (x, z) => 3);
      final group = placed(terrain).groups.single;
      expect(group.count, greaterThan(0));
      for (var n = 0; n < group.count; n++) {
        // The cube runs −1 to 1, so each column is half the block.
        expect(column(group, n, 0).length, closeTo(0.1, 1e-5));
        expect(column(group, n, 1), _near(Vector3(0, 0.5, 0)));
        expect(column(group, n, 2).length, closeTo(0.2, 1e-5));
        expect(spots(group)[n].y, closeTo(3 - 0.1 + 0.5, 1e-5));
      }
    });

    test('a model stands on its origin, at its size', () {
      const tree = ScatterLayer(
        name: 'tree',
        density: 1,
        mesh: 'models/tree.glb',
        size: (2, 3, 2),
        minScale: 0.5,
        maxScale: 1.5,
      );
      final terrain = flat(across: 1, scatter: [tree], height: (x, z) => 3);
      final group = placed(terrain).groups.single;
      for (var n = 0; n < group.count; n++) {
        final scale = column(group, n, 1).length / 3;
        expect(scale, inInclusiveRange(0.5 - 1e-6, 1.5 + 1e-6));
        expect(column(group, n, 0).length, closeTo(2 * scale, 1e-4));
        expect(column(group, n, 2).length, closeTo(2 * scale, 1e-4));
        expect(spots(group)[n].y, closeTo(3, 1e-5));
      }
      // Not all one size.
      final sizes = {
        for (var n = 0; n < group.count; n++)
          column(group, n, 1).length.toStringAsFixed(3),
      };
      expect(sizes.length, greaterThan(10));
    });

    test('leans with the ground as far as it is told', () {
      const upright = ScatterLayer(name: 'upright', density: 1, mesh: 'm');
      const square = ScatterLayer(
        name: 'square',
        density: 1,
        mesh: 'm',
        lean: 1,
      );
      const between = ScatterLayer(
        name: 'between',
        density: 1,
        mesh: 'm',
        lean: 0.5,
      );
      final terrain = flat(
        across: 1,
        scatter: [upright, square, between],
        height: (x, z) => x * math.tan(30 * math.pi / 180),
      );
      final placer = placed(terrain);
      for (var layer = 0; layer < 3; layer++) {
        final group = placer.groups[layer];
        expect(group.count, greaterThan(0));
        for (var n = 0; n < group.count; n++) {
          final spot = spots(group)[n];
          final normal = terrain.normalAt(spot.x, spot.z)!;
          final up = column(group, n, 1).normalized();
          final tilt = math.acos(up.y.clamp(-1.0, 1.0)) * 180 / math.pi;
          switch (layer) {
            case 0:
              expect(up, _near(Vector3(0, 1, 0)));
            case 1:
              expect(up, _near(normal));
            case 2:
              // Halfway leans half as far as the ground does.
              final ground = math.acos(normal.y) * 180 / math.pi;
              expect(tilt, closeTo(ground / 2, 1e-3));
              expect(up.x, lessThan(0));
          }
          // Still a rotation: the columns square to one another.
          final side = column(group, n, 0).normalized();
          final front = column(group, n, 2).normalized();
          expect(side.dot(up), closeTo(0, 1e-5));
          expect(front.dot(up), closeTo(0, 1e-5));
          expect(side.cross(up).dot(front), closeTo(1, 1e-5));
        }
      }
    });

    test('turns each a different way unless told not to', () {
      const turned = ScatterLayer(name: 'turned', density: 1, mesh: 'm');
      const straight = ScatterLayer(
        name: 'straight',
        density: 1,
        mesh: 'm',
        turn: false,
      );
      final placer = placed(flat(across: 1, scatter: [turned, straight]));
      final angles = {
        for (var n = 0; n < placer.groups[0].count; n++)
          column(placer.groups[0], n, 0).x.toStringAsFixed(2),
      };
      expect(angles.length, greaterThan(20));
      for (var n = 0; n < placer.groups[1].count; n++) {
        expect(column(placer.groups[1], n, 0), _near(Vector3(1, 0, 0)));
      }
    });

    test('its box holds every one of them', () {
      final placer = placed(
        flat(
          across: 1,
          scatter: [
            const ScatterLayer(name: 'rocks', density: 1, size: (1, 2, 1)),
          ],
          height: (x, z) => math.sin(x) * 3,
        ),
      );
      final group = placer.groups.single;
      for (var n = 0; n < group.count; n++) {
        final spot = spots(group)[n];
        final top = spot + column(group, n, 1);
        for (final point in [spot, top]) {
          for (var axis = 0; axis < 3; axis++) {
            expect(point[axis], greaterThanOrEqualTo(group.minimum[axis]));
            expect(point[axis], lessThanOrEqualTo(group.maximum[axis]));
          }
        }
      }
    });

    test('is its colour, varied and tinted by the ground', () {
      const plain = ScatterLayer(name: 'plain', density: 1, colour: 0x808080);
      const varied = ScatterLayer(
        name: 'varied',
        density: 1,
        colour: 0x808080,
        colourVariation: 0.5,
      );
      const tinted = ScatterLayer(
        name: 'tinted',
        density: 1,
        colour: 0xFFFFFF,
        groundTint: 1,
      );
      final terrain = flat(across: 1, scatter: [plain, varied, tinted]);
      final region = terrain.regionAt(const RegionKey(0, 0))!;
      for (var j = 0; j < 16; j++) {
        for (var i = 0; i < 16; i++) {
          region.setColour(i, j, GroundColour.of(red: 255, green: 0, blue: 0));
        }
      }
      final placer = placed(terrain);
      // 0x80 is a fifth of the way up in linear light.
      final grey = placer.groups[0].colours;
      for (final channel in grey) {
        expect(channel, closeTo(0.2158, 1e-3));
      }
      final shades = placer.groups[1].colours;
      expect(shades.reduce(math.min), lessThan(0.2158 * 0.8));
      expect(shades.reduce(math.max), greaterThan(0.2158 * 1.2));
      expect(
        shades.reduce(math.min),
        greaterThanOrEqualTo(0.2158 * 0.5 - 1e-6),
      );
      final red = placer.groups[2].colours;
      for (var n = 0; n < red.length; n += 3) {
        expect(red[n], closeTo(1, 1e-6));
        expect(red[n + 1], closeTo(0, 1e-6));
        expect(red[n + 2], closeTo(0, 1e-6));
      }
    });
  });

  group('keeping up', () {
    test('does nothing when nothing changed', () {
      final terrain = flat(scatter: [grass]);
      final placer = ScatterPlacer();
      expect(placer.update(terrain), isTrue);
      final revision = placer.revision;
      expect(placer.update(terrain), isFalse);
      expect(placer.revision, revision);
    });

    test('places again only the region that was edited', () {
      final terrain = flat(across: 3, scatter: [grass]);
      final placer = placed(terrain);
      final before = {for (final group in placer.groups) group.key: group};
      // The middle of the middle region: nowhere near an edge.
      terrain.regionAt(const RegionKey(1, 1))!.setHeight(8, 8, 2);
      expect(placer.update(terrain), isTrue);
      for (final group in placer.groups) {
        if (group.key == const RegionKey(1, 1)) {
          expect(group.revision, isNot(before[group.key]!.revision));
          expect(group.id, before[group.key]!.id);
        } else {
          expect(group, same(before[group.key]));
        }
      }
    });

    test('places a neighbour again when their shared edge moves', () {
      final terrain = flat(across: 3, scatter: [grass]);
      final placer = placed(terrain);
      final before = {for (final group in placer.groups) group.key: group};
      // The first column of the middle region: its left neighbour reads it.
      terrain.regionAt(const RegionKey(1, 1))!.setHeight(0, 8, 2);
      placer.update(terrain);
      final moved = {
        for (final group in placer.groups)
          if (!identical(group, before[group.key])) group.key,
      };
      expect(moved, {const RegionKey(1, 1), const RegionKey(0, 1)});
    });

    test('moves nothing outside the region it places again', () {
      final terrain = flat(across: 2, scatter: [grass]);
      final placer = placed(terrain);
      final before = allSpots(placer).map((s) => s.toString()).toList();
      final region = terrain.regionAt(const RegionKey(0, 0))!;
      for (var j = 2; j < 12; j++) {
        for (var i = 2; i < 12; i++) {
          region.setCover(i, j, Cover.auto.withHole(true));
        }
      }
      placer.update(terrain);
      final after = allSpots(placer).map((s) => s.toString()).toSet();
      final kept = before.where(after.contains).length;
      // Everything outside the hole is where it was.
      expect(kept, allSpots(placer).length);
      expect(after.length, lessThan(before.length));
    });

    test(
      'places a layer again everywhere when its rules change, and only it',
      () {
        final terrain = flat(
          scatter: [
            grass,
            const ScatterLayer(name: 'rocks', seed: 2, density: 0.2),
          ],
        );
        final placer = placed(terrain);
        final rocks = [
          for (final g in placer.groups)
            if (g.layer == 1) g,
        ];
        terrain.scatter[0] = const ScatterLayer(
          name: 'grass',
          seed: 7,
          density: 1,
        );
        expect(placer.update(terrain), isTrue);
        for (final group in placer.groups) {
          if (group.layer == 1) {
            expect(rocks, contains(same(group)));
          }
        }
        expect(placer.count, lessThan(2 * 31 * 31));
      },
    );

    test('follows layers and regions coming and going', () {
      final terrain = flat(scatter: [grass]);
      final placer = placed(terrain);
      expect(placer.groups.length, 4);
      terrain.scatter.add(const ScatterLayer(name: 'rocks', seed: 2));
      expect(placer.update(terrain), isTrue);
      expect(placer.groups.length, 8);
      expect(placer.layers.map((l) => l.name), ['grass', 'rocks']);
      terrain.scatter.removeAt(0);
      // The rules a group was placed by stay until the next update.
      expect(placer.layers.map((l) => l.name), ['grass', 'rocks']);
      expect(placer.update(terrain), isTrue);
      expect(placer.groups.length, 4);
      expect(placer.groups.map((g) => g.layer).toSet(), {0});
      expect(placer.layers.single.name, 'rocks');
      terrain.removeRegion(const RegionKey(1, 0));
      expect(placer.update(terrain), isTrue);
      expect(placer.groups.length, 3);
      expect(placer.groupsIn(const RegionKey(1, 0)), isEmpty);
      // Ids are never shared between two groups held at once.
      final ids = placer.groups.map((g) => g.id).toList();
      expect(ids.toSet().length, ids.length);
    });

    test('places automatic ground again when its rule changes', () {
      const onGrass = ScatterLayer(name: 'grass', density: 2, sets: [1]);
      final terrain = flat(across: 1, scatter: [onGrass]);
      final placer = placed(terrain);
      expect(placer.count, greaterThan(0));
      terrain.autoCover = const AutoCover(steep: 1, flat: 0);
      expect(placer.update(terrain), isTrue);
      expect(placer.count, 0);
    });
  });

  group('the file', () {
    test('a layer says only what is not its default', () {
      expect(const ScatterLayer(name: 'grass').toJson(), {
        'name': 'grass',
        'density': 1.0,
        'size': [1.0, 1.0, 1.0],
        'colour': '#ffffff',
      });
    });

    test('a layer comes back as it went', () {
      const layer = ScatterLayer(
        name: 'pines',
        seed: 12,
        density: 0.05,
        sets: [1, 3],
        minSlope: 2,
        maxSlope: 35,
        minHeight: -4,
        maxHeight: 400,
        size: (1.2, 1.4, 1.2),
        minScale: 0.7,
        maxScale: 1.6,
        lean: 0.1,
        turn: false,
        lift: -0.3,
        colour: 0x2f5d3a,
        colourVariation: 0.15,
        groundTint: 0.4,
        range: 600,
        castShadows: true,
        mesh: 'models/pine.glb',
        material: 'materials/pine.omat',
      );
      final problems = <String>[];
      expect(ScatterLayer.fromJson(layer.toJson(), problems), layer);
      expect(problems, isEmpty);
    });

    test('a layer out of range is brought into it, with a note', () {
      final problems = <String>[];
      final layer = ScatterLayer.fromJson({
        'name': 'weeds',
        'density': 5000,
        'sets': [1, 99],
        'minSlope': 50,
        'maxSlope': 10,
        'lean': 3,
        'size': [1, -2, 1],
      }, problems)!;
      expect(layer.density, ScatterLayer.maxDensity);
      expect(layer.sets, [1]);
      expect((layer.minSlope, layer.maxSlope), (10, 50));
      expect(layer.lean, 1);
      expect(layer.size, (1, 1, 1));
      expect(problems, hasLength(4));
      expect(ScatterLayer.fromJson({'density': 2}, problems), isNull);
    });

    test('travels in the terrain file, a layer to a line', () {
      final terrain = Terrain(
        regionSize: 16,
        scatter: [
          grass,
          const ScatterLayer(name: 'rocks', seed: 2, density: 0.1),
        ],
      );
      final text = terrain.encode();
      expect(text, contains('\n    {"name":"grass"'));
      expect(text, contains('\n    {"name":"rocks"'));
      final back = Terrain.decode(text);
      expect(back.problems, isEmpty);
      expect(back.terrain.scatter, terrain.scatter);
    });

    test('a terrain with nothing scattered is written as it was before', () {
      expect(Terrain(regionSize: 16).encode(), isNot(contains('scatter')));
    });
  });
}

Matcher _near(Vector3 expected) => predicate<Vector3>(
  (actual) => (actual - expected).length < 1e-5,
  'within 1e-5 of $expected',
);
