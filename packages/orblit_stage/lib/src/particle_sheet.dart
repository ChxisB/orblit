import 'dart:typed_data';

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:vector_math/vector_math_64.dart';

/// A grid of particles drawn as a sheet, for a cloth that something other than
/// the renderer moves.
///
/// The renderer has no way to take new vertex positions each frame, and a
/// skin is the one thing it takes new every frame. So the sheet has one vertex
/// for every particle and one joint for every vertex. Each joint is set to
/// where its particle is, and the vertex goes with it.
///
/// Made at the origin, in a layout of its own: columns along x, rows down −y,
/// and the face toward +z. [poses] is in that same space, so a cloth that
/// hangs somewhere else is drawn by moving the object with a transform and
/// handing [poses] positions taken from that point. That keeps the renderer's
/// bounds, which come from the sheet at rest, near the cloth.
///
/// Particle `row * columns + column` is the vertex and the joint of the same
/// number, which is how `SoftBody.cloth` numbers its particles.
final class OrblitParticleSheet {
  OrblitParticleSheet({
    required this.columns,
    required this.rows,
    required this.spacing,
    this.colour = const [0.8, 0.8, 0.8],
  }) {
    if (columns < 2 || rows < 2) {
      throw ArgumentError('A sheet needs at least 2 by 2 particles');
    }
    if (columns * rows > maxParticles) {
      throw ArgumentError(
        '$columns by $rows is ${columns * rows} particles, and a skin holds '
        '$maxParticles joints',
      );
    }
    if (!(spacing > 0 && spacing.isFinite)) {
      throw ArgumentError.value(spacing, 'spacing', 'must be above zero');
    }
  }

  /// The most joints a renderer skin has. Past it a sheet would need a skin
  /// for each part, which nothing here does.
  static const maxParticles = 256;

  final int columns;
  final int rows;

  /// The distance between neighbouring particles at rest, in metres.
  final double spacing;

  /// The colour of the cloth, linear, as red, green and blue.
  final List<double> colour;

  int get count => columns * rows;

  /// The sheet as a binary glTF, to hand to `OrblitResources.provide`.
  ///
  /// Both faces are drawn, since a flag has two sides.
  Uint8List toGlb() {
    final buffer = GltfBuffer();
    final position = buffer.addPositions(_rest());
    final normal = buffer.addFloats(
      Float32List.fromList([
        for (var i = 0; i < count; i++) ...const [0.0, 0.0, 1.0],
      ]),
      'VEC3',
      target: GltfTarget.arrayBuffer,
    );
    final uv = buffer.addFloats(_uvs(), 'VEC2', target: GltfTarget.arrayBuffer);
    final joints = buffer.addAccessor(
      view: buffer.addView(_jointIds(), target: GltfTarget.arrayBuffer),
      componentType: GltfComponent.unsignedByte,
      count: count,
      type: 'VEC4',
    );
    final weights = buffer.addFloats(
      Float32List.fromList([
        for (var i = 0; i < count; i++) ...const [1.0, 0.0, 0.0, 0.0],
      ]),
      'VEC4',
      target: GltfTarget.arrayBuffer,
    );
    final bind = buffer.addFloats(_inverseBinds(), 'MAT4');
    final indices = buffer.addIndices(_triangles());

    final json = <String, Object?>{
      'asset': {'version': '2.0', 'generator': 'Orblit particle sheet'},
      'scene': 0,
      'scenes': [
        {
          'nodes': [for (var i = 0; i <= count; i++) i],
        },
      ],
      'nodes': [
        {'mesh': 0, 'skin': 0},
        for (var i = 0; i < count; i++)
          {'translation': _restAt(i).storage.toList()},
      ],
      'skins': [
        {
          'joints': [for (var i = 1; i <= count; i++) i],
          'inverseBindMatrices': bind,
        },
      ],
      'meshes': [
        {
          'primitives': [
            {
              'attributes': {
                'POSITION': position,
                'NORMAL': normal,
                'TEXCOORD_0': uv,
                'JOINTS_0': joints,
                'WEIGHTS_0': weights,
              },
              'indices': indices,
              'material': 0,
            },
          ],
        },
      ],
      'materials': [
        {
          'doubleSided': true,
          'pbrMetallicRoughness': {
            'baseColorFactor': [...colour, 1.0],
            'metallicFactor': 0.0,
            'roughnessFactor': 0.85,
          },
        },
      ],
      'accessors': buffer.accessors,
      'bufferViews': buffer.views,
      'buffers': buffer.buffers,
    };
    return glbBytes(json, buffer.bytes);
  }

  /// Every joint set to its particle, for `OrblitObject.joints`.
  ///
  /// [at] gives where particle `i` is. A joint is moved to the particle and
  /// turned to follow the cloth there: its rows are the way the columns run,
  /// the way the rows run, and the way the face points, found from the
  /// neighbours on each side. The turn does not change where the vertex is.
  /// It is what the light sees, because the sheet's normals are turned with
  /// the joints.
  List<OrblitJointPose> poses(Vector3 Function(int particle) at) {
    final here = [for (var i = 0; i < count; i++) at(i)];
    return [
      for (var i = 0; i < count; i++)
        OrblitJointPose(skin: 0, joint: i, transform: _pose(here, i)),
    ];
  }

  Matrix4 _pose(List<Vector3> here, int i) {
    final row = i ~/ columns;
    final column = i % columns;
    final across = _span(here, row, column, 1, 0);
    final down = _span(here, row, column, 0, 1);

    final face = down.cross(across);
    // Where the neighbours are on a line, or on top of each other, there is no
    // face to find. The sheet's own turn is kept rather than a made-up one.
    if (face.length2 < 1e-18) {
      return Matrix4.translation(here[i]);
    }
    face.normalize();
    final along = across.normalized();
    final up = face.cross(along);
    return Matrix4.columns(
      Vector4(along.x, along.y, along.z, 0),
      Vector4(up.x, up.y, up.z, 0),
      Vector4(face.x, face.y, face.z, 0),
      Vector4(here[i].x, here[i].y, here[i].z, 1),
    );
  }

  /// The direction the cloth runs one way at ([row], [column]): from the
  /// neighbour before to the neighbour after, or from the particle itself
  /// at an edge.
  Vector3 _span(List<Vector3> here, int row, int column, int dx, int dy) {
    final before = (row - dy < 0 || column - dx < 0)
        ? (row * columns + column)
        : (row - dy) * columns + column - dx;
    final after = (row + dy >= rows || column + dx >= columns)
        ? (row * columns + column)
        : (row + dy) * columns + column + dx;
    return here[after] - here[before];
  }

  Vector3 _restAt(int i) =>
      Vector3((i % columns) * spacing, -(i ~/ columns) * spacing, 0);

  Float32List _rest() {
    final out = Float32List(count * 3);
    for (var i = 0; i < count; i++) {
      final at = _restAt(i);
      out[i * 3] = at.x;
      out[i * 3 + 1] = at.y;
      out[i * 3 + 2] = at.z;
    }
    return out;
  }

  Float32List _uvs() {
    final out = Float32List(count * 2);
    for (var i = 0; i < count; i++) {
      out[i * 2] = (i % columns) / (columns - 1);
      out[i * 2 + 1] = (i ~/ columns) / (rows - 1);
    }
    return out;
  }

  /// Each vertex names its own joint, in the first of four slots.
  Uint8List _jointIds() {
    final out = Uint8List(count * 4);
    for (var i = 0; i < count; i++) {
      out[i * 4] = i;
    }
    return out;
  }

  /// The inverse of each joint's rest transform, which is only a move back
  /// from where the joint sits.
  Float32List _inverseBinds() {
    final out = Float32List(count * 16);
    for (var i = 0; i < count; i++) {
      final at = _restAt(i);
      out
        ..[i * 16] = 1
        ..[i * 16 + 5] = 1
        ..[i * 16 + 10] = 1
        ..[i * 16 + 12] = -at.x
        ..[i * 16 + 13] = -at.y
        ..[i * 16 + 15] = 1;
    }
    return out;
  }

  /// Two triangles to a cell, wound to face +z at rest.
  List<int> _triangles() {
    final out = <int>[];
    for (var row = 0; row < rows - 1; row++) {
      for (var column = 0; column < columns - 1; column++) {
        final a = row * columns + column;
        final b = a + 1;
        final c = a + columns;
        final d = c + 1;
        out.addAll([a, c, b, b, c, d]);
      }
    }
    return out;
  }
}
