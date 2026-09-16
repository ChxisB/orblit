import 'dart:math' as math;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// What the renderer said about the fox and a lamp, shortened, in the shape
/// OrblitModels.cpp writes it.
const _fox =
    '{"clips":[{"name":"Survey","seconds":3.41666675},'
    '{"name":"Walk","seconds":0.708333313},{"name":"Run","seconds":1.1583333}],'
    '"skins":[{"name":"","joints":["_rootJoint","b_Root_00","b_Hip_01"]}],'
    '"variants":[],"materials":["fox_material"],"lights":[],"cameras":[],'
    '"bounds":{"min":[-8,0,-60],"max":[8,80,70]},"unsupported":[]}';

const _lamp =
    '{"clips":[],"skins":[],"variants":["on","off"],"materials":[],'
    '"lights":[{"name":"Bulb","kind":1,"colour":[1,0.9,0.8],"intensity":800,'
    '"falloff":0,"inner":0,"outer":0,'
    '"transform":[1,0,0,0,0,1,0,0,0,0,1,0,0,2,0,1]},'
    '{"name":"Shade","kind":2,"colour":[1,1,1],"intensity":300,'
    '"falloff":5,"inner":0.3,"outer":0.5,'
    '"transform":[1,0,0,0,0,0,-1,0,0,1,0,0,0,1.5,0,1]}],'
    '"cameras":[{"name":"Front","orthographic":false,"fieldOfView":39.6,'
    '"viewHeight":0,"near":0.1,"far":null,'
    '"transform":[1,0,0,0,0,1,0,0,0,0,1,0,0,1,4,1]}],'
    '"bounds":{"min":[-0.5,0,-0.5],"max":[0.5,2.2,0.5]},'
    '"unsupported":["KHR_materials_anisotropy"]}';

OrblitAssetInfo _read(String json) =>
    OrblitAssetInfo.split({'orblit.model:model': json}).models.single;

void main() {
  group('model descriptions in the notes', () {
    test('are taken out, and the problems left behind', () {
      final split = OrblitAssetInfo.split({
        'orblit.model:/models/fox.glb': _fox,
        '/models/crate.glb': 'The file could not be read.',
        'pose 3': 'Clip 7 was asked for, and the file has 3.',
      });
      expect(split.notes.keys, ['/models/crate.glb', 'pose 3']);
      expect(split.models.single.path, '/models/fox.glb');
    });

    test('that do not parse are dropped, not thrown', () {
      final split = OrblitAssetInfo.split({
        'orblit.model:/a.glb': '{not json',
        'orblit.model:/b.glb': '[1, 2, 3]',
        'orblit.model:/c.glb': '{"clips": "sideways"}',
      });
      expect(split.notes, isEmpty);
      expect(split.models, isEmpty);
    });
  });

  group('a description', () {
    test('names its clips, with how long each is', () {
      final fox = _read(_fox);
      expect(
        [for (final clip in fox.clips) clip.name],
        ['Survey', 'Walk', 'Run'],
      );
      expect(fox.clips[1].seconds, closeTo(0.7083, 1e-4));
      expect(fox.clipNamed('Run'), 2);
      expect(fox.clipNamed('Swim'), isNull);
    });

    test('finds a joint by name in whichever skin has it', () {
      final fox = _read(_fox);
      expect(fox.jointNamed('b_Hip_01'), (skin: 0, joint: 2));
      expect(fox.jointNamed('tail'), isNull);
    });

    test('reads its bounds, variants, cameras and what it cannot draw', () {
      final fox = _read(_fox);
      final lamp = _read(_lamp);
      expect(fox.boundsMax.y, 80);
      expect(lamp.variants, ['on', 'off']);
      expect(lamp.unsupported, ['KHR_materials_anisotropy']);
      final camera = lamp.cameras.single;
      expect(camera.name, 'Front');
      expect(camera.fieldOfView, closeTo(39.6, 1e-9));
      // No far plane in the file is a camera that sees forever.
      expect(camera.far, double.infinity);
      expect(camera.transform.getTranslation(), Vector3(0, 1, 4));
    });
  });

  group("a file's lights as scene lights", () {
    test('stand where the model does, in its units and kinds', () {
      final lamp = _read(_lamp);
      final placement = Matrix4.translation(Vector3(10, 0, 0));
      final lights = lamp.lightsFor(placement, keyOf: (i) => 500 + i);

      expect([for (final light in lights) light.key], [500, 501]);
      expect(lights[0].kind, OrblitLightKind.point);
      expect(lights[0].intensity, 800);
      expect(lights[0].position, Vector3(10, 2, 0));
      expect(lights[1].kind, OrblitLightKind.spot);
      expect(lights[1].outerConeAngle, closeTo(0.5, 1e-9));
      expect(lights[1].falloffRadius, 5);
    });

    test('shine along their own minus z, turned as the model is', () {
      final lamp = _read(_lamp);
      final lights = lamp.lightsFor(Matrix4.identity(), keyOf: (i) => i);
      // The shade's node turns minus z to minus y: it points at the floor.
      final down = lights[1].direction;
      expect(down.x, closeTo(0, 1e-6));
      expect(down.y, closeTo(-1, 1e-6));
      expect(down.z, closeTo(0, 1e-6));

      final turned = lamp.lightsFor(
        Matrix4.rotationY(math.pi / 2),
        keyOf: (i) => i,
      );
      // The bulb has no turn of its own, so it faces the model's minus z,
      // which a quarter turn about y makes minus x.
      expect(turned[0].direction.x, closeTo(-1, 1e-6));
    });

    test('with no range, reach as far as a tenth of a lux', () {
      final lamp = _read(_lamp);
      final lights = lamp.lightsFor(Matrix4.identity(), keyOf: (i) => i);
      expect(lights[0].falloffRadius, OrblitFileLight.reachOf(800));
      expect(
        OrblitFileLight.reachOf(800),
        closeTo(math.sqrt(800 / (4 * math.pi) / 0.1), 1e-9),
      );
    });
  });
}
