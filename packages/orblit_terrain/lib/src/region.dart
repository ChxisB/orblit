import 'dart:convert';
import 'dart:typed_data';

import 'cover.dart';
import 'ground_colour.dart';

/// What a region's file name ends in.
const String regionExtension = '.oregion';

/// Which square of the world a region is: the `x`th along and the `z`th
/// across, counting in regions. Region (0, 0) starts at the origin and runs
/// towards +x and +z; region (−1, 0) is the one just short of it.
final class RegionKey {
  const RegionKey(this.x, this.z);

  /// The region holding texel ([i], [j]) of a terrain whose regions are
  /// [size] texels across.
  factory RegionKey.containing(int i, int j, int size) =>
      RegionKey(floorDiv(i, size), floorDiv(j, size));

  /// The region a file is named for, or null when the name is not one a
  /// region is written under.
  static RegionKey? fromFileName(String name) {
    final match = _fileName.firstMatch(name);
    if (match == null) return null;
    return RegionKey(int.parse(match[1]!), int.parse(match[2]!));
  }

  static final RegExp _fileName = RegExp(r'^x(-?\d+)_z(-?\d+)\.oregion$');

  final int x;
  final int z;

  /// The name this region is written under, beside the terrain's own file.
  String get fileName => 'x${x}_z$z$regionExtension';

  @override
  bool operator ==(Object other) =>
      other is RegionKey && other.x == x && other.z == z;

  @override
  int get hashCode => Object.hash(x, z);

  @override
  String toString() => 'RegionKey($x, $z)';
}

/// [a] divided by [b] and rounded down, for a positive [b]. Dart's `~/`
/// rounds towards zero, which puts texel −1 in region 0; and `>>` on a
/// negative number is not the same on the web.
int floorDiv(int a, int b) => (a - a % b) ~/ b;

/// One square of ground: [size] texels a side, each with a height, a [Cover]
/// and a [GroundColour].
///
/// Texel (`i`, `j`) sits `i` texels along +x and `j` along +z from the
/// region's corner, and is stored at `j * size + i` in every map. The maps
/// are handed out as they are, so a brush can write a thousand texels
/// without a thousand calls; whoever writes into them directly calls
/// [touch] afterwards, and the setters here do it themselves.
class TerrainRegion {
  /// A region of flat ground at height 0 under [Cover.auto] and no colour,
  /// or of the maps given.
  TerrainRegion(
    this.key,
    this.size, {
    Float32List? heights,
    Uint32List? cover,
    Uint8List? colour,
  }) : heights = heights ?? Float32List(size * size),
       cover = cover ?? _filledCover(size),
       colour = colour ?? _filledColour(size) {
    if (!validSize(size)) {
      throw ArgumentError.value(
        size,
        'size',
        'A region is a power of two texels across, from $minSize to $maxSize.',
      );
    }
    _checkLength('heights', this.heights.length, size * size);
    _checkLength('cover', this.cover.length, size * size);
    _checkLength('colour', this.colour.length, size * size * 4);
  }

  static const String marker = 'orblit.region';
  static const int formatVersion = 1;

  /// The steps that bring an older region file up to [formatVersion], oldest
  /// first. Empty: this is the first format.
  static const List<RegionMigration> migrations = [];

  static const int minSize = 2;
  static const int maxSize = 4096;

  /// Whether [size] can be a region's: a power of two, [minSize] to
  /// [maxSize].
  static bool validSize(int size) =>
      size >= minSize && size <= maxSize && size & (size - 1) == 0;

  final RegionKey key;

  /// Texels a side.
  final int size;

  /// Heights in metres, one per texel.
  final Float32List heights;

  /// [Cover] words, one per texel.
  final Uint32List cover;

  /// [GroundColour] bytes, four per texel: red, green, blue, roughness.
  final Uint8List colour;

  static int _lastRevision = 0;

  /// Changes whenever the maps do, and is never the same for two regions or
  /// two states of one. A renderer that remembers the revision it sent knows
  /// whether to send again, even for a region removed and put back.
  int get revision => _revision;
  int _revision = ++_lastRevision;

  /// Says the maps have changed. Called by the setters here; call it after
  /// writing into [heights], [cover] or [colour] directly.
  void touch() {
    _revision = ++_lastRevision;
    _range = null;
  }

  int indexOf(int i, int j) {
    RangeError.checkValueInInterval(i, 0, size - 1, 'i');
    RangeError.checkValueInInterval(j, 0, size - 1, 'j');
    return j * size + i;
  }

  double heightAt(int i, int j) => heights[indexOf(i, j)];

  void setHeight(int i, int j, double height) {
    heights[indexOf(i, j)] = height;
    touch();
  }

  Cover coverAt(int i, int j) => Cover(cover[indexOf(i, j)]);

  void setCover(int i, int j, Cover value) {
    cover[indexOf(i, j)] = value.word;
    touch();
  }

  GroundColour colourAt(int i, int j) {
    final at = indexOf(i, j) * 4;
    return GroundColour.bytes(
      colour[at],
      colour[at + 1],
      colour[at + 2],
      colour[at + 3],
    );
  }

  void setColour(int i, int j, GroundColour value) {
    final at = indexOf(i, j) * 4;
    colour
      ..[at] = value.red
      ..[at + 1] = value.green
      ..[at + 2] = value.blue
      ..[at + 3] = value.roughnessByte;
    touch();
  }

  /// The lowest height in the region, holes and all.
  double get minHeight => (_range ??= _measure()).$1;

  /// The highest height in the region, holes and all.
  double get maxHeight => (_range ??= _measure()).$2;

  (double, double)? _range;

  (double, double) _measure() {
    var low = double.infinity;
    var high = double.negativeInfinity;
    for (final height in heights) {
      if (height < low) low = height;
      if (height > high) high = height;
    }
    return (low, high);
  }

  /// The region as a file: a line of JSON saying what follows, padded to
  /// four bytes, then each map that is not still at its default, in the
  /// order the line lists them, little-endian.
  ///
  /// Binary because the maps are: a 256² region is a quarter of a million
  /// heights, and a quarter of a million numbers written out as text would
  /// be six times the size and not one of them readable.
  Uint8List encode() {
    final maps = <(String, Uint8List)>[
      if (heights.any((height) => height != 0))
        ('height', _floatBytes(heights)),
      if (cover.any((word) => word != Cover.auto.word))
        ('cover', _wordBytes(cover)),
      if (!_isDefaultColour(colour)) ('colour', Uint8List.fromList(colour)),
    ];
    final header = utf8.encode(
      '${jsonEncode({
        'kind': marker,
        'formatVersion': formatVersion,
        'x': key.x,
        'z': key.z,
        'size': size,
        'maps': [
          for (final (name, bytes) in maps) {'name': name, 'bytes': bytes.length},
        ],
      })}\n',
    );
    final start = _aligned(header.length);
    final out = BytesBuilder(copy: false)
      ..add(header)
      ..add(Uint8List(start - header.length));
    for (final (_, bytes) in maps) {
      out.add(bytes);
    }
    return out.takeBytes();
  }

  /// A region out of a file's bytes, with whatever could not be read.
  ///
  /// Strict about the maps: a map whose length is not the region's is a
  /// damaged file, and half a height map is worse than none, so it throws
  /// [RegionFormatException] like a file that is not a region or is one from
  /// a newer Orblit. A map this version does not know is left out with a
  /// note.
  static RegionLoad decode(Uint8List bytes) {
    final end = bytes.indexOf(0x0A);
    if (end < 0) {
      throw const RegionFormatException('This is not a region file.');
    }
    final Object? parsed;
    try {
      parsed = jsonDecode(utf8.decode(Uint8List.sublistView(bytes, 0, end)));
    } on FormatException catch (error) {
      throw RegionFormatException(
        'This is not a region file: ${error.message}',
      );
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw const RegionFormatException('This is not a region file.');
    }
    final version = parsed['formatVersion'];
    if (version is int && version > formatVersion) {
      throw const RegionFormatException(
        'This region was written by a newer Orblit.',
      );
    }

    final maps = <String, Uint8List>{};
    var at = _aligned(end + 1);
    final listed = parsed['maps'];
    for (final entry in listed is List ? listed : const <Object?>[]) {
      final name = entry is Map<String, Object?> ? entry['name'] : null;
      final length = entry is Map<String, Object?> ? entry['bytes'] : null;
      if (name is! String || length is! int || length < 0) {
        throw const RegionFormatException('A map in this region is unnamed.');
      }
      if (at + length > bytes.length) {
        throw RegionFormatException('The $name map is cut short.');
      }
      maps[name] = Uint8List.sublistView(bytes, at, at + length);
      at += length;
    }

    final problems = <String>[];
    var parts = RegionParts(parsed, maps);
    for (final step in migrations) {
      if (version is int && step.from >= version) {
        parts = step.apply(parts, problems);
      }
    }

    final x = parts.header['x'];
    final z = parts.header['z'];
    final size = parts.header['size'];
    if (x is! int || z is! int || size is! int || !validSize(size)) {
      throw const RegionFormatException(
        'This region does not say where it is or how big.',
      );
    }
    Uint8List? take(String name, int length) {
      final bytes = parts.maps.remove(name);
      if (bytes != null && bytes.length != length) {
        throw RegionFormatException(
          'The $name map is ${bytes.length} bytes; a region $size across '
          'needs $length.',
        );
      }
      return bytes;
    }

    final texels = size * size;
    final height = take('height', texels * 4);
    final cover = take('cover', texels * 4);
    final colour = take('colour', texels * 4);
    for (final name in parts.maps.keys) {
      problems.add('A "$name" map this Orblit does not know was left out.');
    }
    return RegionLoad(
      region: TerrainRegion(
        RegionKey(x, z),
        size,
        heights: height == null ? null : _readFloats(height),
        cover: cover == null ? null : _readWords(cover),
        colour: colour == null ? null : Uint8List.fromList(colour),
      ),
      problems: problems,
    );
  }

  static Uint32List _filledCover(int size) =>
      Uint32List(size * size)..fillRange(0, size * size, Cover.auto.word);

  static Uint8List _filledColour(int size) {
    final none = GroundColour.none;
    final bytes = Uint8List(size * size * 4);
    for (var at = 0; at < bytes.length; at += 4) {
      bytes
        ..[at] = none.red
        ..[at + 1] = none.green
        ..[at + 2] = none.blue
        ..[at + 3] = none.roughnessByte;
    }
    return bytes;
  }

  static bool _isDefaultColour(Uint8List bytes) {
    final none = GroundColour.none;
    for (var at = 0; at < bytes.length; at += 4) {
      if (bytes[at] != none.red ||
          bytes[at + 1] != none.green ||
          bytes[at + 2] != none.blue ||
          bytes[at + 3] != none.roughnessByte) {
        return false;
      }
    }
    return true;
  }

  static void _checkLength(String name, int length, int expected) {
    if (length != expected) {
      throw ArgumentError.value(length, name, 'Expected $expected entries.');
    }
  }

  static int _aligned(int length) => (length + 3) & ~3;

  static Uint8List _floatBytes(Float32List values) {
    final data = ByteData(values.length * 4);
    for (var n = 0; n < values.length; n++) {
      data.setFloat32(n * 4, values[n], Endian.little);
    }
    return data.buffer.asUint8List();
  }

  static Uint8List _wordBytes(Uint32List values) {
    final data = ByteData(values.length * 4);
    for (var n = 0; n < values.length; n++) {
      data.setUint32(n * 4, values[n], Endian.little);
    }
    return data.buffer.asUint8List();
  }

  static Float32List _readFloats(Uint8List bytes) {
    final data = ByteData.sublistView(bytes);
    final values = Float32List(bytes.length ~/ 4);
    for (var n = 0; n < values.length; n++) {
      values[n] = data.getFloat32(n * 4, Endian.little);
    }
    return values;
  }

  static Uint32List _readWords(Uint8List bytes) {
    final data = ByteData.sublistView(bytes);
    final values = Uint32List(bytes.length ~/ 4);
    for (var n = 0; n < values.length; n++) {
      values[n] = data.getUint32(n * 4, Endian.little);
    }
    return values;
  }
}

/// What reading a region file gave: the region, and what had to be left out.
class RegionLoad {
  const RegionLoad({required this.region, this.problems = const []});

  final TerrainRegion region;
  final List<String> problems;
}

/// A region file that cannot be read at all.
class RegionFormatException implements Exception {
  const RegionFormatException(this.message);

  final String message;

  @override
  String toString() => 'RegionFormatException: $message';
}

/// A region file taken apart: the header line, decoded, and each map's
/// bytes by name. What a [RegionMigration] works on, since a step may need
/// to rewrite a map as well as the line that describes it.
class RegionParts {
  RegionParts(this.header, this.maps);

  final Map<String, Object?> header;
  final Map<String, Uint8List> maps;
}

/// One step between region formats: parts at [from] in, at [to] out.
abstract class RegionMigration {
  const RegionMigration();

  int get from;

  int get to => from + 1;

  RegionParts apply(RegionParts parts, List<String> notes);
}
