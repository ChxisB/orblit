import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:test/test.dart';

import 'tiled_fixture.dart';

void main() {
  // A 16 pixel grid with a set of 16 pixel tiles, a set of 32 by 48 pixel
  // trees and a set of 8 pixel flowers.
  final tilesets = [
    tiledSet('grass', firstId: 1),
    tiledSet('trees', firstId: 9, tileWidth: 32, tileHeight: 48),
    tiledSet('flowers', firstId: 17, tileWidth: 8, tileHeight: 8),
  ];

  final map = TileMap.read(
    tiledMap(
      layers: [
        tiledLayer('ground', 2, [1, 9, 17, 0]),
      ],
      tilesets: tilesets,
    ),
  )!;

  group('a tile larger than the grid', () {
    test('is cut from its set at its own size', () {
      expect(map.setFor(9)!.rectOf(10), (x: 32, y: 0, width: 32, height: 48));
    });

    test('is drawn from the bottom left of its cell', () {
      // Cell (2, 3) spans 32 to 48 across and 48 to 64 down. The tree grows
      // up from 64 and out to the right.
      expect(map.placeOf(9, 2, 3), (x: 32, y: 16, width: 32, height: 48));
    });

    test('a tile the size of the grid fills its cell', () {
      expect(map.placeOf(1, 2, 3), (x: 32, y: 48, width: 16, height: 16));
    });

    test('a tile smaller than the grid sits in the bottom left corner', () {
      expect(map.placeOf(17, 2, 3), (x: 32, y: 56, width: 8, height: 8));
    });

    test('is drawn above the row it overlaps', () {
      // The tree in the top row has its base on that row, so it starts
      // above the map.
      expect(map.placeOf(9, 0, 0), (x: 0, y: -32, width: 32, height: 48));
    });

    test('is placed by cells left of and above the origin', () {
      expect(map.placeOf(9, -1, -1), (x: -16, y: -48, width: 32, height: 48));
    });

    test('has no place when no set holds it', () {
      expect(map.placeOf(0, 0, 0), isNull);
      expect(map.placeOf(25, 0, 0), isNull);
      expect(map.placeOf(-1, 0, 0), isNull);
    });
  });
}
