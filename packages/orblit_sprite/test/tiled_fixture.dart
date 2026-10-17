import 'dart:convert';

/// A tile set as Tiled writes it into a map: [tileWidth] by [tileHeight]
/// tiles, four to a row.
Map<String, Object?> tiledSet(
  String name, {
  required int firstId,
  int tileWidth = 16,
  int tileHeight = 16,
}) => {
  'firstgid': firstId,
  'name': name,
  'image': '$name.png',
  'imagewidth': tileWidth * 4,
  'imageheight': tileHeight * 2,
  'tilewidth': tileWidth,
  'tileheight': tileHeight,
  'columns': 4,
  'tilecount': 8,
  'margin': 0,
  'spacing': 0,
};

/// An orthogonal map in the layout Tiled saves as JSON, on a 16 pixel grid.
String tiledMap({
  required List<Map<String, Object?>> layers,
  required List<Map<String, Object?>> tilesets,
  bool infinite = false,
}) => jsonEncode({
  'type': 'map',
  'version': '1.10',
  'orientation': 'orthogonal',
  'renderorder': 'right-down',
  'infinite': infinite,
  'width': 4,
  'height': 4,
  'tilewidth': 16,
  'tileheight': 16,
  'layers': layers,
  'tilesets': tilesets,
});

/// A tile layer of a map of fixed size.
Map<String, Object?> tiledLayer(String name, int width, List<int> data) => {
  'type': 'tilelayer',
  'name': name,
  'x': 0,
  'y': 0,
  'width': width,
  'height': data.length ~/ width,
  'opacity': 1,
  'visible': true,
  'data': data,
};

/// A tile layer of an infinite map, as chunks of [size] by [size] cells.
/// Each chunk is its corner cell and its tiles.
Map<String, Object?> tiledChunks(
  String name,
  int size,
  List<({int x, int y, List<int> data})> chunks,
) => {
  'type': 'tilelayer',
  'name': name,
  'x': 0,
  'y': 0,
  'opacity': 1,
  'visible': true,
  'chunks': [
    for (final chunk in chunks)
      {
        'x': chunk.x,
        'y': chunk.y,
        'width': size,
        'height': size,
        'data': chunk.data,
      },
  ],
};
