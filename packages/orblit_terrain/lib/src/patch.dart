import 'dart:math' as math;
import 'dart:typed_data';

import 'region.dart';
import 'terrain.dart';

/// Which of a region's maps an edit changes.
enum TerrainLayer { height, cover, colour }

/// What an edit changed: the tiles it touched, as they were before and as
/// they were after.
///
/// A tile is a square of [tileSize] texels inside one region, and only the
/// maps in [layers] are kept for it. So a stroke across one hillside costs a
/// few kilobytes, where a copy of the regions it crossed would cost
/// megabytes, and an undo history of those would be a leak with a menu item.
///
/// Made by a [TerrainRecorder]. [apply] and [revert] write the tiles back
/// into whatever terrain they are handed, into the regions that exist there;
/// a tile whose region has since gone is skipped rather than made.
class TerrainPatch {
  TerrainPatch._(
    this.tileSize,
    this.regionSize,
    this.layers,
    this._before,
    this._after,
  );

  /// Nothing changed.
  TerrainPatch.empty({this.tileSize = defaultTileSize, this.regionSize = 256})
    : layers = const {},
      _before = const {},
      _after = const {};

  /// Texels a tile is across, unless the regions are smaller: 32, so a tile
  /// of heights is 4 KB and a small brush touches one to four of them.
  static const int defaultTileSize = 32;

  final int tileSize;

  /// Texels a region is across in the terrain this was recorded on.
  final int regionSize;

  final Set<TerrainLayer> layers;

  final Map<_TileKey, _Tile> _before;
  final Map<_TileKey, _Tile> _after;

  bool get isEmpty => _before.isEmpty;

  int get tileCount => _before.length;

  /// How much it holds, before and after together.
  int get byteCount {
    var total = 0;
    for (final tile in _before.values) {
      total += tile.byteCount;
    }
    for (final tile in _after.values) {
      total += tile.byteCount;
    }
    return total;
  }

  /// The regions it changes.
  Set<RegionKey> get regions => {
    for (final key in _before.keys)
      RegionKey.containing(key.x * tileSize, key.z * tileSize, regionSize),
  };

  /// Makes [terrain] how it was after the edit. Says which regions changed.
  Set<RegionKey> apply(Terrain terrain) => _write(terrain, _after);

  /// Makes [terrain] how it was before the edit. Says which regions changed.
  Set<RegionKey> revert(Terrain terrain) => _write(terrain, _before);

  /// This edit and then [later], as one: the earlier of the two befores and
  /// the later of the two afters for each tile. How a stroke recorded a
  /// move at a time becomes one step to undo.
  TerrainPatch followedBy(TerrainPatch later) {
    if (isEmpty) return later;
    if (later.isEmpty) return this;
    if (later.tileSize != tileSize || later.regionSize != regionSize) {
      throw ArgumentError.value(
        later,
        'later',
        'Was recorded with other tiles or regions than this one.',
      );
    }
    final before = Map.of(_before);
    final after = Map.of(_after);
    for (final MapEntry(:key, :value) in later._before.entries) {
      final had = before[key];
      before[key] = had == null ? value : had.joined(value);
    }
    for (final MapEntry(:key, :value) in later._after.entries) {
      final had = after[key];
      after[key] = had == null ? value : value.joined(had);
    }
    return TerrainPatch._(
      tileSize,
      regionSize,
      {...layers, ...later.layers},
      before,
      after,
    );
  }

  Set<RegionKey> _write(Terrain terrain, Map<_TileKey, _Tile> tiles) {
    if (terrain.regionSize != regionSize) {
      throw ArgumentError.value(
        terrain,
        'terrain',
        'Its regions are ${terrain.regionSize} across; this was recorded on '
            'regions $regionSize across.',
      );
    }
    final changed = <TerrainRegion>{};
    for (final MapEntry(:key, :value) in tiles.entries) {
      final region = terrain.regionAt(key.region(tileSize, regionSize));
      if (region == null) continue;
      value.writeInto(region, key.cornerIn(region, tileSize), tileSize);
      changed.add(region);
    }
    for (final region in changed) {
      region.touch();
    }
    return {for (final region in changed) region.key};
  }
}

/// Keeps the tiles an edit is about to write, so that what it did can be
/// undone.
///
/// Tell it which texels are about to change with [keep] before writing them;
/// each tile is copied the first time, and only the first. [finish] copies
/// the same tiles again as they are then, and hands both over as a
/// [TerrainPatch].
class TerrainRecorder {
  TerrainRecorder(
    this.terrain,
    Set<TerrainLayer> layers, {
    int tileSize = TerrainPatch.defaultTileSize,
  }) : layers = Set.unmodifiable(layers),
       tileSize = math.min(tileSize, terrain.regionSize) {
    if (!TerrainRegion.validSize(this.tileSize)) {
      throw ArgumentError.value(
        tileSize,
        'tileSize',
        'A tile is a power of two texels across.',
      );
    }
  }

  final Terrain terrain;

  final Set<TerrainLayer> layers;

  final int tileSize;

  final Map<_TileKey, _Tile> _before = {};

  /// Copies every tile holding a texel from ([i0], [j0]) to ([i1], [j1]) of
  /// the whole terrain, both corners included, that is not copied already.
  /// Tiles where there is no region are left out: nothing can be written
  /// there.
  void keep(int i0, int j0, int i1, int j1) {
    for (var z = floorDiv(j0, tileSize); z <= floorDiv(j1, tileSize); z++) {
      for (var x = floorDiv(i0, tileSize); x <= floorDiv(i1, tileSize); x++) {
        final key = _TileKey(x, z);
        if (_before.containsKey(key)) continue;
        final region = _regionOf(key);
        if (region == null) continue;
        _before[key] = _Tile.copy(
          region,
          key.cornerIn(region, tileSize),
          tileSize,
          layers,
        );
      }
    }
  }

  /// What was kept, before and as it is now.
  TerrainPatch finish() {
    final before = <_TileKey, _Tile>{};
    final after = <_TileKey, _Tile>{};
    for (final MapEntry(:key, :value) in _before.entries) {
      // A region taken out between keeping and finishing leaves nothing to
      // compare, so its tiles are not part of the edit.
      final region = _regionOf(key);
      if (region == null) continue;
      before[key] = value;
      after[key] = _Tile.copy(
        region,
        key.cornerIn(region, tileSize),
        tileSize,
        layers,
      );
    }
    return TerrainPatch._(tileSize, terrain.regionSize, layers, before, after);
  }

  TerrainRegion? _regionOf(_TileKey key) =>
      terrain.regionAt(key.region(tileSize, terrain.regionSize));
}

/// Which tile, counted in tiles across the whole terrain.
final class _TileKey {
  const _TileKey(this.x, this.z);

  final int x;
  final int z;

  RegionKey region(int tileSize, int regionSize) =>
      RegionKey.containing(x * tileSize, z * tileSize, regionSize);

  /// The texel within [region] where this tile starts.
  (int, int) cornerIn(TerrainRegion region, int tileSize) => (
    x * tileSize - region.key.x * region.size,
    z * tileSize - region.key.z * region.size,
  );

  @override
  bool operator ==(Object other) =>
      other is _TileKey && other.x == x && other.z == z;

  @override
  int get hashCode => Object.hash(x, z);
}

/// One tile's maps, the ones an edit touched.
final class _Tile {
  const _Tile({this.heights, this.cover, this.colour});

  factory _Tile.copy(
    TerrainRegion region,
    (int, int) corner,
    int size,
    Set<TerrainLayer> layers,
  ) {
    final (i0, j0) = corner;
    final heights = layers.contains(TerrainLayer.height)
        ? Float32List(size * size)
        : null;
    final cover = layers.contains(TerrainLayer.cover)
        ? Uint32List(size * size)
        : null;
    final colour = layers.contains(TerrainLayer.colour)
        ? Uint8List(size * size * 4)
        : null;
    for (var row = 0; row < size; row++) {
      final from = (j0 + row) * region.size + i0;
      final to = row * size;
      heights?.setRange(to, to + size, region.heights, from);
      cover?.setRange(to, to + size, region.cover, from);
      colour?.setRange(to * 4, (to + size) * 4, region.colour, from * 4);
    }
    return _Tile(heights: heights, cover: cover, colour: colour);
  }

  final Float32List? heights;
  final Uint32List? cover;
  final Uint8List? colour;

  int get byteCount =>
      (heights?.lengthInBytes ?? 0) +
      (cover?.lengthInBytes ?? 0) +
      (colour?.lengthInBytes ?? 0);

  /// This tile's maps, and [other]'s for any this one lacks. Two strokes of
  /// different tools can touch one tile: the heights one kept and the cover
  /// the other did are both part of it.
  _Tile joined(_Tile other) => _Tile(
    heights: heights ?? other.heights,
    cover: cover ?? other.cover,
    colour: colour ?? other.colour,
  );

  void writeInto(TerrainRegion region, (int, int) corner, int size) {
    final (i0, j0) = corner;
    final heights = this.heights;
    final cover = this.cover;
    final colour = this.colour;
    for (var row = 0; row < size; row++) {
      final to = (j0 + row) * region.size + i0;
      final from = row * size;
      if (heights != null) {
        region.heights.setRange(to, to + size, heights, from);
      }
      if (cover != null) region.cover.setRange(to, to + size, cover, from);
      if (colour != null) {
        region.colour.setRange(to * 4, (to + size) * 4, colour, from * 4);
      }
    }
  }
}
