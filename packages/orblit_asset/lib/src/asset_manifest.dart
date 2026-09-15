import 'dart:convert';

import 'asset_id.dart';
import 'content_hash.dart';

/// Which bytes one asset currently is.
class AssetEntry {
  const AssetEntry(this.hash, this.bytes) : assert(bytes >= 0);

  final ContentHash hash;

  /// How many bytes that is.
  ///
  /// Carried alongside the hash, although the hash pins the bytes down on its
  /// own, because a size can be used before a single byte has arrived: to
  /// show a download's progress, to make room for it, or to notice a file cut
  /// short without reading all of it to hash.
  final int bytes;

  @override
  bool operator ==(Object other) =>
      other is AssetEntry && other.hash == hash && other.bytes == bytes;

  @override
  int get hashCode => Object.hash(hash, bytes);
}

/// A manifest read back from text, with anything that could not be read.
///
/// Problems are returned rather than thrown. A manifest with one broken entry
/// still says what every other asset is, and losing all of them over the one
/// is the worse outcome — whatever read it can say what it dropped.
class AssetManifestLoad {
  const AssetManifestLoad({required this.manifest, this.problems = const []});

  final AssetManifest manifest;

  /// One readable sentence for each entry that was left out.
  final List<String> problems;

  bool get hasProblems => problems.isNotEmpty;
}

/// Which content each of a project's assets currently is.
///
/// The one place an [AssetId] and a [ContentHash] meet. Everything else deals
/// in one or the other: scenes store ids, stores and caches deal in hashes.
/// Changing what `models/robot.glb` is means changing one line here, and
/// nothing already stored has to move or be renamed.
///
/// Written as JSON with a stable order, because a manifest lives in somebody's
/// repository, and one that reorders itself every time it is written turns
/// every commit into a diff nobody can review.
class AssetManifest {
  AssetManifest({Map<AssetId, AssetEntry> entries = const {}})
    : entries = Map.unmodifiable(entries);

  /// Bumped when the shape changes in a way an older reader could misread.
  static const int formatVersion = 1;

  static const JsonEncoder _encoder = JsonEncoder.withIndent('  ');

  /// Every asset and what it is. Cannot be changed; a changed manifest is a
  /// new one.
  final Map<AssetId, AssetEntry> entries;

  /// The manifest as JSON, the same bytes every time for the same entries.
  ///
  /// Assets are written in order of id however the map was built, and each
  /// entry's keys in a fixed order, with a trailing newline so that the last
  /// line of the file is not forever showing up as changed.
  String encode() {
    final ids = entries.keys.toList()
      ..sort((a, b) => a.toString().compareTo(b.toString()));
    final json = {
      'formatVersion': formatVersion,
      'assets': {
        for (final id in ids)
          id.toString(): {
            'bytes': entries[id]!.bytes,
            'hash': entries[id]!.hash.hex,
          },
      },
    };
    return '${_encoder.convert(json)}\n';
  }

  /// Reads what [encode] writes.
  ///
  /// Throws a [FormatException] for text that cannot be a manifest at all:
  /// not JSON, not an object, no format version, a version newer than this
  /// reads, or no assets. Any single entry that is wrong — an id that is not
  /// one, a hash that is not one, a size that is not a size — is left out and
  /// named in [AssetManifestLoad.problems] instead.
  static AssetManifestLoad read(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw FormatException(
        'This is not an asset manifest, because it is not JSON: '
        '${error.message}',
      );
    }

    if (parsed is! Map<String, Object?>) {
      throw const FormatException(
        'An asset manifest has to be a JSON object, and this is not one.',
      );
    }

    final version = parsed['formatVersion'];
    if (version is! int) {
      throw const FormatException(
        'This manifest does not say what format version it is, so it cannot '
        'be read safely.',
      );
    }
    if (version > formatVersion) {
      // Refused rather than half-read: a newer file may mean something
      // different by the same keys, and a manifest misread is assets quietly
      // resolved to the wrong bytes.
      throw FormatException(
        'This manifest was written by a newer Orblit (format $version; this '
        'one reads up to $formatVersion).',
      );
    }
    if (version < 1) {
      throw FormatException(
        'This manifest says it is format $version, which no Orblit has ever '
        'written.',
      );
    }

    // Refused rather than read as empty. Other Orblit files carry a format
    // version too, and one of those opened by mistake should say so, not
    // look like a project with no assets in it.
    final assets = parsed['assets'];
    if (assets is! Map<String, Object?>) {
      throw const FormatException(
        'This manifest has no "assets" object in it.',
      );
    }

    final entries = <AssetId, AssetEntry>{};
    final problems = <String>[];

    for (final MapEntry(:key, :value) in assets.entries) {
      final AssetId id;
      try {
        id = AssetId.parse(key);
      } on FormatException catch (error) {
        problems.add('${error.message} Its entry was left out.');
        continue;
      }

      if (value is! Map<String, Object?>) {
        problems.add(
          'The entry for "$id" was left out, because it is not an object.',
        );
        continue;
      }

      final hashText = value['hash'];
      if (hashText is! String) {
        problems.add(
          'The entry for "$id" was left out, because it has no hash.',
        );
        continue;
      }
      final ContentHash hash;
      try {
        hash = ContentHash.parse(hashText);
      } on FormatException catch (error) {
        problems.add('The entry for "$id" was left out. ${error.message}');
        continue;
      }

      final bytes = value['bytes'];
      if (bytes is! int) {
        problems.add(
          'The entry for "$id" was left out, because it does not give its '
          'size as a whole number of bytes.',
        );
        continue;
      }
      if (bytes < 0) {
        problems.add(
          'The entry for "$id" was left out, because its size is $bytes '
          'bytes, and a size cannot be negative.',
        );
        continue;
      }

      entries[id] = AssetEntry(hash, bytes);
    }

    return AssetManifestLoad(
      manifest: AssetManifest(entries: entries),
      problems: problems,
    );
  }
}
