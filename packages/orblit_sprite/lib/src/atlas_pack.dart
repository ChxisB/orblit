import 'dart:convert';
import 'dart:typed_data';

import 'atlas.dart';

/// One picture going into a pack.
///
/// Straight (non-premultiplied) alpha, RGBA8, row nought at the top — the
/// shape a PNG decodes to, and the shape [Atlas.uv] already assumes. The
/// packer never blends a pixel against another, only copies and rotates whole
/// texels, so it packs a page identically whichever convention the source
/// used; a renderer that wants pictures premultiplied, as `sprite.mat` does,
/// still premultiplies after this, the same as it would a page that was never
/// packed at all.
class AtlasSprite {
  AtlasSprite({
    required this.name,
    required this.width,
    required this.height,
    required this.pixels,
  }) : assert(
         pixels.length == width * height * 4,
         'pixels must hold $width x $height RGBA8 texels '
         '(${width * height * 4} bytes), got ${pixels.length}',
       );

  final String name;
  final int width;
  final int height;
  final Uint8List pixels;
}

/// Which corner of "A Thousand Ways to Pack the Bin" a placement is scored
/// by. All five are the ones that paper found worth keeping.
enum MaxRectsHeuristic {
  /// Fits into the free rectangle that leaves the smallest gap on its
  /// tighter side. The one that wins most often on a mixed set of sprites.
  bestShortSideFit,

  /// Fits into the free rectangle that leaves the smallest gap on its
  /// *longer* side, so what remains free stays as squarish as possible.
  bestLongSideFit,

  /// Fits into the free rectangle whose leftover area, after the sprite is
  /// cut out of it, is smallest.
  bestAreaFit,

  /// Sits as low and then as far left as it can — the rule a person packing
  /// boxes by hand already uses.
  bottomLeft,

  /// Touches as much of the page's edge and of what is already placed as it
  /// can, so the packed sprites stay contiguous instead of leaving slivers
  /// between them that nothing later happens to fit.
  contactPoint,
}

/// How [packAtlas] should lay sprites out.
///
/// `orblit_sprite` does not depend on `orblit_filament`, so it cannot read
/// [OrblitDeviceProfile] itself — pass [maxPageSize] as
/// `OrblitDeviceProfile.textureSizeBudget` (or the raw device maximum) from
/// wherever the profile was already asked for.
class AtlasPackOptions {
  const AtlasPackOptions({
    this.maxPageSize = 2048,
    this.minPageSize = 64,
    this.padding = 2,
    this.border = 0,
    this.allowRotation = true,
    this.trim = true,
    this.trimAlphaThreshold = 0,
    this.extrude = 1,
    this.powerOfTwo = true,
    this.square = false,
    this.mergeDuplicates = true,
    this.heuristic,
  }) : assert(maxPageSize > 0, 'a page has to have some size'),
       assert(minPageSize > 0, 'a page has to have some size'),
       assert(padding >= 0, 'padding cannot be negative'),
       assert(border >= 0, 'a border cannot be negative'),
       assert(extrude >= 0, 'extrusion cannot be negative');

  /// The largest a page's width or height may be, in texels. Pass the
  /// device's texture size budget, not a constant — see the class comment.
  final int maxPageSize;

  /// The smallest a shrunk page may come down to. Only the last page of a
  /// pack is ever shrunk; every other page is [maxPageSize].
  final int minPageSize;

  /// Empty texels kept between neighbouring sprites, so bilinear filtering
  /// at a sprite's edge samples its own extruded border rather than its
  /// neighbour's pixels.
  final int padding;

  /// Empty texels kept between every sprite and the edge of the page, for
  /// the same reason [padding] keeps them apart from each other.
  final int border;

  /// Whether a sprite may be turned a quarter turn to fit better.
  ///
  /// Off by default would waste space that rotation would otherwise use, but
  /// on is still the caller's call: see the note on rotation in the packer's
  /// report — nothing in the renderer draws a rotated region's UVs correctly
  /// yet, so a caller whose sprites must draw today, not once that lands,
  /// should pass `false`.
  final bool allowRotation;

  /// Whether transparent borders are cut before packing.
  ///
  /// Cutting them is almost always a win — the packer fits more per page,
  /// and [Region.offsetX]/[Region.offsetY] carry the sprite back to its
  /// original placement — but it costs a scan of every pixel, which a caller
  /// packing pictures it already knows have no transparent margin can skip.
  final bool trim;

  /// A texel's alpha has to be *greater* than this to count as content
  /// while trimming. Nought trims only fully transparent texels; raised, it
  /// also trims a soft, nearly invisible fringe some exporters leave behind.
  final int trimAlphaThreshold;

  /// How far a sprite's edge texels are copied out into its padding, so a
  /// mipmap or a filtered sample at the very edge reads more of the same
  /// picture rather than a seam of its neighbour's colour.
  ///
  /// Clamped to [padding] when packing runs, since there is nowhere to
  /// extrude into past it.
  final int extrude;

  /// Whether a page's size, before any shrinking, is rounded up to a power
  /// of two. Most GPUs no longer require it, but mipmap generation and some
  /// compressed formats still assume it, so it stays the default.
  final bool powerOfTwo;

  /// Whether a page must come out width equal to height.
  final bool square;

  /// Whether sprites with byte-identical trimmed pixels are packed once and
  /// given the same rectangle.
  ///
  /// A tile set or an animation exported with a few repeated frames is the
  /// common case; sharing costs a hash and a byte comparison per sprite and
  /// saves a full copy in the page for every repeat found.
  final bool mergeDuplicates;

  /// Which heuristic chooses a sprite's spot. Left null, [packAtlas] tries
  /// every [MaxRectsHeuristic] and keeps whichever packed the input into
  /// fewer pages, or — tied on pages — the smaller total page area; ties
  /// after that keep whichever heuristic comes first in
  /// [MaxRectsHeuristic.values], so the choice is never left to iteration
  /// order. Pinning one heuristic here is close to five times faster and is
  /// what a large, repeated cook should do once it knows which wins for its
  /// own sprites.
  final MaxRectsHeuristic? heuristic;
}

/// A sprite [packAtlas] would have had to place off the edge of even an
/// empty page, reported rather than thrown so the rest of a pack still
/// happens.
class AtlasPackProblem {
  const AtlasPackProblem(this.name, this.reason);

  final String name;
  final String reason;

  @override
  String toString() => '$name: $reason';
}

/// One page [packAtlas] produced: the pixels of a page-sized picture, and
/// where every sprite placed on it ended up.
class AtlasPackPage {
  const AtlasPackPage({
    required this.width,
    required this.height,
    required this.pixels,
    required this.regions,
  });

  final int width;
  final int height;

  /// RGBA8, straight alpha, row nought at the top — see [AtlasSprite].
  final Uint8List pixels;
  final Map<String, Region> regions;

  /// This page as the existing [Atlas] type, under [image].
  Atlas toAtlas(String image) =>
      Atlas(image: image, regions: regions, width: width, height: height);

  /// The fraction of this page actually covered by sprites, ignoring padding
  /// and the border — the number a fill-ratio measurement means.
  double get fillRatio {
    if (width <= 0 || height <= 0) return 0;
    var covered = 0;
    for (final region in regions.values) {
      covered += region.width * region.height;
    }
    return covered / (width * height);
  }
}

/// What [packAtlas] produced: every page, and every sprite it could not
/// place at all.
class AtlasPackResult {
  const AtlasPackResult({
    required this.pages,
    required this.problems,
    required this.heuristic,
  });

  final List<AtlasPackPage> pages;

  /// Sprites too big for a page even alone on it — see [AtlasPackProblem].
  /// Everything else asked for is somewhere in [pages], including a sprite
  /// that packed to nothing because it was empty or fully transparent: it
  /// still has a [Region], sized zero, so looking it up by name never
  /// returns null just because there was nothing to draw.
  final List<AtlasPackProblem> problems;

  /// Which [MaxRectsHeuristic] produced this result — the one asked for, or
  /// whichever [AtlasPackOptions.heuristic] left null chose.
  final MaxRectsHeuristic heuristic;

  /// Every page as the existing [Atlas] type, named by [imageName] (default:
  /// `page_0.png`, `page_1.png`, and so on — override it to match whatever
  /// naming a cooker's own contract wants) and gathered into the smallest
  /// type that can look a region up without knowing which page it landed on.
  AtlasSet toAtlasSet({String Function(int index)? imageName}) {
    final name = imageName ?? (int i) => 'page_$i.png';
    return AtlasSet([
      for (var i = 0; i < pages.length; i++) pages[i].toAtlas(name(i)),
    ]);
  }
}

/// Packs [sprites] into as few pages as [options] allow.
///
/// Pure and synchronous: it does no I/O and returns as soon as the CPU work
/// is done. That is what lets [packAtlasInBackground] hand the very same
/// function to an [Isolate] and lets the offline cooker call it directly
/// without a `dart:isolate` import of its own.
///
/// Deterministic for the same [sprites] and [options] regardless of call
/// order or platform: sprites are placed in an order derived only from their
/// own size and name, never from a [Map] or [Set]'s iteration, so two runs —
/// or a run today against a run next year on a different Dart version —
/// produce the same pages down to the byte.
AtlasPackResult packAtlas(
  List<AtlasSprite> sprites, [
  AtlasPackOptions options = const AtlasPackOptions(),
]) {
  final problems = <AtlasPackProblem>[];
  final emptyRegions = <String, Region>{};
  final prepared = <_Prepared>[];

  for (final sprite in sprites) {
    if (sprite.width <= 0 || sprite.height <= 0) {
      emptyRegions[sprite.name] = Region(
        name: sprite.name,
        x: 0,
        y: 0,
        width: 0,
        height: 0,
      );
      continue;
    }

    final box = options.trim
        ? _trimBox(
            sprite.pixels,
            sprite.width,
            sprite.height,
            options.trimAlphaThreshold,
          )
        : (x: 0, y: 0, w: sprite.width, h: sprite.height);

    if (box.w <= 0 || box.h <= 0) {
      // Fully transparent: nothing to place, but the sprite still had a
      // size, and a game laying things out by it should still get one back.
      emptyRegions[sprite.name] = Region(
        name: sprite.name,
        x: 0,
        y: 0,
        width: 0,
        height: 0,
        trimmed: true,
        sourceWidth: sprite.width,
        sourceHeight: sprite.height,
      );
      continue;
    }

    final trimmed = box.w != sprite.width || box.h != sprite.height;
    prepared.add(
      _Prepared(
        name: sprite.name,
        trimmedX: box.x,
        trimmedY: box.y,
        width: box.w,
        height: box.h,
        sourceWidth: trimmed ? sprite.width : 0,
        sourceHeight: trimmed ? sprite.height : 0,
        trimmed: trimmed,
        pixels: _copyBox(
          sprite.pixels,
          sprite.width,
          box.x,
          box.y,
          box.w,
          box.h,
        ),
      ),
    );
  }

  final pieces = options.mergeDuplicates
      ? _groupDuplicates(prepared)
      : [
          for (final p in prepared)
            _Piece(
              width: p.width,
              height: p.height,
              pixels: p.pixels,
              members: [p],
            ),
        ];

  final available = options.maxPageSize - 2 * options.border;
  final packable = <_Piece>[];
  for (final piece in pieces) {
    final fits =
        _fitsFootprint(piece.width, piece.height, options.padding, available) ||
        (options.allowRotation &&
            _fitsFootprint(
              piece.height,
              piece.width,
              options.padding,
              available,
            ));
    if (fits) {
      packable.add(piece);
    } else {
      for (final member in piece.members) {
        problems.add(
          AtlasPackProblem(
            member.name,
            'is ${piece.width}x${piece.height}, too large for a '
            '${options.maxPageSize}x${options.maxPageSize} page even rotated',
          ),
        );
      }
    }
  }

  packable.sort(_comparePieces);

  final heuristics = options.heuristic != null
      ? [options.heuristic!]
      : MaxRectsHeuristic.values;
  _PackRun? bestRun;
  var bestHeuristic = heuristics.first;
  for (final heuristic in heuristics) {
    final run = _packOnce(packable, options, heuristic);
    if (bestRun == null || _runIsBetter(run, bestRun)) {
      bestRun = run;
      bestHeuristic = heuristic;
    }
  }

  final pieceByKey = {for (final piece in packable) piece.key: piece};
  final pages = _render(
    bestRun ?? _PackRun([]),
    options,
    pieceByKey,
    emptyRegions,
  );

  return AtlasPackResult(
    pages: pages,
    problems: problems,
    heuristic: bestHeuristic,
  );
}

/// [atlas] written as the same TexturePacker-shaped JSON [Atlas.read]
/// already parses — every field [Region] has, verbatim, so
/// `Atlas.read(writeAtlas(atlas))` gives back an atlas equal to [atlas]
/// field for field.
///
/// Frames are written in name order rather than the map's own insertion
/// order, so packing the same sprites twice writes the same bytes and a
/// diff of a cooked atlas shows only what actually changed.
String writeAtlas(Atlas atlas) {
  final names = atlas.regions.keys.toList()..sort();
  final json = <String, Object?>{
    'frames': {
      for (final name in names) name: _regionJson(atlas.regions[name]!),
    },
    'meta': {
      'image': atlas.image,
      'size': {'w': atlas.width, 'h': atlas.height},
    },
  };
  return '${const JsonEncoder.withIndent('  ').convert(json)}\n';
}

Map<String, Object?> _regionJson(Region region) => {
  'frame': {
    'x': region.x,
    'y': region.y,
    'w': region.width,
    'h': region.height,
  },
  'rotated': region.rotated,
  'trimmed': region.trimmed,
  'spriteSourceSize': {'x': region.offsetX, 'y': region.offsetY},
  'sourceSize': {'w': region.sourceWidth, 'h': region.sourceHeight},
};

/// The pixels [region] names, read back out of the page it was packed onto
/// and returned exactly as [sprite] was given to [packAtlas]: un-rotated,
/// and — when [region] was trimmed — padded back out to
/// [Region.sourceWidth] by [Region.sourceHeight] with the trimmed pixels at
/// [Region.offsetX], [Region.offsetY], everywhere else transparent.
///
/// What a rotation or a trim actually did is only provable by reading pixels
/// back through the region that describes them, which is what this is for:
/// a symmetric test image can't tell a correct rotation from a wrong one,
/// but comparing what this returns against the original sprite can.
Uint8List extractRegionPixels({
  required int pageWidth,
  required int pageHeight,
  required Uint8List pagePixels,
  required Region region,
}) {
  if (region.width <= 0 || region.height <= 0) {
    final w = region.sourceWidth > 0 ? region.sourceWidth : 0;
    final h = region.sourceHeight > 0 ? region.sourceHeight : 0;
    return Uint8List(w * h * 4);
  }

  final physWidth = region.rotated ? region.height : region.width;
  final physHeight = region.rotated ? region.width : region.height;
  final block = _copyBox(
    pagePixels,
    pageWidth,
    region.x,
    region.y,
    physWidth,
    physHeight,
  );
  final trimmedBlock = region.rotated
      ? _rotateCcw(block, physWidth, physHeight)
      : block;

  if (!region.trimmed || region.sourceWidth <= 0 || region.sourceHeight <= 0) {
    return trimmedBlock;
  }

  final canvas = Uint8List(region.sourceWidth * region.sourceHeight * 4);
  _pasteBox(
    canvas,
    region.sourceWidth,
    region.offsetX,
    region.offsetY,
    region.width,
    region.height,
    trimmedBlock,
  );
  return canvas;
}

// --- Preparation -----------------------------------------------------------

class _Prepared {
  _Prepared({
    required this.name,
    required this.trimmedX,
    required this.trimmedY,
    required this.width,
    required this.height,
    required this.sourceWidth,
    required this.sourceHeight,
    required this.trimmed,
    required this.pixels,
  });

  final String name;
  final int trimmedX;
  final int trimmedY;
  final int width;
  final int height;
  final int sourceWidth;
  final int sourceHeight;
  final bool trimmed;
  final Uint8List pixels;
}

class _Piece {
  _Piece({
    required this.width,
    required this.height,
    required this.pixels,
    required List<_Prepared> members,
  }) : members = members..sort((a, b) => a.name.compareTo(b.name));

  final int width;
  final int height;
  final Uint8List pixels;
  final List<_Prepared> members;

  /// The smallest member name, so two packs of the same sprites choose the
  /// same representative regardless of what order duplicates arrived in.
  String get key => members.first.name;
}

({int x, int y, int w, int h}) _trimBox(
  Uint8List pixels,
  int width,
  int height,
  int threshold,
) {
  var minX = width;
  var minY = height;
  var maxX = -1;
  var maxY = -1;
  for (var y = 0; y < height; y++) {
    final row = y * width;
    for (var x = 0; x < width; x++) {
      if (pixels[(row + x) * 4 + 3] > threshold) {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }
  if (maxX < minX || maxY < minY) return (x: 0, y: 0, w: 0, h: 0);
  return (x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1);
}

Uint8List _copyBox(Uint8List src, int srcWidth, int x, int y, int w, int h) {
  final out = Uint8List(w * h * 4);
  for (var row = 0; row < h; row++) {
    final srcStart = ((y + row) * srcWidth + x) * 4;
    final dstStart = row * w * 4;
    out.setRange(dstStart, dstStart + w * 4, src, srcStart);
  }
  return out;
}

void _pasteBox(
  Uint8List dst,
  int dstWidth,
  int x,
  int y,
  int w,
  int h,
  Uint8List block,
) {
  for (var row = 0; row < h; row++) {
    final dstStart = ((y + row) * dstWidth + x) * 4;
    final srcStart = row * w * 4;
    dst.setRange(dstStart, dstStart + w * 4, block, srcStart);
  }
}

Uint8List _rotateCw(Uint8List src, int width, int height) {
  // dst(h-1-sy, sx) = src(sx, sy); the destination is height wide and width
  // tall, since a quarter turn swaps them.
  final out = Uint8List(width * height * 4);
  for (var y = 0; y < height; y++) {
    for (var x = 0; x < width; x++) {
      final dx = height - 1 - y;
      final dy = x;
      final si = (y * width + x) * 4;
      final di = (dy * height + dx) * 4;
      out[di] = src[si];
      out[di + 1] = src[si + 1];
      out[di + 2] = src[si + 2];
      out[di + 3] = src[si + 3];
    }
  }
  return out;
}

Uint8List _rotateCcw(Uint8List src, int width, int height) {
  // The exact inverse of _rotateCw(original, outHeight, outWidth): that
  // forward pass reads original(x, y) and writes it to
  // dst(width - 1 - y, x) in a buffer `width` by `height`. Reading this
  // buffer at (x, y) therefore has to look up original(dy, width - 1 - dx)
  // — for every dst pixel, run that mapping backwards rather than trying to
  // re-derive a second formula from scratch, which is exactly the kind of
  // one-off algebra a rotation bug hides in.
  final outWidth = height;
  final outHeight = width;
  final out = Uint8List(outWidth * outHeight * 4);
  for (var y = 0; y < outHeight; y++) {
    for (var x = 0; x < outWidth; x++) {
      final dx = width - 1 - y;
      final dy = x;
      final si = (dy * width + dx) * 4;
      final di = (y * outWidth + x) * 4;
      out[di] = src[si];
      out[di + 1] = src[si + 1];
      out[di + 2] = src[si + 2];
      out[di + 3] = src[si + 3];
    }
  }
  return out;
}

// --- Duplicate merging -------------------------------------------------------

List<_Piece> _groupDuplicates(List<_Prepared> prepared) {
  final buckets = <int, List<_Piece>>{};
  for (final candidate in prepared) {
    final hash = _contentHash(
      candidate.pixels,
      candidate.width,
      candidate.height,
    );
    final bucket = buckets.putIfAbsent(hash, () => []);
    _Piece? match;
    for (final piece in bucket) {
      if (piece.width == candidate.width &&
          piece.height == candidate.height &&
          _bytesEqual(piece.pixels, candidate.pixels)) {
        match = piece;
        break;
      }
    }
    if (match != null) {
      match.members
        ..add(candidate)
        ..sort((a, b) => a.name.compareTo(b.name));
    } else {
      bucket.add(
        _Piece(
          width: candidate.width,
          height: candidate.height,
          pixels: candidate.pixels,
          members: [candidate],
        ),
      );
    }
  }
  return [for (final bucket in buckets.values) ...bucket];
}

int _contentHash(Uint8List bytes, int width, int height) {
  // FNV-1a. Only ever used as a bucket key — every match is confirmed with a
  // full byte comparison — so a collision costs a few extra comparisons and
  // nothing else.
  var hash = 0x811c9dc5;
  hash = (hash ^ width) & 0xffffffff;
  hash = (hash * 0x01000193) & 0xffffffff;
  hash = (hash ^ height) & 0xffffffff;
  hash = (hash * 0x01000193) & 0xffffffff;
  for (final byte in bytes) {
    hash = (hash ^ byte) & 0xffffffff;
    hash = (hash * 0x01000193) & 0xffffffff;
  }
  return hash;
}

bool _bytesEqual(Uint8List a, Uint8List b) {
  if (a.length != b.length) return false;
  for (var i = 0; i < a.length; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}

bool _fitsFootprint(int w, int h, int padding, int available) =>
    (w + padding) <= available && (h + padding) <= available;

int _comparePieces(_Piece a, _Piece b) {
  final aMax = a.width > a.height ? a.width : a.height;
  final bMax = b.width > b.height ? b.width : b.height;
  if (aMax != bMax) return bMax.compareTo(aMax);
  final aMin = a.width < a.height ? a.width : a.height;
  final bMin = b.width < b.height ? b.width : b.height;
  if (aMin != bMin) return bMin.compareTo(aMin);
  return a.key.compareTo(b.key);
}

// --- MaxRects ----------------------------------------------------------------

class _FreeRect {
  _FreeRect(this.x, this.y, this.w, this.h);
  final int x;
  final int y;
  final int w;
  final int h;
}

class _Placed {
  const _Placed(this.x, this.y, this.w, this.h);
  final int x;
  final int y;
  final int w;
  final int h;
}

class _Placement {
  const _Placement(this.x, this.y, this.rotated);
  final int x;
  final int y;
  final bool rotated;
}

class _PageBuild {
  _PageBuild(this.size, this.border)
    : free = [_FreeRect(border, border, size - 2 * border, size - 2 * border)];

  final int size;
  final int border;
  final List<_FreeRect> free;
  final List<_Placed> placed = [];
  final Map<String, _Placement> placements = {};
  int maxX = 0;
  int maxY = 0;

  bool tryPlace(
    _Piece piece,
    AtlasPackOptions options,
    MaxRectsHeuristic heuristic,
  ) {
    final fw = piece.width + options.padding;
    final fh = piece.height + options.padding;

    _FreeRect? bestFree;
    var bestRotated = false;
    num bestScore1 = 0;
    num bestScore2 = 0;
    var found = false;

    for (final rect in free) {
      final orientations = options.allowRotation
          ? const [false, true]
          : const [false];
      for (final rotated in orientations) {
        final pw = rotated ? fh : fw;
        final ph = rotated ? fw : fh;
        if (pw > rect.w || ph > rect.h) continue;
        final (score1, score2) = _score(heuristic, rect, pw, ph);
        if (!found ||
            score1 < bestScore1 ||
            (score1 == bestScore1 && score2 < bestScore2)) {
          found = true;
          bestFree = rect;
          bestRotated = rotated;
          bestScore1 = score1;
          bestScore2 = score2;
        }
      }
    }

    if (!found || bestFree == null) return false;

    final placedW = bestRotated ? fh : fw;
    final placedH = bestRotated ? fw : fh;
    final x = bestFree.x;
    final y = bestFree.y;
    _place(x, y, placedW, placedH);
    placements[piece.key] = _Placement(x, y, bestRotated);
    return true;
  }

  (num, num) _score(MaxRectsHeuristic heuristic, _FreeRect rect, int w, int h) {
    final leftoverW = rect.w - w;
    final leftoverH = rect.h - h;
    final shortLeftover = leftoverW < leftoverH ? leftoverW : leftoverH;
    final longLeftover = leftoverW < leftoverH ? leftoverH : leftoverW;
    switch (heuristic) {
      case MaxRectsHeuristic.bestShortSideFit:
        return (shortLeftover, longLeftover);
      case MaxRectsHeuristic.bestLongSideFit:
        return (longLeftover, shortLeftover);
      case MaxRectsHeuristic.bestAreaFit:
        return (rect.w * rect.h - w * h, shortLeftover);
      case MaxRectsHeuristic.bottomLeft:
        return (rect.y + h, rect.x);
      case MaxRectsHeuristic.contactPoint:
        return (-_contactScore(rect.x, rect.y, w, h), 0);
    }
  }

  int _contactScore(int x, int y, int w, int h) {
    var contact = 0;
    if (x == border) contact += h;
    if (y == border) contact += w;
    if (x + w == size - border) contact += h;
    if (y + h == size - border) contact += w;
    for (final other in placed) {
      if (other.x + other.w == x || x + w == other.x) {
        contact += _overlap1D(other.y, other.y + other.h, y, y + h);
      }
      if (other.y + other.h == y || y + h == other.y) {
        contact += _overlap1D(other.x, other.x + other.w, x, x + w);
      }
    }
    return contact;
  }

  static int _overlap1D(int aStart, int aEnd, int bStart, int bEnd) {
    final start = aStart > bStart ? aStart : bStart;
    final end = aEnd < bEnd ? aEnd : bEnd;
    return end > start ? end - start : 0;
  }

  void _place(int x, int y, int w, int h) {
    final placedRect = _Placed(x, y, w, h);
    final next = <_FreeRect>[];
    for (final rect in free) {
      if (!_intersects(rect, placedRect)) {
        next.add(rect);
        continue;
      }
      if (placedRect.x > rect.x) {
        next.add(_FreeRect(rect.x, rect.y, placedRect.x - rect.x, rect.h));
      }
      if (placedRect.x + placedRect.w < rect.x + rect.w) {
        next.add(
          _FreeRect(
            placedRect.x + placedRect.w,
            rect.y,
            rect.x + rect.w - (placedRect.x + placedRect.w),
            rect.h,
          ),
        );
      }
      if (placedRect.y > rect.y) {
        next.add(_FreeRect(rect.x, rect.y, rect.w, placedRect.y - rect.y));
      }
      if (placedRect.y + placedRect.h < rect.y + rect.h) {
        next.add(
          _FreeRect(
            rect.x,
            placedRect.y + placedRect.h,
            rect.w,
            rect.y + rect.h - (placedRect.y + placedRect.h),
          ),
        );
      }
    }
    free
      ..clear()
      ..addAll(next.where((r) => r.w > 0 && r.h > 0));
    _prune();
    placed.add(placedRect);
    if (x + w > maxX) maxX = x + w;
    if (y + h > maxY) maxY = y + h;
  }

  void _prune() {
    final keep = <_FreeRect>[];
    for (var i = 0; i < free.length; i++) {
      var dominated = false;
      for (var j = 0; j < free.length; j++) {
        if (i == j) continue;
        if (_containsRect(free[j], free[i]) &&
            (!_sameRect(free[i], free[j]) || j < i)) {
          dominated = true;
          break;
        }
      }
      if (!dominated) keep.add(free[i]);
    }
    free
      ..clear()
      ..addAll(keep);
  }

  static bool _intersects(_FreeRect a, _Placed b) =>
      a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;

  static bool _containsRect(_FreeRect outer, _FreeRect inner) =>
      inner.x >= outer.x &&
      inner.y >= outer.y &&
      inner.x + inner.w <= outer.x + outer.w &&
      inner.y + inner.h <= outer.y + outer.h;

  static bool _sameRect(_FreeRect a, _FreeRect b) =>
      a.x == b.x && a.y == b.y && a.w == b.w && a.h == b.h;
}

class _PackRun {
  _PackRun(this.pages);
  final List<_PageBuild> pages;
}

_PackRun _packOnce(
  List<_Piece> pieces,
  AtlasPackOptions options,
  MaxRectsHeuristic heuristic,
) {
  final pages = <_PageBuild>[];
  for (final piece in pieces) {
    var placed = false;
    for (final page in pages) {
      if (page.tryPlace(piece, options, heuristic)) {
        placed = true;
        break;
      }
    }
    if (!placed) {
      final page = _PageBuild(options.maxPageSize, options.border);
      final ok = page.tryPlace(piece, options, heuristic);
      assert(ok, 'oversize pieces are filtered out before packing starts');
      pages.add(page);
    }
  }
  return _PackRun(pages);
}

bool _runIsBetter(_PackRun candidate, _PackRun current) {
  if (candidate.pages.length != current.pages.length) {
    return candidate.pages.length < current.pages.length;
  }
  var candidateArea = 0;
  var currentArea = 0;
  for (final page in candidate.pages) {
    candidateArea += page.maxX * page.maxY;
  }
  for (final page in current.pages) {
    currentArea += page.maxX * page.maxY;
  }
  return candidateArea < currentArea;
}

// --- Rendering -----------------------------------------------------------

List<AtlasPackPage> _render(
  _PackRun run,
  AtlasPackOptions options,
  Map<String, _Piece> pieceByKey,
  Map<String, Region> emptyRegions,
) {
  if (run.pages.isEmpty) {
    if (emptyRegions.isEmpty) return const [];
    final size = options.minPageSize < options.maxPageSize
        ? options.minPageSize
        : options.maxPageSize;
    return [
      AtlasPackPage(
        width: size,
        height: size,
        pixels: Uint8List(size * size * 4),
        regions: Map.of(emptyRegions),
      ),
    ];
  }

  final pages = <AtlasPackPage>[];
  for (var i = 0; i < run.pages.length; i++) {
    final build = run.pages[i];
    final isLast = i == run.pages.length - 1;
    final width = isLast
        ? _shrunkSize(build.maxX + build.border, options)
        : options.maxPageSize;
    final height = isLast
        ? _shrunkSize(build.maxY + build.border, options)
        : options.maxPageSize;
    final finalWidth = options.square
        ? (width > height ? width : height)
        : width;
    final finalHeight = options.square ? finalWidth : height;

    final pixels = Uint8List(finalWidth * finalHeight * 4);
    final regions = <String, Region>{};

    build.placements.forEach((key, placement) {
      final piece = pieceByKey[key]!;
      final block = placement.rotated
          ? _rotateCw(piece.pixels, piece.width, piece.height)
          : piece.pixels;
      final physWidth = placement.rotated ? piece.height : piece.width;
      final physHeight = placement.rotated ? piece.width : piece.height;
      _pasteBox(
        pixels,
        finalWidth,
        placement.x,
        placement.y,
        physWidth,
        physHeight,
        block,
      );
      _extrudeRegion(
        pixels,
        finalWidth,
        finalHeight,
        placement.x,
        placement.y,
        physWidth,
        physHeight,
        options.extrude.clamp(
          0,
          options.padding == 0 ? options.extrude : options.padding,
        ),
      );

      for (final member in piece.members) {
        regions[member.name] = Region(
          name: member.name,
          x: placement.x,
          y: placement.y,
          width: piece.width,
          height: piece.height,
          rotated: placement.rotated,
          trimmed: member.trimmed,
          offsetX: member.trimmedX,
          offsetY: member.trimmedY,
          sourceWidth: member.sourceWidth,
          sourceHeight: member.sourceHeight,
        );
      }
    });

    if (isLast) regions.addAll(emptyRegions);
    pages.add(
      AtlasPackPage(
        width: finalWidth,
        height: finalHeight,
        pixels: pixels,
        regions: regions,
      ),
    );
  }
  return pages;
}

int _shrunkSize(int content, AtlasPackOptions options) {
  // minPageSize is a floor, but a caller is free to set maxPageSize below
  // the default floor for a small pack — a 32-texel page for a handful of
  // icons, say — and that has to win rather than making clamp's own range
  // check the one that reports the mistake.
  final floor = options.minPageSize < options.maxPageSize
      ? options.minPageSize
      : options.maxPageSize;
  var size = content.clamp(floor, options.maxPageSize);
  if (options.powerOfTwo) {
    size = _nextPowerOfTwo(size).clamp(floor, options.maxPageSize);
  }
  return size;
}

int _nextPowerOfTwo(int value) {
  var size = 1;
  while (size < value) {
    size <<= 1;
  }
  return size;
}

void _extrudeRegion(
  Uint8List page,
  int pageWidth,
  int pageHeight,
  int x,
  int y,
  int w,
  int h,
  int amount,
) {
  if (amount <= 0 || w <= 0 || h <= 0) return;
  int index(int px, int py) => (py * pageWidth + px) * 4;
  void copyTexel(int dst, int src) {
    page[dst] = page[src];
    page[dst + 1] = page[src + 1];
    page[dst + 2] = page[src + 2];
    page[dst + 3] = page[src + 3];
  }

  for (var row = 0; row < h; row++) {
    final py = y + row;
    if (py < 0 || py >= pageHeight) continue;
    final leftSrc = index(x, py);
    for (var k = 1; k <= amount; k++) {
      final px = x - k;
      if (px < 0) break;
      copyTexel(index(px, py), leftSrc);
    }
    final rightSrc = index(x + w - 1, py);
    for (var k = 1; k <= amount; k++) {
      final px = x + w - 1 + k;
      if (px >= pageWidth) break;
      copyTexel(index(px, py), rightSrc);
    }
  }

  for (var col = -amount; col < w + amount; col++) {
    final px = x + col;
    if (px < 0 || px >= pageWidth) continue;
    final srcCol = col < 0 ? 0 : (col >= w ? w - 1 : col);
    final topSrc = index(x + srcCol, y);
    for (var k = 1; k <= amount; k++) {
      final py = y - k;
      if (py < 0) break;
      copyTexel(index(px, py), topSrc);
    }
    final bottomSrc = index(x + srcCol, y + h - 1);
    for (var k = 1; k <= amount; k++) {
      final py = y + h - 1 + k;
      if (py >= pageHeight) break;
      copyTexel(index(px, py), bottomSrc);
    }
  }
}
