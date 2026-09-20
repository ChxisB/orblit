import 'dart:typed_data';

import 'document.dart';

/// glTF component types, by their numbers.
class ComponentType {
  static const int byte = 5120;
  static const int unsignedByte = 5121;
  static const int short = 5122;
  static const int unsignedShort = 5123;
  static const int unsignedInt = 5125;
  static const int float = 5126;

  /// How wide one component of `type` is.
  static int sizeOf(int type) => switch (type) {
    byte || unsignedByte => 1,
    short || unsignedShort => 2,
    unsignedInt || float => 4,
    _ => throw FormatException('$type is not a glTF component type'),
  };
}

/// How many components a glTF accessor type holds.
int componentsOf(String type) => switch (type) {
  'SCALAR' => 1,
  'VEC2' => 2,
  'VEC3' => 3,
  'VEC4' => 4,
  'MAT2' => 4,
  'MAT3' => 9,
  'MAT4' => 16,
  _ => throw FormatException('"$type" is not a glTF accessor type'),
};

/// Reading and writing the accessors a rewrite touches.
///
/// Only what a rewrite actually needs is here — texture coordinates, indices,
/// positions, and a raw per-element copy for everything else. An accessor this
/// does not understand is left exactly where it is, which is the whole point
/// of editing a document rather than rebuilding one.
extension GltfAccessors on GltfDocument {
  /// One accessor's definition.
  Map<String, Object?> accessor(int index) {
    final accessors = json['accessors'];
    if (accessors is! List || index < 0 || index >= accessors.length) {
      throw FormatException('there is no accessor $index');
    }
    final found = accessors[index];
    if (found is! Map<String, Object?>) {
      throw FormatException('accessor $index is not an object');
    }
    if (found.containsKey('sparse')) {
      // A sparse accessor stores overrides in a second pair of views. Nothing
      // in this rewrite reads one, and silently reading only the base would
      // move the wrong vertices.
      throw FormatException(
        'accessor $index is sparse, which this cook does '
        'not rewrite',
      );
    }
    return found;
  }

  /// How many elements an accessor holds.
  int accessorCount(int index) => accessor(index)['count'] as int? ?? 0;

  /// The bytes of element `i` of an accessor, laid out as glTF has them.
  ///
  /// Used when merging primitives: an attribute whose meaning this rewrite has
  /// no opinion on still has to end up in the merged buffer, and copying the
  /// element whole is both exact and format-agnostic.
  Uint8List elementBytes(int index, int element) {
    final it = accessor(index);
    final view = it['bufferView'] as int?;
    final components = componentsOf(it['type'] as String);
    final width = ComponentType.sizeOf(it['componentType'] as int);
    final size = _paddedElementSize(components, width);
    if (view == null) {
      // No view means every element is zero, which is legal and does happen in
      // documents built by tools that pad an attribute set.
      return Uint8List(size);
    }
    final bytes = viewBytes(view);
    final stride = viewStride(view) ?? size;
    final at = ((it['byteOffset'] as int?) ?? 0) + element * stride;
    if (at + size > bytes.length) {
      throw FormatException('accessor $index reads past its buffer view');
    }
    return Uint8List.sublistView(bytes, at, at + size);
  }

  /// Every element of an accessor, tightly packed, whatever stride the view
  /// it came from used.
  ///
  /// Tightly packed rather than as-found because the result is about to be
  /// concatenated with another accessor's, and two interleaved buffers cannot
  /// be joined end to end and still be read at either stride.
  Uint8List accessorBytes(int index) {
    final it = accessor(index);
    final count = it['count'] as int? ?? 0;
    final size = elementSize(index);
    final view = it['bufferView'] as int?;
    if (view == null) return Uint8List(count * size);

    final bytes = viewBytes(view);
    final stride = viewStride(view) ?? size;
    final base = (it['byteOffset'] as int?) ?? 0;
    // The common case by a wide margin: one accessor over its own view, or a
    // run of one inside a shared one. Copying it whole is one memory move
    // rather than one per vertex, which on a million-vertex mesh is the
    // difference between a cook you wait for and one you notice.
    if (stride == size && base + count * size <= bytes.length) {
      return Uint8List.fromList(
        Uint8List.sublistView(bytes, base, base + count * size),
      );
    }
    final out = Uint8List(count * size);
    for (var i = 0; i < count; i++) {
      final at = base + i * stride;
      if (at + size > bytes.length) {
        throw FormatException('accessor $index reads past its buffer view');
      }
      out.setRange(i * size, (i + 1) * size, bytes, at);
    }
    return out;
  }

  /// How many bytes one element of an accessor takes, tightly packed.
  int elementSize(int index) {
    final it = accessor(index);
    return _paddedElementSize(
      componentsOf(it['type'] as String),
      ComponentType.sizeOf(it['componentType'] as int),
    );
  }

  /// A VEC2 accessor as pairs of floats, whatever it is stored as.
  ///
  /// `KHR_mesh_quantization` lets texture coordinates be bytes or shorts, and
  /// a normalized integer means what its normalized value means, so reading
  /// gives floats in every case and the caller never asks how they were kept.
  Float32List readVec2(int index) {
    final it = accessor(index);
    if (it['type'] != 'VEC2') {
      throw FormatException('accessor $index is ${it['type']}, not VEC2');
    }
    final count = it['count'] as int? ?? 0;
    final type = it['componentType'] as int;
    final normalized = it['normalized'] == true;
    final out = Float32List(count * 2);
    final view = it['bufferView'] as int?;
    if (view == null) return out;

    final bytes = viewBytes(view);
    final data = ByteData.sublistView(bytes);
    final width = ComponentType.sizeOf(type);
    final stride = viewStride(view) ?? width * 2;
    final base = (it['byteOffset'] as int?) ?? 0;
    for (var i = 0; i < count; i++) {
      final at = base + i * stride;
      for (var c = 0; c < 2; c++) {
        out[i * 2 + c] = _readComponent(data, at + c * width, type, normalized);
      }
    }
    return out;
  }

  /// An index accessor as a list of ints, whatever width it is stored at.
  Uint32List readIndices(int index) {
    final it = accessor(index);
    if (it['type'] != 'SCALAR') {
      throw FormatException(
        'index accessor $index is ${it['type']}, not '
        'SCALAR',
      );
    }
    final count = it['count'] as int? ?? 0;
    final type = it['componentType'] as int;
    final out = Uint32List(count);
    final view = it['bufferView'] as int?;
    if (view == null) return out;

    final bytes = viewBytes(view);
    final data = ByteData.sublistView(bytes);
    final width = ComponentType.sizeOf(type);
    final stride = viewStride(view) ?? width;
    final base = (it['byteOffset'] as int?) ?? 0;
    for (var i = 0; i < count; i++) {
      final at = base + i * stride;
      out[i] = switch (type) {
        ComponentType.unsignedByte => data.getUint8(at),
        ComponentType.unsignedShort => data.getUint16(at, Endian.little),
        ComponentType.unsignedInt => data.getUint32(at, Endian.little),
        _ => throw FormatException('indices cannot be component type $type'),
      };
    }
    return out;
  }

  /// A VEC3 float accessor, read straight.
  Float32List readVec3(int index) {
    final it = accessor(index);
    if (it['type'] != 'VEC3') {
      throw FormatException('accessor $index is ${it['type']}, not VEC3');
    }
    final count = it['count'] as int? ?? 0;
    final type = it['componentType'] as int;
    final normalized = it['normalized'] == true;
    final out = Float32List(count * 3);
    final view = it['bufferView'] as int?;
    if (view == null) return out;

    final bytes = viewBytes(view);
    final data = ByteData.sublistView(bytes);
    final width = ComponentType.sizeOf(type);
    final stride = viewStride(view) ?? width * 3;
    final base = (it['byteOffset'] as int?) ?? 0;
    for (var i = 0; i < count; i++) {
      final at = base + i * stride;
      for (var c = 0; c < 3; c++) {
        out[i * 3 + c] = _readComponent(data, at + c * width, type, normalized);
      }
    }
    return out;
  }

  /// Adds a VEC2 float accessor over `values`, returning its index.
  int addVec2(Float32List values) {
    final view = addView(
      Uint8List.sublistView(values),
      target: 34962, // ARRAY_BUFFER
    );
    return add('accessors', <String, Object?>{
      'bufferView': view,
      'componentType': ComponentType.float,
      'count': values.length ~/ 2,
      'type': 'VEC2',
    });
  }

  /// Adds an index accessor over `values`, narrowed to the smallest width
  /// that holds them.
  ///
  /// Width is not cosmetic here: a merged primitive that stays under 65,536
  /// vertices keeps 16-bit indices, which is half the bandwidth per draw and
  /// is what the merge was for.
  int addIndices(List<int> values) {
    var highest = 0;
    for (final value in values) {
      if (value > highest) highest = value;
    }
    final (int type, Uint8List bytes) = switch (highest) {
      < 256 => (ComponentType.unsignedByte, Uint8List.fromList(values)),
      < 65536 => (
        ComponentType.unsignedShort,
        Uint8List.sublistView(Uint16List.fromList(values)),
      ),
      _ => (
        ComponentType.unsignedInt,
        Uint8List.sublistView(Uint32List.fromList(values)),
      ),
    };
    final view = addView(bytes, target: 34963); // ELEMENT_ARRAY_BUFFER
    return add('accessors', <String, Object?>{
      'bufferView': view,
      'componentType': type,
      'count': values.length,
      'type': 'SCALAR',
    });
  }

  /// Adds an accessor over raw element bytes copied from another accessor,
  /// keeping its type, component type and normalization.
  ///
  /// `min` and `max` are glTF-required on `POSITION`, so they are carried when
  /// the caller has recomputed them and dropped otherwise — a stale bounding
  /// box is worse than none, because culling believes it.
  int addLike(
    int template,
    List<int> bytes,
    int count, {
    List<double>? min,
    List<double>? max,
  }) {
    final it = accessor(template);
    final view = addView(bytes, target: 34962);
    return add('accessors', <String, Object?>{
      'bufferView': view,
      'componentType': it['componentType'],
      'count': count,
      'type': it['type'],
      if (it['normalized'] == true) 'normalized': true,
      if (min != null) 'min': min,
      if (max != null) 'max': max,
    });
  }

  static int _paddedElementSize(int components, int width) {
    // glTF pads each element of a matrix accessor so its columns start on
    // four-byte boundaries. Vectors and scalars are packed.
    final size = components * width;
    return size;
  }

  static double _readComponent(
    ByteData data,
    int at,
    int type,
    bool normalized,
  ) => switch (type) {
    ComponentType.float => data.getFloat32(at, Endian.little),
    ComponentType.unsignedByte =>
      normalized ? data.getUint8(at) / 255.0 : data.getUint8(at).toDouble(),
    ComponentType.byte =>
      normalized
          ? (data.getInt8(at) / 127.0).clamp(-1.0, 1.0)
          : data.getInt8(at).toDouble(),
    ComponentType.unsignedShort =>
      normalized
          ? data.getUint16(at, Endian.little) / 65535.0
          : data.getUint16(at, Endian.little).toDouble(),
    ComponentType.short =>
      normalized
          ? (data.getInt16(at, Endian.little) / 32767.0).clamp(-1.0, 1.0)
          : data.getInt16(at, Endian.little).toDouble(),
    ComponentType.unsignedInt => data.getUint32(at, Endian.little).toDouble(),
    _ => throw FormatException('$type is not a glTF component type'),
  };
}
