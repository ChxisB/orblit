import 'dart:math' as math;
import 'dart:typed_data';

/// One region of a terrain as the renderer takes it: a square of ground
/// [size] texels a side, at region ([x], [z]).
///
/// The three maps are meant to be the very lists the terrain's own data keeps,
/// so handing a region over copies nothing. They cross to the renderer only
/// when [revision] is not the one it last took — the bargain a population
/// makes — so a scene that is only looked at sends no ground at all, and a
/// brush stroke sends the regions it touched.
class OrblitTerrainRegion {
  OrblitTerrainRegion({
    required this.x,
    required this.z,
    required this.heights,
    required this.cover,
    required this.colour,
    this.revision = 0,
  }) : size = math.sqrt(heights.length).round() {
    if (size * size != heights.length ||
        cover.length != heights.length ||
        colour.length != heights.length * 4) {
      throw ArgumentError(
        'A region is a square of heights, as many cover words, and four '
        'colour bytes a texel; these are ${heights.length}, ${cover.length} '
        'and ${colour.length}.',
      );
    }
  }

  /// Where it is, in regions: region (x, z) starts at x × region size ×
  /// spacing metres along x, and the same along z.
  final int x;
  final int z;

  /// Texels a side.
  final int size;

  /// A height a texel, in metres, a row of x at a time.
  final Float32List heights;

  /// A cover word a texel: which sets cover it, how they blend, how they are
  /// turned and scaled, and whether there is ground there at all.
  final Uint32List cover;

  /// Four bytes a texel: a tint multiplied over the sets, and in alpha a
  /// shift to their roughness — 128 leaves it alone, 0 and 255 take it to
  /// either end.
  final Uint8List colour;

  /// Bumped by whoever writes into the maps.
  final int revision;
}

/// One kind of ground a cover word can name: a picture, and how it is laid.
///
/// Every set of a terrain has pictures the same size,
/// [OrblitTerrain.textureSize] a side, because they are layers of one array.
class OrblitTerrainSet {
  const OrblitTerrainSet({
    this.albedo,
    this.normal,
    this.tileSize = 4,
    this.triplanar = false,
  });

  /// Its colour, sRGB, with a height in alpha that decides which of two sets
  /// shows where they blend: RGBA bytes, a row at a time. Null is plain pale
  /// grey at half height.
  final Uint8List? albedo;

  /// Its surface's normal, as a normal map stores one, with roughness in
  /// alpha: RGBA bytes like [albedo]. Null faces straight out, fairly rough.
  final Uint8List? normal;

  /// Metres one copy of the picture covers.
  final double tileSize;

  /// Laid from the three sides rather than from above, for cliffs, where a
  /// picture laid from above is stretched down the face. Costs three reads
  /// for one, and only where the set is.
  final bool triplanar;

  /// Floats a set. Must match kTerrainSetParams.
  static const int stride = 1;

  /// The pixel a missing [albedo] or [normal] is made of.
  static const List<int> plainAlbedo = [190, 190, 190, 128];
  static const List<int> plainNormal = [128, 128, 255, 230];
}

/// Ground: regions of heights, what covers them, and the sets they name.
///
/// Drawn as one grid, a few times over at doubling sizes round the camera, and
/// raised by the heights on the GPU — so however much ground there is, it is a
/// handful of draws, and changing it is sending a region.
///
/// The pictures cross to the renderer only when [picturesRevision] is not the
/// one it last took, or the sets or their size change. Everything else about
/// a terrain is sent every frame and costs nothing to change.
class OrblitTerrain {
  OrblitTerrain({
    required this.key,
    this.regions = const [],
    this.regionSize = 256,
    this.spacing = 1,
    this.sets = const [],
    this.picturesRevision = 0,
    this.blendSharpness = 0.87,
    this.autoSteep = 0,
    this.autoFlat = 1,
    this.autoSlope = 1,
    this.autoHeightFalloff = 0.1,
    this.meshSize = 64,
    this.levels = 6,
    this.castShadows = true,
    this.receiveShadows = true,
  }) : textureSize = _textureSizeOf(sets) {
    final problem = _problem();
    if (problem != null) throw ArgumentError(problem);
  }

  /// What this terrain is, across frames.
  final int key;

  /// The regions there is ground in. Anywhere else there is none.
  final List<OrblitTerrainRegion> regions;

  /// Texels a region is across: a power of two, from [minRegionSize] to
  /// [maxRegionSize].
  final int regionSize;

  /// Metres between texels.
  final double spacing;

  /// The kinds of ground, by the index a cover word names them with. At most
  /// [maxSets].
  final List<OrblitTerrainSet> sets;

  /// Bumped by whoever changes a set's pictures.
  final int picturesRevision;

  /// How sharply one set gives way to another where they blend: 0 is a smooth
  /// fade, 1 a hard edge along the taller of the two.
  final double blendSharpness;

  /// The sets ground marked automatic is covered by: [autoSteep] on steep or
  /// high ground, [autoFlat] on level, low ground, and a blend between.
  final int autoSteep;
  final int autoFlat;

  /// How quickly steepness hands over to [autoSteep]: at 1, ground tilted 60°
  /// is all of it.
  final double autoSlope;

  /// How quickly height hands over to [autoSteep], per hundred metres.
  final double autoHeightFalloff;

  /// Cells from the camera to the edge of the finest grid, which is twice as
  /// many across. Even, from [minMeshSize] to [maxMeshSize].
  final int meshSize;

  /// How many times the grid is drawn, each twice the size of the one inside
  /// it, so the ground reaches `meshSize × spacing × 2^(levels − 1)` metres
  /// from the camera. From 1 to [maxLevels].
  final int levels;

  final bool castShadows;
  final bool receiveShadows;

  /// Pixels a side of every set's pictures, read off the first set that has
  /// one; 1 when none does.
  final int textureSize;

  /// Whole numbers at the head of each terrain. Must match kTerrainInts.
  static const int headerInts = 12;

  /// Whole numbers a region. Must match kTerrainRegionInts.
  static const int regionInts = 3;

  /// Floats a terrain, before its sets'. Must match kTerrainParams.
  static const int stride = 4;

  /// The renderer's limits, which are OpenGL ES 3.0's: a texture 2048 across
  /// and an array 256 layers deep.
  static const int minRegionSize = 16;
  static const int maxRegionSize = 2048;
  static const int maxRegions = 256;

  /// Regions from the westmost to the eastmost, and from the northmost to
  /// the southmost, short of which a terrain's regions must lie.
  static const int maxSpan = 128;
  static const int maxSets = 32;
  static const int minMeshSize = 16;
  static const int maxMeshSize = 256;
  static const int maxLevels = 12;
  static const int maxTextureSize = 4096;

  /// The bits the renderer reads, in the order OrblitTerrain.h names them.
  int get flags => (castShadows ? 1 : 0) | (receiveShadows ? 2 : 0);

  /// A bit a set, for the sets laid from three sides.
  int get triplanarMask {
    var mask = 0;
    for (var i = 0; i < sets.length; i++) {
      if (sets[i].triplanar) mask |= 1 << i;
    }
    return mask;
  }

  /// Bytes of pictures: every set's albedo, then every set's normal.
  int get picturesLength => sets.length * textureSize * textureSize * 4 * 2;

  /// Bytes of a region's maps: heights, cover, colour.
  int get regionLength => regionSize * regionSize * 12;

  /// Writes this terrain's [headerInts] whole numbers at [at], then its
  /// regions' [regionInts] each. [picturesArrive] says whether its pictures
  /// are in the message, and [arrived] which regions' maps are, in order.
  void writeInts(
    Int32List into,
    int at, {
    required bool picturesArrive,
    required List<bool> arrived,
  }) {
    into
      ..[at] = key
      ..[at + 1] = flags
      ..[at + 2] = regionSize
      ..[at + 3] = meshSize
      ..[at + 4] = levels
      ..[at + 5] = autoSteep
      ..[at + 6] = autoFlat
      ..[at + 7] = sets.length
      ..[at + 8] = textureSize
      ..[at + 9] = picturesArrive ? 1 : 0
      ..[at + 10] = triplanarMask
      ..[at + 11] = regions.length;
    at += headerInts;
    for (var i = 0; i < regions.length; i++) {
      into
        ..[at] = regions[i].x
        ..[at + 1] = regions[i].z
        ..[at + 2] = arrived[i] ? 1 : 0;
      at += regionInts;
    }
  }

  /// Writes this terrain's [stride] floats at [at], then its sets'
  /// [OrblitTerrainSet.stride] each.
  void writeFloats(Float32List into, int at) {
    into
      ..[at] = spacing
      ..[at + 1] = blendSharpness
      ..[at + 2] = autoSlope
      ..[at + 3] = autoHeightFalloff;
    at += stride;
    for (final set in sets) {
      into[at] = set.tileSize;
      at += OrblitTerrainSet.stride;
    }
  }

  /// Writes every set's albedo and then every set's normal at [at], a plain
  /// picture for any that has none.
  void writePictures(Uint8List into, int at) {
    final layer = textureSize * textureSize * 4;
    for (var pass = 0; pass < 2; pass++) {
      for (final set in sets) {
        final picture = pass == 0 ? set.albedo : set.normal;
        if (picture != null) {
          into.setRange(at, at + layer, picture);
        } else {
          final plain = pass == 0
              ? OrblitTerrainSet.plainAlbedo
              : OrblitTerrainSet.plainNormal;
          for (var i = 0; i < layer; i += 4) {
            into.setRange(at + i, at + i + 4, plain);
          }
        }
        at += layer;
      }
    }
  }

  /// Writes [region]'s heights, cover and colour at [at], as the renderer
  /// reads them: little-endian, which every platform Flutter runs on is.
  static void writeRegion(Uint8List into, int at, OrblitTerrainRegion region) {
    final texels = region.heights.length;
    final heights = region.heights;
    final cover = region.cover;
    into.setRange(
      at,
      at + texels * 4,
      heights.buffer.asUint8List(heights.offsetInBytes, texels * 4),
    );
    at += texels * 4;
    into.setRange(
      at,
      at + texels * 4,
      cover.buffer.asUint8List(cover.offsetInBytes, texels * 4),
    );
    at += texels * 4;
    into.setRange(at, at + texels * 4, region.colour);
  }

  static int _textureSizeOf(List<OrblitTerrainSet> sets) {
    for (final set in sets) {
      final picture = set.albedo ?? set.normal;
      if (picture != null) return math.sqrt(picture.length ~/ 4).round();
    }
    return 1;
  }

  /// What the renderer would refuse, said here instead: it refuses the whole
  /// scene message, which stops the view.
  String? _problem() {
    if (regionSize < minRegionSize ||
        regionSize > maxRegionSize ||
        regionSize & (regionSize - 1) != 0) {
      return 'A region is a power of two texels across, from $minRegionSize '
          'to $maxRegionSize; this is $regionSize.';
    }
    if (!spacing.isFinite || spacing <= 0) {
      return 'Spacing is above zero; this is $spacing.';
    }
    if (meshSize.isOdd || meshSize < minMeshSize || meshSize > maxMeshSize) {
      return 'The mesh is an even number of cells from $minMeshSize to '
          '$maxMeshSize; this is $meshSize.';
    }
    if (levels < 1 || levels > maxLevels) {
      return 'There are 1 to $maxLevels levels; this is $levels.';
    }
    if (sets.length > maxSets) {
      return 'A terrain has at most $maxSets sets; this has ${sets.length}.';
    }
    if (autoSteep < 0 ||
        autoSteep >= maxSets ||
        autoFlat < 0 ||
        autoFlat >= maxSets) {
      return 'The automatic cover names sets 0 to ${maxSets - 1}; these are '
          '$autoSteep and $autoFlat.';
    }
    if (!blendSharpness.isFinite ||
        !autoSlope.isFinite ||
        !autoHeightFalloff.isFinite) {
      return 'The blend and the automatic cover are finite numbers.';
    }
    if (textureSize < 1 || textureSize > maxTextureSize) {
      return 'Pictures are 1 to $maxTextureSize pixels across; these are '
          '$textureSize.';
    }
    final picture = textureSize * textureSize * 4;
    for (var i = 0; i < sets.length; i++) {
      final set = sets[i];
      if (!set.tileSize.isFinite || set.tileSize <= 0) {
        return 'Set $i covers ${set.tileSize} metres a copy; it must be '
            'above zero.';
      }
      for (final bytes in [set.albedo, set.normal]) {
        if (bytes != null && bytes.length != picture) {
          return 'Every picture is $textureSize pixels square, as the '
              'first is; set $i has one of ${bytes.length} bytes.';
        }
      }
    }
    if (regions.length > maxRegions) {
      return 'A terrain has at most $maxRegions regions; this has '
          '${regions.length}.';
    }
    final seen = <(int, int)>{};
    if (regions.isNotEmpty) {
      final xs = regions.map((region) => region.x);
      final zs = regions.map((region) => region.z);
      final wide = xs.reduce(math.max) - xs.reduce(math.min);
      final deep = zs.reduce(math.max) - zs.reduce(math.min);
      // The renderer leaves such a region out rather than refusing it, and
      // then the view would count it as held and never send it again.
      if (wide >= maxSpan || deep >= maxSpan) {
        return 'A terrain\'s regions lie within $maxSpan of each other each '
            'way; these reach $wide across and $deep deep.';
      }
    }
    for (final region in regions) {
      if (region.size != regionSize) {
        return 'Region (${region.x}, ${region.z}) is ${region.size} across '
            'and the terrain\'s are $regionSize.';
      }
      if (!seen.add((region.x, region.z))) {
        return 'Region (${region.x}, ${region.z}) is named twice.';
      }
    }
    return null;
  }
}

/// What one renderer already holds of a terrain, so a message can leave it
/// out.
///
/// Kept by the view rather than the terrain, for the reason populations'
/// revisions are: two views of one scene have been sent different things.
class OrblitTerrainHeld {
  OrblitTerrainHeld.of(OrblitTerrain terrain)
    : regionSize = terrain.regionSize,
      picturesRevision = terrain.picturesRevision,
      setCount = terrain.sets.length,
      textureSize = terrain.textureSize,
      regions = {
        for (final region in terrain.regions)
          (region.x, region.z): region.revision,
      };

  final int regionSize;
  final int picturesRevision;
  final int setCount;
  final int textureSize;
  final Map<(int, int), int> regions;

  /// Whether [terrain]'s pictures are the ones held. A change in the number
  /// of sets or their size is a change of pictures, whatever the revision
  /// says, because the renderer's arrays are the wrong shape for the old ones.
  bool holdsPictures(OrblitTerrain terrain) =>
      terrain.picturesRevision == picturesRevision &&
      terrain.sets.length == setCount &&
      terrain.textureSize == textureSize;

  /// Whether [region]'s maps are the ones held. A terrain whose regions
  /// changed size has none: the renderer drops them all.
  bool holdsRegion(OrblitTerrain terrain, OrblitTerrainRegion region) =>
      terrain.regionSize == regionSize &&
      regions[(region.x, region.z)] == region.revision;
}
