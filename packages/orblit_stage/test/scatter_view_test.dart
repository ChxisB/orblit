import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_terrain/orblit_terrain.dart';

void main() {
  const grass = ScatterLayer(
    name: 'grass',
    density: 2,
    size: (0.05, 0.4, 0.05),
    colour: 0x55AA33,
    range: 40,
  );
  const trees = ScatterLayer(
    name: 'trees',
    seed: 3,
    density: 0.05,
    mesh: 'models/tree.glb',
    material: 'materials/bark.omat',
    castShadows: true,
  );

  (Terrain, ScatterPlacer) placed(List<ScatterLayer> scatter) {
    final terrain = Terrain(regionSize: 16, scatter: scatter);
    terrain.addRegion(const RegionKey(0, 0));
    terrain.addRegion(const RegionKey(1, 0));
    final placer = ScatterPlacer()..update(terrain);
    return (terrain, placer);
  }

  test('a block layer is a population a region, sharing its buffers', () {
    final (_, placer) = placed([grass]);
    final drawn = scatterFrom(placer, key: 500);

    expect(drawn.objects, isEmpty);
    expect(drawn.populations, hasLength(2));
    for (final (i, population) in drawn.populations.indexed) {
      final group = placer.groups[i];
      expect(population.key, 500 + group.id);
      expect(identical(population.transforms, group.transforms), isTrue);
      expect(identical(population.colours, group.colours), isTrue);
      expect(population.minimum, group.minimum);
      expect(population.maximum, group.maximum);
      expect(population.revision, group.revision);
      expect(population.range, 40);
      expect(population.castShadows, isFalse);
      expect(population.mesh, isNull);
    }
    final keys = drawn.populations.map((p) => p.key).toSet();
    expect(keys.length, 2);
  });

  test('an edit sends again only the region it moved', () {
    final (terrain, placer) = placed([grass]);
    final before = scatterFrom(placer, key: 0).populations;
    terrain.regionAt(const RegionKey(1, 0))!.setHeight(8, 8, 3);
    placer.update(terrain);
    final after = scatterFrom(placer, key: 0).populations;

    expect(after.map((p) => p.key), before.map((p) => p.key));
    expect(after[0].revision, before[0].revision);
    expect(after[1].revision, isNot(before[1].revision));
  });

  test('a layer with nothing in a region is left out', () {
    final (terrain, placer) = placed([grass]);
    final hole = terrain.regionAt(const RegionKey(0, 0))!;
    for (var j = 0; j < 16; j++) {
      for (var i = 0; i < 16; i++) {
        hole.setCover(i, j, hole.coverAt(i, j).withHole(true));
      }
    }
    placer.update(terrain);

    expect(placer.groups, hasLength(2));
    final drawn = scatterFrom(placer, key: 0).populations;
    expect(drawn, hasLength(1));
    expect(drawn.single.key, placer.groupsIn(const RegionKey(1, 0))[0].id);
  });

  test('a model layer is an object each, in its material', () {
    final (_, placer) = placed([grass, trees]);
    final drawn = scatterFrom(
      placer,
      key: 0,
      objectKey: 1 << 40,
      mesh: (path) => '/project/$path',
      material: (path) => path == 'materials/bark.omat' ? 17 : null,
    );

    final models = [
      for (final group in placer.groups)
        if (group.layer == 1) group,
    ];
    final count = models.fold(0, (total, group) => total + group.count);
    expect(count, greaterThan(0));
    expect(drawn.objects, hasLength(count));
    expect(drawn.populations, hasLength(2));

    var at = 0;
    for (final group in models) {
      for (var i = 0; i < group.count; i++) {
        final object = drawn.objects[at++];
        expect(object.key, (1 << 40) + group.id * 1048576 + i);
        expect(object.mesh, '/project/models/tree.glb');
        expect(object.material, 17);
        expect(object.castShadows, isTrue);
        for (var n = 0; n < 16; n++) {
          expect(object.transform.storage[n], group.transforms[i * 16 + n]);
        }
        expect(object.colour.storage, [
          group.colours[i * 3],
          group.colours[i * 3 + 1],
          group.colours[i * 3 + 2],
        ]);
      }
    }
    final keys = drawn.objects.map((o) => o.key).toSet();
    expect(keys.length, drawn.objects.length);
  });

  test('leaves model layers out when asked to', () {
    final (_, placer) = placed([grass, trees]);
    final drawn = scatterFrom(placer, key: 0, models: false);

    expect(drawn.objects, isEmpty);
    expect(drawn.populations, hasLength(2));
  });

  test('without resolvers a model keeps its path and its own materials', () {
    final (_, placer) = placed([trees]);
    final drawn = scatterFrom(placer, key: 0);

    expect(drawn.objects, isNotEmpty);
    expect(drawn.objects.first.mesh, 'models/tree.glb');
    expect(drawn.objects.first.material, isNull);
  });

  test('draws by the rules the groups were placed by', () {
    final (terrain, placer) = placed([grass]);
    terrain.scatter[0] = const ScatterLayer(name: 'grass', mesh: 'blade.glb');
    final drawn = scatterFrom(placer, key: 0);

    expect(drawn.populations, hasLength(2));
    expect(drawn.objects, isEmpty);
  });
}
