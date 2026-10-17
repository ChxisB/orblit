import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:test/test.dart';

import 'tiled_fixture.dart';

// The three flip bits as Tiled writes them above a tile's number.
const horizontal = 0x80000000;
const vertical = 0x40000000;
const diagonal = 0x20000000;

void main() {
  // Tile 1 and tile 9 are the first tiles of the grass and stone sets.
  final tilesets = [
    tiledSet('grass', firstId: 1),
    tiledSet('stone', firstId: 9),
  ];

  TileMap read(List<int> data) => TileMap.read(
    tiledMap(layers: [tiledLayer('ground', 2, data)], tilesets: tilesets),
  )!;

  group('a flipped tile', () {
    test('is looked up by its number alone', () {
      final map = read([1 | horizontal, 2 | vertical, 3 | diagonal, 9]);
      final ground = map.layer('ground')!;

      expect(ground.at(0, 0), 1);
      expect(ground.at(1, 0), 2);
      expect(ground.at(0, 1), 3);
      expect(ground.at(1, 1), 9);
    });

    test('still comes from the set its number belongs to', () {
      // A flag read as part of the number picks the last set for everything.
      final map = read([9 | horizontal, 1 | horizontal | vertical, 0, 0]);
      final ground = map.layer('ground')!;

      expect(map.setFor(ground.at(0, 0))!.name, 'stone');
      expect(map.setFor(ground.at(1, 0))!.name, 'grass');
      expect(map.setFor(ground.at(0, 0))!.rectOf(ground.at(0, 0)), (
        x: 0,
        y: 0,
        width: 16,
        height: 16,
      ));
    });

    test('reports which way it is turned', () {
      final ground = read([
        1 | horizontal,
        2 | vertical,
        3 | diagonal,
        4 | horizontal | vertical | diagonal,
      ]).layer('ground')!;

      expect(ground.flipsAt(0, 0), (
        horizontal: true,
        vertical: false,
        diagonal: false,
      ));
      expect(ground.flipsAt(1, 0), (
        horizontal: false,
        vertical: true,
        diagonal: false,
      ));
      expect(ground.flipsAt(0, 1), (
        horizontal: false,
        vertical: false,
        diagonal: true,
      ));
      expect(ground.flipsAt(1, 1), (
        horizontal: true,
        vertical: true,
        diagonal: true,
      ));
    });

    test('a tile that is not turned says so', () {
      final ground = read([1, 0, 0, 0]).layer('ground')!;

      const none = (horizontal: false, vertical: false, diagonal: false);
      expect(ground.flipsAt(0, 0), none);
      expect(ground.flipsAt(1, 1), none);
      expect(ground.flipsAt(-1, 0), none);
      expect(ground.flipsAt(5, 5), none);
    });

    test('is solid, and a flag with no tile is not', () {
      final map = read([1 | horizontal, horizontal, 0, 0]);

      expect(map.isSolidAt(0, 0), isTrue);
      expect(map.isSolidAt(1, 0), isFalse);
      expect(map.layer('ground')!.isEmptyAt(1, 0), isTrue);
    });

    test('keeps its flips in an infinite map', () {
      final map = TileMap.read(
        tiledMap(
          layers: [
            tiledChunks('ground', 2, [
              (x: -2, y: 0, data: [1 | vertical, 0, 0, 2 | diagonal]),
            ]),
          ],
          tilesets: tilesets,
          infinite: true,
        ),
      )!;
      final ground = map.layer('ground')!;

      expect(ground.at(-2, 0), 1);
      expect(ground.flipsAt(-2, 0).vertical, isTrue);
      expect(ground.at(-1, 1), 2);
      expect(ground.flipsAt(-1, 1).diagonal, isTrue);
    });
  });
}
