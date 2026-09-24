import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

void main() {
  /// Four regions of 64 texels at 1 m, from (0, 0) to (128, 128).
  Terrain ground() {
    final terrain = Terrain(regionSize: 64);
    for (final key in const [
      RegionKey(0, 0),
      RegionKey(1, 0),
      RegionKey(0, 1),
      RegionKey(1, 1),
    ]) {
      terrain.addRegion(key);
    }
    return terrain;
  }

  double at(Terrain terrain, int i, int j) => terrain.texelHeight(i, j)!;

  group('a brush', () {
    test('weighs its whole disc at no falloff and nothing past its edge', () {
      const brush = Brush(size: 10, falloff: 0);
      expect(brush.weightAt(0), 1);
      expect(brush.weightAt(4.99), 1);
      expect(brush.weightAt(5), 0);
    });

    test('fades from the centre at full falloff', () {
      const brush = Brush(size: 10, falloff: 1);
      expect(brush.weightAt(0), 1);
      expect(brush.weightAt(2.5), closeTo(0.5, 1e-9));
      expect(brush.weightAt(4.9), lessThan(0.01));
    });
  });

  group('raise and lower', () {
    test('lift the ground under the brush and leave the rest', () {
      final terrain = ground();
      TerrainStroke(
        terrain,
        tool: BrushTool.raise,
        brush: const Brush(size: 8, falloff: 0),
      ).moveTo(20, 20);
      expect(at(terrain, 20, 20), greaterThan(0));
      expect(at(terrain, 23, 20), at(terrain, 20, 20));
      expect(at(terrain, 25, 20), 0);
    });

    test('invert turns one into the other', () {
      final raised = ground();
      final lowered = ground();
      const brush = Brush(size: 8);
      TerrainStroke(raised, tool: BrushTool.raise, brush: brush).moveTo(20, 20);
      TerrainStroke(
        lowered,
        tool: BrushTool.lower,
        brush: brush,
        invert: true,
      ).moveTo(20, 20);
      expect(at(lowered, 20, 20), at(raised, 20, 20));
      TerrainStroke(
        lowered,
        tool: BrushTool.lower,
        brush: brush,
      ).moveTo(20, 20);
      expect(at(lowered, 20, 20), 0);
    });

    test('rise by the same amount a pass at any spacing', () {
      double pass(double spacing) {
        final terrain = ground();
        TerrainStroke(
            terrain,
            tool: BrushTool.raise,
            brush: Brush(size: 16, falloff: 0, spacing: spacing),
          )
          ..moveTo(10, 40)
          ..moveTo(70, 40);
        return at(terrain, 40, 40);
      }

      expect(pass(0.1), closeTo(pass(0.25), 0.25 * pass(0.25)));
    });

    test('cross from one region into the next without a seam', () {
      final terrain = ground();
      TerrainStroke(
        terrain,
        tool: BrushTool.raise,
        brush: const Brush(size: 10, falloff: 0),
      ).moveTo(64, 20);
      expect(at(terrain, 63, 20), at(terrain, 64, 20));
      expect(at(terrain, 64, 20), greaterThan(0));
    });
  });

  test('lays dabs a spacing apart along the way', () {
    final stroke = TerrainStroke(
      ground(),
      tool: BrushTool.raise,
      brush: const Brush(size: 8, spacing: 0.5),
    );
    stroke.moveTo(10, 10);
    expect(stroke.dabs, 1);
    stroke.moveTo(13, 10);
    expect(stroke.dabs, 1, reason: 'three metres is short of the four');
    stroke.moveTo(30, 10);
    expect(stroke.dabs, 6, reason: 'at 14, 18, 22, 26 and 30');
  });

  test('scatters the same way twice from the same seed', () {
    List<double> run(int seed) {
      final terrain = ground();
      TerrainStroke(
          terrain,
          tool: BrushTool.raise,
          brush: const Brush(size: 6, jitter: 1),
          seed: seed,
        )
        ..moveTo(10, 30)
        ..moveTo(50, 30);
      return [for (var i = 0; i < 64; i++) at(terrain, i, 30)];
    }

    expect(run(7), run(7));
    expect(run(7), isNot(run(8)));
  });

  test('smooth takes the edge off a step', () {
    final terrain = ground();
    for (var j = 0; j < 64; j++) {
      for (var i = 20; i < 64; i++) {
        terrain.regionAt(const RegionKey(0, 0))!.heights[j * 64 + i] = 4;
      }
    }
    TerrainStroke(
      terrain,
      tool: BrushTool.smooth,
      brush: const Brush(size: 8, strength: 1),
    ).moveTo(20, 20);
    expect(at(terrain, 19, 20), greaterThan(0));
    expect(at(terrain, 20, 20), lessThan(4));
    expect(at(terrain, 10, 20), 0);
  });

  test('flatten draws the ground to where the stroke began', () {
    final terrain = ground()..fillHeights(const RegionKey(0, 0), (x, z) => x);
    TerrainStroke(
        terrain,
        tool: BrushTool.flatten,
        brush: const Brush(size: 6, strength: 1, falloff: 0),
      )
      ..moveTo(20, 20)
      ..moveTo(30, 20);
    expect(at(terrain, 28, 20), closeTo(20, 0.5));
    expect(at(terrain, 40, 20), 40);
  });

  test('slope lays a ramp from the start to the brush', () {
    final terrain = ground();
    final region = terrain.regionAt(const RegionKey(0, 0))!;
    for (var j = 0; j < 64; j++) {
      for (var i = 40; i < 64; i++) {
        region.heights[j * 64 + i] = 10;
      }
    }
    TerrainStroke(
        terrain,
        tool: BrushTool.slope,
        brush: const Brush(size: 6, strength: 1, falloff: 0, spacing: 0.1),
      )
      ..moveTo(10, 20)
      ..moveTo(45, 20);
    expect(at(terrain, 46, 20), 10, reason: 'climbing on leaves the ledge');
    expect(at(terrain, 27, 20), 0, reason: 'the way out found flat ground');

    TerrainStroke(
        terrain,
        tool: BrushTool.slope,
        brush: const Brush(size: 6, strength: 1, falloff: 0, spacing: 0.1),
      )
      ..moveTo(10, 20)
      ..moveTo(45, 20)
      ..moveTo(10, 20);
    // Halfway along the ramp, halfway up it.
    expect(at(terrain, 27, 20), closeTo(5, 0.5));
    expect(at(terrain, 20, 20), closeTo(10 * 10 / 35, 0.5));
    expect(at(terrain, 5, 20), 0, reason: 'behind the start is left alone');
    expect(at(terrain, 50, 20), 10, reason: 'beyond the far end too');
  });

  group('painting cover', () {
    test('starts from what automatic ground showed', () {
      final terrain = Terrain(
        regionSize: 64,
        sets: const [
          TerrainSet(name: 'rock'),
          TerrainSet(name: 'grass'),
          TerrainSet(name: 'sand'),
        ],
      )..addRegion(const RegionKey(0, 0));
      TerrainStroke(
        terrain,
        tool: BrushTool.cover,
        set: 2,
        brush: const Brush(size: 4, strength: 0.5, falloff: 0),
      ).moveTo(20, 20);
      final cover = terrain.texelCover(20, 20)!;
      expect(cover.automatic, isFalse);
      // Level ground is all grass; grass stays under the sand.
      expect(cover.base, 1);
      expect(cover.overlay, 2);
      expect(cover.blend, closeTo(0.5, 0.01));
      expect(terrain.texelCover(30, 20)!.automatic, isTrue);
    });

    test('gets all the way to the set it paints', () {
      final terrain = ground();
      final stroke = TerrainStroke(
        terrain,
        tool: BrushTool.cover,
        set: 3,
        brush: const Brush(size: 4, strength: 0.2, falloff: 0),
      );
      for (var pass = 0; pass < 40; pass++) {
        stroke
          ..moveTo(20, 20)
          ..moveTo(21, 20);
      }
      final cover = terrain.texelCover(20, 20)!;
      expect(cover.base, 3);
      expect(cover.overlay == 3 || cover.blend == 0, isTrue);
    });

    test('invert hands the ground back to automatic cover', () {
      final terrain = ground();
      final region = terrain.regionAt(const RegionKey(0, 0))!
        ..setCover(20, 20, Cover.of(base: 4));
      TerrainStroke(
        terrain,
        tool: BrushTool.cover,
        invert: true,
        brush: const Brush(size: 4),
      ).moveTo(20, 20);
      expect(region.coverAt(20, 20).automatic, isTrue);
    });
  });

  test('holes are punched and filled, and stop the ground being there', () {
    final terrain = ground();
    TerrainStroke(
      terrain,
      tool: BrushTool.hole,
      brush: const Brush(size: 4, falloff: 0),
    ).moveTo(20, 20);
    expect(terrain.texelCover(20, 20)!.hole, isTrue);
    expect(terrain.heightAt(20, 20), isNull);
    TerrainStroke(
      terrain,
      tool: BrushTool.hole,
      invert: true,
      brush: const Brush(size: 4, falloff: 0),
    ).moveTo(20, 20);
    expect(terrain.texelCover(20, 20)!.hole, isFalse);
  });

  test('colour and roughness paint their own bytes and not each other', () {
    final terrain = ground();
    TerrainStroke(
      terrain,
      tool: BrushTool.colour,
      colour: GroundColour.of(red: 200, green: 100, blue: 0),
      brush: const Brush(size: 4, strength: 1, falloff: 0),
    ).moveTo(20, 20);
    var colour = terrain.texelColour(20, 20)!;
    expect(colour.red, 200);
    expect(colour.blue, 0);
    expect(colour.roughnessByte, GroundColour.none.roughnessByte);

    TerrainStroke(
      terrain,
      tool: BrushTool.roughness,
      roughness: 1,
      brush: const Brush(size: 4, strength: 1, falloff: 0),
    ).moveTo(20, 20);
    colour = terrain.texelColour(20, 20)!;
    expect(colour.roughness, 1);
    expect(colour.red, 200);
  });

  test('leaves ground with no region alone', () {
    final terrain = Terrain(regionSize: 64)..addRegion(const RegionKey(0, 0));
    final patch = TerrainStroke(
      terrain,
      tool: BrushTool.raise,
      brush: const Brush(size: 10),
    ).moveTo(64, 20);
    expect(terrain.regionAt(const RegionKey(1, 0)), isNull);
    expect(patch.regions, {const RegionKey(0, 0)});
  });

  test('touches the regions it writes, and only those', () {
    final terrain = ground();
    final revisions = {
      for (final region in terrain.regions) region.key: region.revision,
    };
    TerrainStroke(
      terrain,
      tool: BrushTool.raise,
      brush: const Brush(size: 4),
    ).moveTo(20, 20);
    for (final region in terrain.regions) {
      expect(
        region.revision != revisions[region.key],
        region.key == const RegionKey(0, 0),
        reason: '${region.key}',
      );
    }
  });
}
