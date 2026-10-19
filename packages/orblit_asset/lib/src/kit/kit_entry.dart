import 'dart:typed_data';

import '../content_hash.dart';
import '../pack/pack_offer.dart';
import 'kit_json.dart';
import 'kit_manifest.dart';
import 'kit_rules.dart';

const int _maxReleases = 200;
const int _maxUrl = 300;
const int _maxNotes = 300;
const int _entryLimit = 256 * 1024;

final RegExp _hash = RegExp(r'^[0-9a-fA-F]{64}$');
final RegExp _date = RegExp(r'^\d{4}-\d{2}-\d{2}$');

/// One published version of a kit: where its file is and what it must be.
///
/// The constructor refuses a value no page could show safely or no download
/// should trust, so a release that exists is one a reader can use.
final class KitRelease {
  KitRelease._({
    required this.version,
    required this.url,
    required this.hash,
    required this.bytes,
    required this.released,
    required this.notes,
  });

  /// Throws a [FormatException] naming the field that is wrong.
  factory KitRelease({
    required String version,
    required String url,
    required ContentHash hash,
    required int bytes,
    required String released,
    String notes = '',
  }) {
    checkKitVersion('version', version);
    _checkUrl(url);
    if (bytes < 1 || bytes > kitPackLimit) {
      throw FormatException(
        '"bytes" is $bytes. A kit file is 1 to $kitPackLimit bytes.',
      );
    }
    checkKitDate('released', released);
    if (notes.length > _maxNotes || unsafeText.hasMatch(notes)) {
      throw const FormatException(
        '"notes" must be one line of up to $_maxNotes characters with no '
        'control characters.',
      );
    }
    return KitRelease._(
      version: version,
      url: url,
      hash: hash,
      bytes: bytes,
      released: released,
      notes: notes,
    );
  }

  /// Reads one release. Keys it does not know are left alone, so the caller
  /// chooses whether a typo is an error.
  factory KitRelease.fromJson(JsonObject json) {
    final hash = json.text('hash');
    if (!_hash.hasMatch(hash)) {
      throw json.problem(
        'has a "hash" of "${kitShow(hash)}", not 64 hexadecimal digits',
      );
    }
    try {
      return KitRelease(
        version: json.text('version'),
        url: json.text('url'),
        hash: ContentHash.parse(hash),
        bytes: json.integer('bytes'),
        released: json.text('released'),
        notes: json.optionalText('notes') ?? '',
      );
    } on FormatException catch (error) {
      throw json.problem('is wrong: ${error.message}');
    }
  }

  /// Three whole numbers, such as `1.2.0`.
  final String version;

  /// An https address of the kit file.
  final String url;

  /// The hash of the pack, which `kit pack` prints and `AssetPack.hash`
  /// answers.
  final ContentHash hash;

  /// The size of the kit file.
  final int bytes;

  /// The day it came out, as `2026-10-19`.
  final String released;

  /// One line saying what changed. May be empty.
  final String notes;

  /// What `PackFolder.download` takes.
  PackOffer get offer => PackOffer(Uri.parse(url), hash);

  Map<String, Object?> toJson() => {
    'bytes': bytes,
    'hash': hash.hex,
    'notes': notes,
    'released': released,
    'url': url,
    'version': version,
  };
}

void _checkUrl(String text) {
  final url = Uri.tryParse(text);
  if (url == null ||
      url.scheme != 'https' ||
      url.host.isEmpty ||
      url.userInfo.isNotEmpty ||
      text.length > _maxUrl ||
      text.contains(' ') ||
      unsafeText.hasMatch(text)) {
    throw FormatException(
      'The url "${kitShow(text)}" is not an https address of up to $_maxUrl '
      'characters with no user name in it.',
    );
  }
}

/// Throws a [FormatException] unless [text] is a day that exists, written as
/// `2026-10-19`.
void checkKitDate(String field, String text) {
  // The round trip refuses a day that rolls over, such as 2026-02-30.
  final parsed = _date.hasMatch(text)
      ? DateTime.tryParse('${text}T00:00:00Z')
      : null;
  if (parsed == null || parsed.toIso8601String().substring(0, 10) != text) {
    throw FormatException(
      '"$field" is "${kitShow(text)}". Write a day that exists, such as '
      '2026-10-19.',
    );
  }
}

/// The releases of one kit, as the file `kits/<publisher>/<slug>.json` lists
/// them. The path names the kit, so the file does not.
final class KitEntry {
  KitEntry._(this.publisher, this.slug, this.releases);

  /// Throws a [FormatException] when the kit has no release or none twice.
  factory KitEntry({
    required String publisher,
    required String slug,
    required List<KitRelease> releases,
  }) {
    checkKitWord('publisher', publisher, 40);
    checkKitWord('slug', slug, 40);
    if (releases.isEmpty || releases.length > _maxReleases) {
      throw const FormatException(
        'An entry lists 1 to $_maxReleases releases.',
      );
    }
    final versions = {for (final release in releases) release.version};
    if (versions.length != releases.length) {
      throw const FormatException('An entry lists a version twice.');
    }
    return KitEntry._(
      publisher,
      slug,
      List.unmodifiable(List<KitRelease>.of(releases)..sort(_newestFirst)),
    );
  }

  /// Reads the entry file for [publisher] and [slug]. Throws a
  /// [FormatException] that names the file, and refuses a key it does not
  /// know.
  static KitEntry parse(
    Uint8List bytes, {
    required String publisher,
    required String slug,
  }) {
    final where = 'The entry "$publisher/$slug"';
    final json = JsonObject.parse(bytes, where, limit: _entryLimit)
      ..only({'releases'});
    try {
      return KitEntry(
        publisher: publisher,
        slug: slug,
        releases: [
          for (final item in json.objects('releases'))
            KitRelease.fromJson(item..only(_releaseKeys)),
        ],
      );
    } on FormatException catch (error) {
      if (error.message.startsWith(where)) rethrow;
      throw json.problem('is wrong: ${error.message}');
    }
  }

  final String publisher;
  final String slug;

  /// Newest version first.
  final List<KitRelease> releases;

  KitRelease get latest => releases.first;
}

const Set<String> _releaseKeys = {
  'version',
  'url',
  'hash',
  'bytes',
  'released',
  'notes',
};

int _newestFirst(KitRelease a, KitRelease b) =>
    compareKitVersions(b.version, a.version);
