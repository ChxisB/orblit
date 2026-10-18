import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_test/orblit_test.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'support.dart';

void main() {
  group('hasComponent', () {
    final lamp = thing(
      'lamp',
      components: {SceneComponents.light: const LightComponent()},
    );

    test('sees a component of the type', () {
      expect(lamp, hasComponent<LightComponent>());
    });

    test('says what is missing', () {
      final matcher = hasComponent<TransformComponent>();

      expect(lamp, isNot(matcher));
      expect(refusal(matcher, lamp), contains('components'));
    });
  });

  group('isPositionedNear', () {
    final crate = thing('crate', at: Vector3(1, 2, 3));

    test('accepts a position within the distance', () {
      expect(crate, isPositionedNear(Vector3(1, 2, 3.0005)));
    });

    test('says how far off it is', () {
      final matcher = isPositionedNear(Vector3(1, 2, 4));

      expect(crate, isNot(matcher));
      expect(refusal(matcher, crate), contains('distance'));
    });

    test('takes a distance of its own', () {
      expect(crate, isPositionedNear(Vector3(1, 2, 4), within: 1));
    });

    test('says so for an entity with no transform', () {
      expect(
        refusal(isPositionedNear(Vector3.zero()), thing('folder')),
        contains('folder has no transform'),
      );
    });
  });

  group('sound', () {
    late RecordedAudio recording;
    late Audio audio;

    setUp(() {
      recording = RecordedAudio();
      audio = Audio(recording);
    });

    test('hasPlayed sees a clip that started, even once it has ended', () {
      audio.play('jump').stop();

      expect(recording, hasPlayed('jump'));
      expect(recording, isNot(hasPlayed('land')));
    });

    test('isSounding sees only a clip that is still playing', () {
      final voice = audio.play('jump');
      audio.play('rain', loop: true);
      expect(recording, isSounding('jump'));

      voice.stop();

      expect(recording, isNot(isSounding('jump')));
      expect(recording, isSounding('rain'));
    });
  });
}
