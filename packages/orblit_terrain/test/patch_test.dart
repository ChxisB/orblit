import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

void main() {
  Terrain ground() => Terrain(regionSize: 128)
    ..addRegion(const RegionKey(0, 0))
    ..addRegion(const RegionKey(1, 0));

  TerrainStroke raise(Terrain terrain) => TerrainStroke(
    terrain,
    tool: BrushTool.raise,
    brush: const Brush(size: 12),
  );

  test('a stroke of many moves undoes and redoes as one', () {
    final terrain = ground();
    final stroke = raise(terrain);
    var patch = stroke.moveTo(20, 20);
    for (var x = 22.0; x <= 100; x += 2) {
      patch = patch.followedBy(stroke.moveTo(x, 20));
    }
    final after = List.of(terrain.regionAt(const RegionKey(0, 0))!.heights);
    expect(after.any((height) => height > 0), isTrue);

    patch.revert(terrain);
    expect(
      terrain.regionAt(const RegionKey(0, 0))!.heights.every((h) => h == 0),
      isTrue,
    );
    patch.apply(terrain);
    expect(terrain.regionAt(const RegionKey(0, 0))!.heights, after);
  });

  test('costs kilobytes for a small brush, not the region', () {
    final terrain = Terrain(regionSize: 1024)..addRegion(const RegionKey(0, 0));
    final patch = raise(terrain).moveTo(300, 300);
    // One 32² tile of heights before and after, or four if it straddles.
    expect(patch.byteCount, lessThanOrEqualTo(4 * 2 * 32 * 32 * 4));
    expect(patch.layers, {TerrainLayer.height});
  });

  test('keeps the earliest before and the latest after', () {
    final terrain = ground();
    final region = terrain.regionAt(const RegionKey(0, 0))!;
    final first = raise(terrain).moveTo(20, 20);
    final middle = region.heightAt(20, 20);
    final second = raise(terrain).moveTo(20, 20);
    final both = first.followedBy(second);
    both.revert(terrain);
    expect(region.heightAt(20, 20), 0);
    both.apply(terrain);
    expect(region.heightAt(20, 20), 2 * middle);
  });

  test('puts back maps of two tools that touched one tile', () {
    final terrain = ground();
    final region = terrain.regionAt(const RegionKey(0, 0))!;
    final shaped = raise(terrain).moveTo(20, 20);
    final painted = TerrainStroke(
      terrain,
      tool: BrushTool.hole,
      brush: const Brush(size: 4),
    ).moveTo(20, 20);
    final both = shaped.followedBy(painted);
    expect(both.layers, {TerrainLayer.height, TerrainLayer.cover});
    both.revert(terrain);
    expect(region.heightAt(20, 20), 0);
    expect(region.coverAt(20, 20).hole, isFalse);
  });

  test('says which regions it changed and touches them', () {
    final terrain = ground();
    final patch = raise(terrain).moveTo(128, 20);
    expect(patch.regions, {const RegionKey(0, 0), const RegionKey(1, 0)});
    final was = terrain.regionAt(const RegionKey(1, 0))!.revision;
    expect(patch.revert(terrain), patch.regions);
    expect(terrain.regionAt(const RegionKey(1, 0))!.revision, isNot(was));
  });

  test('skips a region that has gone since', () {
    final terrain = ground();
    final patch = raise(terrain).moveTo(128, 20);
    terrain.removeRegion(const RegionKey(1, 0));
    expect(patch.revert(terrain), {const RegionKey(0, 0)});
    expect(terrain.regionAt(const RegionKey(1, 0)), isNull);
  });

  test('refuses a terrain with other regions', () {
    final patch = raise(ground()).moveTo(20, 20);
    expect(
      () => patch.apply(Terrain(regionSize: 64)),
      throwsA(isA<ArgumentError>()),
    );
  });

  test('the empty patch changes nothing and folds away', () {
    final terrain = ground();
    final patch = raise(terrain).moveTo(20, 20);
    final empty = TerrainPatch.empty(regionSize: 128);
    expect(empty.isEmpty, isTrue);
    expect(identical(empty.followedBy(patch), patch), isTrue);
    expect(identical(patch.followedBy(empty), patch), isTrue);
    expect(empty.apply(terrain), isEmpty);
  });

  test('a recorder keeps a tile once however often it is asked', () {
    final terrain = ground();
    final recorder = TerrainRecorder(terrain, {TerrainLayer.height})
      ..keep(0, 0, 10, 10)
      ..keep(5, 5, 20, 20);
    terrain.regionAt(const RegionKey(0, 0))!.setHeight(3, 3, 9);
    recorder.keep(0, 0, 4, 4);
    final patch = recorder.finish();
    expect(patch.tileCount, 1);
    patch.revert(terrain);
    expect(terrain.texelHeight(3, 3), 0);
  });
}
