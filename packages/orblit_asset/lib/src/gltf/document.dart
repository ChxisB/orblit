import 'dart:convert';
import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_source.dart';
import '../importer.dart';

/// A glTF document held as the JSON it is, plus the bytes its buffers hold.
///
/// This is deliberately *not* a glTF library. Nothing here turns the document
/// into typed objects, because the moment it does, every part of glTF the
/// types do not model is lost on the way back out — and Orblit's loader is
/// gltfio, so what the cook hands back has to still be a glTF, extensions,
/// `extras`, variants, skins and all. Phase 3 spent an extra-large phase
/// keeping those; a parse-and-re-emit would drop them without a word.
///
/// So the rule is: read the JSON into a map, edit the handful of keys a
/// rewrite actually owns, and let every other key travel through untouched
/// *because it was never looked at*. A rewrite's blast radius is then a list
/// you can read in one line rather than a format you have to model.
///
/// Buffers are resolved on the way in — a GLB's `BIN` chunk, a data URI, a
/// file beside a `.gltf` — and flattened to one buffer on the way out, so
/// whatever came in leaves as a self-contained GLB.
class GltfDocument {
  GltfDocument._(this.json, this._buffers);

  /// The document. Mutable, and meant to be mutated: this is the thing a
  /// rewrite edits.
  final Map<String, Object?> json;

  final List<Uint8List> _buffers;

  /// Bytes appended by [addView], in the order the views were added.
  final BytesBuilder _appended = BytesBuilder(copy: false);

  /// Which views [addView] made, and where in [_appended] each one starts.
  final Map<int, int> _appendedAt = {};

  static const int _glbMagic = 0x46546C67; // 'glTF'
  static const int _jsonChunk = 0x4E4F534A; // 'JSON'
  static const int _binChunk = 0x004E4942; // 'BIN\0'

  /// Reads `bytes` — a `.glb` or a `.gltf` — resolving every buffer it names.
  ///
  /// `source` is asked only for ids the caller has already declared as
  /// dependencies, which for a model is exactly what `GltfImporter.urisIn`
  /// returns. A buffer that cannot be resolved is a failure rather than an
  /// empty one: a rewrite over missing geometry would write a valid file with
  /// nothing in it.
  static Future<GltfDocument> read(
    AssetId id,
    Uint8List bytes,
    AssetSource source,
  ) async {
    final Map<String, Object?> json;
    Uint8List binary = Uint8List(0);

    if (_looksLikeGlb(bytes)) {
      final container = _readGlb(id, bytes);
      json = container.json;
      binary = container.binary;
    } else {
      final Object? parsed;
      try {
        parsed = jsonDecode(utf8.decode(bytes));
      } on FormatException catch (error) {
        throw ImportFailure(id, 'is not readable as glTF JSON', cause: error);
      }
      if (parsed is! Map<String, Object?>) {
        throw ImportFailure(
          id,
          'is not a glTF document: its JSON is not an '
          'object',
        );
      }
      json = parsed;
    }

    final declared = json['buffers'];
    final buffers = <Uint8List>[];
    if (declared is List) {
      for (var i = 0; i < declared.length; i++) {
        final entry = declared[i];
        if (entry is! Map<String, Object?>) {
          throw ImportFailure(id, 'buffer $i is not an object');
        }
        buffers.add(await _bytesOf(id, entry, i, binary, source));
      }
    }
    return GltfDocument._(json, buffers);
  }

  /// A list of objects under `key` — `meshes`, `materials`, `accessors` — or
  /// an empty list when the document has none.
  ///
  /// Returns the live list, so writing into the maps it holds edits the
  /// document. A document missing the key entirely gets an empty list that is
  /// *not* attached, since adding an empty `materials` to a document that had
  /// none would be a change for nothing.
  List<Map<String, Object?>> list(String key) {
    final found = json[key];
    if (found is! List) return const [];
    return [
      for (final entry in found)
        if (entry is Map<String, Object?>) entry,
    ];
  }

  /// The bytes a buffer view covers.
  Uint8List viewBytes(int index) {
    final view = _viewAt(index);
    final appendedAt = _appendedAt[index];
    if (appendedAt != null) {
      final length = view['byteLength'] as int;
      return Uint8List.sublistView(
        _appended.toBytes(),
        appendedAt,
        appendedAt + length,
      );
    }
    final buffer = _buffers[(view['buffer'] as int?) ?? 0];
    final offset = (view['byteOffset'] as int?) ?? 0;
    final length = view['byteLength'] as int;
    if (offset + length > buffer.length) {
      throw const FormatException('a buffer view runs past its buffer');
    }
    return Uint8List.sublistView(buffer, offset, offset + length);
  }

  /// The stride a view reads at, or null when its data is tightly packed.
  int? viewStride(int index) => _viewAt(index)['byteStride'] as int?;

  /// Adds a buffer view over `bytes`, returning its index.
  ///
  /// The bytes are kept aside and written into the output buffer at the end,
  /// after everything that came in. Nothing already in the document moves, so
  /// an accessor this rewrite did not touch still points where it did.
  int addView(List<int> bytes, {int? byteStride, int? target}) {
    final views = json.putIfAbsent('bufferViews', () => <Object?>[]) as List;
    final at = _appended.length;
    _appended.add(bytes);
    // Four-byte alignment between views, because an accessor's offset into a
    // view has to satisfy its component type's alignment and the view's own
    // start is what that offset is measured from.
    final padding = (4 - bytes.length % 4) % 4;
    if (padding > 0) _appended.add(Uint8List(padding));

    final index = views.length;
    views.add(<String, Object?>{
      'buffer': 0,
      'byteOffset': 0, // Filled in by [toGlb]; see [_appendedAt].
      'byteLength': bytes.length,
      if (byteStride != null) 'byteStride': byteStride,
      if (target != null) 'target': target,
    });
    _appendedAt[index] = at;
    return index;
  }

  /// Adds an object to a top-level list, returning its index.
  int add(String key, Map<String, Object?> value) {
    final entries = json.putIfAbsent(key, () => <Object?>[]) as List;
    entries.add(value);
    return entries.length - 1;
  }

  /// The document as a self-contained GLB: one buffer, no external files but
  /// the images it deliberately still names by URI.
  ///
  /// Every input buffer is laid down in order and every view rebased onto the
  /// result, so a `.gltf` with its geometry in a `.bin` beside it comes out as
  /// one file — which is what the cache stores and what the loader is handed.
  Uint8List toGlb() {
    final bases = <int>[];
    var at = 0;
    for (final buffer in _buffers) {
      bases.add(at);
      at += buffer.length;
      at += (4 - at % 4) % 4;
    }
    final appendBase = at;
    final appended = _appended.toBytes();
    final total = appendBase + appended.length;

    final views = json['bufferViews'];
    if (views is List) {
      for (var i = 0; i < views.length; i++) {
        final view = views[i];
        if (view is! Map<String, Object?>) continue;
        final appendedAt = _appendedAt[i];
        // Read where the view was before saying where it is now: the two
        // fields are the same two keys, and writing first loses the answer.
        final was = (view['buffer'] as int?) ?? 0;
        final wasAt = (view['byteOffset'] as int?) ?? 0;
        view['buffer'] = 0;
        view['byteOffset'] = appendedAt != null
            ? appendBase + appendedAt
            : bases[was] + wasAt;
      }
    }
    // A buffer of nought length is an error in the format, and so is a view
    // or an accessor over one. A document with no geometry at all — a scene
    // of lights and cameras is one — therefore names no buffer rather than
    // naming an empty one.
    if (total == 0) {
      json.remove('buffers');
    } else {
      json['buffers'] = [
        {'byteLength': total},
      ];
    }

    final binary = Uint8List(_aligned(total));
    for (var i = 0; i < _buffers.length; i++) {
      binary.setRange(bases[i], bases[i] + _buffers[i].length, _buffers[i]);
    }
    binary.setRange(appendBase, appendBase + appended.length, appended);

    // A GLB's JSON chunk is padded with spaces and its binary chunk with
    // zeroes — the spec says so, and a viewer that trusts the padding is not
    // wrong to.
    final text = Uint8List.fromList(utf8.encode(jsonEncode(json)));
    final jsonLength = _aligned(text.length);
    final jsonBytes = Uint8List(jsonLength)..fillRange(0, jsonLength, 0x20);
    jsonBytes.setRange(0, text.length, text);

    final length =
        12 + 8 + jsonLength + (binary.isEmpty ? 0 : 8 + binary.length);
    final out = Uint8List(length);
    final data = ByteData.sublistView(out);
    data.setUint32(0, _glbMagic, Endian.little);
    data.setUint32(4, 2, Endian.little);
    data.setUint32(8, length, Endian.little);
    data.setUint32(12, jsonLength, Endian.little);
    data.setUint32(16, _jsonChunk, Endian.little);
    out.setRange(20, 20 + jsonLength, jsonBytes);
    if (binary.isNotEmpty) {
      final binAt = 20 + jsonLength;
      data.setUint32(binAt, binary.length, Endian.little);
      data.setUint32(binAt + 4, _binChunk, Endian.little);
      out.setRange(binAt + 8, binAt + 8 + binary.length, binary);
    }
    return out;
  }

  Map<String, Object?> _viewAt(int index) {
    final views = json['bufferViews'];
    if (views is! List || index < 0 || index >= views.length) {
      throw FormatException('there is no buffer view $index');
    }
    final view = views[index];
    if (view is! Map<String, Object?>) {
      throw FormatException('buffer view $index is not an object');
    }
    return view;
  }

  static int _aligned(int length) => length + (4 - length % 4) % 4;

  static bool _looksLikeGlb(Uint8List bytes) =>
      bytes.length >= 12 &&
      ByteData.sublistView(bytes, 0, 4).getUint32(0, Endian.little) ==
          _glbMagic;

  static ({Map<String, Object?> json, Uint8List binary}) _readGlb(
    AssetId id,
    Uint8List bytes,
  ) {
    if (bytes.length < 20) {
      throw ImportFailure(
        id,
        'is too short to be a GLB (${bytes.length} '
        'bytes)',
      );
    }
    final data = ByteData.sublistView(bytes);
    if (data.getUint32(4, Endian.little) != 2) {
      throw ImportFailure(
        id,
        'is a GLB of version '
        '${data.getUint32(4, Endian.little)}, and only version 2 exists',
      );
    }
    final total = data.getUint32(8, Endian.little);
    if (total > bytes.length) {
      throw ImportFailure(
        id,
        'says it is $total bytes and is only '
        '${bytes.length}. It is truncated.',
      );
    }

    Map<String, Object?>? json;
    Uint8List? binary;
    var at = 12;
    while (at + 8 <= total) {
      final length = data.getUint32(at, Endian.little);
      final type = data.getUint32(at + 4, Endian.little);
      final start = at + 8;
      final end = start + length;
      if (end > total) {
        throw ImportFailure(id, 'has a chunk running past the end of the file');
      }
      switch (type) {
        case _jsonChunk:
          if (json != null) {
            throw ImportFailure(id, 'has two JSON chunks, and a GLB has one');
          }
          final Object? parsed;
          try {
            parsed = jsonDecode(utf8.decode(bytes.sublist(start, end)));
          } on FormatException catch (error) {
            throw ImportFailure(id, "'s JSON chunk is not JSON", cause: error);
          }
          if (parsed is! Map<String, Object?>) {
            throw ImportFailure(id, "'s JSON chunk is not an object");
          }
          json = parsed;
        case _binChunk:
          if (binary != null) {
            throw ImportFailure(id, 'has two BIN chunks, and a GLB has one');
          }
          binary = Uint8List.sublistView(bytes, start, end);
        default:
          break; // The spec says to ignore a chunk you do not know.
      }
      at = end;
    }
    if (json == null) {
      throw ImportFailure(id, 'has no JSON chunk, and a GLB must have one');
    }
    return (json: json, binary: binary ?? Uint8List(0));
  }

  /// One buffer's bytes: the BIN chunk, a data URI, or a file beside the
  /// document.
  static Future<Uint8List> _bytesOf(
    AssetId id,
    Map<String, Object?> buffer,
    int index,
    Uint8List binary,
    AssetSource source,
  ) async {
    final uri = buffer['uri'];
    if (uri == null) {
      // No URI means the GLB's own BIN chunk, and only buffer 0 may do that.
      if (index != 0) {
        throw ImportFailure(
          id,
          'buffer $index has no URI, and only the first '
          'buffer of a GLB may leave it out',
        );
      }
      return binary;
    }
    if (uri is! String) {
      throw ImportFailure(id, "buffer $index's URI is not a string");
    }
    if (uri.startsWith('data:')) return _dataUri(id, uri);

    final at = uri.contains(':') ? null : id.resolve(_decode(uri));
    if (at == null) {
      throw ImportFailure(
        id,
        'buffer $index is at "$uri", which is not a '
        'name inside the project',
      );
    }
    return source.read(at);
  }

  static Uint8List _dataUri(AssetId id, String uri) {
    final comma = uri.indexOf(',');
    if (comma < 0) {
      throw ImportFailure(id, 'has a data URI with no comma in it');
    }
    final head = uri.substring(0, comma);
    final body = uri.substring(comma + 1);
    if (!head.endsWith(';base64')) {
      // Percent-encoded data URIs are legal and vanishingly rare; refusing one
      // by name beats guessing at it.
      throw ImportFailure(
        id,
        'has a data URI that is not base64, which this '
        'cook does not read',
      );
    }
    try {
      return base64Decode(body);
    } on FormatException catch (error) {
      throw ImportFailure(
        id,
        'has a data URI that is not valid base64',
        cause: error,
      );
    }
  }

  static String _decode(String uri) {
    try {
      return Uri.decodeComponent(uri);
    } on ArgumentError {
      return uri;
    }
  }
}
