import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

void main() {
  group('RegionKey', () {
    test('rounds down, not towards zero', () {
      expect(RegionKey.containing(0, 0, 256), const RegionKey(0, 0));
      expect(RegionKey.containing(255, 256, 256), const RegionKey(0, 1));
      expect(RegionKey.containing(-1, -256, 256), const RegionKey(-1, -1));
      expect(RegionKey.containing(-257, 0, 256), const RegionKey(-2, 0));
    });

    test('names a file it can be read back from', () {
      const key = RegionKey(-3, 12);
      expect(key.fileName, 'x-3_z12.oregion');
      expect(RegionKey.fromFileName(key.fileName), key);
      expect(RegionKey.fromFileName('x1_z2.png'), isNull);
      expect(RegionKey.fromFileName('terrain.oterrain'), isNull);
    });
  });

  group('TerrainRegion', () {
    test('is flat, automatic and uncoloured when new', () {
      final region = TerrainRegion(const RegionKey(0, 0), 4);
      expect(region.heights, everyElement(0));
      expect(region.cover, everyElement(Cover.auto.word));
      expect(region.colourAt(3, 3), GroundColour.none);
    });

    test('refuses a size that is not a power of two', () {
      expect(
        () => TerrainRegion(const RegionKey(0, 0), 6),
        throwsArgumentError,
      );
      expect(
        () => TerrainRegion(const RegionKey(0, 0), 1),
        throwsArgumentError,
      );
      expect(
        () => TerrainRegion(const RegionKey(0, 0), 4, heights: Float32List(15)),
        throwsArgumentError,
      );
    });

    test('stores texel (i, j) at j × size + i', () {
      final region = TerrainRegion(const RegionKey(0, 0), 4)
        ..setHeight(1, 2, 5);
      expect(region.heights[2 * 4 + 1], 5);
      expect(region.heightAt(1, 2), 5);
      expect(() => region.heightAt(4, 0), throwsRangeError);
    });

    test('a revision is new for every change and every region', () {
      final a = TerrainRegion(const RegionKey(0, 0), 2);
      final b = TerrainRegion(const RegionKey(0, 0), 2);
      expect(a.revision, isNot(b.revision));
      final before = a.revision;
      a.setCover(0, 0, Cover.plain);
      expect(a.revision, greaterThan(before));
      expect(a.revision, greaterThan(b.revision));
    });

    test('measures its height range, again after a touch', () {
      final region = TerrainRegion(const RegionKey(0, 0), 2)
        ..setHeight(0, 0, -3)
        ..setHeight(1, 1, 7);
      expect((region.minHeight, region.maxHeight), (-3, 7));
      region.heights[1] = 20;
      expect(region.maxHeight, 7, reason: 'not told yet');
      region.touch();
      expect(region.maxHeight, 20);
    });
  });

  group('region files', () {
    TerrainRegion painted() {
      final region = TerrainRegion(const RegionKey(-2, 5), 4);
      for (var n = 0; n < 16; n++) {
        region.heights[n] = n * 1.25 - 7;
        region.cover[n] = Cover.of(
          base: n,
          overlay: 31 - n,
          blend: n / 15,
        ).word;
      }
      region.setColour(2, 1, GroundColour.of(red: 90, roughness: 0.5));
      return region;
    }

    test('come back as they went', () {
      final region = painted();
      final load = TerrainRegion.decode(region.encode());
      expect(load.problems, isEmpty);
      expect(load.region.key, region.key);
      expect(load.region.size, 4);
      expect(load.region.heights, region.heights);
      expect(load.region.cover, region.cover);
      expect(load.region.colour, region.colour);
    });

    test('leave out maps still at their defaults', () {
      final bytes = TerrainRegion(const RegionKey(0, 0), 256).encode();
      expect(bytes.length, lessThan(128));
      final load = TerrainRegion.decode(bytes);
      expect(load.region.size, 256);
      expect(load.region.cover.first, Cover.auto.word);
    });

    test('start with a line saying what follows, then aligned maps', () {
      final region = TerrainRegion(const RegionKey(0, 0), 2)
        ..setHeight(0, 0, 1);
      final bytes = region.encode();
      final end = bytes.indexOf(0x0A);
      final header =
          jsonDecode(utf8.decode(bytes.sublist(0, end)))
              as Map<String, Object?>;
      expect(header['kind'], 'orblit.region');
      expect(header['formatVersion'], 1);
      expect(header['maps'], [
        {'name': 'height', 'bytes': 16},
      ]);
      final start = (end + 4) & ~3;
      expect(start % 4, 0);
      expect(bytes.length, start + 16);
      // 1.0 as a little-endian float.
      expect(bytes.sublist(start, start + 4), [0x00, 0x00, 0x80, 0x3F]);
    });

    test('refuse what is not a region', () {
      expect(
        () => TerrainRegion.decode(Uint8List.fromList(utf8.encode('hello'))),
        throwsA(isA<RegionFormatException>()),
      );
      expect(
        () => TerrainRegion.decode(
          Uint8List.fromList(utf8.encode('{"kind":"orblit.clip"}\n')),
        ),
        throwsA(isA<RegionFormatException>()),
      );
    });

    test('refuse one from a newer Orblit', () {
      final text =
          '{"kind":"orblit.region","formatVersion":99,'
          '"x":0,"z":0,"size":2,"maps":[]}\n';
      expect(
        () => TerrainRegion.decode(Uint8List.fromList(utf8.encode(text))),
        throwsA(
          isA<RegionFormatException>().having(
            (error) => error.message,
            'message',
            contains('newer'),
          ),
        ),
      );
    });

    test('refuse a map cut short or the wrong size', () {
      final bytes = painted().encode();
      expect(
        () => TerrainRegion.decode(Uint8List.sublistView(bytes, 0, 200)),
        throwsA(isA<RegionFormatException>()),
      );
      final text =
          '{"kind":"orblit.region","formatVersion":1,'
          '"x":0,"z":0,"size":4,"maps":[{"name":"height","bytes":8}]}\n';
      final header = utf8.encode(text);
      final wrong = Uint8List((header.length + 3 & ~3) + 8)..setAll(0, header);
      expect(
        () => TerrainRegion.decode(wrong),
        throwsA(isA<RegionFormatException>()),
      );
    });

    test('leave out a map they do not know, with a note', () {
      final text =
          '{"kind":"orblit.region","formatVersion":1,'
          '"x":0,"z":0,"size":2,"maps":[{"name":"wetness","bytes":4}]}\n';
      final header = utf8.encode(text);
      final bytes = Uint8List((header.length + 3 & ~3) + 4)..setAll(0, header);
      final load = TerrainRegion.decode(bytes);
      expect(load.problems, [contains('wetness')]);
      expect(load.region.heights, everyElement(0));
    });
  });
}
