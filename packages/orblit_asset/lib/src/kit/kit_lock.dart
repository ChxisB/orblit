import 'dart:convert';
import 'dart:typed_data';

import '../asset_id.dart';
import '../content_hash.dart';
import 'kit_json.dart';
import 'kit_manifest.dart';
import 'kit_rules.dart';

/// The file at the root of a project that says which kits were installed.
const String kitLockName = 'kits.lock';

/// The format of `kits.lock` this code writes, and the newest it reads.
const int kitLockFormat = 1;

const int _lockLimit = 8 * 1024 * 1024;
const int _kitsLimit = 500;

/// One kit an install put in a project, and what it wrote.
///
/// [files] are the files the person kept, at their place in the project, each
/// with the hash of the bytes the kit gave. A later update compares that hash
/// with what is on disk to tell a file the person edited from one they did
/// not. A file the person left out is not listed, so an update never writes
/// it.
final class LockedKit {
  /// Throws a [FormatException] when a name, the version or [into] is wrong
  /// or [files] holds too many files.
  LockedKit({
    required this.publisher,
    required this.slug,
    required this.version,
    required this.hash,
    required Map<AssetId, ContentHash> files,
    this.into = '',
  }) : files = Map.unmodifiable(files) {
    checkKitWord('publisher', publisher, 40);
    checkKitWord('slug', slug, 40);
    checkKitVersion('version', version);
    final misplaced = kitPlaceProblem(into, const []);
    if (misplaced != null) throw FormatException(misplaced);
    if (files.length > kitCountLimit) {
      throw FormatException(
        '$publisher/$slug lists ${files.length} files, over the limit of '
        '$kitCountLimit.',
      );
    }
  }

  factory LockedKit.fromJson(JsonObject json) {
    final files = json.section('files').map;
    return json.read(
      () => LockedKit(
        publisher: json.text('publisher'),
        slug: json.text('slug'),
        version: json.text('version'),
        hash: ContentHash.parse(json.text('hash')),
        into: json.text('into'),
        files: {
          for (final MapEntry(:key, :value) in files.entries)
            AssetId.parse(key): ContentHash.parse(
              value is String
                  ? value
                  : throw json.problem('has a file hash that is not text'),
            ),
        },
      ),
    );
  }

  final String publisher;
  final String slug;
  final String version;

  /// The hash of the kit file the files came from.
  final ContentHash hash;

  /// The folder of the project the kit was written under, or empty.
  final String into;

  final Map<AssetId, ContentHash> files;

  Map<String, Object?> toJson() => {
    'files': {
      for (final id in files.keys.toList()..sort(_byPath))
        '$id': files[id]!.hex,
    },
    'hash': hash.hex,
    'into': into,
    'publisher': publisher,
    'slug': slug,
    'version': version,
  };
}

int _byPath(AssetId a, AssetId b) => '$a'.compareTo('$b');

/// `kits.lock`: every kit installed in one project, sorted by publisher and
/// slug.
final class KitLock {
  KitLock._(this.kits);

  /// Throws a [FormatException] when a kit is listed twice or there are too
  /// many.
  factory KitLock(List<LockedKit> kits) {
    final names = {for (final kit in kits) '${kit.publisher}/${kit.slug}'};
    if (names.length != kits.length || kits.length > _kitsLimit) {
      throw const FormatException(
        'A project lists up to $_kitsLimit kits and no kit twice.',
      );
    }
    return KitLock._(
      List.unmodifiable(List<LockedKit>.of(kits)..sort(_byName)),
    );
  }

  /// A project with no kit installed.
  static final KitLock empty = KitLock._(const []);

  /// Reads `kits.lock`. Keys it does not know are ignored, and a newer format
  /// is refused.
  static KitLock parse(Uint8List bytes) {
    final json = JsonObject.parse(bytes, kitLockName, limit: _lockLimit);
    final format = json.integer('formatVersion');
    if (format > kitLockFormat) {
      throw FormatException(
        '$kitLockName was written by a newer Orblit (format $format; this one '
        'reads up to $kitLockFormat).',
      );
    }
    if (format != kitLockFormat) throw json.problem('says format $format');
    return KitLock([
      for (final item in json.objects('kits')) LockedKit.fromJson(item),
    ]);
  }

  final List<LockedKit> kits;

  /// The installed kit of that name, or null.
  LockedKit? find(String publisher, String slug) {
    for (final kit in kits) {
      if (kit.publisher == publisher && kit.slug == slug) return kit;
    }
    return null;
  }

  /// This lock with [kit] in it, replacing the earlier install of that kit.
  KitLock adding(LockedKit kit) => KitLock([
    for (final held in kits)
      if (held.publisher != kit.publisher || held.slug != kit.slug) held,
    kit,
  ]);

  Uint8List encode() {
    final bytes = Uint8List.fromList(
      utf8.encode(
        jsonEncode({
          'formatVersion': kitLockFormat,
          'kits': [for (final kit in kits) kit.toJson()],
        }),
      ),
    );
    if (bytes.length > _lockLimit) {
      throw FormatException(
        '$kitLockName would be ${bytes.length} bytes, over $_lockLimit.',
      );
    }
    return bytes;
  }
}

int _byName(LockedKit a, LockedKit b) {
  final publishers = a.publisher.compareTo(b.publisher);
  return publishers != 0 ? publishers : a.slug.compareTo(b.slug);
}
