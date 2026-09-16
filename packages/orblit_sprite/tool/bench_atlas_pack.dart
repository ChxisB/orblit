// Measurements for plan task 4.6, not assertions: fill ratio per MaxRects
// heuristic against a naive shelf packer on a realistic mixed set of sprite
// sizes, and packing time at 100, 1,000 and 5,000 sprites. Run with:
//
//   dart run tool/bench_atlas_pack.dart
//
// Kept out of `dart test` on purpose — a benchmark that has to stay fast
// enough for a test suite stops measuring what packing thousands of sprites
// actually costs.
import 'dart:math';
import 'dart:typed_data';

import 'package:orblit_sprite/orblit_sprite.dart';

Uint8List _opaque(int w, int h, int seed) {
  final out = Uint8List(w * h * 4);
  for (var i = 0; i < w * h; i++) {
    out[i * 4] = (seed + i) % 256;
    out[i * 4 + 1] = (seed * 3 + i) % 256;
    out[i * 4 + 2] = (seed * 7 + i) % 256;
    out[i * 4 + 3] = 255;
  }
  return out;
}

/// A mix of sizes closer to a real game's sprites than a uniform
/// distribution would be: small UI icons, mid-sized animation frames, tiles
/// and a handful of large portraits, each sprite's pixels unique so no
/// accidental duplicate sharing changes the shape of the comparison.
List<AtlasSprite> _realisticSet(int count, int seed) {
  final random = Random(seed);
  final sprites = <AtlasSprite>[];
  for (var i = 0; i < count; i++) {
    final roll = random.nextDouble();
    final int w, h;
    if (roll < 0.35) {
      w = 16 + random.nextInt(33); // icons: 16-48
      h = 16 + random.nextInt(33);
    } else if (roll < 0.75) {
      w = 24 + random.nextInt(73); // animation frames: 24-96
      h = 24 + random.nextInt(73);
    } else if (roll < 0.93) {
      w = 16 + random.nextInt(17); // tiles: 16-32
      h = w;
    } else {
      w = 96 + random.nextInt(161); // portraits/backgrounds: 96-256
      h = 96 + random.nextInt(161);
    }
    sprites.add(
      AtlasSprite(
        name: 'sprite_$i',
        width: w,
        height: h,
        pixels: _opaque(w, h, i),
      ),
    );
  }
  return sprites;
}

/// The obvious alternative to MaxRects: sort tallest first, lay sprites left
/// to right, start a new shelf — a new row as tall as whatever started it —
/// once one no longer fits.
({int pages, double fillRatio}) _shelfPack(
  List<AtlasSprite> sprites,
  int pageSize,
  int padding,
) {
  final byHeight = [...sprites]..sort((a, b) => b.height.compareTo(a.height));
  var pages = 1;
  var x = 0;
  var y = 0;
  var shelfHeight = 0;
  var covered = 0;
  final totalPerPage = pageSize * pageSize;

  for (final sprite in byHeight) {
    final fw = sprite.width + padding;
    final fh = sprite.height + padding;
    if (fw > pageSize) {
      continue; // too wide for any page at all; ignored for this comparison
    }
    if (x + fw > pageSize) {
      x = 0;
      y += shelfHeight;
      shelfHeight = 0;
    }
    if (y + fh > pageSize) {
      pages++;
      x = 0;
      y = 0;
      shelfHeight = 0;
    }
    x += fw;
    if (fh > shelfHeight) shelfHeight = fh;
    covered += sprite.width * sprite.height;
  }
  return (pages: pages, fillRatio: covered / (totalPerPage * pages));
}

double _fillRatio(AtlasPackResult result) {
  var covered = 0;
  var area = 0;
  for (final page in result.pages) {
    area += page.width * page.height;
    for (final region in page.regions.values) {
      covered += region.width * region.height;
    }
  }
  return area == 0 ? 0 : covered / area;
}

void main() {
  const padding = 2;

  // A page small enough, relative to the sprite count, that different
  // packing quality actually shows up as a different number of pages —
  // one big page swallows every strategy's leftover space equally and
  // proves nothing.
  const fillPageSize = 512;
  log('Fill ratio per heuristic, and against a naive shelf packer');
  log(
    '(900 sprites, a realistic mixed set of icon/frame/tile/portrait sizes, ${fillPageSize}px pages)',
  );
  final mixed = _realisticSet(900, 1);

  for (final heuristic in MaxRectsHeuristic.values) {
    final result = packAtlas(
      mixed,
      AtlasPackOptions(
        maxPageSize: fillPageSize,
        padding: padding,
        trim: false,
        mergeDuplicates: false,
        heuristic: heuristic,
      ),
    );
    log(
      '  ${heuristic.name.padRight(16)} ${result.pages.length} page(s)  '
      '${(_fillRatio(result) * 100).toStringAsFixed(1)}% full',
    );
  }
  final auto = packAtlas(
    mixed,
    const AtlasPackOptions(
      maxPageSize: fillPageSize,
      padding: padding,
      trim: false,
      mergeDuplicates: false,
    ),
  );
  log(
    '  ${'auto (all five)'.padRight(16)} ${auto.pages.length} page(s)  '
    '${(_fillRatio(auto) * 100).toStringAsFixed(1)}% full  (picked ${auto.heuristic.name})',
  );

  final shelf = _shelfPack(mixed, fillPageSize, padding);
  log(
    '  ${'naive shelf'.padRight(16)} ${shelf.pages} page(s)  '
    '${(shelf.fillRatio * 100).toStringAsFixed(1)}% full',
  );

  const pageSize = 2048;
  log('');
  log('Packing time (${pageSize}px pages)');
  for (final count in [100, 1000, 5000]) {
    final sprites = _realisticSet(count, count);
    final singleWatch = Stopwatch()..start();
    final single = packAtlas(
      sprites,
      const AtlasPackOptions(
        maxPageSize: pageSize,
        padding: padding,
        trim: false,
        heuristic: MaxRectsHeuristic.bestShortSideFit,
      ),
    );
    singleWatch.stop();

    final autoWatch = Stopwatch()..start();
    final autoResult = packAtlas(
      sprites,
      const AtlasPackOptions(
        maxPageSize: pageSize,
        padding: padding,
        trim: false,
      ),
    );
    autoWatch.stop();

    log(
      '  $count sprites: ${singleWatch.elapsedMilliseconds} ms pinned to one heuristic '
      '(${single.pages.length} pages), ${autoWatch.elapsedMilliseconds} ms trying all five '
      '(${autoResult.pages.length} pages)',
    );
  }
}

void log(String line) {
  // ignore: avoid_print
  print(line);
}
