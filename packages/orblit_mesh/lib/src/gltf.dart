import 'dart:convert';
import 'dart:typed_data';

/// Building the buffer half of a glTF document.
///
/// A glTF file is a small JSON document and one flat block of bytes, and
/// almost everything that goes wrong in writing one goes wrong between the
/// two: a byte offset that forgot its padding, an accessor counting elements
/// where the view counts bytes, a `min` and `max` that were never written and
/// a viewer that frames the model at the origin because of it.
///
/// So the bytes are accumulated here rather than in each writer. A mesh and a
/// whole scene want the same three things — put an array in, get a view and
/// an accessor back, hand the lot over at the end — and doing that once means
/// the padding rule is enforced in one place rather than restated in every
/// exporter and got subtly wrong in one of them.
///
/// This builds; it does not read. Rewriting a document that already exists is
/// `orblit_asset`'s `GltfDocument`, which is a different job — it has buffers
/// it did not write and views it must not move — and shares this file's
/// container code rather than keeping its own.

/// The component types anything here writes.
///
/// glTF's own numbers rather than an enum of ours, because they go into the
/// document as they are and somebody reading the JSON beside this file should
/// find the same value in both.
abstract final class GltfComponent {
  static const int unsignedByte = 5121;
  static const int unsignedShort = 5123;
  static const int unsignedInt = 5125;
  static const int float = 5126;
}

/// What a buffer view is for, when it is for one of the two things glTF names.
///
/// Optional in the format and worth writing: a loader that knows a view holds
/// indices can put it in the right kind of GPU buffer without reading the
/// accessors first, and the validator says so when it is missing.
abstract final class GltfTarget {
  static const int arrayBuffer = 34962;
  static const int elementArrayBuffer = 34963;
}

/// One glTF buffer, its views and its accessors, built up in order.
class GltfBuffer {
  final BytesBuilder _bytes = BytesBuilder();

  /// The views, in the order they were added. A view's index is its name in
  /// the document, which is why nothing is ever removed from this.
  final List<Map<String, Object?>> views = [];

  /// The accessors, likewise.
  final List<Map<String, Object?>> accessors = [];

  /// How many bytes are in the buffer so far, padding included.
  int get length => _bytes.length;

  /// A view over [data], four-byte aligned, returning its index.
  ///
  /// Every view starts on a multiple of four. glTF only asks that an
  /// accessor's offset be a multiple of its component size, at most four for
  /// everything written here, so aligning the view satisfies the rule for
  /// every accessor over it and leaves nothing to check per accessor.
  int addView(TypedData data, {int? target}) {
    final at = addBytes(data);
    views.add({
      'buffer': 0,
      'byteOffset': at,
      'byteLength': data.lengthInBytes,
      if (target != null) 'target': target,
    });
    return views.length - 1;
  }

  /// Bytes with nothing pointing at them yet, returning where they start.
  ///
  /// For grafting one document into another: a document being copied in
  /// brings its own buffer views, and what they need is somewhere to point
  /// rather than a view of their own. Aligned like any other, so the views
  /// that arrive with it keep whatever alignment they had.
  int addBytes(TypedData data) {
    _pad();
    final at = _bytes.length;
    _bytes.add(Uint8List.sublistView(data));
    return at;
  }

  /// An accessor over [view], returning its index.
  int addAccessor({
    required int view,
    required int componentType,
    required int count,
    required String type,
    int byteOffset = 0,
    List<double>? min,
    List<double>? max,
    bool normalized = false,
  }) {
    accessors.add({
      'bufferView': view,
      if (byteOffset != 0) 'byteOffset': byteOffset,
      'componentType': componentType,
      'count': count,
      'type': type,
      if (normalized) 'normalized': true,
      if (min != null) 'min': min,
      if (max != null) 'max': max,
    });
    return accessors.length - 1;
  }

  /// Positions: a view, an accessor, and the bounds glTF requires on them.
  ///
  /// Required by the format on POSITION and nowhere else, and required for a
  /// reason — it is how a loader frames, culls and picks a model it has not
  /// decoded. Worked out here rather than asked for, because a caller that
  /// has to supply it is a caller that can supply the wrong one.
  int addPositions(Float32List values) {
    final view = addView(values, target: GltfTarget.arrayBuffer);
    final count = values.length ~/ 3;
    final min = <double>[double.infinity, double.infinity, double.infinity];
    final max = <double>[
      double.negativeInfinity,
      double.negativeInfinity,
      double.negativeInfinity,
    ];
    for (var i = 0; i < count; i++) {
      for (var axis = 0; axis < 3; axis++) {
        final one = values[i * 3 + axis];
        if (one < min[axis]) min[axis] = one;
        if (one > max[axis]) max[axis] = one;
      }
    }
    // An empty mesh is still a valid file, and infinity is not a number glTF
    // will take. Nought is the honest answer for no points at all.
    if (count == 0) {
      min.fillRange(0, 3, 0);
      max.fillRange(0, 3, 0);
    }
    return addAccessor(
      view: view,
      componentType: GltfComponent.float,
      count: count,
      type: 'VEC3',
      min: min,
      max: max,
    );
  }

  /// Floats as [type] — `SCALAR`, `VEC2`, `VEC3`, `VEC4` or `MAT4`.
  int addFloats(Float32List values, String type, {int? target}) {
    final view = addView(values, target: target);
    return addAccessor(
      view: view,
      componentType: GltfComponent.float,
      count: values.length ~/ componentsIn(type),
      type: type,
    );
  }

  /// Indices, in the smallest component type that can hold the largest one.
  ///
  /// Four bytes an index is what a mesh of any size needs and what most of
  /// them do not: a shape under sixty-five thousand vertices — which is most
  /// of what anybody blocks out — halves its index buffer by saying so, and
  /// every loader there is reads all three widths.
  ///
  /// Note that [Triangles] deliberately does *not* narrow, and holds thirty-two
  /// bits whatever it contains. That is not the same decision made twice
  /// differently. `Triangles` picks its width before it knows what it will
  /// hold, so narrowing there means a second path that only small meshes ever
  /// take and that a generated mesh silently wraps around. This picks the
  /// width from the numbers in hand, at the moment of writing, in one place —
  /// there is nothing left to be wrong about.
  int addIndices(List<int> indices) {
    final narrowed = addIndexView(indices);
    return addAccessor(
      view: narrowed.view,
      componentType: narrowed.componentType,
      count: indices.length,
      type: 'SCALAR',
    );
  }

  /// The same view, without an accessor over it, and how to read it.
  ///
  /// For a mesh drawn in more than one material: the faces are one buffer and
  /// each material's run is an accessor into a stretch of it, so the caller
  /// needs the view and the width the narrowing chose. The width is returned
  /// rather than worked out again from the component type, because working it
  /// out again is a second copy of this rule and a second place to be wrong.
  ({int view, int componentType, int width}) addIndexView(List<int> indices) {
    var largest = 0;
    for (final one in indices) {
      if (one > largest) largest = one;
    }

    final TypedData data;
    final int componentType;
    final int width;
    if (largest < 256) {
      data = Uint8List.fromList(indices);
      componentType = GltfComponent.unsignedByte;
      width = 1;
    } else if (largest < 65536) {
      data = Uint16List.fromList(indices);
      componentType = GltfComponent.unsignedShort;
      width = 2;
    } else {
      data = Uint32List.fromList(indices);
      componentType = GltfComponent.unsignedInt;
      width = 4;
    }

    return (
      view: addView(data, target: GltfTarget.elementArrayBuffer),
      componentType: componentType,
      width: width,
    );
  }

  /// The buffer, padded to four bytes so whatever follows starts aligned.
  Uint8List get bytes {
    _pad();
    return _bytes.toBytes();
  }

  /// The `buffers` array for a document that keeps its bytes in a GLB chunk.
  ///
  /// Empty when nothing was written, because a buffer of no length is one of
  /// the few things the validator does call an error.
  List<Map<String, Object?>> get buffers => length == 0
      ? const []
      : [
          {'byteLength': length},
        ];

  /// How many components one element of [type] has.
  static int componentsIn(String type) => switch (type) {
    'SCALAR' => 1,
    'VEC2' => 2,
    'VEC3' => 3,
    'VEC4' => 4,
    'MAT2' => 4,
    'MAT3' => 9,
    'MAT4' => 16,
    _ => 1,
  };

  void _pad() {
    final over = _bytes.length % 4;
    if (over != 0) _bytes.add(Uint8List(4 - over));
  }
}

/// A `.glb`: the document and its bytes in one container.
///
/// The one place this is written. The chunk padding is not decoration — a
/// GLB's JSON chunk is padded with spaces and its binary chunk with zeroes,
/// because the format says a reader may take a chunk at its stated length and
/// find valid JSON there, and a reader that does is not wrong to.
Uint8List glbBytes(Map<String, Object?> json, Uint8List binary) {
  final text = Uint8List.fromList(utf8.encode(jsonEncode(tidy(json))));
  final jsonLength = _aligned(text.length);
  final jsonChunk = Uint8List(jsonLength)..fillRange(0, jsonLength, 0x20);
  jsonChunk.setRange(0, text.length, text);

  final binaryLength = _aligned(binary.length);
  final total = 12 + 8 + jsonLength + (binary.isEmpty ? 0 : 8 + binaryLength);

  final out = Uint8List(total);
  final data = ByteData.sublistView(out);
  data.setUint32(0, _magic, Endian.little);
  data.setUint32(4, 2, Endian.little);
  data.setUint32(8, total, Endian.little);
  data.setUint32(12, jsonLength, Endian.little);
  data.setUint32(16, _jsonChunk, Endian.little);
  out.setRange(20, 20 + jsonLength, jsonChunk);

  if (binary.isNotEmpty) {
    final at = 20 + jsonLength;
    data.setUint32(at, binaryLength, Endian.little);
    data.setUint32(at + 4, _binChunk, Endian.little);
    out.setRange(at + 8, at + 8 + binary.length, binary);
  }
  return out;
}

/// The two chunks of a `.glb`, or null if that is not what these bytes are.
///
/// Lenient about the binary chunk and strict about the header. A file with no
/// geometry is a real file — a scene of lights and cameras has nothing to put
/// in a buffer — while a file with the wrong magic is something else
/// entirely, and reading on would only find that out later and less clearly.
({Map<String, Object?> json, Uint8List binary})? glbChunks(Uint8List bytes) {
  if (bytes.length < 20) return null;
  final data = ByteData.sublistView(bytes);
  if (data.getUint32(0, Endian.little) != _magic) return null;
  if (data.getUint32(4, Endian.little) != 2) return null;

  final jsonLength = data.getUint32(12, Endian.little);
  if (data.getUint32(16, Endian.little) != _jsonChunk) return null;
  if (20 + jsonLength > bytes.length) return null;

  final Object? parsed;
  try {
    parsed = jsonDecode(utf8.decode(bytes.sublist(20, 20 + jsonLength)));
  } on FormatException {
    return null;
  }
  if (parsed is! Map<String, Object?>) return null;

  var binary = Uint8List(0);
  final at = 20 + jsonLength;
  if (at + 8 <= bytes.length) {
    final length = data.getUint32(at, Endian.little);
    final kind = data.getUint32(at + 4, Endian.little);
    if (kind == _binChunk && at + 8 + length <= bytes.length) {
      binary = Uint8List.sublistView(bytes, at + 8, at + 8 + length);
    }
  }
  return (json: parsed, binary: binary);
}

/// A `.gltf`'s JSON, with its buffer named as the file beside it.
///
/// A sidecar rather than a base64 data URI, because a data URI makes the
/// document a third larger than the bytes it carries and unreadable to the
/// person who asked for the open form in the first place.
///
/// Indented for the same reason. Whoever wanted the `.glb` already has it.
String gltfText(Map<String, Object?> json, {String? buffer, int? byteLength}) {
  if (buffer != null && byteLength != null && byteLength > 0) {
    json['buffers'] = [
      {'uri': buffer, 'byteLength': byteLength},
    ];
  } else {
    json.remove('buffers');
  }
  return const JsonEncoder.withIndent('  ').convert(tidy(json));
}

/// The same document with every whole number written as one.
///
/// glTF is fussy about which of its fields are integers, and a count that
/// arrives as `12.0` is a file some loaders refuse outright. Dart's encoder
/// has no say over that — a `double` goes out with its point whatever it
/// holds — so the document is walked once before encoding and the whole ones
/// are narrowed.
///
/// It also shortens the JSON chunk, which is not why it is here but is worth
/// having: most of the numbers in a scene are nought and one.
///
/// A number that is not finite is left exactly as it is, and the encoder
/// refuses it. That is deliberate. `NaN` is not JSON, and a file carrying it
/// is one no loader will open — better to say so here, where the mesh that
/// produced it is still in hand, than to write it and find out later.
Object? tidy(Object? value) {
  if (value is Map) {
    return {for (final one in value.entries) one.key: tidy(one.value)};
  }
  if (value is List) return [for (final one in value) tidy(one)];
  if (value is double && value.isFinite) {
    final whole = value.roundToDouble();
    if (whole == value && value.abs() < 1e15) return value.toInt();
  }
  return value;
}

const int _magic = 0x46546C67; // 'glTF'
const int _jsonChunk = 0x4E4F534A; // 'JSON'
const int _binChunk = 0x004E4942; // 'BIN\0'

int _aligned(int length) => length + (4 - length % 4) % 4;
