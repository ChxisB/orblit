import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'gltf.dart';

/// Reading the numbers out of a glTF document somebody else wrote.
///
/// The other half of [GltfBuffer], kept beside it so the two agree about what
/// an accessor is. Everything that reads a model's bytes goes through here:
/// the scene importer for geometry, the clip importer for keyframes. Two
/// readers of one format drift, and the day they do, one of them reads a
/// stride the other ignores and a model comes in subtly wrong.
///
/// Lenient in the way every reader in Orblit is: an accessor that cannot be
/// read comes back empty with a note in [problems], because a document with
/// one bad accessor still has a hundred good ones.
class GltfAccessors {
  GltfAccessors(
    Map<String, Object?> json,
    this.binary, {
    List<String>? problems,
  }) : problems = problems ?? [],
       _accessors = _maps(json['accessors']),
       _views = _maps(json['bufferViews']);

  /// The bytes the accessors point into: a GLB's `BIN` chunk, or the `.bin`
  /// beside a `.gltf`.
  final Uint8List? binary;

  /// What could not be read, in the order it was met.
  final List<String> problems;

  final List<Map<String, Object?>> _accessors;
  final List<Map<String, Object?>> _views;

  /// How many elements accessor [at] has, or nought when there is no such
  /// accessor.
  int count(int? at) {
    if (at == null || at < 0 || at >= _accessors.length) return 0;
    return _index(_accessors[at]['count']) ?? 0;
  }

  /// Accessor [at], [components] numbers to an element, flattened.
  ///
  /// Floating point is read as it is. Whole numbers are read too, because
  /// glTF lets a model store a rotation or a position in fewer bytes: one
  /// marked `normalized` is scaled into nought to one, or minus one to one,
  /// the way the format defines, and one that is not is read as the number
  /// it is.
  List<double> floats(int? at, int components) {
    if (at == null || at < 0 || at >= _accessors.length) return const [];
    final accessor = _accessors[at];
    final type = _index(accessor['componentType']);
    final normalized = accessor['normalized'] == true;
    final size = _sizeOf(type);
    if (size == 0) {
      problems.add('Accessor $at is not a numeric type; it was not read.');
      return const [];
    }

    double read(ByteData data, int offset) {
      final raw = switch (type) {
        GltfComponent.float => data.getFloat32(offset, Endian.little),
        _byte => data.getInt8(offset).toDouble(),
        GltfComponent.unsignedByte => data.getUint8(offset).toDouble(),
        _short => data.getInt16(offset, Endian.little).toDouble(),
        GltfComponent.unsignedShort =>
          data.getUint16(offset, Endian.little).toDouble(),
        _ => data.getUint32(offset, Endian.little).toDouble(),
      };
      if (!normalized) return raw;
      return switch (type) {
        _byte => math.max(raw / 127, -1),
        GltfComponent.unsignedByte => raw / 255,
        _short => math.max(raw / 32767, -1),
        GltfComponent.unsignedShort => raw / 65535,
        _ => raw,
      };
    }

    final out = <double>[];
    _walk(at, components, size, (data, offset) {
      for (var c = 0; c < components; c++) {
        out.add(read(data, offset + c * size));
      }
    });
    return out;
  }

  /// Accessor [at] as whole numbers, which is what indices are.
  List<int> indices(int at) {
    if (at < 0 || at >= _accessors.length) return const [];
    final size = switch (_index(_accessors[at]['componentType'])) {
      GltfComponent.unsignedByte => 1,
      GltfComponent.unsignedShort => 2,
      GltfComponent.unsignedInt => 4,
      _ => 0,
    };
    if (size == 0) {
      problems.add('Accessor $at is not an index type; it was not read.');
      return const [];
    }

    final out = <int>[];
    _walk(at, 1, size, (data, offset) {
      out.add(switch (size) {
        1 => data.getUint8(offset),
        2 => data.getUint16(offset, Endian.little),
        _ => data.getUint32(offset, Endian.little),
      });
    });
    return out;
  }

  /// Steps through an accessor's elements, minding the view's stride.
  ///
  /// The stride is not a detail that can be skipped: interleaved attributes
  /// are what an optimising exporter writes, and reading one as though it were
  /// tightly packed gives numbers that are *wrong* rather than missing, which
  /// is far harder to notice.
  void _walk(
    int at,
    int components,
    int size,
    void Function(ByteData data, int offset) each,
  ) {
    final accessor = _accessors[at];
    final bytes = binary;
    final viewAt = _index(accessor['bufferView']);
    final count = _index(accessor['count']) ?? 0;
    if (count == 0) return;
    if (accessor['sparse'] != null) {
      problems.add('Accessor $at is sparse, which is not read yet.');
    }
    if (bytes == null) {
      problems.add('The document has data but no buffer was supplied.');
      return;
    }
    if (viewAt == null || viewAt >= _views.length) return;

    final view = _views[viewAt];
    final base = _index(view['byteOffset']) ?? 0;
    final start = base + (_index(accessor['byteOffset']) ?? 0);
    final stride = _index(view['byteStride']) ?? (size * components);
    final end = math.min(
      bytes.length,
      base + (_index(view['byteLength']) ?? 0),
    );

    final data = ByteData.sublistView(bytes);
    for (var i = 0; i < count; i++) {
      final offset = start + i * stride;
      if (offset < 0 || offset + size * components > end) {
        problems.add('An accessor reaches past the end of its buffer view.');
        return;
      }
      each(data, offset);
    }
  }

  static const int _byte = 5120;
  static const int _short = 5122;

  static int _sizeOf(int? type) => switch (type) {
    _byte || GltfComponent.unsignedByte => 1,
    _short || GltfComponent.unsignedShort => 2,
    GltfComponent.unsignedInt || GltfComponent.float => 4,
    _ => 0,
  };

  static List<Map<String, Object?>> _maps(Object? raw) => [
    if (raw is List)
      for (final item in raw)
        if (item is Map<String, Object?>) item,
  ];

  static int? _index(Object? raw) => raw is int && raw >= 0 ? raw : null;
}

/// A glTF document's JSON and the bytes its accessors read, whichever of the
/// two forms it came in.
///
/// Which one comes from the bytes rather than from a name: a `.gltf` that is
/// really a GLB is an ordinary thing to be handed, and a reader that trusts
/// the extension fails on it with a JSON error nobody can act on.
///
/// [files] supplies whatever the document names by URI, the `.bin` beside a
/// `.gltf` being the one that matters. Bytes rather than a way to read them,
/// because this has to run in an editor, a cook step and a browser alike.
///
/// Throws [FormatException] when the bytes are neither a GLB nor a JSON
/// document.
({Map<String, Object?> json, Uint8List? binary}) gltfParts(
  Uint8List bytes, {
  Map<String, Uint8List> files = const {},
}) {
  final chunks = glbChunks(bytes);
  if (chunks != null) return (json: chunks.json, binary: chunks.binary);

  final Object? decoded;
  try {
    decoded = jsonDecode(utf8.decode(bytes));
  } on FormatException catch (error) {
    throw FormatException(
      'This is neither a GLB nor glTF JSON: ${error.message}',
    );
  }
  if (decoded is! Map<String, Object?>) {
    throw const FormatException('A glTF document must be a JSON object.');
  }
  return (json: decoded, binary: _buffer(decoded, files));
}

/// The bytes the document's first buffer names, wherever they are.
///
/// Only the first, because that is the only one anything here writes and the
/// only one an accessor in a document we produced ever points into. A document
/// with several is read as far as its first, and the accessors that reach past
/// it say so themselves.
Uint8List? _buffer(Map<String, Object?> json, Map<String, Uint8List> files) {
  final buffers = json['buffers'];
  if (buffers is! List || buffers.isEmpty) return null;
  final first = buffers.first;
  if (first is! Map<String, Object?>) return null;

  final uri = first['uri'];
  if (uri is! String || uri.isEmpty) return null;

  if (uri.startsWith('data:')) {
    final comma = uri.indexOf(',');
    // Only base64 is worth reading: a percent-encoded binary buffer is legal
    // and is also several times the size, so nothing writes one.
    if (comma < 0 || !uri.substring(0, comma).endsWith(';base64')) return null;
    try {
      return base64Decode(uri.substring(comma + 1));
    } on FormatException {
      return null;
    }
  }

  // Named twice, because a URI is escaped and a filename is not: a buffer
  // written beside a scene called `old town.gltf` arrives as `old%20town.bin`
  // and is on disk under the name with the space in it.
  return files[uri] ?? files[Uri.decodeComponent(uri)];
}
