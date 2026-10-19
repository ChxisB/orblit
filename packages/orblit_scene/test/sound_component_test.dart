import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

void main() {
  group('a sound source', () {
    test(
      'reads as a looping sound on the master bus when the file is bare',
      () {
        final source = SoundSourceComponent.fromJson(const {});

        expect(source.clip, isNull);
        expect(source.bus, 'master');
        expect(source.loop, isTrue);
        expect(source.volume, 1);
        expect([source.near, source.far], [1, 50]);
        expect(source.curve, SoundCurve.inverse);
      },
    );

    test('is the source it was written as', () {
      final source = SoundSourceComponent(
        clip: 'sound/fire.ogg',
        bus: 'sfx',
        loop: false,
        volume: 0.5,
        near: 2,
        far: 30,
        curve: SoundCurve.linear,
      );

      final read = SoundSourceComponent.fromJson(source.toJson());

      expect(read.toJson(), source.toJson());
      expect(read.clip, 'sound/fire.ogg');
      expect(read.curve, SoundCurve.linear);
      expect(read.loop, isFalse);
    });

    test('leaves the clip out of the file when it has none', () {
      expect(SoundSourceComponent().toJson().containsKey('clip'), isFalse);
    });

    test('reads an empty clip as none and an empty bus as the master', () {
      final source = SoundSourceComponent.fromJson(const {
        'clip': '',
        'bus': '',
      });

      expect(source.clip, isNull);
      expect(source.bus, 'master');
    });

    test('reads a range without an end and a bad volume as the default', () {
      final source = SoundSourceComponent.fromJson(const {
        'far': double.infinity,
        'volume': -1,
      });

      expect([source.near, source.far], [1, 50]);
      expect(source.volume, 1);
      expect(SoundSourceComponent.fromJson(const {'volume': 0}).volume, 0);
    });

    test('reads a range that is not a range as the default one', () {
      final source = SoundSourceComponent.fromJson(const {
        'near': 20,
        'far': 5,
      });

      expect([source.near, source.far], [1, 50]);
    });

    test('is the same source with one field changed by copyWith', () {
      final source = SoundSourceComponent(clip: 'a.ogg', far: 9);

      final louder = source.copyWith(volume: 2);

      expect(louder.volume, 2);
      expect(louder.clip, 'a.ogg');
      expect(louder.far, 9);
    });
  });

  test('a listener and a surface survive a trip through a document', () {
    final document = SceneDocument(
      name: 'Scene',
      entities: [
        SceneEntity(
          id: 'ear',
          name: 'Ear',
          components: {
            SceneComponents.transform: TransformComponent(),
            SceneComponents.soundListener: const SoundListenerComponent(),
            SceneComponents.soundSurface: const SoundSurfaceComponent(
              name: 'wood',
            ),
          },
        ),
      ],
    );

    final read = SceneDocument.decode(document.encode());
    final ear = read.document['ear']!;

    expect(ear[SceneComponents.soundListener], isA<SoundListenerComponent>());
    expect(
      (ear[SceneComponents.soundSurface] as SoundSurfaceComponent).name,
      'wood',
    );
  });
}
