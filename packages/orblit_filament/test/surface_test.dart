import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  OrblitScene sceneOf(List<OrblitMaterial> materials) => OrblitScene(
    objects: const [],
    materials: materials,
    camera: OrblitCamera(position: Vector3(0, 2, 6), target: Vector3.zero()),
  );

  const picture = OrblitTexture('/art/noise.png', srgb: false);

  test('a surface sets the custom flag and a plain material does not', () {
    final worn = OrblitMaterial(
      key: 1,
      surface: OrblitSurface(package: '/cook/pulse.filamat'),
    );
    expect(worn.flags & (1 << 15), isNonZero);
    expect(const OrblitMaterial(key: 2).flags & (1 << 15), 0);
    expect(worn.copyWith(roughness: 0.1).surface, isNotNull);
  });

  test('the message lays surfaces out as the C entry point reads them', () {
    final message = sceneOf([
      const OrblitMaterial(key: 7),
      OrblitMaterial(
        key: 8,
        surface: OrblitSurface(
          package: '/cook/pulse.filamat',
          values: {
            'speed': [2.5],
            'tint': [1, 0.5, 0.25, 1],
          },
          textures: {'noise': picture},
        ),
      ),
      OrblitMaterial(
        key: 9,
        surface: OrblitSurface(
          package: '/cook/pulse.filamat',
          values: {
            'speed': [4],
          },
          textures: {'noise': picture},
        ),
      ),
    ]).toMessage(0);

    expect(message['surfaceKeys'], [8, 9]);
    expect(message['surfacePackages'], [
      '/cook/pulse.filamat',
      '/cook/pulse.filamat',
    ]);
    expect(message['surfaceEntryCounts'], Int32List.fromList([3, 2]));
    expect(message['surfaceNames'], [
      'speed',
      'tint',
      'noise',
      'speed',
      'noise',
    ]);
    expect(message['surfaceWidths'], Int32List.fromList([1, 4, 0, 1, 0]));
    // The same picture is sent once and pointed at twice.
    expect(message['surfaceMaps'], Int32List.fromList([-1, -1, 0, -1, 0]));
    expect(message['surfaceTexturePaths'], ['/art/noise.png']);
    expect(message['surfaceTextureSrgb'], Int32List.fromList([0]));
    expect(
      message['surfaceValues'],
      Float32List.fromList([2.5, 1, 0.5, 0.25, 1, 4]),
    );
    expect((message['materialFlags']! as Int32List).map((f) => f & (1 << 15)), [
      0,
      1 << 15,
      1 << 15,
    ]);
  });

  test('a scene with no surface sends nothing about them', () {
    final message = sceneOf([const OrblitMaterial(key: 1)]).toMessage(0);
    expect(message.keys.where((k) => k.startsWith('surface')), isEmpty);
  });

  test('a surface refuses a value that no shader could take', () {
    expect(
      () => OrblitSurface(package: 'p', values: {'a': <double>[]}),
      throwsArgumentError,
    );
    expect(
      () => OrblitSurface(
        package: 'p',
        values: {
          'a': [1, 2, 3, 4, 5],
        },
      ),
      throwsArgumentError,
    );
    expect(
      () => OrblitSurface(
        package: 'p',
        values: {
          'a': [1],
        },
        textures: {'a': picture},
      ),
      throwsArgumentError,
    );
  });
}
