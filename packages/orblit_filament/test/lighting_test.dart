import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  group('a rectangle of light', () {
    OrblitScene sceneWith(OrblitLight light) => OrblitScene(
      objects: const [],
      camera: OrblitCamera(position: Vector3(0, 0, 5), target: Vector3.zero()),
      lights: [light],
    );

    test('its size and its edge reach the renderer', () {
      // A rectangle is the one light whose orientation is not settled by
      // where it points: the face leaves it free to spin in its own plane.
      // So the edge crosses as well as the aim, and both have to arrive.
      final params =
          sceneWith(
                OrblitLight(
                  key: 1,
                  kind: OrblitLightKind.area,
                  intensity: 5000,
                  position: Vector3(0, 3, 0),
                  direction: Vector3(0, -1, 0),
                  tangent: Vector3(0, 0, 1),
                  width: 2.5,
                  height: 0.75,
                ),
              ).toMessage(1)['lightParams']!
              as List<double>;

      expect(params, hasLength(OrblitLight.stride));
      expect(params[17], 2.5, reason: 'width');
      expect(params[18], 0.75, reason: 'height');
      expect(params[19], 0, reason: 'tangent x');
      expect(params[20], 0, reason: 'tangent y');
      expect(params[21], 1, reason: 'tangent z');
    });

    test('it is a kind of its own, not a point in disguise', () {
      // The renderer refuses a kind it does not know rather than falling
      // through to one it does, so the index matters as much as the floats.
      final kinds =
          sceneWith(
                OrblitLight(
                  key: 1,
                  kind: OrblitLightKind.area,
                  intensity: 5000,
                ),
              ).toMessage(1)['lightKinds']!
              as List<int>;
      expect(kinds.single, OrblitLightKind.area.index);
      expect(OrblitLightKind.area.index, 3);
    });

    test('a rectangle left undescribed is still a rectangle', () {
      // Nought width would divide by nothing when the flux is turned into a
      // luminance, so the defaults have to be a real panel rather than a
      // degenerate one.
      final light = OrblitLight(
        key: 1,
        kind: OrblitLightKind.area,
        intensity: 100,
      );
      expect(light.width, greaterThan(0));
      expect(light.height, greaterThan(0));
      expect(light.tangent.length, greaterThan(0));
    });
  });

  group('what crosses to the renderer keeps its shape', () {
    test('a light writes exactly the stride it declares', () {
      // The renderer walks this array by a stride of its own. When the two
      // disagree every light after the first reads a mixture of the one
      // before it and itself — which is what happened when the halo fields
      // took a light from sixteen floats to eighteen.
      final scene = OrblitScene(
        objects: const [],
        camera: OrblitCamera(
          position: Vector3(0, 0, 5),
          target: Vector3.zero(),
        ),
        lights: [
          OrblitLight(
            key: 1,
            kind: OrblitLightKind.directional,
            intensity: 100000,
            direction: Vector3(0, -1, 0),
          ),
          OrblitLight(
            key: 2,
            kind: OrblitLightKind.point,
            intensity: 800,
            position: Vector3(3, 2, 1),
          ),
        ],
      );

      final params = scene.toMessage(1)['lightParams']! as List<double>;
      expect(params, hasLength(2 * OrblitLight.stride));

      // The second light starts exactly one stride in, and its own numbers
      // are there rather than the tail of the first.
      expect(params[OrblitLight.stride + 3], 800);
      expect(params[OrblitLight.stride + 4], 3);
      expect(params[OrblitLight.stride + 5], 2);
      expect(params[OrblitLight.stride + 6], 1);
    });

    test('a shading model keeps the number the renderer reads it by', () {
      // The renderer branches on this index. Reordering the enum to put a new
      // model in the middle is a silent swap of every video screen in every
      // scene for whatever took its place.
      expect(OrblitShading.lit.index, 0);
      expect(OrblitShading.unlit.index, 1);
      expect(OrblitShading.video.index, 2);
      expect(OrblitShading.shadowCatcher.index, 3);
    });
  });

  group('light clustering', () {
    test('the defaults are the ones a room-sized scene wants', () {
      final lighting = OrblitLighting();
      expect(lighting.clusterNear, 5);
      expect(lighting.clusterFar, 100);
    });

    test('it crosses on the end of the pipeline block', () {
      final pipeline = OrblitPipeline(
        lighting: OrblitLighting(clusterNear: 2, clusterFar: 400),
      );
      final packed = pipeline.packed;

      expect(packed, hasLength(OrblitPipeline.stride));
      expect(packed[16], 2);
      expect(packed[17], 400);
    });

    test('a pipeline that says nothing still sends the numbers', () {
      // A short block is one from before clustering existed, and the renderer
      // reads it as "use Filament's own defaults". A pipeline built today is
      // never short, so the two paths do not have to agree about anything.
      expect(OrblitPipeline().packed[16], 5);
      expect(OrblitPipeline().packed[17], 100);
    });

    test('every named detail tier carries clustering too', () {
      for (final detail in OrblitDetail.values) {
        expect(
          OrblitPipeline.at(detail).packed,
          hasLength(OrblitPipeline.stride),
        );
      }
    });
  });

  group('a shadow catcher', () {
    test('is a shading model, not a blend mode', () {
      // Its blending is fixed by what it is: a surface that is only its own
      // shadow is see-through by definition.
      const floor = OrblitMaterial(
        key: 9,
        shading: OrblitShading.shadowCatcher,
      );
      expect(floor.shading, OrblitShading.shadowCatcher);
      expect(OrblitShading.shadowCatcher.isSurface, isFalse);
      expect(OrblitShading.lit.isSurface, isTrue);
    });

    test('it reaches the renderer as a shading model in the flags', () {
      final scene = OrblitScene(
        objects: const [],
        camera: OrblitCamera(
          position: Vector3(0, 0, 5),
          target: Vector3.zero(),
        ),
        materials: const [
          OrblitMaterial(key: 9, shading: OrblitShading.shadowCatcher),
        ],
      );
      final flags = scene.toMessage(1)['materialFlags']! as List<int>;
      expect(flags.single & 3, 3);
    });
  });
}
