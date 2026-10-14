import 'dart:convert';
import 'dart:typed_data';

import '../asset_id.dart';
import '../content_hash.dart';

/// The four bytes every pack starts with: `OPAK`.
const List<int> packMarker = [0x4f, 0x50, 0x41, 0x4b];

/// The format this code writes, and the newest it reads.
const int packFormat = 1;

/// Blobs start on a multiple of this.
///
/// Dart's typed views need an offset that is a multiple of the element size.
/// A glTF's float accessors are read as `Float32List` views, and a blob on a
/// 16-byte boundary keeps any view of it aligned.
const int packAlignment = 16;

/// The `kind` marker in a pack's directory.
const String packKind = 'orblit.pack';

/// The next multiple of [packAlignment] at or after [offset].
///
/// Arithmetic, not `& ~15`. Bitwise operators are 32 bits wide on the web.
int alignedOffset(int offset) =>
    (offset + packAlignment - 1) ~/ packAlignment * packAlignment;

/// The 32 fixed bytes at the start of a pack.
///
/// Sixty-four bit values are two 32-bit halves. `ByteData.getUint64` throws on
/// the web, and a pack has to open there.
final class PackHeader {
  const PackHeader({required this.directoryAt, required this.directoryBytes});

  static const int size = 32;

  /// Where the directory starts.
  final int directoryAt;

  /// How long the directory is. Stored, so a short download reads as short
  /// and not as broken JSON.
  final int directoryBytes;

  Uint8List encode() {
    final bytes = Uint8List(size)..setRange(0, 4, packMarker);
    final view = ByteData.sublistView(bytes)
      ..setUint32(4, packFormat, Endian.little);
    _setUint64(view, 8, directoryAt);
    _setUint64(view, 16, directoryBytes);
    return bytes;
  }

  /// Reads the header, refusing a file that is not a pack of a format this
  /// code reads.
  static PackHeader read(Uint8List bytes) {
    if (bytes.length < size) {
      throw FormatException(
        'This is not an Orblit pack. It is ${bytes.length} bytes long and a '
        'pack header alone is $size.',
      );
    }
    for (var i = 0; i < packMarker.length; i++) {
      if (bytes[i] != packMarker[i]) {
        throw const FormatException(
          'This is not an Orblit pack. It does not start with OPAK.',
        );
      }
    }
    final view = ByteData.sublistView(bytes);
    final format = view.getUint32(4, Endian.little);
    if (format > packFormat) {
      throw FormatException(
        'This pack was written by a newer Orblit (format $format; this one '
        'reads up to $packFormat).',
      );
    }
    if (format < 1) {
      throw FormatException(
        'This pack says it is format $format, which no Orblit has written.',
      );
    }
    return PackHeader(
      directoryAt: _getUint64(view, 8),
      directoryBytes: _getUint64(view, 16),
    );
  }
}

void _setUint64(ByteData view, int at, int value) {
  view.setUint32(at, value % 0x100000000, Endian.little);
  view.setUint32(at + 4, value ~/ 0x100000000, Endian.little);
}

int _getUint64(ByteData view, int at) =>
    view.getUint32(at, Endian.little) +
    view.getUint32(at + 4, Endian.little) * 0x100000000;

/// Where one blob sits in a pack.
final class PackBlob {
  const PackBlob({required this.at, required this.bytes});

  final int at;
  final int bytes;

  int get end => at + bytes;
}

/// What a pack holds and where. The last thing in the file.
///
/// A pack is written by a tool, so a bad one is a bad file and not a hand
/// edit. Reading one that is wrong in any way throws, whole, where a manifest
/// carries on past a broken entry.
final class PackDirectory {
  PackDirectory._({
    required this.target,
    required this.base,
    required this.removed,
    required this.assets,
    required this.blobs,
  });

  /// Builds a directory, throwing a [FormatException] for a layout no reader
  /// would accept. Everything [directoryAt] and the header leave unsaid is
  /// checked here, so the writer cannot produce what the reader refuses.
  factory PackDirectory({
    required String target,
    required ContentHash? base,
    required List<AssetId> removed,
    required Map<AssetId, ContentHash> assets,
    required Map<ContentHash, PackBlob> blobs,
    required int directoryAt,
  }) {
    _checkAssets(assets, blobs);
    _checkBlobs(blobs, directoryAt);
    return PackDirectory._(
      target: target,
      base: base,
      removed: List.unmodifiable(removed),
      assets: Map.unmodifiable(assets),
      blobs: Map.unmodifiable(blobs),
    );
  }

  /// The cook target the textures in this pack were made for.
  final String target;

  /// The hash of the pack a patch was built against, or null.
  final ContentHash? base;

  /// Ids the base had and this pack takes away.
  final List<AssetId> removed;

  /// Which blob each asset is.
  final Map<AssetId, ContentHash> assets;

  /// Where each blob is.
  final Map<ContentHash, PackBlob> blobs;

  /// The directory as JSON, the same bytes every time for the same content.
  ///
  /// Keys are in alphabetical order and there are no timestamps, so two packs
  /// of one bundle are one file.
  Uint8List encode() {
    final ids = assets.keys.toList()
      ..sort((a, b) => a.toString().compareTo(b.toString()));
    final hashes = blobs.keys.toList()..sort((a, b) => a.hex.compareTo(b.hex));
    final sortedRemoved = removed.map((id) => id.toString()).toList()..sort();
    final json = {
      'assets': {for (final id in ids) id.toString(): assets[id]!.hex},
      'base': base?.hex,
      'blobs': {
        for (final hash in hashes)
          hash.hex: {'at': blobs[hash]!.at, 'bytes': blobs[hash]!.bytes},
      },
      'formatVersion': packFormat,
      'kind': packKind,
      'removed': sortedRemoved,
      'target': target,
    };
    return Uint8List.fromList(utf8.encode(jsonEncode(json)));
  }

  /// Reads what [encode] writes, for a directory that starts at
  /// [directoryAt].
  static PackDirectory decode(Uint8List bytes, {required int directoryAt}) {
    final Object? parsed;
    try {
      parsed = jsonDecode(utf8.decode(bytes));
    } on FormatException catch (error) {
      throw FormatException(
        'The directory of this pack is not JSON: ${error.message}',
      );
    }
    if (parsed is! Map<String, Object?>) {
      throw const FormatException(
        'The directory of this pack is not a JSON object.',
      );
    }
    if (parsed['kind'] != packKind) {
      throw FormatException(
        'The directory of this pack says it is "${parsed['kind']}", not '
        '"$packKind".',
      );
    }
    if (parsed['formatVersion'] != packFormat) {
      throw FormatException(
        'The directory of this pack says format ${parsed['formatVersion']}, '
        'and its header says $packFormat.',
      );
    }
    final base = parsed['base'];
    return PackDirectory(
      target: _text(parsed, 'target'),
      base: base == null ? null : _hash(base, 'base'),
      removed: [for (final id in _list(parsed, 'removed')) _id(id, 'removed')],
      assets: {
        for (final MapEntry(:key, :value) in _object(parsed, 'assets').entries)
          _id(key, 'assets'): _hash(value, 'assets'),
      },
      blobs: {
        for (final MapEntry(:key, :value) in _object(parsed, 'blobs').entries)
          _hash(key, 'blobs'): _blob(key, value),
      },
      directoryAt: directoryAt,
    );
  }
}

void _checkAssets(
  Map<AssetId, ContentHash> assets,
  Map<ContentHash, PackBlob> blobs,
) {
  for (final MapEntry(:key, :value) in assets.entries) {
    if (!blobs.containsKey(value)) {
      throw FormatException(
        'The asset "$key" names a blob, $value, that this pack does not '
        'have.',
      );
    }
  }
}

void _checkBlobs(Map<ContentHash, PackBlob> blobs, int directoryAt) {
  // An empty blob shares its start with the blob after it, so it sorts first.
  final byPlace = blobs.entries.toList()
    ..sort((a, b) {
      final byStart = a.value.at.compareTo(b.value.at);
      return byStart != 0 ? byStart : a.value.bytes.compareTo(b.value.bytes);
    });
  var previousEnd = PackHeader.size;
  ContentHash? previous;
  for (final MapEntry(key: hash, value: blob) in byPlace) {
    if (blob.at < PackHeader.size) {
      throw FormatException(
        'The blob $hash starts at byte ${blob.at}, inside the header.',
      );
    }
    if (blob.at % packAlignment != 0) {
      throw FormatException(
        'The blob $hash starts at byte ${blob.at}, which is not a multiple '
        'of $packAlignment.',
      );
    }
    if (blob.at < previousEnd) {
      throw FormatException('The blobs $previous and $hash overlap.');
    }
    if (blob.end > directoryAt) {
      throw FormatException(
        'The blob $hash ends at byte ${blob.end}, inside the directory that '
        'starts at $directoryAt.',
      );
    }
    previousEnd = blob.end;
    previous = hash;
  }
}

Map<String, Object?> _object(Map<String, Object?> from, String key) {
  final value = from[key];
  if (value is Map<String, Object?>) return value;
  throw FormatException('The directory of this pack has no "$key" object.');
}

List<Object?> _list(Map<String, Object?> from, String key) {
  final value = from[key];
  if (value is List<Object?>) return value;
  throw FormatException('The directory of this pack has no "$key" list.');
}

String _text(Map<String, Object?> from, String key) {
  final value = from[key];
  if (value is String) return value;
  throw FormatException('The directory of this pack has no "$key" text.');
}

AssetId _id(Object? value, String where) {
  if (value is! String) {
    throw FormatException('"$where" in this pack holds $value, not an id.');
  }
  return AssetId.parse(value);
}

ContentHash _hash(Object? value, String where) {
  if (value is! String) {
    throw FormatException('"$where" in this pack holds $value, not a hash.');
  }
  return ContentHash.parse(value);
}

PackBlob _blob(String hash, Object? value) {
  if (value is Map<String, Object?>) {
    final at = value['at'];
    final bytes = value['bytes'];
    if (at is int && bytes is int && at >= 0 && bytes >= 0) {
      return PackBlob(at: at, bytes: bytes);
    }
  }
  throw FormatException(
    'The blob $hash does not give "at" and "bytes" as whole numbers.',
  );
}
