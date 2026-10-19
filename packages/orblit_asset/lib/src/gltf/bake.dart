import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import '../asset_id.dart';
import '../asset_source.dart';
import '../importer.dart' show ImportFailure;
import 'accessor.dart';
import 'document.dart';
import 'merge.dart' show primitiveGroupKey;

/// A model placed once for each of [placements], as one GLB.
///
/// A scene of a thousand rocks is a thousand draws however small each rock is,
/// because a draw is a primitive and a renderer cannot join primitives that sit
/// at different transforms. Baking each placement into the vertices turns the
/// thousand into one model with as many primitives as the rock has materials.
/// What it costs is that the thousand can no longer move on their own, so the
/// caller bakes objects that stay put and bakes again when one of them leaves.
///
/// The transform of every mesh node in the model is baked in too, so the rock
/// comes out where it would have been drawn. Normals turn with the inverse
/// transpose, so a stretch does not tilt the light. A mirrored placement turns
/// its triangles round, or it would draw inside out.
///
/// Returns null when the model cannot be baked, and the caller keeps drawing
/// it as it was. That is a model with a skin, an animation, a morph target, a
/// compressed buffer, a node that is instanced already, a position, normal or
/// tangent not stored as floats, a buffer that is not in the file, or bytes that
/// are not a model. A placement with no volume or with a NaN or an infinity in
/// it is skipped. Lights and cameras in the model are not carried over.
///
/// The model's own geometry stays in the file beside the baked copy, because
/// its textures share the buffer. A model whose geometry dwarfs its placements
/// is not worth baking.
Future<Uint8List?> bakeInstances(
  Uint8List model,
  List<Matrix4> placements, {
  int maxVertices = 65536,
}) async {
  if (placements.isEmpty) return null;
  final GltfDocument document;
  try {
    document = await GltfDocument.read(
      AssetId.parse('baked.glb'),
      model,
      MemoryAssetSource(const {}),
    );
  } on ImportFailure {
    return null;
  } on FormatException {
    return null;
  } on AssetNotFound {
    return null;
  }
  final parts = _parts(document);
  if (parts == null) return null;

  final batches = <String, _Batch>{};
  for (final placement in placements) {
    // A placement with no volume draws nothing and has no normal matrix. One
    // with a NaN or an infinity would put them in the file's bounds.
    if (!_placeable(placement)) continue;
    for (final part in parts) {
      final matrix = placement.multiplied(part.world);
      (batches[part.key] ??= _Batch(
        document,
        maxVertices,
      )).add(part.source, matrix);
    }
  }

  if (batches.isEmpty) return null;
  document.json
    ..['meshes'] = [
      {
        'primitives': [for (final batch in batches.values) ...batch.finish()],
      },
    ]
    ..['nodes'] = [
      {'mesh': 0},
    ]
    ..['scenes'] = [
      {
        'nodes': [0],
      },
    ]
    ..['scene'] = 0;
  return document.toGlb();
}

bool _placeable(Matrix4 placement) =>
    placement.storage.every((v) => v.isFinite) && placement.determinant() != 0;

const _refused = ['EXT_meshopt_compression', 'KHR_draco_mesh_compression'];

/// One primitive of the model with the world transform of the node that draws
/// it, or null when anything in the model rules baking out.
List<_Part>? _parts(GltfDocument document) {
  if (document.list('skins').isNotEmpty) return null;
  if (document.list('animations').isNotEmpty) return null;
  final required = document.json['extensionsRequired'];
  if (required is List && required.any(_refused.contains)) return null;

  final nodes = document.list('nodes');
  final meshes = document.list('meshes');
  final parts = <_Part>[];
  final seen = <int>{};

  bool visit(int index, Matrix4 parent) {
    if (index < 0 || index >= nodes.length || !seen.add(index)) return false;
    final node = nodes[index];
    final extensions = node['extensions'];
    if (extensions is Map &&
        extensions.containsKey('KHR_mesh_gpu_instancing')) {
      return false;
    }
    final world = parent.multiplied(_local(node));
    final mesh = node['mesh'];
    if (mesh is int) {
      final primitives = mesh < meshes.length
          ? meshes[mesh]['primitives']
          : null;
      if (primitives is! List) return false;
      for (final primitive in primitives) {
        final part = _part(document, primitive, world);
        if (part == null) return false;
        parts.add(part);
      }
    }
    final children = node['children'];
    if (children is! List) return true;
    return children.every((child) => child is int && visit(child, world));
  }

  for (final root in _roots(document, nodes)) {
    if (!visit(root, Matrix4.identity())) return null;
  }
  return parts;
}

_Part? _part(GltfDocument document, Object? primitive, Matrix4 world) {
  if (primitive is! Map<String, Object?>) return null;
  final key = primitiveGroupKey(document, primitive);
  if (key == null) return null;
  final attributes = primitive['attributes']! as Map<String, Object?>;
  for (final name in const ['POSITION', 'NORMAL', 'TANGENT']) {
    final index = attributes[name];
    if (index is! int) continue;
    final accessor = document.accessor(index);
    if (accessor['componentType'] != ComponentType.float) return null;
  }
  return _Part(key, world, _Source(document, primitive));
}

/// The nodes a scene starts from.
List<int> _roots(GltfDocument document, List<Map<String, Object?>> nodes) {
  final scenes = document.list('scenes');
  if (scenes.isNotEmpty) {
    final scene = document.json['scene'];
    final listed = scenes[scene is int && scene < scenes.length ? scene : 0];
    final roots = listed['nodes'];
    return roots is List ? roots.whereType<int>().toList() : const [];
  }
  final children = <int>{
    for (final node in nodes)
      if (node['children'] case final List<Object?> list)
        ...list.whereType<int>(),
  };
  return [
    for (var i = 0; i < nodes.length; i++)
      if (!children.contains(i)) i,
  ];
}

Matrix4 _local(Map<String, Object?> node) {
  final matrix = node['matrix'];
  if (matrix is List && matrix.length == 16) {
    return Matrix4.fromList([for (final v in matrix) (v as num).toDouble()]);
  }
  return Matrix4.compose(
    _vector(node['translation'], 0),
    _quaternion(node['rotation']),
    _vector(node['scale'], 1),
  );
}

Vector3 _vector(Object? list, double fallback) =>
    list is List && list.length == 3
    ? Vector3(
        (list[0] as num).toDouble(),
        (list[1] as num).toDouble(),
        (list[2] as num).toDouble(),
      )
    : Vector3.all(fallback);

Quaternion _quaternion(Object? list) => list is List && list.length == 4
    ? Quaternion(
        (list[0] as num).toDouble(),
        (list[1] as num).toDouble(),
        (list[2] as num).toDouble(),
        (list[3] as num).toDouble(),
      )
    : Quaternion.identity();

final class _Part {
  _Part(this.key, this.world, this.source);

  final String key;
  final Matrix4 world;
  final _Source source;
}

/// One primitive of the model, read once and placed many times.
final class _Source {
  _Source(GltfDocument document, this.primitive) {
    final attributes = primitive['attributes']! as Map<String, Object?>;
    for (final MapEntry(:key, :value) in attributes.entries) {
      final index = value! as int;
      switch (key) {
        case 'POSITION':
          positions = document.readVec3(index);
        case 'NORMAL':
          normals = document.readVec3(index);
        case 'TANGENT':
          tangents = Float32List.sublistView(document.accessorBytes(index));
        default:
          raw[key] = document.accessorBytes(index);
      }
    }
    final indexed = primitive['indices'];
    indices = indexed is int
        ? document.readIndices(indexed)
        : Uint32List.fromList([
            for (var i = 0; i < positions.length ~/ 3; i++) i,
          ]);
  }

  final Map<String, Object?> primitive;
  late final Float32List positions;
  Float32List? normals;
  Float32List? tangents;
  final Map<String, Uint8List> raw = {};
  late final Uint32List indices;

  int get vertices => positions.length ~/ 3;
}

/// Primitives that share everything that makes a draw, filled one placement
/// at a time and closed before the vertex count would widen the indices.
final class _Batch {
  _Batch(this._document, this._maxVertices);

  final GltfDocument _document;
  final int _maxVertices;
  final List<Map<String, Object?>> _done = [];

  Map<String, Object?>? _template;
  final Map<String, BytesBuilder> _bytes = {};
  final List<int> _indices = [];
  int _vertices = 0;
  final List<double> _low = [];
  final List<double> _high = [];

  void add(_Source source, Matrix4 matrix) {
    if (_vertices > 0 && _vertices + source.vertices > _maxVertices) _close();
    _template ??= source.primitive;
    final storage = matrix.storage;
    final positions = _points(source.positions, storage);
    _bounds(positions);
    _bytes.putIfAbsent('POSITION', BytesBuilder.new).add(_view(positions));
    // The renderer turns a tangent with the normal matrix and leaves its sign
    // alone, mirrored or not. Doing the same keeps a baked group looking like
    // the objects it replaced.
    final turn = Matrix3.zero()..copyNormalMatrix(matrix);
    final normals = source.normals;
    if (normals != null) {
      _bytes
          .putIfAbsent('NORMAL', BytesBuilder.new)
          .add(_view(_directions(normals, 3, turn)));
    }
    final tangents = source.tangents;
    if (tangents != null) {
      _bytes
          .putIfAbsent('TANGENT', BytesBuilder.new)
          .add(_view(_directions(tangents, 4, turn)));
    }
    for (final MapEntry(:key, :value) in source.raw.entries) {
      _bytes.putIfAbsent(key, BytesBuilder.new).add(value);
    }
    _addIndices(source.indices, mirrored: matrix.determinant() < 0);
    _vertices += source.vertices;
  }

  List<Map<String, Object?>> finish() {
    _close();
    return _done;
  }

  void _addIndices(Uint32List indices, {required bool mirrored}) {
    for (var i = 0; i + 2 < indices.length; i += 3) {
      _indices
        ..add(_vertices + indices[i])
        ..add(_vertices + indices[mirrored ? i + 2 : i + 1])
        ..add(_vertices + indices[mirrored ? i + 1 : i + 2]);
    }
  }

  void _bounds(Float32List positions) {
    if (_low.isEmpty) {
      _low.addAll(const [double.infinity, double.infinity, double.infinity]);
      _high.addAll(const [
        -double.infinity,
        -double.infinity,
        -double.infinity,
      ]);
    }
    for (var i = 0; i < positions.length; i += 3) {
      for (var c = 0; c < 3; c++) {
        final value = positions[i + c];
        if (value < _low[c]) _low[c] = value;
        if (value > _high[c]) _high[c] = value;
      }
    }
  }

  void _close() {
    final template = _template;
    if (template == null || _vertices == 0) return;
    final names = template['attributes']! as Map<String, Object?>;
    final attributes = <String, Object?>{
      for (final MapEntry(:key, :value) in names.entries)
        key: _document.addLike(
          value! as int,
          _bytes[key]!.takeBytes(),
          _vertices,
          min: key == 'POSITION' ? List.of(_low) : null,
          max: key == 'POSITION' ? List.of(_high) : null,
        ),
    };
    _done.add({
      'attributes': attributes,
      'indices': _document.addIndices(_indices),
      if (template['material'] != null) 'material': template['material'],
      if (template['extras'] != null) 'extras': template['extras'],
    });
    _bytes.clear();
    _indices.clear();
    _low.clear();
    _high.clear();
    _vertices = 0;
  }
}

Uint8List _view(Float32List values) => Uint8List.sublistView(values);

Float32List _points(Float32List from, Float64List m) {
  final out = Float32List(from.length);
  for (var i = 0; i < from.length; i += 3) {
    final x = from[i];
    final y = from[i + 1];
    final z = from[i + 2];
    out[i] = m[0] * x + m[4] * y + m[8] * z + m[12];
    out[i + 1] = m[1] * x + m[5] * y + m[9] * z + m[13];
    out[i + 2] = m[2] * x + m[6] * y + m[10] * z + m[14];
  }
  return out;
}

/// Unit directions turned by [m]. An element is [stride] floats; the first
/// three are the direction, and a fourth is a sign that is left as it is.
Float32List _directions(Float32List from, int stride, Matrix3 m) {
  final s = m.storage;
  final out = Float32List(from.length);
  for (var i = 0; i < from.length; i += stride) {
    final x = from[i];
    final y = from[i + 1];
    final z = from[i + 2];
    final a = s[0] * x + s[3] * y + s[6] * z;
    final b = s[1] * x + s[4] * y + s[7] * z;
    final c = s[2] * x + s[5] * y + s[8] * z;
    final length = Vector3(a, b, c).length;
    final scale = length == 0 ? 1.0 : 1 / length;
    out[i] = a * scale;
    out[i + 1] = b * scale;
    out[i + 2] = c * scale;
    if (stride == 4) out[i + 3] = from[i + 3];
  }
  return out;
}
