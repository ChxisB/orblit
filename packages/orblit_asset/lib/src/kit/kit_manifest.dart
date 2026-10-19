import 'dart:convert';
import 'dart:typed_data';

import '../asset_id.dart';

/// The `kind` marker in a kit's manifest.
const String kitKind = 'orblit.kit';

/// The format this code writes, and the newest it reads.
const int kitFormat = 1;

/// The cook target of a kit. No game runs on it, so a game refuses to mount
/// a kit.
const String kitTarget = 'source';

/// The asset id the manifest is stored under, at the root of every kit.
final AssetId kitManifestId = AssetId.parse('kit.json');

final RegExp _word = RegExp(r'^[a-z0-9]+(-[a-z0-9]+)*$');
final RegExp _version = RegExp(
  r'^(0|[1-9]\d{0,5})\.(0|[1-9]\d{0,5})\.(0|[1-9]\d{0,5})$',
);
final RegExp _licence = RegExp(r'^[A-Za-z0-9][A-Za-z0-9.+() -]*$');

/// Characters that change how a line is read or drawn: control characters,
/// the C1 range, zero width and bidirectional marks, and the line separators.
final RegExp unsafeText = RegExp(
  r'[\u0000-\u001f\u007f-\u009f\u200b-\u200f\u2028-\u202e'
  r'\u2060-\u2064\u2066-\u206f\ufeff\ufff9-\ufffb]',
);

/// [text] with each unsafe character written as `\uXXXX`, for a message. A kit
/// is somebody else's file, and its words must not reach a terminal raw.
String kitShow(String text) => text.replaceAllMapped(
  unsafeText,
  (match) =>
      '\\u${match.input.codeUnitAt(match.start).toRadixString(16).padLeft(4, '0')}',
);

/// The largest `kit.json` read.
const int kitManifestLimit = 64 * 1024;
const int _maxUrl = 200;

const Set<String> _previewExtensions = {'png', 'jpg', 'jpeg', 'webp'};
const int _maxTags = 8;
/// The most previews a kit names.
const int kitPreviewLimit = 6;

/// What a kit says about itself, as `kit.json` in the pack.
///
/// The constructor refuses a field no index or install dialog could show
/// safely, so a manifest that exists is one that reads.
///
/// shortcut: a version is three plain numbers and a licence is any id or
/// expression of the right letters. Add pre-release tags and a check against
/// the SPDX list when a publisher needs them.
final class KitManifest {
  KitManifest._({
    required this.slug,
    required this.publisher,
    required this.name,
    required this.summary,
    required this.version,
    required this.licence,
    required this.engine,
    required this.tags,
    required this.previews,
    required this.containsScripts,
  });

  /// Throws a [FormatException] naming the field that is wrong.
  factory KitManifest({
    required String slug,
    required String publisher,
    required String name,
    required String summary,
    required String version,
    required ({String spdx, String url}) licence,
    required ({String from, String to}) engine,
    required bool containsScripts,
    List<String> tags = const [],
    List<AssetId> previews = const [],
  }) {
    checkKitWord('slug', slug, 40);
    checkKitWord('publisher', publisher, 40);
    _checkLine('name', name, 60);
    _checkLine('summary', summary, 200);
    checkKitVersion('version', version);
    _checkLicence(licence);
    checkKitEngine(engine);
    _checkTags(tags);
    _checkPreviews(previews);
    return KitManifest._(
      slug: slug,
      publisher: publisher,
      name: name,
      summary: summary,
      version: version,
      licence: licence,
      engine: engine,
      tags: List.unmodifiable(tags),
      previews: List.unmodifiable(previews),
      containsScripts: containsScripts,
    );
  }

  /// Reads a manifest out of a pack, which says whether it holds scripts.
  static KitManifest read(Uint8List bytes) {
    final json = _object(bytes);
    if (json['kind'] != kitKind) {
      throw FormatException(
        'This manifest says it is "${kitShow('${json['kind']}')}", not '
        '"$kitKind".',
      );
    }
    final format = json['formatVersion'];
    if (format is int && format > kitFormat) {
      throw FormatException(
        'This kit was written by a newer Orblit (format $format; this one '
        'reads up to $kitFormat).',
      );
    }
    if (format != kitFormat) {
      throw FormatException('This manifest says format ${kitShow('$format')}.');
    }
    return _fields(json, containsScripts: _flag(json, 'containsScripts'));
  }

  /// Reads the `kit.json` an author wrote in a folder. The tool counts the
  /// scripts, so a `containsScripts` typed there is ignored.
  static KitManifest readAuthored(
    Uint8List bytes, {
    required bool containsScripts,
  }) => _fields(_object(bytes), containsScripts: containsScripts);

  final String slug;
  final String publisher;
  final String name;
  final String summary;

  /// Three whole numbers, such as `1.2.0`.
  final String version;

  /// An SPDX id or expression, and where its text can be read.
  final ({String spdx, String url}) licence;

  /// The lowest and highest `orblit` package versions the kit was tried with.
  final ({String from, String to}) engine;

  final List<String> tags;

  /// Ids of images in the pack, shown before the kit is installed.
  final List<AssetId> previews;

  /// True when the kit holds a file a person could run or compile.
  final bool containsScripts;

  /// The manifest as JSON, the same bytes every time for the same content.
  Uint8List encode() => Uint8List.fromList(
    utf8.encode(
      jsonEncode({
        'containsScripts': containsScripts,
        'engine': {'from': engine.from, 'to': engine.to},
        'formatVersion': kitFormat,
        'kind': kitKind,
        'licence': {'spdx': licence.spdx, 'url': licence.url},
        'name': name,
        'previews': [for (final id in previews) id.toString()],
        'publisher': publisher,
        'slug': slug,
        'summary': summary,
        'tags': tags,
        'version': version,
      }),
    ),
  );
}

KitManifest _fields(
  Map<String, Object?> json, {
  required bool containsScripts,
}) {
  final licence = _section(json, 'licence');
  final engine = _section(json, 'engine');
  return KitManifest(
    slug: _text(json, 'slug'),
    publisher: _text(json, 'publisher'),
    name: _text(json, 'name'),
    summary: _text(json, 'summary'),
    version: _text(json, 'version'),
    licence: (spdx: _text(licence, 'spdx'), url: _text(licence, 'url')),
    engine: (from: _text(engine, 'from'), to: _text(engine, 'to')),
    containsScripts: containsScripts,
    tags: [for (final tag in _list(json, 'tags')) _item(tag, 'tags')],
    previews: [
      for (final id in _list(json, 'previews'))
        AssetId.parse(_item(id, 'previews')),
    ],
  );
}

/// Throws a [FormatException] unless [value] is lower case letters and digits
/// in groups joined by single hyphens, up to [longest] characters.
void checkKitWord(String field, String value, int longest) {
  if (value.length > longest || !_word.hasMatch(value)) {
    throw FormatException(
      '"$field" is "${kitShow(value)}". Use lower case letters and digits '
      'in groups joined by single hyphens, up to $longest characters.',
    );
  }
}

void _checkLine(String field, String value, int longest) {
  if (value.trim().isEmpty ||
      value.length > longest ||
      unsafeText.hasMatch(value)) {
    throw FormatException(
      '"$field" must be one line of 1 to $longest characters with no '
      'control characters.',
    );
  }
}

/// Throws a [FormatException] unless [value] is three whole numbers.
void checkKitVersion(String field, String value) {
  if (!_version.hasMatch(value)) {
    throw FormatException(
      '"$field" is "${kitShow(value)}". Write three whole numbers of up to '
      'six digits, such as 1.2.0.',
    );
  }
}

/// Orders two versions that passed [checkKitVersion], as `compareTo` does.
int compareKitVersions(String a, String b) {
  final left = a.split('.').map(int.parse).toList();
  final right = b.split('.').map(int.parse).toList();
  for (var i = 0; i < 3; i++) {
    if (left[i] != right[i]) return left[i].compareTo(right[i]);
  }
  return 0;
}

void _checkLicence(({String spdx, String url}) licence) {
  final spdx = licence.spdx;
  final none =
      spdx.toUpperCase() == 'NONE' || spdx.toUpperCase() == 'NOASSERTION';
  if (spdx.length > 80 || none || !_licence.hasMatch(spdx)) {
    throw FormatException(
      'The licence "${kitShow(spdx)}" is not an SPDX id or expression, such '
      'as MIT or CC-BY-4.0. A kit needs a licence.',
    );
  }
  _checkUrl(licence.url);
}

void _checkUrl(String text) {
  final url = Uri.tryParse(text);
  final web = url != null && (url.scheme == 'https' || url.scheme == 'http');
  if (!web ||
      url.host.isEmpty ||
      url.userInfo.isNotEmpty ||
      text.length > _maxUrl ||
      text.contains(' ') ||
      unsafeText.hasMatch(text)) {
    throw FormatException(
      'The licence URL "${kitShow(text)}" is not an http or https address of '
      'up to $_maxUrl characters with no user name in it.',
    );
  }
}

/// Throws a [FormatException] unless both ends are versions and the range
/// does not run backwards.
void checkKitEngine(({String from, String to}) engine) {
  checkKitVersion('engine.from', engine.from);
  checkKitVersion('engine.to', engine.to);
  if (compareKitVersions(engine.from, engine.to) > 0) {
    throw FormatException(
      'The engine range runs from ${engine.from} down to ${engine.to}.',
    );
  }
}

void _checkTags(List<String> tags) {
  if (tags.length > _maxTags || tags.toSet().length != tags.length) {
    throw const FormatException(
      'A kit has up to $_maxTags tags and no tag twice.',
    );
  }
  for (final tag in tags) {
    checkKitWord('tag', tag, 24);
  }
}

void _checkPreviews(List<AssetId> previews) {
  if (previews.length > kitPreviewLimit ||
      previews.toSet().length != previews.length) {
    throw const FormatException(
      'A kit has up to $kitPreviewLimit previews and no preview twice.',
    );
  }
  for (final id in previews) {
    if (!_previewExtensions.contains(id.extension)) {
      throw FormatException(
        'The preview "${kitShow('$id')}" is not a png, jpg or webp image.',
      );
    }
  }
}

Map<String, Object?> _object(Uint8List bytes) {
  if (bytes.length > kitManifestLimit) {
    throw const FormatException(
      'The manifest is over $kitManifestLimit bytes.',
    );
  }
  final Object? parsed;
  try {
    parsed = jsonDecode(utf8.decode(bytes));
  } on FormatException catch (error) {
    throw FormatException('The manifest is not JSON: ${error.message}');
  }
  if (parsed is Map<String, Object?>) return parsed;
  throw const FormatException('The manifest is not a JSON object.');
}

Map<String, Object?> _section(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is Map<String, Object?>) return value;
  throw FormatException('The manifest has no "$key" object.');
}

String _text(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is String) return value;
  throw FormatException('The manifest has no "$key" text.');
}

bool _flag(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is bool) return value;
  throw FormatException('The manifest has no "$key" true or false.');
}

List<Object?> _list(Map<String, Object?> json, String key) {
  final value = json[key] ?? const <Object?>[];
  if (value is List<Object?>) return value;
  throw FormatException('"$key" in the manifest is not a list.');
}

String _item(Object? value, String where) {
  if (value is String) return value;
  throw FormatException(
    '"$where" in the manifest holds ${kitShow('$value')}, not text.',
  );
}
