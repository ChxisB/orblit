import 'dart:convert';

import 'package:vector_math/vector_math_64.dart';

import 'cover.dart';
import 'ground_colour.dart';
import 'region.dart';

/// What a terrain's settings file name ends in.
const String terrainExtension = '.oterrain';

/// One kind of ground a [Cover] can name: a pair of images and how they lie.
class TerrainSet {
  const TerrainSet({
    required this.name,
    this.albedo,
    this.normal,
    this.tileSize = 4,
    this.triplanar = false,
  });

  /// What the set is called where people choose it: "grass", "scree".
  final String name;

  /// The image whose colour is the ground's colour and whose alpha is its
  /// height, which is what lets one set show through another's gaps instead
  /// of fading into it. A path within the project.
  final String? albedo;

  /// The image whose colour is a normal map and whose alpha is roughness. A
  /// path within the project.
  final String? normal;

  /// How many metres one repeat of the images covers.
  final double tileSize;

  /// Whether the images are laid from three sides rather than dropped from
  /// above, so a cliff is not a smear. Costs three reads instead of one.
  final bool triplanar;

  Map<String, Object?> toJson() => {
    'name': name,
    'albedo': ?albedo,
    'normal': ?normal,
    'tileSize': tileSize,
    if (triplanar) 'triplanar': true,
  };

  static TerrainSet? fromJson(Object? raw, List<String> problems) {
    if (raw is! Map<String, Object?> || raw['name'] is! String) {
      problems.add('A texture set with no name was left out.');
      return null;
    }
    final tileSize = _number(raw, 'tileSize');
    if (tileSize != null && tileSize <= 0) {
      problems.add(
        'The "${raw['name']}" set repeated every $tileSize m; it repeats '
        'every 4 m now.',
      );
    }
    return TerrainSet(
      name: raw['name']! as String,
      albedo: _text(raw, 'albedo'),
      normal: _text(raw, 'normal'),
      tileSize: tileSize != null && tileSize > 0 ? tileSize : 4,
      triplanar: raw['triplanar'] == true,
    );
  }
}

/// How ground marked [Cover.automatic] chooses its sets: [steep] where the
/// slope is steep or the ground high, [flat] where it is level and low, and
/// a blend between.
class AutoCover {
  const AutoCover({
    this.steep = 0,
    this.flat = 1,
    this.slope = 1,
    this.heightFalloff = 0.1,
  });

  /// The set on steep and high ground. Cliffs, scree.
  final int steep;

  /// The set on level, low ground. Grass.
  final int flat;

  /// How quickly steepness hands over to [steep]. At 1, ground tilted 60°
  /// is all [steep]; at 2, ground tilted 41° is.
  final double slope;

  /// How quickly height hands over to [steep], per hundred metres: at 0.1,
  /// ground a thousand metres up is all [steep] however level it is.
  final double heightFalloff;

  /// How much of [flat] shows on ground whose normal points [normalY] up, at
  /// [height]: 1 is all [flat], 0 all [steep].
  double flatness(double normalY, double height) =>
      (slope * 2 * (normalY - 1) + 1 - heightFalloff * 0.01 * height).clamp(
        0.0,
        1.0,
      );

  /// The cover automatic ground has at that slope and height.
  Cover coverFor(double normalY, double height) =>
      Cover.of(base: steep, overlay: flat, blend: flatness(normalY, height));

  Map<String, Object?> toJson() => {
    'steep': steep,
    'flat': flat,
    'slope': slope,
    'heightFalloff': heightFalloff,
  };

  static AutoCover fromJson(Object? raw) {
    if (raw is! Map<String, Object?>) return const AutoCover();
    const fallback = AutoCover();
    return AutoCover(
      steep: _integer(raw, 'steep') ?? fallback.steep,
      flat: _integer(raw, 'flat') ?? fallback.flat,
      slope: _number(raw, 'slope') ?? fallback.slope,
      heightFalloff: _number(raw, 'heightFalloff') ?? fallback.heightFalloff,
    );
  }
}

/// Ground: a grid of heights, the sets that cover it, and the regions it is
/// stored in.
///
/// Texel (`i`, `j`) of the whole terrain sits at (`i × spacing`, `j ×
/// spacing`) in the world, and is texel (`i − x × regionSize`, `j − z ×
/// regionSize`) of region (`x`, `z`). Regions exist only where the ground
/// does; asking about anywhere else answers null.
///
/// The settings are one small file and each region is another beside it,
/// named by [RegionKey.fileName], so an edit to one corner of a large world
/// rewrites one region and not the world. Reading and writing the files is
/// left to the caller: this package knows their bytes and not where they
/// live, so it runs where there is no file system.
class Terrain {
  Terrain({
    this.regionSize = 256,
    this.spacing = 1,
    List<TerrainSet>? sets,
    this.autoCover = const AutoCover(),
    this.blendSharpness = 0.87,
  }) : sets = [...?sets] {
    if (!TerrainRegion.validSize(regionSize)) {
      throw ArgumentError.value(
        regionSize,
        'regionSize',
        'A region is a power of two texels across, from '
            '${TerrainRegion.minSize} to ${TerrainRegion.maxSize}.',
      );
    }
    if (!(spacing > 0) || !spacing.isFinite) {
      throw ArgumentError.value(spacing, 'spacing', 'Must be above zero.');
    }
    if (this.sets.length > Cover.setCount) {
      throw ArgumentError.value(
        this.sets.length,
        'sets',
        'A terrain has at most ${Cover.setCount} sets.',
      );
    }
  }

  static const String marker = 'orblit.terrain';
  static const int formatVersion = 1;

  /// The steps that bring an older terrain file up to [formatVersion],
  /// oldest first. Empty: this is the first format.
  static const List<TerrainMigration> migrations = [];

  /// Texels a region is across.
  final int regionSize;

  /// Metres between texels.
  final double spacing;

  /// The kinds of ground a [Cover] can name, by index. At most
  /// [Cover.setCount]. The terrain's own copy of the list it was made with,
  /// so it can be changed whatever that list was.
  final List<TerrainSet> sets;

  /// How ground marked [Cover.automatic] chooses its sets.
  AutoCover autoCover;

  /// How sharply one set gives way to another where they blend: 0 is a
  /// smooth fade, 1 a hard edge along the taller of the two.
  double blendSharpness;

  final Map<RegionKey, TerrainRegion> _regions = {};

  Iterable<TerrainRegion> get regions => _regions.values;

  TerrainRegion? regionAt(RegionKey key) => _regions[key];

  /// The region at [key], made flat and empty if there was none.
  TerrainRegion addRegion(RegionKey key) =>
      _regions.putIfAbsent(key, () => TerrainRegion(key, regionSize));

  /// Puts [region] in, replacing any at its key. Throws if it is not
  /// [regionSize] across.
  void putRegion(TerrainRegion region) {
    if (region.size != regionSize) {
      throw ArgumentError.value(
        region.size,
        'region',
        'This terrain\'s regions are $regionSize across.',
      );
    }
    _regions[region.key] = region;
  }

  TerrainRegion? removeRegion(RegionKey key) => _regions.remove(key);

  /// The region the world point ([x], [z]) lies in, whether or not it
  /// exists.
  RegionKey keyAt(double x, double z) => RegionKey.containing(
    (x / spacing).floor(),
    (z / spacing).floor(),
    regionSize,
  );

  /// Sets every height of the region at [key] from [height], asked at each
  /// texel's world position. Makes the region if it was not there.
  TerrainRegion fillHeights(
    RegionKey key,
    double Function(double x, double z) height,
  ) {
    final region = addRegion(key);
    final i0 = key.x * regionSize;
    final j0 = key.z * regionSize;
    for (var j = 0; j < regionSize; j++) {
      for (var i = 0; i < regionSize; i++) {
        region.heights[j * regionSize + i] = height(
          (i0 + i) * spacing,
          (j0 + j) * spacing,
        );
      }
    }
    region.touch();
    return region;
  }

  /// The height of texel ([i], [j]) of the whole terrain, holes included,
  /// or null where there is no region.
  double? texelHeight(int i, int j) {
    final region = _regions[RegionKey.containing(i, j, regionSize)];
    return region?.heights[_indexIn(region, i, j)];
  }

  /// The cover of texel ([i], [j]), or null where there is no region.
  Cover? texelCover(int i, int j) {
    final region = _regions[RegionKey.containing(i, j, regionSize)];
    return region == null ? null : Cover(region.cover[_indexIn(region, i, j)]);
  }

  /// The colour of texel ([i], [j]), or null where there is no region.
  GroundColour? texelColour(int i, int j) {
    final region = _regions[RegionKey.containing(i, j, regionSize)];
    if (region == null) return null;
    final at = _indexIn(region, i, j) * 4;
    final bytes = region.colour;
    return GroundColour.bytes(
      bytes[at],
      bytes[at + 1],
      bytes[at + 2],
      bytes[at + 3],
    );
  }

  int _indexIn(TerrainRegion region, int i, int j) =>
      (j - region.key.z * regionSize) * regionSize +
      (i - region.key.x * regionSize);

  /// The height of the texel, or null where there is no ground to stand on:
  /// no region, or a hole.
  double? _solidHeight(int i, int j) {
    final region = _regions[RegionKey.containing(i, j, regionSize)];
    if (region == null) return null;
    final at = _indexIn(region, i, j);
    if (Cover(region.cover[at]).hole) return null;
    return region.heights[at];
  }

  /// The height of the ground at world ([x], [z]), exactly as it is drawn
  /// nearby, or null where nothing is drawn.
  ///
  /// Each square of four texels is two triangles split along the diagonal
  /// from its low corner to its high one, the same two the mesh draws, so a
  /// foot put here meets the surface the eye sees rather than a smoothed
  /// guess at it. A triangle with a hole or a missing region at any corner
  /// is not drawn, and has no height.
  double? heightAt(double x, double z) {
    if (!x.isFinite || !z.isFinite) return null;
    final u = x / spacing;
    final v = z / spacing;
    final i = u.floor();
    final j = v.floor();
    final fu = u - i;
    final fv = v - j;
    final h00 = _solidHeight(i, j);
    final h11 = _solidHeight(i + 1, j + 1);
    if (h00 == null || h11 == null) return null;
    if (fu >= fv) {
      final h10 = _solidHeight(i + 1, j);
      if (h10 == null) return null;
      return h00 + fu * (h10 - h00) + fv * (h11 - h10);
    }
    final h01 = _solidHeight(i, j + 1);
    if (h01 == null) return null;
    return h00 + fv * (h01 - h00) + fu * (h11 - h01);
  }

  /// Which way the ground faces at world ([x], [z]), the way it is lit: a
  /// slope worked out at each of the four nearest texels and blended
  /// between them, so it turns smoothly across a triangle instead of
  /// jumping at its edges. Null only where none of the four texels exists.
  ///
  /// Holes make no difference here. A texel with no neighbour on one side
  /// takes its own height for the missing one.
  Vector3? normalAt(double x, double z) {
    if (!x.isFinite || !z.isFinite) return null;
    final u = x / spacing;
    final v = z / spacing;
    final i = u.floor();
    final j = v.floor();
    final fu = u - i;
    final fv = v - j;
    var gx = 0.0;
    var gz = 0.0;
    var total = 0.0;
    void corner(int ci, int cj, double weight) {
      if (weight == 0) return;
      final centre = texelHeight(ci, cj);
      if (centre == null) return;
      gx +=
          weight *
          ((texelHeight(ci + 1, cj) ?? centre) -
              (texelHeight(ci - 1, cj) ?? centre));
      gz +=
          weight *
          ((texelHeight(ci, cj + 1) ?? centre) -
              (texelHeight(ci, cj - 1) ?? centre));
      total += weight;
    }

    corner(i, j, (1 - fu) * (1 - fv));
    corner(i + 1, j, fu * (1 - fv));
    corner(i, j + 1, (1 - fu) * fv);
    corner(i + 1, j + 1, fu * fv);
    if (total == 0) return null;
    final scale = 1 / (total * 2 * spacing);
    return Vector3(-gx * scale, 1, -gz * scale)..normalize();
  }

  /// The cover of the texel nearest world ([x], [z]), as it is stored:
  /// [Cover.automatic] ground says so rather than naming its sets. Null
  /// where there is no region.
  Cover? coverAt(double x, double z) {
    if (!x.isFinite || !z.isFinite) return null;
    return texelCover((x / spacing).round(), (z / spacing).round());
  }

  Map<String, Object?> toJson() {
    final keys = _regions.keys.toList()
      ..sort((a, b) => a.z != b.z ? a.z.compareTo(b.z) : a.x.compareTo(b.x));
    return {
      'kind': marker,
      'formatVersion': formatVersion,
      'regionSize': regionSize,
      'spacing': spacing,
      'blendSharpness': blendSharpness,
      'autoCover': autoCover.toJson(),
      'sets': [for (final set in sets) set.toJson()],
      'regions': [
        for (final key in keys) [key.x, key.z],
      ],
    };
  }

  /// The settings file's text, with a key to a line and a set to a line, so
  /// the diff of two versions is the settings that changed. The regions are
  /// listed and not included: each is its own file.
  String encode() {
    final lines = <String>[];
    for (final MapEntry(:key, :value) in toJson().entries) {
      final text = key == 'sets' && value is List && value.isNotEmpty
          ? '[\n${value.map((set) => '    ${jsonEncode(set)}').join(',\n')}\n  ]'
          : jsonEncode(value);
      lines.add('  ${jsonEncode(key)}: $text');
    }
    return '{\n${lines.join(',\n')}\n}\n';
  }

  /// A terrain's settings out of a file's text, with the regions it lists
  /// and whatever could not be read. The regions come back as keys and not
  /// as regions: each is a file of its own for the caller to read and
  /// [putRegion].
  ///
  /// Lenient about the parts, strict about the whole: a set or a setting
  /// that cannot be read is dropped with a note, and a file that is not a
  /// terrain, is one from a newer Orblit, or does not say how big its
  /// regions are throws [TerrainFormatException].
  static TerrainLoad decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw TerrainFormatException(
        'This is not a terrain file: ${error.message}',
      );
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw const TerrainFormatException('This is not a terrain file.');
    }
    final version = parsed['formatVersion'];
    if (version is int && version > formatVersion) {
      throw const TerrainFormatException(
        'This terrain was written by a newer Orblit.',
      );
    }
    final problems = <String>[];
    var json = parsed;
    for (final step in migrations) {
      if (version is int && step.from >= version) {
        json = step.apply(json, problems);
      }
    }

    final regionSize = _integer(json, 'regionSize');
    if (regionSize == null || !TerrainRegion.validSize(regionSize)) {
      throw const TerrainFormatException(
        'This terrain does not say how big its regions are.',
      );
    }
    var spacing = _number(json, 'spacing') ?? 1;
    if (spacing <= 0) {
      problems.add('The texels were $spacing m apart; they are 1 m now.');
      spacing = 1;
    }

    final sets = <TerrainSet>[];
    final rawSets = json['sets'];
    for (final raw in rawSets is List ? rawSets : const <Object?>[]) {
      final set = TerrainSet.fromJson(raw, problems);
      if (set == null) continue;
      if (sets.length == Cover.setCount) {
        problems.add(
          'Sets past the ${Cover.setCount}th were left out, starting with '
          '"${set.name}".',
        );
        break;
      }
      sets.add(set);
    }

    final keys = <RegionKey>{};
    final rawRegions = json['regions'];
    for (final raw in rawRegions is List ? rawRegions : const <Object?>[]) {
      if (raw is List && raw.length == 2 && raw[0] is int && raw[1] is int) {
        if (!keys.add(RegionKey(raw[0] as int, raw[1] as int))) {
          problems.add('Region ${raw[0]}, ${raw[1]} was listed twice.');
        }
      } else {
        problems.add('A region that does not say where it is was left out.');
      }
    }

    return TerrainLoad(
      terrain: Terrain(
        regionSize: regionSize,
        spacing: spacing,
        sets: sets,
        autoCover: AutoCover.fromJson(json['autoCover']),
        blendSharpness: (_number(json, 'blendSharpness') ?? 0.87).clamp(
          0.0,
          1.0,
        ),
      ),
      regions: keys.toList(),
      problems: problems,
    );
  }
}

/// What reading a terrain file gave: the terrain with no regions in it yet,
/// the regions its file lists, and what had to be left out.
class TerrainLoad {
  const TerrainLoad({
    required this.terrain,
    this.regions = const [],
    this.problems = const [],
  });

  final Terrain terrain;
  final List<RegionKey> regions;
  final List<String> problems;
}

/// A terrain file that cannot be read at all.
class TerrainFormatException implements Exception {
  const TerrainFormatException(this.message);

  final String message;

  @override
  String toString() => 'TerrainFormatException: $message';
}

/// One step between terrain formats: decoded JSON at [from] in, at [to] out.
abstract class TerrainMigration {
  const TerrainMigration();

  int get from;

  int get to => from + 1;

  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes);
}

double? _number(Map<String, Object?> json, String key) {
  final value = json[key];
  return value is num && value.isFinite ? value.toDouble() : null;
}

int? _integer(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is int) return value;
  if (value is double && value.isFinite && value == value.roundToDouble()) {
    return value.toInt();
  }
  return null;
}

String? _text(Map<String, Object?> json, String key) {
  final value = json[key];
  return value is String && value.isNotEmpty ? value : null;
}
