import 'dart:convert';
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'asset_id.dart';
import 'asset_source.dart';
import 'gltf/accessor.dart';
import 'gltf/document.dart';

/// Static model geometry in the model's frame, independent of its materials.
final class CollisionMesh {
  CollisionMesh({required List<double> vertices, required List<int> indices})
    : vertices = List.unmodifiable(vertices),
      indices = List.unmodifiable(indices) {
    if (vertices.length < 9 ||
        vertices.length % 3 != 0 ||
        vertices.any((v) => !v.isFinite) ||
        indices.isEmpty ||
        indices.length % 3 != 0 ||
        indices.any((i) => i < 0 || i >= vertices.length ~/ 3)) {
      throw ArgumentError('Expected finite positions and indexed triangles.');
    }
  }

  final List<double> vertices;
  final List<int> indices;

  Map<String, Object?> toJson() => {
    'version': 1,
    'vertices': vertices,
    'indices': indices,
  };

  Uint8List encode() => Uint8List.fromList(utf8.encode(jsonEncode(toJson())));

  static CollisionMesh decode(List<int> bytes) {
    final Object? json = jsonDecode(utf8.decode(bytes));
    if (json is! Map<String, Object?> ||
        json['version'] != 1 ||
        json['vertices'] is! List ||
        json['indices'] is! List) {
      throw const FormatException('Expected collision mesh version 1.');
    }
    final positions = json['vertices'] as List;
    final triangles = json['indices'] as List;
    if (positions.any((v) => v is! num) || triangles.any((v) => v is! int)) {
      throw const FormatException('Invalid collision mesh geometry.');
    }
    return CollisionMesh(
      vertices: [for (final v in positions) (v as num).toDouble()],
      indices: triangles.cast<int>(),
    );
  }

  /// Reads the active scene with node transforms, strips and fans baked in.
  /// Skins and morph targets need a posed collider and are refused here.
  static Future<CollisionMesh> fromGltf(
    AssetId id,
    Uint8List bytes,
    AssetSource source,
  ) async {
    final document = await GltfDocument.read(id, bytes, source);
    return _ModelGeometry(document).read();
  }
}

final class _ModelGeometry {
  _ModelGeometry(this.document);

  final GltfDocument document;
  final List<double> vertices = [];
  final List<int> indices = [];
  final Set<int> visiting = {};

  CollisionMesh read() {
    _validate();
    final nodes = document.list('nodes');
    final scenes = document.list('scenes');
    if (scenes.isNotEmpty) {
      final selected = _index(document.json['scene']) ?? 0;
      if (selected < 0 || selected >= scenes.length) {
        throw const FormatException('Invalid scene.');
      }
      for (final node in _integers(scenes[selected]['nodes'])) {
        _node(node, Matrix4.identity());
      }
    } else if (nodes.isNotEmpty) {
      final children = {
        for (final node in nodes) ..._integers(node['children']),
      };
      for (var i = 0; i < nodes.length; i++) {
        if (!children.contains(i)) _node(i, Matrix4.identity());
      }
    } else {
      for (var i = 0; i < document.list('meshes').length; i++) {
        _mesh(i, Matrix4.identity());
      }
    }
    return CollisionMesh(vertices: vertices, indices: indices);
  }

  void _validate() {
    for (final key in ['nodes', 'scenes', 'meshes', 'bufferViews']) {
      final list = document.json[key];
      if (list != null &&
          (list is! List || list.any((v) => v is! Map<String, Object?>))) {
        throw FormatException('Invalid model $key.');
      }
    }
    for (final view in document.list('bufferViews')) {
      if (_extension(view, 'EXT_meshopt_compression') ||
          _extension(view, 'KHR_meshopt_compression')) {
        throw const FormatException(
          'Compressed collision geometry is unsupported.',
        );
      }
    }
  }

  static bool _extension(Map<String, Object?> value, String name) =>
      value['extensions'] is Map &&
      (value['extensions'] as Map).containsKey(name);

  void _node(int index, Matrix4 parent) {
    final nodes = document.list('nodes');
    if (index < 0 || index >= nodes.length || !visiting.add(index)) {
      throw const FormatException('Invalid or cyclic model hierarchy.');
    }
    final node = nodes[index];
    if (node.containsKey('skin')) {
      throw const FormatException('Skinned collision geometry is unsupported.');
    }
    final world = parent.clone()..multiply(_transform(node));
    final mesh = _index(node['mesh']);
    if (mesh != null) _mesh(mesh, world);
    for (final child in _integers(node['children'])) {
      _node(child, world);
    }
    visiting.remove(index);
  }

  void _mesh(int index, Matrix4 world) {
    final meshes = document.list('meshes');
    if (index < 0 || index >= meshes.length) {
      throw const FormatException('Invalid mesh.');
    }
    final primitives = meshes[index]['primitives'];
    if (primitives is! List) {
      throw const FormatException('Missing mesh primitives.');
    }
    for (final value in primitives) {
      if (value is! Map<String, Object?>) {
        throw const FormatException('Invalid primitive.');
      }
      _primitive(value, world);
    }
  }

  void _primitive(Map<String, Object?> primitive, Matrix4 world) {
    final mode = _index(primitive['mode']) ?? 4;
    if (mode < 4 || mode > 6) return;
    if (_extension(primitive, 'KHR_draco_mesh_compression')) {
      throw const FormatException(
        'Compressed collision geometry is unsupported.',
      );
    }
    if (primitive.containsKey('targets')) {
      throw const FormatException('Morph collision geometry is unsupported.');
    }
    final attributes = primitive['attributes'];
    if (attributes is! Map<String, Object?> || attributes['POSITION'] is! int) {
      throw const FormatException('Missing collision positions.');
    }
    final points = document.readVec3(attributes['POSITION'] as int);
    final base = vertices.length ~/ 3;
    for (var i = 0; i < points.length; i += 3) {
      final p = world.transformed3(
        Vector3(points[i], points[i + 1], points[i + 2]),
      );
      vertices.addAll(p.storage);
    }
    final accessor = _index(primitive['indices']);
    final from = accessor != null
        ? document.readIndices(accessor)
        : List.generate(points.length ~/ 3, (i) => i);
    if (from.any((i) => i < 0 || i >= points.length ~/ 3)) {
      throw const FormatException('Collision index is outside its primitive.');
    }
    final triangles = _triangles(from, mode);
    final mirrored = world.determinant() < 0;
    for (var i = 0; i < triangles.length; i += 3) {
      indices.addAll([
        base + triangles[i],
        base + triangles[i + (mirrored ? 2 : 1)],
        base + triangles[i + (mirrored ? 1 : 2)],
      ]);
    }
  }

  static List<int> _triangles(List<int> from, int mode) {
    if (mode == 4) {
      if (from.length % 3 != 0) {
        throw const FormatException('Incomplete triangles.');
      }
      return from;
    }
    return [
      for (var i = 2; i < from.length; i++) ...[
        mode == 6 ? from[0] : from[i - (i.isEven ? 2 : 1)],
        from[i - (mode == 6 || i.isEven ? 1 : 2)],
        from[i],
      ],
    ];
  }

  static int? _index(Object? raw) {
    if (raw != null && raw is! int) {
      throw const FormatException('Expected an integer model index.');
    }
    return raw as int?;
  }

  static List<int> _integers(Object? raw) {
    if (raw == null) {
      return const [];
    }
    if (raw is! List || raw.any((v) => v is! int)) {
      throw const FormatException('Expected node indices.');
    }
    return raw.cast<int>();
  }

  static List<double> _numbers(Object? raw, List<double> fallback) {
    if (raw == null) {
      return fallback;
    }
    if (raw is! List ||
        raw.length != fallback.length ||
        raw.any((v) => v is! num || !v.isFinite)) {
      throw const FormatException('Invalid model transform.');
    }
    return [for (final v in raw) (v as num).toDouble()];
  }

  static Matrix4 _transform(Map<String, Object?> node) {
    if (node.containsKey('matrix')) {
      return Matrix4.fromList(
        _numbers(node['matrix'], Matrix4.identity().storage),
      );
    }
    final at = _numbers(node['translation'], [0, 0, 0]);
    final turn = _numbers(node['rotation'], [0, 0, 0, 1]);
    final scale = _numbers(node['scale'], [1, 1, 1]);
    final rotation = Quaternion(turn[0], turn[1], turn[2], turn[3]);
    if (rotation.length2 == 0) {
      throw const FormatException('Zero model rotation.');
    }
    rotation.normalize();
    return Matrix4.compose(Vector3.array(at), rotation, Vector3.array(scale));
  }
}
