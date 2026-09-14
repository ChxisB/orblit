import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

void main() {
  OrblitObject objectOn(int layer) => OrblitObject(
    key: 1,
    transform: Matrix4.identity(),
    colour: Vector3(1, 1, 1),
    layer: layer,
  );

  OrblitScene sceneOf(List<OrblitObject> objects, {OrblitRenderGraph? graph}) =>
      OrblitScene(
        objects: objects,
        graph: graph,
        camera: OrblitCamera(position: Vector3(0, 0, 5), target: Vector3.zero()),
      );

  group('render layers', () {
    test('an object that has never heard of layers is on the first one', () {
      final scene = sceneOf([objectOn(0)]);
      final flags = scene.toMessage(1)['objectFlags']! as List<int>;
      // The same bits it always sent: cast, receive, visible, and a layer of
      // nought that changes nothing.
      expect(flags.single & 0xFF, 1 | 2 | 4);
      expect(flags.single >> 8, 0);
    });

    test('the layer rides in the high bits rather than in a fourth array', () {
      final scene = sceneOf([objectOn(3)]);
      final flags = scene.toMessage(1)['objectFlags']! as List<int>;

      expect(flags.single >> 8, 3);
      // And the low bits still mean what they meant.
      expect(flags.single & 0xFF, 1 | 2 | 4);
    });

    test('a layer past the last one is clamped, not wrapped', () {
      // Wrapping would put an object on layer nought — visible in every pass
      // — which is the opposite of what somebody asking for layer twelve
      // wanted.
      final scene = sceneOf([objectOn(99)]);
      final flags = scene.toMessage(1)['objectFlags']! as List<int>;
      expect(flags.single >> 8, OrblitScene.maxLayer);
    });

    test('a population carries a layer the same way', () {
      final forest = OrblitPopulation(
        key: 1,
        transforms: Matrix4.identity().storage.buffer.asFloat32List(0, 16),
        colours: Float32List.fromList([1, 1, 1]),
        minimum: Vector3.zero(),
        maximum: Vector3.all(1),
        layer: 2,
      );
      expect(forest.flags >> 8, 2);
      expect(forest.flags & 0xFF, 2);
    });
  });

  group('a material sampling a pass', () {
    test('a target is a path with a scheme, not a second field', () {
      final mirror = OrblitTexture.ofTarget('reflection');

      expect(mirror.target, 'reflection');
      expect(mirror.path, 'orblit:target/reflection');
      // Never sRGB: what a pass drew is already linear, and decoding it again
      // would darken every reflection in the scene.
      expect(mirror.srgb, isFalse);
    });

    test('an ordinary texture names no target', () {
      const image = OrblitTexture('/tmp/wood.png');
      expect(image.target, isNull);
    });
  });

  group('the graph on a scene', () {
    test(
      'a scene that says nothing about it draws one pass into the frame',
      () {
        final scene = sceneOf([objectOn(0)]);
        expect(scene.passNames, ['scene']);

        final message = scene.toMessage(1);
        expect(message['graphPasses'], hasLength(OrblitRenderGraph.passStride));
        expect(message['graphTargets'], isEmpty);
      },
    );

    test('the passes cross in the order they run, with their targets', () {
      final scene = sceneOf(
        [objectOn(0)],
        graph: const OrblitRenderGraph(
          targets: [OrblitTarget(name: 'mirror', scale: 0.5)],
          passes: [
            OrblitPass(name: 'frame', reads: ['mirror'], layers: 0x01),
            OrblitPass(
              name: 'water',
              kind: OrblitPassKind.reflection,
              into: 'mirror',
              layers: 0x02,
              plane: [0, 1, 0, 0],
            ),
          ],
        ),
      );

      expect(scene.passNames, ['water', 'frame']);
      final message = scene.toMessage(1);
      expect(message['graphTargetNames'], ['mirror']);
      expect(
        message['graphPasses'],
        hasLength(2 * OrblitRenderGraph.passStride),
      );
    });
  });
}
