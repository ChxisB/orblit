import 'dart:typed_data';

import '../content_hash.dart';
import '../pack/pack_offer.dart';
import 'kit_json.dart';
import 'kit_manifest.dart';
import 'kit_rules.dart';

/// The most releases one kit lists, in an entry and in `releases.json`.
const int kitReleaseLimit = 200;

/// The largest entry file read.
const int kitEntryLimit = 256 * 1024;

const int _maxUrl = 300;
const int _maxNotes = 300;

final RegExp _hash = RegExp(r'^[0-9a-fA-F]{64}$');
final RegExp _date = RegExp(r'^\d{4}-\d{2}-\d{2}$');
final RegExp _hostLabel = RegExp(r'^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$');
final RegExp _urlTrouble = RegExp(r'[\\#@\s]');
final RegExp _startsWithLetter = RegExp('^[a-z]');
const Set<String> _insideNames = {'localhost', 'local', 'internal'};

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
    return json.read(
      () => KitRelease(
        version: json.text('version'),
        url: json.text('url'),
        hash: ContentHash.parse(hash),
        bytes: json.integer('bytes'),
        released: json.text('released'),
        notes: json.optionalText('notes') ?? '',
      ),
    );
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

/// Throws unless [text] is an https address of a public host name.
///
/// The builder fetches it from a CI runner, so an address, `localhost` or a
/// port other than 443 is refused before anything is sent. A backslash, `#` or
/// `@` is refused because parsers disagree about what follows it.
///
/// shortcut: a public name can still point at a private address in DNS, and a
/// redirect can lead anywhere. The review of the pull request reads the url
/// and the hash has to match what comes back. Resolve the name and refuse
/// private addresses when a store takes entries from people nobody reviews.
void _checkUrl(String text) {
  final url = Uri.tryParse(text);
  if (url == null ||
      url.scheme != 'https' ||
      (url.hasPort && url.port != 443) ||
      !_isPublicName(url.host) ||
      text.length > _maxUrl ||
      _urlTrouble.hasMatch(text) ||
      unsafeText.hasMatch(text)) {
    throw FormatException(
      'The url "${kitShow(text)}" is not an https address of up to $_maxUrl '
      'characters at a public host name. It has no port, space, backslash, '
      '# or @.',
    );
  }
}

bool _isPublicName(String host) {
  final labels = host.split('.');
  // A last label that starts with a digit reads as part of an address, as in
  // 127.1 or 0x7f.0.0.1.
  return labels.length >= 2 &&
      labels.every(_hostLabel.hasMatch) &&
      _startsWithLetter.hasMatch(labels.last) &&
      !_insideNames.contains(labels.last);
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
    if (releases.isEmpty || releases.length > kitReleaseLimit) {
      throw const FormatException(
        'An entry lists 1 to $kitReleaseLimit releases.',
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
    final where = 'The entry "${kitShow('$publisher/$slug')}"';
    final json = JsonObject.parse(bytes, where, limit: kitEntryLimit)
      ..only({'releases'});
    return json.read(
      () => KitEntry(
        publisher: publisher,
        slug: slug,
        releases: [
          for (final item in json.objects('releases'))
            KitRelease.fromJson(item..only(_releaseKeys)),
        ],
      ),
    );
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
