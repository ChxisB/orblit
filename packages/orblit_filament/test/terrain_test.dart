import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_filament/src/terrain.dart' show OrblitTerrainHeld;
import 'package:vector_math/vector_math_64.dart';

/// Terrain as it crosses to the renderer: three arrays read in step, and
/// which of a terrain's pictures and regions travel at all.
void main() {
  OrblitScene sceneWith(List<OrblitTerrain> terrain) => OrblitScene(
    objects: const [],
    terrain: terrain,
    camera: OrblitCamera(position: Vector3(0, 10, 10), target: Vector3.zero()),
  );

  OrblitTerrainRegion region(int x, int z, {int revision = 1, int size = 16}) {
    final texels = size * size;
    return OrblitTerrainRegion(
      x: x,
      z: z,
      heights: Float32List(texels)..[0] = 2.5,
      cover: Uint32List(texels)..[0] = 0xF8000001,
      colour: Uint8List(texels * 4)..fillRange(0, 4, 200),
      revision: revision,
    );
  }

  Uint8List picture(int size, int value) =>
      Uint8List(size * size * 4)..fillRange(0, size * size * 4, value);

  OrblitTerrain ground({
    int key = 3,
    List<OrblitTerrainRegion>? regions,
    int regionSize = 16,
    List<OrblitTerrainSet>? sets,
    int picturesRevision = 0,
  }) => OrblitTerrain(
    key: key,
    regions: regions ?? [region(0, 0), region(-1, 2)],
    regionSize: regionSize,
    spacing: 0.5,
    meshSize: 32,
    levels: 4,
    autoSteep: 2,
    autoFlat: 5,
    sets:
        sets ??
        [
          OrblitTerrainSet(albedo: picture(2, 10), normal: picture(2, 20)),
          const OrblitTerrainSet(tileSize: 8, triplanar: true),
        ],
    picturesRevision: picturesRevision,
    castShadows: false,
  );

  group('the message', () {
    test('a scene with no terrain sends none', () {
      final message = sceneWith(const []).toMessage(1);
      expect(message.keys.where((k) => k.startsWith('terrain')), isEmpty);
    });

    test('is laid out as the renderer reads it, and used up exactly', () {
      final read = _Reader(sceneWith([ground()]).toMessage(1));
      expect(read.ints(1), [1]);
      expect(read.ints(OrblitTerrain.headerInts), [
        3, // key
        2, // receives shadows, casts none
        16, 32, 4, // region size, mesh size, levels
        2, 5, // automatic steep and flat
        2, 2, 1, // sets, their size, and the pictures came
        0x2, // the second set is triplanar
        2, // regions
      ]);
      expect(
        read.floats(OrblitTerrain.stride),
        [0.5, 0.87, 1, 0.1].map(_f32).toList(),
      );
      expect(read.floats(2), [4, 8]);
      expect(read.ints(2 * OrblitTerrain.regionInts), [0, 0, 1, -1, 2, 1]);

      // Albedos, then normals; a set with none is the plain pixel.
      final layer = 2 * 2 * 4;
      expect(read.bytes(layer), everyElement(10));
      expect(read.bytes(layer), [
        for (var i = 0; i < 4; i++) ...[190, 190, 190, 128],
      ]);
      expect(read.bytes(layer), everyElement(20));
      expect(read.bytes(layer), [
        for (var i = 0; i < 4; i++) ...[128, 128, 255, 230],
      ]);

      for (var r = 0; r < 2; r++) {
        final maps = read.bytes(16 * 16 * 12);
        final view = ByteData.sublistView(maps);
        expect(view.getFloat32(0, Endian.little), 2.5);
        expect(view.getUint32(16 * 16 * 4, Endian.little), 0xF8000001);
        expect(maps.sublist(16 * 16 * 8, 16 * 16 * 8 + 5), [
          200,
          200,
          200,
          200,
          0,
        ]);
      }
      read.finished();
    });

    test('a terrain the renderer holds sends its settings only', () {
      final terrain = ground();
      final message = sceneWith([
        terrain,
      ]).toMessage(1, sentTerrain: {3: OrblitTerrainHeld.of(terrain)});
      final read = _Reader(message);
      read.ints(1);
      expect(read.ints(OrblitTerrain.headerInts)[9], 0);
      read.floats(OrblitTerrain.stride + 2);
      expect(read.ints(2 * OrblitTerrain.regionInts), [0, 0, 0, -1, 2, 0]);
      read.finished();
      expect(message['terrainData'], isEmpty);
    });

    test('a region whose revision moved is the only one sent', () {
      final before = ground();
      final after = ground(regions: [region(0, 0), region(-1, 2, revision: 2)]);
      final read = _Reader(
        sceneWith([
          after,
        ]).toMessage(1, sentTerrain: {3: OrblitTerrainHeld.of(before)}),
      );
      read.ints(1 + OrblitTerrain.headerInts);
      read.floats(OrblitTerrain.stride + 2);
      expect(read.ints(2 * OrblitTerrain.regionInts), [0, 0, 0, -1, 2, 1]);
      read.bytes(16 * 16 * 12);
      read.finished();
    });

    test('a new region size sends every region again', () {
      final before = ground();
      final after = ground(
        regionSize: 32,
        regions: [region(0, 0, size: 32), region(-1, 2, size: 32)],
      );
      final message = sceneWith([
        after,
      ]).toMessage(1, sentTerrain: {3: OrblitTerrainHeld.of(before)});
      expect((message['terrainData']! as Uint8List).length, 2 * 32 * 32 * 12);
    });

    test('pictures travel again when their revision, count or size moves', () {
      final held = OrblitTerrainHeld.of(ground());
      bool sends(OrblitTerrain terrain) {
        final message = sceneWith([
          terrain,
        ]).toMessage(1, sentTerrain: {3: held});
        return (message['terrainInts']! as Int32List)[1 + 9] == 1;
      }

      expect(sends(ground()), isFalse);
      expect(sends(ground(picturesRevision: 1)), isTrue);
      expect(sends(ground(sets: const [OrblitTerrainSet()])), isTrue);
      expect(
        sends(ground(sets: [OrblitTerrainSet(albedo: picture(4, 1))])),
        isTrue,
      );
    });

    test('a terrain named twice is a mistake', () {
      expect(
        () => sceneWith([ground(), ground()]).toMessage(1),
        throwsA(isA<AssertionError>()),
      );
    });
  });

  group('what the renderer would refuse is refused here', () {
    test('a region size it cannot draw', () {
      for (final size in [8, 24, 4096]) {
        expect(
          () => OrblitTerrain(key: 1, regionSize: size),
          throwsArgumentError,
          reason: '$size',
        );
      }
    });

    test('a mesh, a level count or an automatic set out of range', () {
      expect(() => OrblitTerrain(key: 1, meshSize: 33), throwsArgumentError);
      expect(() => OrblitTerrain(key: 1, levels: 0), throwsArgumentError);
      expect(() => OrblitTerrain(key: 1, autoFlat: 32), throwsArgumentError);
    });

    test('pictures of different sizes', () {
      expect(
        () => OrblitTerrain(
          key: 1,
          sets: [
            OrblitTerrainSet(albedo: picture(4, 0)),
            OrblitTerrainSet(normal: picture(2, 0)),
          ],
        ),
        throwsArgumentError,
      );
    });

    test('a region the wrong size, or one named twice', () {
      expect(
        () => OrblitTerrain(key: 1, regionSize: 32, regions: [region(0, 0)]),
        throwsArgumentError,
      );
      expect(
        () => OrblitTerrain(
          key: 1,
          regionSize: 16,
          regions: [region(0, 0), region(0, 0)],
        ),
        throwsArgumentError,
      );
    });

    test('regions further apart than the renderer can index', () {
      expect(
        () => OrblitTerrain(
          key: 1,
          regionSize: 16,
          regions: [region(-64, 0), region(64, 0)],
        ),
        throwsArgumentError,
      );
      expect(
        OrblitTerrain(
          key: 1,
          regionSize: 16,
          regions: [region(0, -63), region(0, 64)],
        ).regions,
        hasLength(2),
      );
    });

    test('maps that are not a square of texels', () {
      expect(
        () => OrblitTerrainRegion(
          x: 0,
          z: 0,
          heights: Float32List(20),
          cover: Uint32List(20),
          colour: Uint8List(80),
        ),
        throwsArgumentError,
      );
    });
  });
}

double _f32(num value) => (Float32List(1)..[0] = value.toDouble())[0];

/// Reads a terrain message part by part, the way the renderer does, and
/// fails unless every part is there and nothing is left over.
class _Reader {
  _Reader(Map<String, Object> message)
    : _ints = message['terrainInts']! as Int32List,
      _floats = message['terrainFloats']! as Float32List,
      _data = message['terrainData']! as Uint8List;

  final Int32List _ints;
  final Float32List _floats;
  final Uint8List _data;
  var _intAt = 0;
  var _floatAt = 0;
  var _dataAt = 0;

  List<int> ints(int count) => _ints.sublist(_intAt, _intAt += count).toList();

  List<double> floats(int count) =>
      _floats.sublist(_floatAt, _floatAt += count).toList();

  Uint8List bytes(int count) => _data.sublist(_dataAt, _dataAt += count);

  void finished() {
    expect(_intAt, _ints.length, reason: 'whole numbers left over');
    expect(_floatAt, _floats.length, reason: 'floats left over');
    expect(_dataAt, _data.length, reason: 'bytes left over');
  }
}
