import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// Sprite layers as they cross to the renderer: the records a sprite is
/// packed into, a layer's settings, and when its sprites travel at all.
void main() {
  OrblitScene sceneWith(List<OrblitSprites> sprites) => OrblitScene(
    objects: const [],
    sprites: sprites,
    camera: OrblitCamera(
      position: Vector3(0, 0, 10),
      target: Vector3.zero(),
      orthographic: true,
    ),
  );

  final two = OrblitSprites.pack(const [
    OrblitSprite(x: 1, y: 2, width: 3, height: 4, rotation: 0.5),
    OrblitSprite(x: -1, y: -2, u0: 0.25, v1: 0.5, alpha: 0.75),
  ]);

  group('packing', () {
    test('a sprite is sixteen floats, in the order the renderer reads', () {
      expect(two.length, 2 * OrblitSprites.stride);
      expect(two.sublist(0, 16), [
        1, 2, 0, 0.5, 3, 4, 0.5, 0.5, //
        0, 0, 1, 1, 1, 1, 1, 1,
      ]);
      expect(two[16 + 8], 0.25);
      expect(two[16 + 11], 0.5);
      expect(two[16 + 15], 0.75);
    });

    test('a rectangle given in pixels becomes fractions of the image', () {
      final sprite = const OrblitSprite(x: 0, y: 0).cut(16, 32, 16, 8, 64, 64);
      expect(
        [sprite.u0, sprite.v0, sprite.u1, sprite.v1],
        [0.25, 0.5, 0.5, 0.625],
      );
    });

    test('a layer is its transform and then its tint', () {
      final layer = OrblitSprites(
        key: 1,
        sprites: two,
        transform: Matrix4.translationValues(5, 6, 7),
        tint: Vector4(0.1, 0.2, 0.3, 0.4),
      );
      final into = Float32List(OrblitSprites.layerStride);
      layer.packParams(into, 0);
      expect(into[12], 5);
      expect(into[13], 6);
      expect(into[14], 7);
      expect(into.sublist(16), [
        closeTo(0.1, 1e-6),
        closeTo(0.2, 1e-6),
        closeTo(0.3, 1e-6),
        closeTo(0.4, 1e-6),
      ]);
    });

    test(
      'a sharp layer snaps unless told not to, and a smooth one does not',
      () {
        expect(OrblitSprites(key: 1, sprites: two).snap, isTrue);
        expect(
          OrblitSprites(key: 1, sprites: two, filter: OrblitFilter.smooth).snap,
          isFalse,
        );
        expect(OrblitSprites(key: 1, sprites: two, snap: false).snap, isFalse);
      },
    );
  });

  group('the message', () {
    test('a scene with no sprites says nothing about them', () {
      final message = sceneWith(const []).toMessage(1);
      expect(message.keys.where((k) => k.startsWith('sprite')), isEmpty);
    });

    test('every layer is described, and its sprites travel the first time', () {
      final message = sceneWith([
        OrblitSprites(
          key: 7,
          sprites: two,
          image: const OrblitTexture('orblit:resource/atlas.png'),
          order: -3,
        ),
      ]).toMessage(1);

      expect(message['spriteKeys'], [7]);
      expect(message['spriteOrders'], [-3]);
      expect(message['spritePaths'], ['orblit:resource/atlas.png']);
      expect(
        (message['spriteParams']! as Float32List).length,
        OrblitSprites.layerStride,
      );
      expect(message['spriteChanged'], [7]);
      expect(message['spriteChangedCounts'], [2]);
      expect(message['spriteData'], two);
    });

    test('a layer the renderer already holds sends its settings only', () {
      final message = sceneWith([
        OrblitSprites(key: 7, sprites: two, revision: 4),
      ]).toMessage(1, sentSpriteRevisions: {7: 4});

      expect(message['spriteKeys'], [7]);
      expect(message['spriteChanged'], isEmpty);
      expect((message['spriteData']! as Float32List), isEmpty);
    });

    test('a layer whose revision moved sends its sprites again', () {
      final message = sceneWith([
        OrblitSprites(key: 7, sprites: two, revision: 5),
      ]).toMessage(1, sentSpriteRevisions: {7: 4});

      expect(message['spriteChanged'], [7]);
      expect((message['spriteData']! as Float32List).length, two.length);
    });

    test('a layer with no image names none', () {
      final message = sceneWith([
        OrblitSprites(key: 1, sprites: two),
      ]).toMessage(1);
      expect(message['spritePaths'], ['']);
    });
  });
}
