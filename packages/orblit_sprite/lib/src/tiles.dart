import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

// The top three bits of a tile number say how the tile is turned. The rest
// is the number.
const _flipHorizontal = 0x80000000;
const _flipVertical = 0x40000000;
const _flipDiagonal = 0x20000000;
const _idMask = 0x1FFFFFFF;

// shortcut: an infinite layer is one dense grid over all its chunks, so chunks
// far apart cost the empty space between them. Upgrade to keyed chunks when a
// map needs more than this.
const _maxCells = 1 << 22;

/// How a tile is turned. Swap the x and y axes first when [diagonal] is set,
/// then mirror across [horizontal] and [vertical].
typedef TileFlips = ({bool horizontal, bool vertical, bool diagonal});

/// One layer of a tile map.
class TileLayer {
  const TileLayer({
    required this.name,
    required this.width,
    required this.height,
    required this.tiles,
    this.left = 0,
    this.top = 0,
    this.visible = true,
    this.opacity = 1,
  });

  final String name;
  final int width;
  final int height;

  /// The cell the first tile sits in. Zero for a map of fixed size. An
  /// infinite map's layer starts wherever its first chunk does, and that can
  /// be left of or above the origin.
  final int left;
  final int top;

  /// The tile at each cell, in reading order. Zero is nothing.
  ///
  /// Zero rather than -1 because that is what every editor writes, and
  /// translating it on the way in is one more place for an off-by-one. A
  /// number may carry the flip bits of [flipsAt], so read it with [at].
  final List<int> tiles;

  final bool visible;
  final double opacity;

  /// The tile at a cell, or zero outside the map. The flip bits are left off.
  ///
  /// Outside is empty rather than an error: a map is walked by things that
  /// wander off the edge of it, and every caller checking the bounds itself
  /// is every caller getting it slightly wrong.
  int at(int x, int y) => _raw(x, y) & _idMask;

  bool isEmptyAt(int x, int y) => at(x, y) == 0;

  /// How the tile at a cell is turned. Not turned outside the map.
  TileFlips flipsAt(int x, int y) {
    final raw = _raw(x, y);
    return (
      horizontal: (raw & _flipHorizontal) != 0,
      vertical: (raw & _flipVertical) != 0,
      diagonal: (raw & _flipDiagonal) != 0,
    );
  }

  int _raw(int x, int y) {
    final column = x - left;
    final row = y - top;
    if (column < 0 || row < 0 || column >= width || row >= height) return 0;
    return tiles[row * width + column];
  }
}

/// Which image a tile number comes from, and where in it.
class Tileset {
  const Tileset({
    required this.name,
    required this.image,
    required this.firstId,
    required this.tileWidth,
    required this.tileHeight,
    required this.columns,
    this.count = 0,
    this.spacing = 0,
    this.margin = 0,
  });

  final String name;
  final String image;

  /// The number the first tile of this set has in the map.
  ///
  /// A map with several tilesets numbers them end to end, so which set a tile
  /// belongs to is worked out by finding the last set whose first id is not
  /// above it. Getting that backwards draws the right shape from the wrong
  /// sheet, which reads as a corrupt map rather than a lookup fault.
  final int firstId;

  final int tileWidth;
  final int tileHeight;
  final int columns;
  final int count;
  final int spacing;
  final int margin;

  bool has(int id) => id >= firstId && (count == 0 || id < firstId + count);

  /// Where tile [id] sits in this set's image, in pixels.
  ({int x, int y, int width, int height})? rectOf(int id) {
    if (!has(id) || columns <= 0) return null;
    final local = id - firstId;
    final column = local % columns;
    final row = local ~/ columns;
    return (
      x: margin + column * (tileWidth + spacing),
      y: margin + row * (tileHeight + spacing),
      width: tileWidth,
      height: tileHeight,
    );
  }
}

/// A map of tiles, as an editor wrote it.
///
/// Read rather than reinvented. Tiled is what people already use, its format
/// is stable, and a bespoke one would mean an editor to go with it — so this
/// takes the subset that matters and says plainly what it ignores.
class TileMap {
  const TileMap({
    required this.width,
    required this.height,
    required this.tileWidth,
    required this.tileHeight,
    required this.layers,
    required this.tilesets,
    this.properties = const {},
  });

  /// In tiles.
  final int width;
  final int height;

  /// In pixels.
  final int tileWidth;
  final int tileHeight;

  final List<TileLayer> layers;
  final List<Tileset> tilesets;

  /// Whatever the map carried, unchanged.
  final Map<String, Object?> properties;

  TileLayer? layer(String name) {
    for (final found in layers) {
      if (found.name == name) return found;
    }
    return null;
  }

  /// Which set tile [id] comes from.
  Tileset? setFor(int id) {
    if (id <= 0) return null;
    Tileset? best;
    for (final set in tilesets) {
      if (set.firstId <= id && (best == null || set.firstId > best.firstId)) {
        best = set;
      }
    }
    return best;
  }

  /// Where tile [id] is drawn when it sits in cell ([x], [y]), in world
  /// pixels, or null when no set holds it.
  ///
  /// A tile is anchored to the bottom left of its cell. One taller or wider
  /// than the grid grows up and to the right and overlaps its neighbours.
  ({int x, int y, int width, int height})? placeOf(int id, int x, int y) {
    final source = setFor(id)?.rectOf(id);
    if (source == null) return null;
    return (
      x: x * tileWidth,
      y: (y + 1) * tileHeight - source.height,
      width: source.width,
      height: source.height,
    );
  }

  /// The cell a point in world pixels falls in.
  (int, int) cellAt(double x, double y) =>
      ((x / tileWidth).floor(), (y / tileHeight).floor());

  /// Whether any layer has something at a cell.
  bool isSolidAt(int x, int y, {Set<String>? only}) {
    for (final layer in layers) {
      if (only != null && !only.contains(layer.name)) continue;
      if (!layer.visible) continue;
      if (layer.at(x, y) != 0) return true;
    }
    return false;
  }

  /// Reads a Tiled map saved as JSON.
  ///
  /// The orthogonal, uncompressed subset — which is what the editor writes by
  /// default and covers nearly every 2D game. What it does not read is said
  /// out loud rather than failing quietly: isometric and hexagonal maps,
  /// base64 or zlib layer data, object layers, and external tileset files.
  /// Any of those come back as null, so a caller knows to look rather than
  /// wondering why the map is empty. So does a layer with fewer tiles than
  /// its size, and an infinite map whose chunks span more than four million
  /// cells.
  ///
  /// An infinite map's chunks are joined into one grid per layer. It has no
  /// fixed size, so [TileMap.width] and [TileMap.height] are only what the
  /// file says. Ask a layer for its extent.
  static TileMap? read(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException {
      return null;
    }
    if (parsed is! Map<String, Object?>) return null;

    // Anything but a plain grid is a different reader, and pretending
    // otherwise draws a map at the wrong angle.
    final orientation = parsed['orientation'];
    if (orientation is String && orientation != 'orthogonal') return null;

    final layers = <TileLayer>[];
    final rawLayers = parsed['layers'];
    if (rawLayers is! List) return null;

    for (final entry in rawLayers) {
      if (entry is! Map) continue;
      final map = entry.cast<String, Object?>();
      if (map['type'] != 'tilelayer') continue;

      final layer = _readLayer(map);
      if (layer == null) return null;
      layers.add(layer);
    }

    final tilesets = <Tileset>[];
    final rawSets = parsed['tilesets'];
    if (rawSets is List) {
      for (final entry in rawSets) {
        if (entry is! Map) continue;
        final map = entry.cast<String, Object?>();
        // An external tileset is a second file this was not given.
        if (map['source'] != null) return null;

        tilesets.add(
          Tileset(
            name: map['name'] is String ? map['name']! as String : '',
            image: map['image'] is String ? map['image']! as String : '',
            firstId: _int(map['firstgid']),
            tileWidth: _int(map['tilewidth']),
            tileHeight: _int(map['tileheight']),
            columns: _int(map['columns']),
            count: _int(map['tilecount']),
            spacing: _int(map['spacing']),
            margin: _int(map['margin']),
          ),
        );
      }
    }

    return TileMap(
      width: _int(parsed['width']),
      height: _int(parsed['height']),
      tileWidth: _int(parsed['tilewidth']),
      tileHeight: _int(parsed['tileheight']),
      layers: layers,
      tilesets: tilesets,
      properties: parsed['properties'] is Map
          ? (parsed['properties']! as Map).cast<String, Object?>()
          : const {},
    );
  }
}

/// Null when the layer is in a form this does not read.
TileLayer? _readLayer(Map<String, Object?> map) {
  // Compressed or base64 data is a decoder this does not have. Returning
  // an empty layer would be a map that silently lost a floor.
  final encoding = map['encoding'];
  if (encoding != null && encoding != 'csv') return null;
  if (map['compression'] != null && map['compression'] != '') return null;

  final chunks = map['chunks'];
  final cells = chunks is List ? _readChunks(chunks) : _readCells(map);
  if (cells == null) return null;

  return TileLayer(
    name: map['name'] is String ? map['name']! as String : '',
    width: cells.width,
    height: cells.height,
    tiles: cells.tiles,
    left: cells.left,
    top: cells.top,
    visible: map['visible'] != false,
    opacity: map['opacity'] is num ? (map['opacity']! as num).toDouble() : 1.0,
  );
}

typedef _Cells = ({int left, int top, int width, int height, List<int> tiles});

/// The grid a layer or a chunk holds, or null when its data is missing or
/// shorter than its size.
_Cells? _readCells(Map<String, Object?> map) {
  final data = map['data'];
  final width = _int(map['width']);
  final height = _int(map['height']);
  if (data is! List || width < 0 || height < 0) return null;
  if (data.length < width * height) return null;
  return (
    left: _int(map['x']),
    top: _int(map['y']),
    width: width,
    height: height,
    tiles: [for (final tile in data) _int(tile)],
  );
}

/// One grid over the chunks of an infinite layer. Chunks may sit at
/// negative cells, and the cells between them are empty.
_Cells? _readChunks(List<Object?> raw) {
  final chunks = <_Cells>[];
  for (final entry in raw) {
    final chunk = entry is Map
        ? _readCells(entry.cast<String, Object?>())
        : null;
    if (chunk == null) return null;
    chunks.add(chunk);
  }
  if (chunks.isEmpty) return (left: 0, top: 0, width: 0, height: 0, tiles: []);

  var left = chunks.first.left;
  var top = chunks.first.top;
  var right = left;
  var bottom = top;
  for (final chunk in chunks) {
    left = math.min(left, chunk.left);
    top = math.min(top, chunk.top);
    right = math.max(right, chunk.left + chunk.width);
    bottom = math.max(bottom, chunk.top + chunk.height);
  }
  final width = right - left;
  final height = bottom - top;
  if (width * height > _maxCells) return null;

  final tiles = Uint32List(width * height);
  for (final chunk in chunks) {
    for (var row = 0; row < chunk.height; row++) {
      final start = (chunk.top - top + row) * width + chunk.left - left;
      tiles.setRange(
        start,
        start + chunk.width,
        chunk.tiles,
        row * chunk.width,
      );
    }
  }
  return (left: left, top: top, width: width, height: height, tiles: tiles);
}

int _int(Object? value) => value is num ? value.round() : 0;
