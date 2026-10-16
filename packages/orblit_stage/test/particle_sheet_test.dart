import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

const _columns = 4;
const _rows = 3;
const _spacing = 0.25;

OrblitParticleSheet _sheet({int columns = _columns, int rows = _rows}) =>
    OrblitParticleSheet(columns: columns, rows: rows, spacing: _spacing);

/// Where particle [i] sits when the sheet is as it was made.
Vector3 _rest(int i) =>
    Vector3((i % _columns) * _spacing, -(i ~/ _columns) * _spacing, 0);

/// What the renderer does to a vertex: the joint's world transform, then the
/// inverse bind, applied to the vertex at rest.
Vector3 _skinned(List<OrblitJointPose> poses, int i) {
  final bind = Matrix4.translation(-_rest(i));
  return poses[i].transform.multiplied(bind).transformed3(_rest(i));
}

void _expectVector(Vector3 actual, Vector3 expected) {
  expect(actual.x, closeTo(expected.x, 1e-9), reason: '$actual vs $expected');
  expect(actual.y, closeTo(expected.y, 1e-9), reason: '$actual vs $expected');
  expect(actual.z, closeTo(expected.z, 1e-9), reason: '$actual vs $expected');
}

void main() {
  group('the glb', () {
    final parts = gltfParts(_sheet().toGlb());
    final json = parts.json;
    final read = GltfAccessors(json, parts.binary);
    final primitive =
        ((json['meshes'] as List).first as Map)['primitives'] as List;
    final attributes = (primitive.first as Map)['attributes'] as Map;

    test('has a vertex and a joint to a particle', () {
      expect(read.count(attributes['POSITION'] as int), _columns * _rows);
      final skin = (json['skins'] as List).first as Map;
      expect((skin['joints'] as List).length, _columns * _rows);
      // The mesh node, then the joints.
      expect((json['nodes'] as List).length, 1 + _columns * _rows);
    });

    test('lays the vertices out as the particles are numbered', () {
      final positions = read.floats(attributes['POSITION'] as int, 3);
      for (var i = 0; i < _columns * _rows; i++) {
        _expectVector(
          Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]),
          _rest(i),
        );
      }
    });

    test('puts every vertex wholly on its own joint', () {
      final joints = read.floats(attributes['JOINTS_0'] as int, 4);
      final weights = read.floats(attributes['WEIGHTS_0'] as int, 4);
      for (var i = 0; i < _columns * _rows; i++) {
        expect(joints.sublist(i * 4, i * 4 + 4), [i, 0, 0, 0]);
        expect(weights.sublist(i * 4, i * 4 + 4), [1, 0, 0, 0]);
      }
    });

    test('binds each joint by moving back from where it sits', () {
      final skin = (json['skins'] as List).first as Map;
      final bind = read.floats(skin['inverseBindMatrices'] as int, 16);
      for (var i = 0; i < _columns * _rows; i++) {
        final matrix = Matrix4.fromList(bind.sublist(i * 16, i * 16 + 16));
        _expectVector(matrix.transformed3(_rest(i)), Vector3.zero());
      }
    });

    test('sets each joint node where its vertex is', () {
      final nodes = json['nodes'] as List;
      for (var i = 0; i < _columns * _rows; i++) {
        final translation = ((nodes[i + 1] as Map)['translation'] as List)
            .map((one) => (one as num).toDouble())
            .toList();
        _expectVector(
          Vector3(translation[0], translation[1], translation[2]),
          _rest(i),
        );
      }
    });

    test('winds every triangle to face +z at rest', () {
      final positions = read.floats(attributes['POSITION'] as int, 3);
      final indices = read.indices((primitive.first as Map)['indices'] as int);
      expect(indices.length, (_columns - 1) * (_rows - 1) * 6);
      Vector3 at(int i) =>
          Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
      for (var t = 0; t < indices.length; t += 3) {
        final a = at(indices[t]);
        final face = (at(indices[t + 1]) - a).cross(at(indices[t + 2]) - a);
        expect(face.z, greaterThan(0));
      }
    });

    test('draws both faces, in the colour it was given', () {
      final material = (json['materials'] as List).first as Map;
      expect(material['doubleSided'], isTrue);
      final red = OrblitParticleSheet(
        columns: 2,
        rows: 2,
        spacing: 1,
        colour: const [0.9, 0.1, 0.1],
      );
      final painted = gltfParts(red.toGlb()).json['materials'] as List;
      final factor =
          ((painted.first as Map)['pbrMetallicRoughness']
              as Map)['baseColorFactor'];
      expect(factor, [0.9, 0.1, 0.1, 1]);
    });
  });

  group('poses', () {
    test('are a plain move to the particle while the sheet is as made', () {
      final sheet = _sheet();
      final poses = sheet.poses(_rest);
      expect(poses.length, _columns * _rows);
      for (var i = 0; i < poses.length; i++) {
        expect(poses[i].skin, 0);
        expect(poses[i].joint, i);
        final expected = Matrix4.translation(_rest(i));
        for (var e = 0; e < 16; e++) {
          expect(
            poses[i].transform.storage[e],
            closeTo(expected.storage[e], 1e-12),
          );
        }
      }
    });

    test('put every vertex on its particle however the cloth lies', () {
      final sheet = _sheet();
      // Part of a turn, not a quarter of one: a quarter looks the same turned
      // either way.
      final lie = Matrix4.translation(Vector3(1, 2, 3))..rotateY(0.5);
      Vector3 where(int i) => lie.transformed3(_rest(i));
      final poses = sheet.poses(where);
      for (var i = 0; i < poses.length; i++) {
        _expectVector(_skinned(poses, i), where(i));
      }
    });

    test('turn a cloth that lies turned by that turn', () {
      final sheet = _sheet();
      final turn = Matrix4.rotationY(0.5);
      final poses = sheet.poses((i) => turn.transformed3(_rest(i)));
      for (final pose in poses) {
        final got = pose.transform.getRotation();
        final want = turn.getRotation();
        for (var e = 0; e < 9; e++) {
          expect(got.storage[e], closeTo(want.storage[e], 1e-9));
        }
      }
    });

    test('stay a rigid turn where the cloth is folded and stretched', () {
      final sheet = _sheet();
      // A crease down the middle, one side lifted, and the rows sheared.
      Vector3 folded(int i) {
        final at = _rest(i);
        final lift = at.x > 0.3 ? (at.x - 0.3) * 2 : 0.0;
        return Vector3(at.x * 1.3 + at.y * 0.4, at.y, at.z + lift);
      }

      for (final pose in sheet.poses(folded)) {
        final turn = pose.transform.getRotation();
        expect(turn.determinant(), closeTo(1, 1e-9));
        final shouldBeIdentity = turn.transposed().multiplied(turn);
        for (var e = 0; e < 9; e++) {
          expect(
            shouldBeIdentity.storage[e],
            closeTo(Matrix3.identity().storage[e], 1e-9),
          );
        }
      }
    });

    test('tilt the face where the cloth is tilted', () {
      final sheet = _sheet();
      // Each row 0.1 nearer than the one above it, over rows 0.25 apart. The
      // face looks up as well as out, in that ratio.
      final poses = sheet.poses((i) => _rest(i)..z += (i ~/ _columns) * 0.1);
      final normal = poses[_columns + 1].transform.getRotation().transformed(
        Vector3(0, 0, 1),
      );
      expect(normal.y / normal.z, closeTo(0.1 / 0.25, 1e-9));
    });

    test('keep the move and drop the turn where the cloth has no face', () {
      final sheet = _sheet();
      final at = Vector3(1, 2, 3);
      for (final pose in sheet.poses((_) => at)) {
        final expected = Matrix4.translation(at);
        for (var e = 0; e < 16; e++) {
          expect(
            pose.transform.storage[e],
            closeTo(expected.storage[e], 1e-12),
          );
        }
      }
    });
  });

  group('a sheet', () {
    test('is refused when it has no cells, too many joints or no size', () {
      expect(() => _sheet(columns: 1), throwsArgumentError);
      expect(() => _sheet(rows: 1), throwsArgumentError);
      expect(
        () => OrblitParticleSheet(columns: 17, rows: 16, spacing: 0.1),
        throwsArgumentError,
      );
      expect(
        () => OrblitParticleSheet(columns: 16, rows: 16, spacing: 0.1),
        returnsNormally,
      );
      expect(
        () => OrblitParticleSheet(columns: 2, rows: 2, spacing: 0),
        throwsArgumentError,
      );
      expect(
        () => OrblitParticleSheet(columns: 2, rows: 2, spacing: double.nan),
        throwsArgumentError,
      );
    });
  });
}
