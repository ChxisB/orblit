import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:test/test.dart';

import 'tiled_fixture.dart';

void main() {
  final grass = tiledSet('grass', firstId: 1);

  TileMap? read(List<Map<String, Object?>> layers) =>
      TileMap.read(tiledMap(layers: layers, tilesets: [grass], infinite: true));

  // Two 2 by 2 chunks, one up and left of the origin and one at it.
  final chunks = tiledChunks('ground', 2, [
    (x: -2, y: -2, data: [1, 2, 3, 4]),
    (x: 0, y: 0, data: [5, 0, 0, 6]),
  ]);

  group('an infinite map', () {
    test('keeps each tile at the cell its chunk puts it in', () {
      final ground = read([chunks])!.layer('ground')!;

      expect(ground.at(-2, -2), 1);
      expect(ground.at(-1, -2), 2);
      expect(ground.at(-2, -1), 3);
      expect(ground.at(-1, -1), 4);
      expect(ground.at(0, 0), 5);
      expect(ground.at(1, 1), 6);
    });

    test('the cells between the chunks are empty', () {
      final ground = read([chunks])!.layer('ground')!;

      expect(ground.at(0, -1), 0);
      expect(ground.at(-1, 0), 0);
      expect(ground.at(1, 0), 0);
      expect(ground.at(0, 1), 0);
    });

    test('outside every chunk is empty rather than an error', () {
      final ground = read([chunks])!.layer('ground')!;

      expect(ground.at(2, 2), 0);
      expect(ground.at(-3, -3), 0);
      expect(ground.at(100, -100), 0);
      // Just past one edge with the other coordinate inside the layer.
      expect(ground.at(2, -2), 0);
      expect(ground.at(-2, 2), 0);
      expect(ground.at(-3, 0), 0);
      expect(ground.at(0, -3), 0);
    });

    test('the layer starts where its first cell is', () {
      final ground = read([chunks])!.layer('ground')!;

      expect((ground.left, ground.top), (-2, -2));
      expect((ground.width, ground.height), (4, 4));
    });

    test('chunks in any order give the same layer', () {
      final reversed = tiledChunks('ground', 2, [
        (x: 0, y: 0, data: [5, 0, 0, 6]),
        (x: -2, y: -2, data: [1, 2, 3, 4]),
      ]);

      expect(
        read([reversed])!.layer('ground')!.tiles,
        read([chunks])!.layer('ground')!.tiles,
      );
    });

    test('a map asks about its cells the same way', () {
      final map = read([chunks])!;

      expect(map.isSolidAt(-2, -2), isTrue);
      expect(map.isSolidAt(-1, 0), isFalse);
      expect(map.cellAt(-16, -32), (-1, -2));
      expect(map.setFor(map.layer('ground')!.at(-1, -1))!.name, 'grass');
    });

    test('the size a layer writes is not believed', () {
      // Tiled writes a size and a start for an infinite layer, and either
      // can be out of date after the chunks were edited.
      final stale = {...chunks, 'width': 99, 'height': 99, 'startx': 7};
      final ground = read([stale])!.layer('ground')!;

      expect((ground.left, ground.width), (-2, 4));
      expect(ground.at(1, 1), 6);
    });

    test('a layer with no chunks is empty, not unreadable', () {
      final map = read([tiledChunks('ground', 2, const [])]);

      expect(map, isNotNull);
      expect(map!.layer('ground')!.at(0, 0), 0);
      expect(map.layer('ground')!.width, 0);
    });

    test('a chunk with fewer tiles than its size is not read', () {
      // A map that silently lost a floor is worse than one that did not load.
      final short = tiledChunks('ground', 2, [
        (x: 0, y: 0, data: [1, 2, 3]),
      ]);

      expect(read([short]), isNull);
    });

    test('a chunk of negative size is not read', () {
      final upside = {
        ...chunks,
        'chunks': [
          {
            'x': 0,
            'y': 0,
            'width': -2,
            'height': 2,
            'data': [1, 2],
          },
        ],
      };

      expect(read([upside]), isNull);
    });

    test('a chunk with no tiles of its own is not read', () {
      final bare = {
        ...chunks,
        'chunks': [
          {'x': 0, 'y': 0, 'width': 2, 'height': 2},
        ],
      };

      expect(read([bare]), isNull);
    });

    test('a chunk that is not an object is not read', () {
      expect(
        read([
          {
            ...chunks,
            'chunks': [1],
          },
        ]),
        isNull,
      );
    });

    test('chunks of base64 data are not read, like any other layer', () {
      final packed = {...chunks, 'encoding': 'base64'};

      expect(read([packed]), isNull);
    });

    test('chunks too far apart are not read', () {
      // One stray chunk would otherwise ask for memory for all the space
      // between it and the rest.
      final far = tiledChunks('ground', 2, [
        (x: 0, y: 0, data: [1, 2, 3, 4]),
        (x: 100000, y: 100000, data: [1, 2, 3, 4]),
      ]);

      expect(read([far]), isNull);
    });
  });
}
