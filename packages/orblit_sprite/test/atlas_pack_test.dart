import 'dart:typed_data';

import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:test/test.dart';

/// A solid rectangle of one colour, fully opaque.
Uint8List _solid(int w, int h, int r, int g, int b, int a) {
  final out = Uint8List(w * h * 4);
  for (var i = 0; i < w * h; i++) {
    out[i * 4] = r;
    out[i * 4 + 1] = g;
    out[i * 4 + 2] = b;
    out[i * 4 + 3] = a;
  }
  return out;
}

/// A picture with no two pixels alike, so a rotation that lands on its side
/// or comes out mirrored shows up as a pixel mismatch rather than nothing at
/// all — a checkerboard or a flat colour can't tell a right rotation from a
/// wrong one.
Uint8List _asymmetricPattern(int w, int h) {
  final out = Uint8List(w * h * 4);
  for (var y = 0; y < h; y++) {
    for (var x = 0; x < w; x++) {
      final i = (y * w + x) * 4;
      out[i] = (x * 23 + 7) % 256;
      out[i + 1] = (y * 61 + 13) % 256;
      out[i + 2] = (x + y * 5 + 1) % 256;
      out[i + 3] = 255;
    }
  }
  return out;
}

/// A sprite [w] by [h] with a solid, asymmetric [inner] rectangle placed at
/// ([offsetX], [offsetY]) and every other texel fully transparent black —
/// what trimming is supposed to see through to and hand back unchanged.
Uint8List _withTransparentMargin(int w, int h, int offsetX, int offsetY, int innerW, int innerH) {
  final out = Uint8List(w * h * 4);
  final inner = _asymmetricPattern(innerW, innerH);
  for (var y = 0; y < innerH; y++) {
    for (var x = 0; x < innerW; x++) {
      final dst = ((offsetY + y) * w + (offsetX + x)) * 4;
      final src = (y * innerW + x) * 4;
      out[dst] = inner[src];
      out[dst + 1] = inner[src + 1];
      out[dst + 2] = inner[src + 2];
      out[dst + 3] = inner[src + 3];
    }
  }
  return out;
}

({int x, int y, int w, int h}) _physBox(Region r) =>
    (x: r.x, y: r.y, w: r.rotated ? r.height : r.width, h: r.rotated ? r.width : r.height);

bool _rectsOverlap(({int x, int y, int w, int h}) a, ({int x, int y, int w, int h}) b) =>
    a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;

void main() {
  group('packing rectangles', () {
    test('nothing overlaps, everything sits inside its page', () {
      final sprites = [
        for (var i = 0; i < 40; i++)
          AtlasSprite(
            name: 'sprite_$i',
            width: 6 + (i * 7) % 37,
            height: 6 + (i * 13) % 29,
            pixels: _solid(6 + (i * 7) % 37, 6 + (i * 13) % 29, i % 256, (i * 3) % 256, (i * 5) % 256, 255),
          ),
      ];
      final result = packAtlas(
        sprites,
        const AtlasPackOptions(maxPageSize: 256, padding: 3, border: 2, trim: false, mergeDuplicates: false),
      );
      expect(result.problems, isEmpty);

      for (final page in result.pages) {
        final boxes = [for (final region in page.regions.values) _physBox(region)];
        for (final box in boxes) {
          expect(box.x, greaterThanOrEqualTo(2));
          expect(box.y, greaterThanOrEqualTo(2));
          expect(box.x + box.w, lessThanOrEqualTo(page.width - 2));
          expect(box.y + box.h, lessThanOrEqualTo(page.height - 2));
        }
        for (var i = 0; i < boxes.length; i++) {
          for (var j = i + 1; j < boxes.length; j++) {
            expect(_rectsOverlap(boxes[i], boxes[j]), isFalse, reason: 'regions $i and $j overlap');
          }
        }
      }
    });

    test('padding keeps a gap between neighbours, not just no overlap', () {
      final sprites = [
        for (var i = 0; i < 12; i++)
          AtlasSprite(name: 'p$i', width: 10, height: 10, pixels: _solid(10, 10, 1, 2, 3, 255)),
      ];
      const padding = 4;
      final result = packAtlas(
        sprites,
        const AtlasPackOptions(maxPageSize: 128, padding: padding, border: 0, trim: false, mergeDuplicates: false),
      );
      for (final page in result.pages) {
        final boxes = [for (final region in page.regions.values) _physBox(region)];
        for (var i = 0; i < boxes.length; i++) {
          // Every box padded out on its own right and bottom edge, checked
          // against every other box, since that is exactly the space the
          // packer reserved for it.
          final grown = (x: boxes[i].x, y: boxes[i].y, w: boxes[i].w + padding, h: boxes[i].h + padding);
          for (var j = 0; j < boxes.length; j++) {
            if (i == j) continue;
            expect(_rectsOverlap(grown, boxes[j]), isFalse, reason: 'boxes $i and $j are closer than padding');
          }
        }
      }
    });

    test('a page never exceeds the maximum it was given', () {
      final sprites = [
        for (var i = 0; i < 200; i++)
          AtlasSprite(name: 's$i', width: 8, height: 8, pixels: _solid(8, 8, 9, 9, 9, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 64, trim: false));
      for (final page in result.pages) {
        expect(page.width, lessThanOrEqualTo(64));
        expect(page.height, lessThanOrEqualTo(64));
      }
    });
  });

  group('rotation', () {
    test('a rotated region reads back rotated the right way round', () {
      // A filler placed first leaves exactly one free strip behind it, too
      // narrow for the banner unless the banner turns on its side — so
      // whichever heuristic runs, only a correct rotation can have produced
      // a valid pack at all.
      final filler = AtlasSprite(name: 'filler', width: 6, height: 10, pixels: _solid(6, 10, 5, 5, 5, 255));
      final bannerPixels = _asymmetricPattern(8, 3);
      final banner = AtlasSprite(name: 'banner', width: 8, height: 3, pixels: bannerPixels);

      final result = packAtlas(
        [filler, banner],
        const AtlasPackOptions(
          maxPageSize: 10,
          minPageSize: 1,
          padding: 0,
          border: 0,
          trim: false,
          powerOfTwo: false,
          mergeDuplicates: false,
          heuristic: MaxRectsHeuristic.bestShortSideFit,
        ),
      );

      expect(result.problems, isEmpty);
      expect(result.pages, hasLength(1));
      final page = result.pages.single;
      final region = page.regions['banner']!;
      expect(region.rotated, isTrue, reason: 'the only free space left was too narrow unless it turned');
      expect(region.width, 8);
      expect(region.height, 3);

      final recovered = extractRegionPixels(
        pageWidth: page.width,
        pageHeight: page.height,
        pagePixels: page.pixels,
        region: region,
      );
      expect(recovered, equals(bannerPixels));
    });

    test('an unrotated region reads back exactly as packed', () {
      final pixels = _asymmetricPattern(5, 9);
      final sprite = AtlasSprite(name: 'plain', width: 5, height: 9, pixels: pixels);
      final result = packAtlas(
        [sprite],
        const AtlasPackOptions(maxPageSize: 32, padding: 0, border: 0, trim: false, allowRotation: false),
      );
      final page = result.pages.single;
      final region = page.regions['plain']!;
      expect(region.rotated, isFalse);
      final recovered = extractRegionPixels(
        pageWidth: page.width,
        pageHeight: page.height,
        pagePixels: page.pixels,
        region: region,
      );
      expect(recovered, equals(pixels));
    });
  });

  group('trimming', () {
    test('a trimmed sprite comes back at its original size and place', () {
      final original = _withTransparentMargin(20, 16, 5, 4, 9, 7);
      final sprite = AtlasSprite(name: 'card', width: 20, height: 16, pixels: original);
      final result = packAtlas(
        [sprite],
        const AtlasPackOptions(maxPageSize: 64, padding: 1, trim: true, allowRotation: false),
      );
      final page = result.pages.single;
      final region = page.regions['card']!;

      expect(region.trimmed, isTrue);
      expect(region.offsetX, 5);
      expect(region.offsetY, 4);
      expect(region.sourceWidth, 20);
      expect(region.sourceHeight, 16);
      expect(region.width, 9);
      expect(region.height, 7);
      expect(region.placedSize, (20, 16));

      final recovered = extractRegionPixels(
        pageWidth: page.width,
        pageHeight: page.height,
        pagePixels: page.pixels,
        region: region,
      );
      expect(recovered, equals(original));
    });

    test('a sprite with no transparent margin is not marked trimmed', () {
      final pixels = _solid(10, 10, 4, 5, 6, 255);
      final sprite = AtlasSprite(name: 'full', width: 10, height: 10, pixels: pixels);
      final result = packAtlas([sprite], const AtlasPackOptions(maxPageSize: 32, trim: true));
      final region = result.pages.single.regions['full']!;
      expect(region.trimmed, isFalse);
      expect(region.sourceWidth, 0);
      expect(region.sourceHeight, 0);
    });
  });

  group('extrusion', () {
    test('a placed sprite bleeds its edge colour into its padding', () {
      final sprite = AtlasSprite(name: 'chip', width: 4, height: 4, pixels: _solid(4, 4, 200, 100, 50, 255));
      final result = packAtlas(
        [sprite],
        const AtlasPackOptions(
          maxPageSize: 16,
          minPageSize: 16,
          padding: 3,
          border: 0,
          trim: false,
          extrude: 2,
          powerOfTwo: false,
        ),
      );
      final page = result.pages.single;
      final region = page.regions['chip']!;
      expect(region.rotated, isFalse);

      int at(int x, int y) => (y * page.width + x) * 4;
      final edgeColor = [
        page.pixels[at(region.x + 3, region.y)],
        page.pixels[at(region.x + 3, region.y + 1)],
        page.pixels[at(region.x + 3, region.y + 2)],
        page.pixels[at(region.x + 3, region.y + 3)],
      ];
      expect(edgeColor, everyElement(200));

      // One and two texels to the right of the sprite's own right edge, both
      // inside the padding, both extruded.
      final right1 = page.pixels.sublist(at(region.x + 4, region.y), at(region.x + 4, region.y) + 4);
      final right2 = page.pixels.sublist(at(region.x + 5, region.y), at(region.x + 5, region.y) + 4);
      expect(right1, [200, 100, 50, 255]);
      expect(right2, [200, 100, 50, 255]);

      final below1 = page.pixels.sublist(at(region.x, region.y + 4), at(region.x, region.y + 4) + 4);
      expect(below1, [200, 100, 50, 255]);
    });

    test('nothing is extruded when extrude is nought', () {
      final sprite = AtlasSprite(name: 'chip', width: 4, height: 4, pixels: _solid(4, 4, 200, 100, 50, 255));
      final result = packAtlas(
        [sprite],
        const AtlasPackOptions(maxPageSize: 16, minPageSize: 16, padding: 3, trim: false, extrude: 0, powerOfTwo: false),
      );
      final page = result.pages.single;
      final region = page.regions['chip']!;
      int at(int x, int y) => (y * page.width + x) * 4;
      final right = page.pixels.sublist(at(region.x + 4, region.y), at(region.x + 4, region.y) + 4);
      expect(right, [0, 0, 0, 0]);
    });
  });

  group('multiple pages', () {
    test('a full page overflows into another rather than growing without limit', () {
      final sprites = [
        for (var i = 0; i < 25; i++)
          AtlasSprite(name: 'tile_$i', width: 12, height: 12, pixels: _solid(12, 12, i % 256, (i * 3) % 256, (i * 7) % 256, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 32, padding: 1, trim: false));
      expect(result.problems, isEmpty);
      expect(result.pages.length, greaterThan(1));

      final placedNames = <String>{};
      for (final page in result.pages) {
        placedNames.addAll(page.regions.keys);
      }
      expect(placedNames, hasLength(25));
      for (final page in result.pages) {
        expect(page.width, lessThanOrEqualTo(32));
        expect(page.height, lessThanOrEqualTo(32));
      }
    });

    test('only the last page shrinks; earlier pages stay at the maximum', () {
      final sprites = [
        for (var i = 0; i < 25; i++)
          AtlasSprite(name: 'tile_$i', width: 12, height: 12, pixels: _solid(12, 12, i % 256, (i * 3) % 256, (i * 7) % 256, 255)),
      ];
      final result = packAtlas(
        sprites,
        const AtlasPackOptions(maxPageSize: 32, padding: 1, trim: false, powerOfTwo: false),
      );
      for (final page in result.pages.take(result.pages.length - 1)) {
        expect(page.width, 32);
        expect(page.height, 32);
      }
      final last = result.pages.last;
      expect(last.width <= 32 && last.height <= 32, isTrue);
    });

    test('an atlas set finds a region wherever its page is', () {
      final sprites = [
        for (var i = 0; i < 25; i++)
          AtlasSprite(name: 'tile_$i', width: 12, height: 12, pixels: _solid(12, 12, i % 256, (i * 3) % 256, (i * 7) % 256, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 32, padding: 1, trim: false));
      final set = result.toAtlasSet();
      expect(set.length, 25);
      final located = set.locate('tile_24');
      expect(located, isNotNull);
      expect(set.pages, contains(located!.atlas));
      expect(set.find('nonesuch'), isNull);
    });
  });

  group('determinism', () {
    test('the same input packs to the same pages every time', () {
      final sprites = [
        for (var i = 0; i < 60; i++)
          AtlasSprite(
            name: 'd_$i',
            width: 5 + (i * 3) % 20,
            height: 5 + (i * 7) % 16,
            pixels: _solid(5 + (i * 3) % 20, 5 + (i * 7) % 16, i % 256, 0, 0, 255),
          ),
      ];
      const options = AtlasPackOptions(maxPageSize: 128, padding: 2, trim: false);
      final a = packAtlas(sprites, options);
      final b = packAtlas(sprites, options);

      expect(a.pages.length, b.pages.length);
      for (var i = 0; i < a.pages.length; i++) {
        expect(a.pages[i].pixels, equals(b.pages[i].pixels));
        expect(a.pages[i].width, b.pages[i].width);
        expect(a.pages[i].height, b.pages[i].height);
      }
    });

    test('shuffling the input list does not change what is produced', () {
      final sprites = [
        for (var i = 0; i < 60; i++)
          AtlasSprite(
            name: 'd_$i',
            width: 5 + (i * 3) % 20,
            height: 5 + (i * 7) % 16,
            pixels: _solid(5 + (i * 3) % 20, 5 + (i * 7) % 16, i % 256, 0, 0, 255),
          ),
      ];
      final shuffled = [
        for (var i = sprites.length - 1; i >= 0; i--) sprites[i],
      ];
      const options = AtlasPackOptions(maxPageSize: 128, padding: 2, trim: false);
      final a = packAtlas(sprites, options);
      final b = packAtlas(shuffled, options);

      expect(a.pages.length, b.pages.length);
      for (var i = 0; i < a.pages.length; i++) {
        expect(a.pages[i].pixels, equals(b.pages[i].pixels));
      }
    });
  });

  group('duplicates', () {
    test('identical sprites share one packed rectangle', () {
      final pixels = _asymmetricPattern(6, 6);
      final sprites = [
        AtlasSprite(name: 'a', width: 6, height: 6, pixels: pixels),
        AtlasSprite(name: 'b', width: 6, height: 6, pixels: Uint8List.fromList(pixels)),
        AtlasSprite(name: 'c', width: 6, height: 6, pixels: _solid(6, 6, 9, 9, 9, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 64, trim: false));
      final page = result.pages.single;
      final a = page.regions['a']!;
      final b = page.regions['b']!;
      final c = page.regions['c']!;
      expect((a.x, a.y), (b.x, b.y));
      expect((a.x, a.y) == (c.x, c.y), isFalse);
    });

    test('turning duplicate merging off packs every sprite on its own', () {
      final pixels = _asymmetricPattern(6, 6);
      final sprites = [
        AtlasSprite(name: 'a', width: 6, height: 6, pixels: pixels),
        AtlasSprite(name: 'b', width: 6, height: 6, pixels: Uint8List.fromList(pixels)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 64, trim: false, mergeDuplicates: false));
      final page = result.pages.single;
      expect((page.regions['a']!.x, page.regions['a']!.y) == (page.regions['b']!.x, page.regions['b']!.y), isFalse);
    });
  });

  group('the written descriptor', () {
    test('pack, write and read gives back an equal atlas', () {
      final sprites = [
        AtlasSprite(name: 'rot', width: 8, height: 3, pixels: _asymmetricPattern(8, 3)),
        AtlasSprite(name: 'trimmed', width: 12, height: 10, pixels: _withTransparentMargin(12, 10, 2, 1, 8, 7)),
        AtlasSprite(name: 'plain', width: 5, height: 5, pixels: _solid(5, 5, 1, 2, 3, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 64));
      for (final page in result.pages) {
        final atlas = page.toAtlas('sheet.png');
        final text = writeAtlas(atlas);
        final read = Atlas.read(text)!;
        expect(read.image, 'sheet.png');
        expect(read.width, atlas.width);
        expect(read.height, atlas.height);
        expect(read.length, atlas.length);
        for (final name in atlas.regions.keys) {
          final expectedRegion = atlas[name]!;
          final actual = read[name]!;
          expect(actual.x, expectedRegion.x, reason: '$name.x');
          expect(actual.y, expectedRegion.y, reason: '$name.y');
          expect(actual.width, expectedRegion.width, reason: '$name.width');
          expect(actual.height, expectedRegion.height, reason: '$name.height');
          expect(actual.rotated, expectedRegion.rotated, reason: '$name.rotated');
          expect(actual.trimmed, expectedRegion.trimmed, reason: '$name.trimmed');
          expect(actual.offsetX, expectedRegion.offsetX, reason: '$name.offsetX');
          expect(actual.offsetY, expectedRegion.offsetY, reason: '$name.offsetY');
          expect(actual.sourceWidth, expectedRegion.sourceWidth, reason: '$name.sourceWidth');
          expect(actual.sourceHeight, expectedRegion.sourceHeight, reason: '$name.sourceHeight');
        }
      }
    });
  });

  group('the background entry point', () {
    test('packing off the calling isolate gives the same answer', () async {
      final sprites = [
        for (var i = 0; i < 10; i++)
          AtlasSprite(name: 's$i', width: 8, height: 8, pixels: _solid(8, 8, 1, 2, 3, 255)),
      ];
      const options = AtlasPackOptions(maxPageSize: 64, trim: false);
      final direct = packAtlas(sprites, options);
      final background = await packAtlasInBackground(sprites, options);
      expect(background.pages.length, direct.pages.length);
      expect(background.pages.single.pixels, equals(direct.pages.single.pixels));
    });
  });

  group('edge cases', () {
    test('a sprite larger than any page is refused with a reason, not thrown', () {
      final sprites = [
        AtlasSprite(name: 'huge', width: 200, height: 200, pixels: _solid(200, 200, 1, 1, 1, 255)),
        AtlasSprite(name: 'fine', width: 10, height: 10, pixels: _solid(10, 10, 2, 2, 2, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 64, trim: false));
      expect(result.problems, hasLength(1));
      expect(result.problems.single.name, 'huge');
      expect(result.problems.single.reason, isNotEmpty);
      expect(result.toAtlasSet().find('huge'), isNull);
      expect(result.toAtlasSet().find('fine'), isNotNull);
    });

    test('a zero-size sprite is handled rather than thrown', () {
      final sprites = [
        AtlasSprite(name: 'zero', width: 0, height: 0, pixels: Uint8List(0)),
        AtlasSprite(name: 'fine', width: 6, height: 6, pixels: _solid(6, 6, 1, 1, 1, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 32));
      expect(result.problems, isEmpty);
      final zero = result.toAtlasSet().find('zero');
      expect(zero, isNotNull);
      expect(zero!.width, 0);
      expect(zero.height, 0);
    });

    test('a fully transparent sprite draws nothing but still has a region', () {
      final sprites = [
        AtlasSprite(name: 'ghost', width: 10, height: 8, pixels: Uint8List(10 * 8 * 4)),
        AtlasSprite(name: 'fine', width: 6, height: 6, pixels: _solid(6, 6, 1, 1, 1, 255)),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 32));
      expect(result.problems, isEmpty);
      final ghost = result.toAtlasSet().find('ghost')!;
      expect(ghost.width, 0);
      expect(ghost.height, 0);
      expect(ghost.trimmed, isTrue);
      expect(ghost.sourceWidth, 10);
      expect(ghost.sourceHeight, 8);
    });

    test('thousands of tiny sprites pack without error or overlap', () {
      final sprites = [
        for (var i = 0; i < 2000; i++)
          AtlasSprite(
            name: 'tiny_$i',
            width: 2 + i % 6,
            height: 2 + (i * 3) % 6,
            pixels: _solid(2 + i % 6, 2 + (i * 3) % 6, 1, 1, 1, 255),
          ),
      ];
      // Duplicate merging is its own group's concern; on, it would legally
      // give many of these identical-content sprites the very same
      // rectangle, which the overlap check below would misread as a bug.
      final result = packAtlas(
        sprites,
        const AtlasPackOptions(maxPageSize: 512, padding: 1, trim: false, mergeDuplicates: false),
      );
      expect(result.problems, isEmpty);
      var total = 0;
      for (final page in result.pages) {
        total += page.regions.length;
        final boxes = [for (final region in page.regions.values) _physBox(region)];
        for (var i = 0; i < boxes.length; i++) {
          for (var j = i + 1; j < boxes.length; j++) {
            expect(_rectsOverlap(boxes[i], boxes[j]), isFalse);
          }
        }
      }
      expect(total, 2000);
    });
  });

  group('heuristics', () {
    test('pinning a heuristic is honoured', () {
      final sprites = [
        AtlasSprite(name: 'a', width: 10, height: 10, pixels: _solid(10, 10, 1, 1, 1, 255)),
      ];
      final result = packAtlas(
        sprites,
        const AtlasPackOptions(maxPageSize: 32, heuristic: MaxRectsHeuristic.contactPoint),
      );
      expect(result.heuristic, MaxRectsHeuristic.contactPoint);
    });

    test('left to choose, the packer picks one of the five and says which', () {
      final sprites = [
        for (var i = 0; i < 30; i++)
          AtlasSprite(
            name: 's$i',
            width: 4 + (i * 5) % 20,
            height: 4 + (i * 9) % 18,
            pixels: _solid(4 + (i * 5) % 20, 4 + (i * 9) % 18, 1, 1, 1, 255),
          ),
      ];
      final result = packAtlas(sprites, const AtlasPackOptions(maxPageSize: 128, trim: false));
      expect(MaxRectsHeuristic.values, contains(result.heuristic));
    });
  });
}
