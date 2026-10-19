import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'mesh.dart';
import 'read_limits.dart';
import 'triangles.dart' show GlbMaterial;
import 'uv.dart';

/// The faces an `o` or `g` line named.
final class ObjGroup {
  const ObjGroup(this.name, this.faces);

  final String name;

  /// Indices into [ObjModel.mesh]'s faces, in the order they were read.
  final List<int> faces;
}

/// An OBJ file, read.
final class ObjModel {
  const ObjModel({
    required this.mesh,
    required this.groups,
    required this.materials,
    required this.problems,
  });

  /// Every point and face in the file, n-gons as they were written. A face's
  /// [Face.material] indexes [materials].
  final Mesh mesh;

  /// The named parts of the file, in the order each first had a face.
  final List<ObjGroup> groups;

  /// The materials the file's libraries defined, and any a face named that no
  /// library defined, as a default one with that name.
  final List<GlbMaterial> materials;

  /// What could not be read, in the order it was met.
  final List<String> problems;

  /// [group] as a mesh of its own, holding only the points its faces use.
  /// The material indices still index [materials].
  Mesh meshOf(ObjGroup group) {
    final taken = <int, int>{};
    final out = Mesh();
    for (final at in group.faces) {
      final face = mesh.faces[at].copy();
      for (var i = 0; i < face.vertices.length; i++) {
        face.vertices[i] = taken.putIfAbsent(face.vertices[i], () {
          out.positions.add(mesh.positions[face.vertices[i]].clone());
          return out.positions.length - 1;
        });
      }
      out.faces.add(face);
    }
    return out;
  }
}

/// Reads the Wavefront OBJ in [bytes].
///
/// [files] holds the `.mtl` the file names, by the name it gives. The folder
/// in a name is ignored when the whole name is not there.
///
/// Reads `v`, `vt`, `f`, `o`, `g`, `s`, `usemtl` and `mtllib`. Normals, lines,
/// points and curves are skipped without a note, and so are the pictures an
/// `.mtl` names, with one note for each material that has any.
///
/// Lenient, like the glTF readers: a line that cannot be read is a note in
/// [ObjModel.problems] and the rest of the file is still read. Throws
/// [FormatException] when a limit in [limits] is passed, or when the file
/// holds no face.
ObjModel readObj(
  Uint8List bytes, {
  Map<String, Uint8List> files = const {},
  ReadLimits limits = const ReadLimits(),
}) {
  if (bytes.length > limits.bytes) {
    throw FormatException('An OBJ over ${limits.bytes} bytes is not read.');
  }
  return _ObjReader(
    files,
    limits,
  ).read(utf8.decode(bytes, allowMalformed: true));
}

/// The box an OBJ's points sit in, or null when it cannot be read.
///
/// The third of the readers beside [boundsOfGlb] and [boundsOfGltf]. An OBJ
/// has no minimum and maximum written down, so this reads the points.
({Vector3 min, Vector3 max})? boundsOfObj(Uint8List obj) {
  try {
    return readObj(obj).mesh.bounds;
  } on FormatException {
    return null;
  }
}

final _space = RegExp(r'\s+');
final _slash = RegExp(r'[\\/]');

/// The most notes kept. A file of bad lines should not fill memory with
/// sentences about them.
const _listed = 50;

final class _ObjReader {
  _ObjReader(this.files, this.limits);

  final Map<String, Uint8List> files;
  final ReadLimits limits;

  final Mesh _mesh = Mesh();
  final List<double> _uvs = [];
  final Map<String, List<int>> _groups = {};
  final List<GlbMaterial> _materials = [];
  final Map<String, int> _materialAt = {};
  final List<String> _problems = [];
  var _unlisted = 0;
  var _line = 0;
  var _group = 'default';
  var _material = 0;
  var _smooth = false;

  ObjModel read(String text) {
    final body = text.startsWith('﻿') ? text.substring(1) : text;
    for (final (number, line) in _lines(body)) {
      _line = number;
      _take(line);
    }
    if (_mesh.faces.isEmpty) {
      throw const FormatException('This OBJ has no faces, so no model.');
    }
    if (_unlisted > 0) {
      _problems.add('$_unlisted more problems were not listed.');
    }
    return ObjModel(
      mesh: _mesh,
      groups: [
        for (final entry in _groups.entries) ObjGroup(entry.key, entry.value),
      ],
      materials: List.unmodifiable(_materials),
      problems: _problems,
    );
  }

  /// The file's lines, each with the number of the line it began on. A line
  /// ending in a backslash goes on to the next, which is how long faces are
  /// wrapped.
  static Iterable<(int, String)> _lines(String text) sync* {
    final joined = StringBuffer();
    var start = 0;
    var number = 0;
    for (final raw in text.split('\n')) {
      number++;
      final line = raw.trimRight();
      if (joined.isEmpty) start = number;
      if (line.endsWith(r'\')) {
        joined
          ..write(line.substring(0, line.length - 1))
          ..write(' ');
        continue;
      }
      joined.write(line);
      yield (start, joined.toString());
      joined.clear();
    }
    if (joined.isNotEmpty) yield (start, joined.toString());
  }

  void _take(String line) {
    final hash = line.indexOf('#');
    final words = (hash < 0 ? line : line.substring(0, hash)).trim().split(
      _space,
    );
    final args = words.sublist(1);
    switch (words.first) {
      case 'v':
        _vertex(args);
      case 'vt':
        _coordinate(args);
      case 'f':
        _face(args);
      case 'o' || 'g':
        _group = args.isEmpty ? 'default' : args.join(' ');
      case 'usemtl':
        _material = _materialNamed(args.join(' '));
      case 'mtllib':
        args.forEach(_library);
      case 's':
        _smooth = args.isNotEmpty && args.first != 'off' && args.first != '0';
    }
  }

  void _vertex(List<String> args) {
    if (_mesh.vertexCount >= limits.vertices) {
      throw FormatException('More than ${limits.vertices} points.');
    }
    final xyz = _numbers(args, 3);
    if (xyz == null) return _note('a point needs three finite numbers.');
    _mesh.positions.add(Vector3(xyz[0], xyz[1], xyz[2]));
  }

  void _coordinate(List<String> args) {
    if (_uvs.length ~/ 2 >= limits.vertices) {
      throw FormatException('More than ${limits.vertices} coordinates.');
    }
    final uv = _numbers(args.length == 1 ? [args[0], '0'] : args, 2);
    if (uv == null) return _note('a coordinate needs finite numbers.');
    _uvs.addAll(uv);
  }

  void _face(List<String> args) {
    if (_mesh.faceCount >= limits.faces) {
      throw FormatException('More than ${limits.faces} faces.');
    }
    if (args.length < 3) return _note('a face needs three corners.');
    if (args.length > limits.corners) {
      return _note(
        'a face has ${args.length} corners, over ${limits.corners}.',
      );
    }
    final points = <int>[];
    final uvs = <Vector2>[];
    for (final word in args) {
      final parts = word.split('/');
      final point = _resolve(parts[0], _mesh.vertexCount);
      if (point == null) return _note('"$word" is not a point read so far.');
      points.add(point);
      final uv = parts.length > 1 ? _resolve(parts[1], _uvs.length ~/ 2) : null;
      if (uv != null) uvs.add(Vector2(_uvs[uv * 2], _uvs[uv * 2 + 1]));
    }
    _groups.putIfAbsent(_group, () => []).add(_mesh.faceCount);
    _mesh.faces.add(
      Face(
        points,
        material: _material,
        smooth: _smooth,
        // Coordinates are kept only when every corner had one, because a
        // face with some is no more drawn than one with none.
        uv: uvs.length == points.length ? FaceUv(manual: uvs) : null,
      ),
    );
  }

  /// The zero-based index [word] names among [count] items, or null when it
  /// is not a number or is outside them. A negative one counts back from the
  /// last item read.
  static int? _resolve(String word, int count) {
    final number = int.tryParse(word);
    if (number == null || number == 0) return null;
    final at = number > 0 ? number - 1 : count + number;
    return at >= 0 && at < count ? at : null;
  }

  static List<double>? _numbers(List<String> words, int count) {
    if (words.length < count) return null;
    final out = <double>[];
    for (final word in words.take(count)) {
      final value = double.tryParse(word);
      if (value == null || !value.isFinite) return null;
      out.add(value);
    }
    return out;
  }

  int _materialNamed(String name) => _materialAt.putIfAbsent(name, () {
    _materials.add(GlbMaterial(name: name));
    return _materials.length - 1;
  });

  void _define(GlbMaterial material) {
    final at = _materialAt[material.name];
    if (at == null) {
      _materialAt[material.name] = _materials.length;
      _materials.add(material);
    } else {
      _materials[at] = material;
    }
  }

  void _library(String name) {
    final bytes = files[name] ?? files[name.split(_slash).last];
    if (bytes == null) return _note('the library $name was not given.');
    if (bytes.length > limits.bytes) {
      return _note('the library $name is over ${limits.bytes} bytes.');
    }
    _Draft? draft;
    for (final raw in utf8.decode(bytes, allowMalformed: true).split('\n')) {
      final line = raw.split('#').first.trim();
      if (line.isEmpty) continue;
      final words = line.split(_space);
      if (words.first.toLowerCase() == 'newmtl') {
        _finish(draft);
        draft = _Draft(words.sublist(1).join(' '));
      } else if (draft != null && !draft.read(words)) {
        _remark('Material ${draft.name}: "$line" was not read.');
      }
    }
    _finish(draft);
  }

  void _finish(_Draft? draft) {
    if (draft == null) return;
    _define(draft.build());
    if (draft.pictures) {
      _remark('Material ${draft.name} has pictures, which were not read.');
    }
  }

  void _note(String text) => _remark('Line $_line: $text');

  void _remark(String text) {
    if (_problems.length < _listed) {
      _problems.add(text);
    } else {
      _unlisted++;
    }
  }
}

/// A material being read out of an `.mtl`.
final class _Draft {
  _Draft(this.name);

  final String name;
  final List<double> colour = [0.8, 0.8, 0.8, 1.0];
  final List<double> emissive = [0, 0, 0];
  double? _roughness;
  double? _exponent;
  double _metallic = 0;
  bool pictures = false;

  /// Takes one line. False when its numbers are not numbers.
  bool read(List<String> words) {
    final args = words.sublist(1);
    switch (words.first.toLowerCase()) {
      case 'kd':
        return _into(colour, args);
      case 'ke':
        return _into(emissive, args);
      case 'd':
        return _one(args, (v) => colour[3] = v);
      case 'tr':
        return _one(args, (v) => colour[3] = 1 - v);
      case 'ns':
        return _one(args, (v) => _exponent = v);
      case 'pr':
        return _one(args, (v) => _roughness = v);
      case 'pm':
        return _one(args, (v) => _metallic = v);
      case final other when _isPicture(other):
        pictures = true;
    }
    return true;
  }

  static bool _isPicture(String key) =>
      key.startsWith('map_') || const {'bump', 'disp', 'decal'}.contains(key);

  static bool _into(List<double> out, List<String> args) {
    final values = _ObjReader._numbers(args, 3);
    if (values == null) return false;
    out.setRange(0, 3, values);
    return true;
  }

  static bool _one(List<String> args, void Function(double) set) {
    final values = _ObjReader._numbers(args, 1);
    if (values == null) return false;
    set(values.first);
    return true;
  }

  /// A material from what was read.
  ///
  /// Roughness is `Pr` when the file has it. Otherwise it is the specular
  /// exponent run backwards through the mapping `toObj` writes. `Ks` is not
  /// read for metalness, because OBJ files fill it with a half by default and
  /// every material would come out half metal; `Pm` is the one that says.
  GlbMaterial build() {
    final exponent = _exponent;
    final roughness =
        _roughness ??
        (exponent == null
            ? 0.5
            : 1 - math.sqrt(math.max(0, exponent - 1) / 900));
    return GlbMaterial(
      name: name,
      colour: List.unmodifiable(colour),
      metallic: _metallic.clamp(0.0, 1.0),
      roughness: roughness.clamp(0.0, 1.0),
      emissive: List.unmodifiable(emissive),
    );
  }
}
