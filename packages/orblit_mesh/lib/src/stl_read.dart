import 'dart:convert';
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'mesh.dart';
import 'read_limits.dart';

/// Reads the STL in [bytes], binary or text, as a mesh of triangles.
///
/// STL repeats every corner for each triangle that uses it. Corners at the
/// same place are joined, so the mesh comes back as a solid and not as a pile
/// of loose triangles. A triangle with two equal corners has no area and is
/// dropped. The normals in the file are not kept, because a mesh works them
/// out from the winding.
///
/// Throws [FormatException] when the file is not an STL, is cut short, holds
/// a number that is not finite, or passes [limits].
///
/// shortcut: a binary file is told by its length being exactly 84 plus 50 for
/// each triangle, so one with bytes after the last triangle is read as text and
/// refused. Loosen the check when a writer that pads turns up.
Mesh readStl(Uint8List bytes, {ReadLimits limits = const ReadLimits()}) {
  if (bytes.length > limits.bytes) {
    throw FormatException('An STL over ${limits.bytes} bytes is not read.');
  }
  final welder = _Welder(limits);
  if (_isBinary(bytes)) {
    _binary(bytes, welder, limits);
  } else {
    _text(bytes, welder, limits);
  }
  if (welder.mesh.faces.isEmpty || !welder.isBetweenTriangles) {
    throw const FormatException('This STL has no whole triangle.');
  }
  return welder.mesh;
}

bool _isBinary(Uint8List bytes) {
  if (bytes.length < 84) return false;
  final count = ByteData.sublistView(bytes).getUint32(80, Endian.little);
  return bytes.length == 84 + 50 * count;
}

void _binary(Uint8List bytes, _Welder welder, ReadLimits limits) {
  final data = ByteData.sublistView(bytes);
  final count = (bytes.length - 84) ~/ 50;
  if (count > limits.faces) {
    throw FormatException('More than ${limits.faces} triangles.');
  }
  for (var i = 0; i < count; i++) {
    // Twelve bytes of normal, then the three corners.
    final at = 84 + i * 50 + 12;
    for (var corner = 0; corner < 3; corner++) {
      final p = at + corner * 12;
      welder.add(
        data.getFloat32(p, Endian.little),
        data.getFloat32(p + 4, Endian.little),
        data.getFloat32(p + 8, Endian.little),
      );
    }
  }
}

final _space = RegExp(r'\s+');

final _solid = RegExp(r'^\s*solid');

void _text(Uint8List bytes, _Welder welder, ReadLimits limits) {
  final text = utf8.decode(bytes, allowMalformed: true);
  if (!_solid.hasMatch(text)) {
    throw const FormatException('This is not an STL.');
  }
  for (final line in linesOf(text, limits)) {
    final words = line.trim().split(_space);
    if (words.first.toLowerCase() != 'vertex') continue;
    final xyz = words.length < 4
        ? null
        : [for (final word in words.skip(1).take(3)) double.tryParse(word)];
    if (xyz == null || xyz.contains(null)) {
      throw FormatException('"${line.trim()}" is not a point.');
    }
    welder.add(xyz[0]!, xyz[1]!, xyz[2]!);
  }
}

/// Joins corners that sit at one place, and makes a face of every three.
final class _Welder {
  _Welder(this._limits);

  final ReadLimits _limits;
  final Mesh mesh = Mesh();
  final Map<(double, double, double), int> _seen = {};
  final List<int> _pending = [];

  bool get isBetweenTriangles => _pending.isEmpty;

  void add(double x, double y, double z) {
    if (!(x.isFinite && y.isFinite && z.isFinite)) {
      throw const FormatException('An STL point is not a finite number.');
    }
    if (_pending.isEmpty && mesh.faceCount >= _limits.faces) {
      throw FormatException('More than ${_limits.faces} triangles.');
    }
    // Adding zero turns -0 into 0. The two are equal, and a key that holds
    // one must find the other.
    final at = _seen.putIfAbsent((x + 0.0, y + 0.0, z + 0.0), () {
      if (mesh.vertexCount >= _limits.vertices) {
        throw FormatException('More than ${_limits.vertices} points.');
      }
      mesh.positions.add(Vector3(x, y, z));
      return mesh.vertexCount - 1;
    });
    _pending.add(at);
    if (_pending.length < 3) return;
    final [a, b, c] = _pending;
    if (a != b && b != c && a != c) mesh.faces.add(Face([a, b, c]));
    _pending.clear();
  }
}
