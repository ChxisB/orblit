import 'dart:convert';
import 'dart:typed_data';

import 'kit_entry.dart';
import 'kit_json.dart';
import 'kit_manifest.dart';

/// The format of `index.json` and `releases.json` this code writes, and the
/// newest it reads.
const int kitStoreFormat = 1;

const int _maxRows = 50000;
const int _indexLimit = 16 * 1024 * 1024;
const int _releasesLimit = 1024 * 1024;

final RegExp _previewName = RegExp(r'^preview-[0-5]\.(png|jpg|jpeg|webp)$');

/// One kit in `index.json`: what a browser lists before anyone opens it.
///
/// The words come from the kit's own manifest, so they were checked once when
/// the kit was packed and are checked again here, because a reader gets this
/// file from a server and not from the pack.
final class KitRow {
  KitRow._(this.manifest, this.updated, this.thumbnail);

  /// Throws a [FormatException] when a field is wrong or [thumbnail] is not
  /// one of this kit's preview files.
  factory KitRow({
    required KitManifest manifest,
    required String updated,
    String? thumbnail,
  }) {
    checkKitDate('updated', updated);
    if (thumbnail != null &&
        !RegExp(
          '^kits/${manifest.publisher}/${manifest.slug}/preview-[0-5]'
          r'\.(png|jpg|jpeg|webp)$',
        ).hasMatch(thumbnail)) {
      throw FormatException(
        'The thumbnail "${kitShow(thumbnail)}" is not a preview file of '
        '${manifest.publisher}/${manifest.slug}.',
      );
    }
    return KitRow._(manifest, updated, thumbnail);
  }

  factory KitRow.fromJson(JsonObject json) {
    final licence = json.section('licence');
    final engine = json.section('engine');
    return json.read(
      () => KitRow(
        manifest: KitManifest(
          slug: json.text('slug'),
          publisher: json.text('publisher'),
          name: json.text('name'),
          summary: json.text('summary'),
          version: json.text('version'),
          licence: (spdx: licence.text('spdx'), url: licence.text('url')),
          engine: (from: engine.text('from'), to: engine.text('to')),
          containsScripts: json.flag('containsScripts'),
          tags: json.strings('tags'),
        ),
        updated: json.text('updated'),
        thumbnail: json.optionalText('thumbnail'),
      ),
    );
  }

  /// The latest release's manifest, without its previews.
  final KitManifest manifest;

  /// The day the latest release came out.
  final String updated;

  /// A path under the index, such as `kits/brass-lamp/crates/preview-0.png`.
  final String? thumbnail;

  Map<String, Object?> toJson() => {
    'containsScripts': manifest.containsScripts,
    'engine': {'from': manifest.engine.from, 'to': manifest.engine.to},
    'licence': {'spdx': manifest.licence.spdx, 'url': manifest.licence.url},
    'name': manifest.name,
    'publisher': manifest.publisher,
    'slug': manifest.slug,
    'summary': manifest.summary,
    'tags': manifest.tags,
    'thumbnail': ?thumbnail,
    'updated': updated,
    'version': manifest.version,
  };
}

/// `index.json`: a row for every kit in the store, sorted by publisher and
/// slug.
final class KitIndex {
  KitIndex._(this.rows);

  /// Throws a [FormatException] when a kit is listed twice or there are too
  /// many.
  factory KitIndex(List<KitRow> rows) {
    final names = {
      for (final row in rows) '${row.manifest.publisher}/${row.manifest.slug}',
    };
    if (names.length != rows.length || rows.length > _maxRows) {
      throw const FormatException(
        'An index lists up to $_maxRows kits and no kit twice.',
      );
    }
    return KitIndex._(List.unmodifiable(List<KitRow>.of(rows)..sort(_byName)));
  }

  /// Reads `index.json`. Keys it does not know are ignored, and a newer
  /// format is refused.
  static KitIndex parse(Uint8List bytes) {
    final json = JsonObject.parse(bytes, 'index.json', limit: _indexLimit);
    _checkFormat(json);
    return KitIndex([
      for (final item in json.objects('kits')) KitRow.fromJson(item),
    ]);
  }

  final List<KitRow> rows;

  Uint8List encode() => _encode('index.json', _indexLimit, {
    'formatVersion': kitStoreFormat,
    'kits': [for (final row in rows) row.toJson()],
  });
}

int _byName(KitRow a, KitRow b) {
  final publishers = a.manifest.publisher.compareTo(b.manifest.publisher);
  return publishers != 0
      ? publishers
      : a.manifest.slug.compareTo(b.manifest.slug);
}

/// A release as `releases.json` lists it: the entry's own words, and what the
/// builder read out of the kit file.
final class PublishedRelease {
  PublishedRelease({
    required this.release,
    required this.engine,
    required this.containsScripts,
  }) {
    checkKitEngine(engine);
  }

  factory PublishedRelease.fromJson(JsonObject json) {
    final engine = json.section('engine');
    return json.read(
      () => PublishedRelease(
        release: KitRelease.fromJson(json),
        engine: (from: engine.text('from'), to: engine.text('to')),
        containsScripts: json.flag('containsScripts'),
      ),
    );
  }

  final KitRelease release;

  /// The engine range of the kit file.
  final ({String from, String to}) engine;

  final bool containsScripts;

  Map<String, Object?> toJson() => {
    ...release.toJson(),
    'containsScripts': containsScripts,
    'engine': {'from': engine.from, 'to': engine.to},
  };
}

/// `kits/<publisher>/<slug>/releases.json`: every release of one kit, newest
/// first, and the names of the preview images beside it.
final class KitReleases {
  KitReleases._(this.previews, this.releases);

  /// Throws a [FormatException] when a version is listed twice, a preview
  /// name is not `preview-<0 to 5>.<png, jpg, jpeg or webp>`, or a list is too
  /// long.
  factory KitReleases({
    required List<String> previews,
    required List<PublishedRelease> releases,
  }) {
    final versions = {for (final item in releases) item.release.version};
    if (releases.isEmpty ||
        releases.length > kitReleaseLimit ||
        versions.length != releases.length) {
      throw const FormatException(
        'A release list holds 1 to $kitReleaseLimit releases and no version '
        'twice.',
      );
    }
    if (previews.length > kitPreviewLimit ||
        previews.toSet().length != previews.length) {
      throw const FormatException(
        'A release list names up to $kitPreviewLimit previews and no preview '
        'twice.',
      );
    }
    for (final name in previews) {
      if (!_previewName.hasMatch(name)) {
        throw FormatException(
          'The preview "${kitShow(name)}" is not named preview-0.png to '
          'preview-5.webp.',
        );
      }
    }
    return KitReleases._(
      List.unmodifiable(previews),
      List.unmodifiable(
        List<PublishedRelease>.of(releases)..sort(
          (a, b) => compareKitVersions(b.release.version, a.release.version),
        ),
      ),
    );
  }

  /// Reads `releases.json`. Keys it does not know are ignored, and a newer
  /// format is refused.
  static KitReleases parse(Uint8List bytes) {
    final json = JsonObject.parse(
      bytes,
      'releases.json',
      limit: _releasesLimit,
    );
    _checkFormat(json);
    return KitReleases(
      previews: json.strings('previews'),
      releases: [
        for (final item in json.objects('releases'))
          PublishedRelease.fromJson(item),
      ],
    );
  }

  /// File names beside `releases.json`, such as `preview-0.png`.
  final List<String> previews;

  /// Newest version first.
  final List<PublishedRelease> releases;

  Uint8List encode() => _encode('releases.json', _releasesLimit, {
    'formatVersion': kitStoreFormat,
    'previews': previews,
    'releases': [for (final item in releases) item.toJson()],
  });
}

void _checkFormat(JsonObject json) {
  final format = json.integer('formatVersion');
  if (format > kitStoreFormat) {
    throw FormatException(
      '${json.where} was written by a newer Orblit (format $format; this one '
      'reads up to $kitStoreFormat).',
    );
  }
  if (format != kitStoreFormat) {
    throw json.problem('says format $format');
  }
}

/// The bytes of [json], refused when the reader would refuse them.
Uint8List _encode(String name, int limit, Map<String, Object?> json) {
  final bytes = Uint8List.fromList(utf8.encode(jsonEncode(json)));
  if (bytes.length > limit) {
    throw FormatException('$name would be ${bytes.length} bytes, over $limit.');
  }
  return bytes;
}
