import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

SceneEntity thing(
  String id,
  Vector3 at,
  Map<String, SceneComponent> parts, {
  bool visible = true,
}) => SceneEntity(
  id: id,
  name: id,
  visible: visible,
  components: {
    SceneComponents.transform: TransformComponent(position: at),
    ...parts,
  },
);

SceneEntity fire(
  Vector3 at, {
  SoundSourceComponent? source,
  bool visible = true,
}) => thing('fire', at, {
  SceneComponents.soundSource:
      source ?? SoundSourceComponent(clip: 'fire.ogg', bus: 'sfx'),
}, visible: visible);

/// A camera at (0, 0, 10) looking down -Z, with [others] in the scene.
SceneDocument world(List<SceneEntity> others) => SceneDocument(
  entities: [
    thing('game', Vector3(0, 0, 10), {
      SceneComponents.camera: const CameraComponent(),
    }),
    ...others,
  ],
);

CutsceneDocument swell() => CutsceneDocument(
  motion: ClipDocument(name: 'Swell', duration: 1, channels: const []),
  sounds: [CutsceneSound(sound: 'swell.ogg', start: 0, duration: 1)],
);

void main() {
  late RecordedAudio backend;
  late Audio audio;
  late OrblitDocumentView view;
  late OrblitSound sound;

  void open(List<SceneEntity> others) {
    backend = RecordedAudio();
    audio = Audio(backend)..mixer.add('sfx');
    view = OrblitDocumentView(world(others));
    sound = OrblitSound(view, audio);
  }

  void frame([double seconds = 1 / 60, OrblitCutsceneStep? step]) {
    sound.advance(seconds, step);
    audio.advance(seconds);
  }

  List<AudioStarted> starts() =>
      backend.calls.whereType<AudioStarted>().toList();

  Level lastLevel() => backend.calls.last is AudioUpdated
      ? (backend.calls.last as AudioUpdated).level
      : starts().last.level;

  group('a source', () {
    test('plays from its entity while it is shown', () {
      open([fire(Vector3(10, 0, 10))]);
      frame();
      expect(starts().single.clip, 'fire.ogg');
      expect(starts().single.loop, isTrue);
      expect(starts().single.level.pan, closeTo(1, 1e-9));
      expect(starts().single.level.gain, inExclusiveRange(0, 1));

      view.replace(world([fire(Vector3(10, 0, 10), visible: false)]));
      frame(0.1);
      expect(backend.calls.last, isA<AudioStopped>());

      view.replace(world([fire(Vector3(10, 0, 10))]));
      frame();
      expect(starts(), hasLength(2));
    });

    test('is silent with no clip', () {
      open([fire(Vector3.zero(), source: SoundSourceComponent())]);
      frame();
      expect(backend.calls, isEmpty);
    });

    test('follows a change of volume and starts over for a new clip', () {
      open([fire(Vector3(0, 0, 5))]);
      frame();
      final full = starts().single.level.gain;

      view.replace(
        world([
          fire(
            Vector3(0, 0, 5),
            source: SoundSourceComponent(
              clip: 'fire.ogg',
              bus: 'sfx',
              volume: 0.5,
            ),
          ),
        ]),
      );
      frame();
      expect(starts(), hasLength(1));
      expect(lastLevel().gain, closeTo(full / 2, 1e-9));

      view.replace(
        world([
          fire(
            Vector3(0, 0, 5),
            source: SoundSourceComponent(clip: 'blaze.ogg', bus: 'sfx'),
          ),
        ]),
      );
      frame(0.1);
      expect(starts().map((s) => s.clip), ['fire.ogg', 'blaze.ogg']);
      expect(backend.calls.whereType<AudioStopped>(), hasLength(1));
    });

    test('that does not loop plays once, and again when asked', () {
      open([
        fire(
          Vector3(0, 0, 5),
          source: SoundSourceComponent(
            clip: 'bang.ogg',
            bus: 'sfx',
            loop: false,
          ),
        ),
      ]);
      frame();
      frame();
      expect(starts(), hasLength(1));
      expect(starts().single.loop, isFalse);

      backend.finish(starts().single.voice);
      sound.play('fire');
      frame();
      expect(starts(), hasLength(2));
    });

    test('on a bus the mixer lacks is refused by name', () {
      open([
        fire(
          Vector3.zero(),
          source: SoundSourceComponent(clip: 'fire.ogg', bus: 'nowhere'),
        ),
      ]);
      expect(
        frame,
        throwsA(
          isA<ArgumentError>().having(
            (e) => e.message,
            'message',
            contains('"fire"'),
          ),
        ),
      );
    });

    test('moving slowly is heard as a speed and a cut is not', () {
      open([fire(Vector3(10, 0, 10))]);
      frame();

      view.replace(world([fire(Vector3(9, 0, 10))]));
      frame(1);
      expect(lastLevel().pitch, greaterThan(1));

      view.replace(world([fire(Vector3(109, 0, 10))]));
      frame(1 / 60);
      expect(lastLevel().pitch, 1);
    });

    test('is silenced by stop', () {
      open([fire(Vector3(0, 0, 5))]);
      frame();
      sound.stop();
      audio.advance(0.1);
      expect(backend.calls.last, isA<AudioStopped>());
    });
  });

  group('the listener', () {
    test('is the camera when no entity says otherwise', () {
      open([fire(Vector3(10, 0, 10))]);
      frame();
      expect(starts().single.level.pan, closeTo(1, 1e-9));
    });

    test('is the first entity that has one, facing down its own -Z', () {
      open([
        fire(Vector3(10, 0, 10)),
        thing('ear', Vector3(10, 0, 20), {
          SceneComponents.soundListener: const SoundListenerComponent(),
        }),
      ]);
      frame();
      expect(starts().single.level.pan, closeTo(0, 1e-9));
    });
  });

  group('a cutscene', () {
    test('plays its sound and lets go at the end', () {
      open([]);
      final cutscenes = OrblitCutscenes(view, [swell()])..start('Swell');
      frame(0.25, cutscenes.advance(0.25));
      expect(starts().single.clip, 'swell.ogg');
      expect(starts().single.offset, closeTo(0.25, 1e-9));
      frame(0.25, cutscenes.advance(0.25));
      expect(starts(), hasLength(1));

      frame(1, cutscenes.advance(1));
      expect(backend.calls.last, isA<AudioStopped>());
    });

    test(
      'hears a bound cue from its entity and an unbound one from nowhere',
      () {
        open([thing('cube', Vector3(4, 0, 0), const {})]);
        frame(
          1 / 60,
          const OrblitCutsceneStep(
            sounds: [
              SoundAt('thud.ogg', 0, 1, binding: 'cube'),
              SoundAt('thud.ogg', 0, 1, binding: 'nobody'),
            ],
          ),
        );
        final [bound, unbound] = starts();
        expect(bound.level.pan, greaterThan(0.3));
        expect(unbound.level.pan, 0);
      },
    );
  });

  test('names what an entity is made of', () {
    open([
      thing('crate', Vector3.zero(), {
        SceneComponents.soundSurface: const SoundSurfaceComponent(name: 'wood'),
      }),
      thing('rock', Vector3.zero(), {
        SceneComponents.soundSurface: const SoundSurfaceComponent(),
      }),
    ]);
    expect(sound.surfaceOf('crate'), 'wood');
    expect(sound.surfaceOf('rock'), isNull);
    expect(sound.surfaceOf('game'), isNull);
    expect(sound.surfaceOf('nobody'), isNull);
  });
}
