// Writes dotnet/fixtures/orblit_filament/scene.json: the arrays the scene
// packer sends for a handful of scenes, as bytes. It needs the Flutter package
// it reads, so tool/parity/flutter/regen.sh copies it into that package's test
// folder, runs it there and removes the copy.
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

OrblitCamera _camera() =>
    OrblitCamera(position: Vector3(0, 2, 8), target: Vector3.zero());

OrblitObject _object(
  int key, {
  String? mesh,
  Matrix4? transform,
  Vector3? colour,
  bool castShadows = true,
  bool receiveShadows = true,
  bool visible = true,
  int layer = 0,
  List<double>? morphWeights,
  OrblitAnimation? animation,
  int? variant,
  List<OrblitJointPose>? joints,
}) => OrblitObject(
  key: key,
  mesh: mesh,
  transform: transform ?? Matrix4.identity(),
  colour: colour ?? Vector3(1, 1, 1),
  castShadows: castShadows,
  receiveShadows: receiveShadows,
  visible: visible,
  layer: layer,
  morphWeights: morphWeights,
  animation: animation,
  variant: variant,
  joints: joints,
);

Map<String, ({OrblitScene scene, double? at})> _scenes() => {
  'objects': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [
        _object(1, mesh: '/m/a.glb'),
        _object(
          2,
          mesh: '/m/a.glb',
          transform:
              Matrix4.translationValues(1, 2, 3) * Matrix4.rotationY(0.7),
          colour: Vector3(0.1, 0.25, 0.9),
          castShadows: false,
          receiveShadows: false,
          layer: 3,
        ),
        _object(
          3,
          colour: Vector3(2, -1, 1 / 3),
          visible: false,
          layer: 99,
          morphWeights: [0.25, 0.5],
        ),
        _object(4, mesh: '/m/b.glb', morphWeights: []),
        _object(5, mesh: '/m/c.glb', receiveShadows: false, morphWeights: [1]),
      ],
    ),
    at: null,
  ),
  'poses': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [
        _object(1, mesh: '/m/1.glb'),
        _object(
          7,
          mesh: '/m/7.glb',
          animation: const OrblitAnimation(
            clip: 2,
            seconds: 1.5,
            speed: 0.5,
            fade: 0.25,
          ),
        ),
        _object(
          3,
          mesh: '/m/3.glb',
          animation: const OrblitAnimation(
            clip: 1,
            seconds: 0.25,
            speed: 2,
            loop: false,
            from: OrblitAnimation(clip: 0, seconds: 3, speed: 1, loop: false),
            fade: 0.5,
          ),
        ),
        _object(2, mesh: '/m/2.glb', variant: 3),
        _object(
          5,
          mesh: '/m/5.glb',
          joints: [
            OrblitJointPose(
              skin: 0,
              joint: 6,
              transform: Matrix4.translationValues(1, 2, 3),
            ),
            OrblitJointPose(
              skin: 1,
              joint: 2,
              transform: Matrix4.rotationZ(0.3),
            ),
          ],
        ),
        _object(6, mesh: '/m/6.glb', joints: const []),
      ],
    ),
    at: 12.5,
  ),
  'lights': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      lights: [
        OrblitLight(
          key: 20,
          kind: OrblitLightKind.directional,
          intensity: 100000,
          colour: Vector3(1, 0.9, 0.8),
          direction: Vector3(0.2, -1, 0.1),
          sunAngularRadius: 0.3,
          haloSize: 12,
          haloFalloff: 70,
        ),
        OrblitLight(
          key: 21,
          kind: OrblitLightKind.point,
          intensity: 800,
          position: Vector3(1, 2, 3),
          falloffRadius: 7,
          sourceRadius: 0.2,
          castShadows: false,
        ),
        OrblitLight(
          key: 22,
          kind: OrblitLightKind.spot,
          intensity: 1200,
          position: Vector3(0, 3, 0),
          innerConeAngle: 0.3,
          outerConeAngle: 0.7,
          falloffRadius: 12,
        ),
        OrblitLight(
          key: 23,
          kind: OrblitLightKind.area,
          intensity: 500,
          position: Vector3(0, 1, -2),
          direction: Vector3(0, 0, 1),
          width: 2,
          height: 0.5,
          tangent: Vector3(0, 0, 1),
        ),
      ],
    ),
    at: null,
  ),
  'air': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      sky: OrblitSky(
        colour: Vector3(0.2, 0.3, 0.4),
        zenith: Vector3(0.1, 0.2, 0.5),
        horizon: Vector3(0.7, 0.8, 0.9),
        ambient: 15000,
        quality: SkyQuality.fair,
        bodyDirection: Vector3(0, 2, 0),
        bodyColour: Vector3(1, 0.8, 0.6),
        bodySize: 0.01,
        flash: 0.5,
        flashDirection: Vector3(1, 1, 0),
        flashSeed: 7,
        clouds: OrblitClouds(
          colour: Vector3(0.9, 0.9, 0.95),
          cover: 0.5,
          wind: Vector2(3, 1),
          featureSize: 1 / 500,
          altitude: 1200,
          thickness: 700,
          density: 0.8,
          billow: 0.6,
          extinction: 0.02,
        ),
      ),
      fog: OrblitFog(
        colour: Vector3(0.4, 0.5, 0.6),
        density: 0.1,
        distance: 5,
        cutOffDistance: 500,
        maximumOpacity: 0.9,
        height: 3,
        heightFalloff: 0.5,
        structure: 0.4,
        wind: Vector2(0.1, 0.2),
        featureSize: 0.05,
        thickness: 8,
      ),
      precipitation: OrblitPrecipitation(
        colour: Vector3(0.8, 0.8, 0.9),
        amount: 0.6,
        fall: 12,
        wind: Vector2(1, 0.5),
        dropsPerMetre: 8,
        stretch: 30,
        threshold: 0.6,
      ),
    ),
    at: null,
  ),
  'air-fallbacks': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      sky: OrblitSky(
        drawn: false,
        showBody: false,
        bodyDirection: Vector3.zero(),
        flashDirection: Vector3.zero(),
      ),
      fog: OrblitFog(),
    ),
    at: null,
  ),
  'camera': (
    scene: OrblitScene(
      camera: OrblitCamera(
        position: Vector3(3, 4, 5),
        target: Vector3(1, 0, -1),
        fieldOfView: 70,
        orthographic: true,
        viewHeight: 12,
        aperture: 2.8,
        shutterSpeed: 1 / 60,
        sensitivity: 400,
      ),
      objects: const [],
    ),
    at: null,
  ),
};

String _b64(TypedData data) => base64Encode(
  data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
);

Object _number(double value) => value.isFinite ? value : value.toString();

Map<String, Object?> _dump(Object? value) => switch (value) {
  final Float32List l => {'f32': _b64(l)},
  final Float64List l => {'f64': _b64(l)},
  final Int32List l => {'i32': _b64(l)},
  final Int64List l => {'i64': _b64(l)},
  final Uint8List l => {'u8': _b64(l)},
  final List<String> l => {'strings': l},
  final List<int> l => {'ints': l},
  final bool b => {'bool': b},
  final int i => {'int': i},
  final double d => {'double': _number(d)},
  final String s => {'string': s},
  _ => {'other': value.runtimeType.toString()},
};

String _commit(String root) {
  String git(List<String> args) =>
      (Process.runSync('git', args, workingDirectory: root).stdout as String)
          .trim();
  const path = 'packages/orblit_filament';
  final dirty = git(['status', '--porcelain', '--', path])
      .split('\n')
      .where((line) => line.isNotEmpty && !line.contains('/test/zz_'))
      .isNotEmpty;
  final commit = git(['log', '-1', '--format=%H', '--', path]);
  return dirty ? '$commit+dirty' : commit;
}

void main() {
  test('writes the scene fixture', () {
    final root = Directory('../..').absolute.path;
    final cases = [
      for (final MapEntry(key: name, value: scene) in _scenes().entries)
        {
          'op': 'scene',
          'name': name,
          'at': scene.at,
          'expect': {
            for (final MapEntry(key: key, value: value)
                in scene.scene.toMessage(1, at: scene.at).entries)
              key: _dump(value),
          },
        },
    ];
    final body = cases.map((c) => '    ${jsonEncode(c)}').join(',\n');
    final file = File('$root/dotnet/fixtures/orblit_filament/scene.json');
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(
      '{\n'
      '  "package": "orblit_filament",\n'
      '  "sourceCommit": ${jsonEncode(_commit(root))},\n'
      '  "cases": [\n$body\n  ]\n'
      '}\n',
    );
  });
}
