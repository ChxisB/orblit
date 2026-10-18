import 'package:orblit_audio/orblit_audio.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  late RecordedAudio backend;
  late Audio audio;

  setUp(() {
    backend = RecordedAudio();
    audio = Audio(backend);
  });

  test('a voice starts at the gain of its volume and every bus above it', () {
    final music = audio.mixer.add('music')..volume = 0.5;
    audio.mixer.master.volume = 0.8;

    final voice = audio.play('theme', bus: music, volume: 0.5, loop: true);

    expect(backend.calls, hasLength(1));
    final started = backend.calls.single as AudioStarted;
    expect(started.voice, voice.id);
    expect(started.clip, 'theme');
    expect(started.loop, isTrue);
    expect(started.level.gain, closeTo(0.2, 1e-9));
  });

  test('each voice has its own id', () {
    final a = audio.play('a');
    final b = audio.play('b');

    expect(a.id, isNot(b.id));
  });

  test('a bus change reaches the backend on the next advance, once', () {
    final sfx = audio.mixer.add('sfx');
    final voice = audio.play('hit', bus: sfx, loop: true);
    backend.calls.clear();

    sfx.volume = 0.25;
    expect(backend.calls, isEmpty);
    audio.advance(0);
    audio.advance(0);

    expect(backend.calls, hasLength(1));
    final update = backend.calls.single as AudioUpdated;
    expect(update.voice, voice.id);
    expect(update.level.gain, 0.25);
  });

  test('muting a bus silences its voices and unmuting brings them back', () {
    final sfx = audio.mixer.add('sfx');
    final voice = audio.play('hit', bus: sfx, loop: true);

    sfx.muted = true;
    audio.advance(0);
    expect(backend.levelOf(voice.id)!.gain, 0);

    sfx.muted = false;
    audio.advance(0);
    expect(backend.levelOf(voice.id)!.gain, 1);
  });

  group('fades', () {
    test('a fade moves the gain in a straight line', () {
      final voice = audio.play('hum', loop: true);

      voice.fadeTo(0.2, seconds: 4);
      audio.advance(1);
      expect(backend.levelOf(voice.id)!.gain, closeTo(0.8, 1e-9));
      audio.advance(2);
      expect(backend.levelOf(voice.id)!.gain, closeTo(0.4, 1e-9));
      audio.advance(1);

      expect(backend.levelOf(voice.id)!.gain, 0.2);
      expect(voice.volume, 0.2);
      expect(voice.isPlaying, isTrue);
    });

    test('a fade past its end holds at the target', () {
      final voice = audio.play('hum', loop: true);

      voice.fadeTo(0.5, seconds: 1);
      audio.advance(10);

      expect(voice.volume, 0.5);
    });

    test('a fade of no time is a jump', () {
      final voice = audio.play('hum', loop: true);

      voice.fadeTo(0.5, seconds: 0);

      expect(voice.volume, 0.5);
    });

    test('a second fade starts from where the first had got to', () {
      final voice = audio.play('hum', loop: true);
      voice.fadeTo(0, seconds: 2);
      audio.advance(1);

      voice.fadeTo(1, seconds: 1);
      audio.advance(0.5);

      expect(voice.volume, closeTo(0.75, 1e-9));
    });

    test('setting the volume cancels a fade', () {
      final voice = audio.play('hum', loop: true);
      voice.fadeTo(0, seconds: 2);

      voice.volume = 0.6;
      audio.advance(5);

      expect(voice.volume, 0.6);
    });

    test('a fade to a negative volume is refused', () {
      final voice = audio.play('hum');

      expect(() => voice.fadeTo(-1, seconds: 1), throwsArgumentError);
    });
  });

  group('stop', () {
    test('stops the backend at once and leaves the list of voices', () {
      final voice = audio.play('hum', loop: true);

      voice.stop();

      expect(backend.calls.last, isA<AudioStopped>());
      expect(voice.isPlaying, isFalse);
      expect(audio.voices, isEmpty);
    });

    test('with a fade it falls to silence, then stops once', () {
      final voice = audio.play('hum', loop: true);

      voice.stop(fadeSeconds: 2);
      audio.advance(1);
      expect(voice.isPlaying, isTrue);
      expect(backend.levelOf(voice.id)!.gain, closeTo(0.5, 1e-9));
      audio.advance(1);
      audio.advance(1);

      expect(voice.isPlaying, isFalse);
      expect(backend.calls.whereType<AudioStopped>(), hasLength(1));
      expect(backend.calls.last, isA<AudioStopped>());
    });

    test('a stopped voice ignores a later stop or fade', () {
      final voice = audio.play('hum', loop: true);
      voice.stop();

      voice.stop();
      voice.fadeTo(1, seconds: 1);
      audio.advance(1);

      expect(backend.calls.whereType<AudioStopped>(), hasLength(1));
      expect(backend.calls.whereType<AudioUpdated>(), isEmpty);
    });
  });

  test('a clip that plays to its end drops out of the voices', () {
    final voice = audio.play('ping');
    expect(audio.voices, [voice]);

    backend.finish(voice.id);
    audio.advance(0.1);

    expect(voice.isPlaying, isFalse);
    expect(audio.voices, isEmpty);
    expect(backend.calls.whereType<AudioStopped>(), isEmpty);
  });

  test('pitch changes reach the backend and must be above zero', () {
    final voice = audio.play('engine', loop: true);

    voice.pitch = 1.5;
    audio.advance(0);

    expect(backend.levelOf(voice.id)!.pitch, 1.5);
    expect(() => voice.pitch = 0, throwsArgumentError);
    expect(() => audio.play('x', pitch: -1), throwsArgumentError);
  });

  group('a positioned voice', () {
    test('is heard by distance from the listener and moves with it', () {
      final emitter = Emitter(
        position: Vector3(0, 0, -6),
        falloff: Falloff(near: 1, far: 11, curve: FalloffCurve.linear),
      );
      final voice = audio.play('fire', at: emitter, loop: true);
      expect(backend.levelOf(voice.id)!.gain, closeTo(0.5, 1e-9));

      audio.listener.teleport(Vector3(0, 0, -6.5));
      audio.advance(0);
      expect(backend.levelOf(voice.id)!.gain, closeTo(1, 1e-9));

      audio.listener.teleport(Vector3(0, 0, 40));
      audio.advance(0);
      expect(backend.levelOf(voice.id)!.gain, 0);
    });

    test('pans to the side it is on', () {
      final voice = audio.play(
        'fire',
        at: Emitter(position: Vector3(5, 0, 0)),
        loop: true,
      );

      expect(backend.levelOf(voice.id)!.pan, closeTo(1, 1e-9));
    });

    test('shifts in pitch as the emitter drives towards the listener', () {
      final emitter = Emitter(
        position: Vector3(0, 0, -100),
        falloff: Falloff(near: 1, far: 500),
      );
      final voice = audio.play('car', at: emitter, loop: true);

      emitter.moveTo(Vector3(0, 0, -70), 1);
      audio.advance(1);

      expect(backend.levelOf(voice.id)!.pitch, closeTo(343 / (343 - 30), 1e-9));
    });

    test('is scaled by its bus on top of the distance', () {
      final sfx = audio.mixer.add('sfx')..volume = 0.5;
      final emitter = Emitter(
        position: Vector3(0, 0, -6),
        falloff: Falloff(near: 1, far: 11, curve: FalloffCurve.linear),
      );

      final voice = audio.play('fire', at: emitter, bus: sfx, volume: 0.5);

      expect(backend.levelOf(voice.id)!.gain, closeTo(0.125, 1e-9));
    });
  });

  test('a voice with no emitter is the same wherever the listener is', () {
    final voice = audio.play('theme', loop: true);

    audio.listener.teleport(Vector3(900, 0, 0));
    audio.advance(0);

    expect(backend.levelOf(voice.id), const Level());
    expect(backend.calls.whereType<AudioUpdated>(), isEmpty);
  });

  test('the calls arrive in the order they were made', () {
    final a = audio.play('a', loop: true);
    final b = audio.play('b', loop: true);
    a.stop();
    b.volume = 0.5;
    audio.advance(0);

    expect(backend.calls.map((call) => (call.runtimeType, call.voice)), [
      (AudioStarted, a.id),
      (AudioStarted, b.id),
      (AudioStopped, a.id),
      (AudioUpdated, b.id),
    ]);
  });

  test(
    'a fade to the end of a voice holds when the volume is set meanwhile',
    () {
      final a = audio.play('a', loop: true)..stop(fadeSeconds: 2);
      final b = audio.play('b', loop: true)..stop(fadeSeconds: 2);
      a.volume = 0.5;
      b.fadeTo(1, seconds: 10);

      audio.advance(1);
      expect(a.isPlaying && b.isPlaying, isTrue);
      audio.advance(1);

      expect(a.isPlaying, isFalse);
      expect(b.isPlaying, isFalse);
    },
  );

  test('a stop is not sent for a clip that already played to its end', () {
    final voice = audio.play('hit');
    backend.finish(voice.id);

    voice.stop();

    expect(voice.isPlaying, isFalse);
    expect(backend.calls.whereType<AudioStopped>(), isEmpty);
  });

  test(
    'a length of time is zero or more, and said even for an ended voice',
    () {
      final voice = audio.play('hit')..stop();

      expect(() => voice.fadeTo(1, seconds: -1), throwsArgumentError);
      expect(() => voice.fadeTo(1, seconds: double.nan), throwsArgumentError);
      expect(() => voice.fadeTo(-1, seconds: 1), throwsArgumentError);
      expect(() => voice.stop(fadeSeconds: -1), throwsArgumentError);
    },
  );

  test('a fade or a stop on an ended voice changes nothing', () {
    final voice = audio.play('hit')
      ..volume = 0.3
      ..stop();

    voice
      ..fadeTo(1, seconds: 0)
      ..stop(fadeSeconds: 1);

    expect(voice.volume, 0.3);
    expect(backend.calls.whereType<AudioStopped>(), hasLength(1));
  });

  test('a fade of no time replaces a fade under way', () {
    final voice = audio.play('a', loop: true)..fadeTo(0, seconds: 10);

    voice.fadeTo(0.5, seconds: 0);
    audio.advance(1);

    expect(voice.volume, 0.5);
  });

  test('a backend that cannot start a clip leaves no voice behind', () {
    final refusing = Audio(_Refusing());

    expect(() => refusing.play('missing'), throwsStateError);

    expect(refusing.voices, isEmpty);
  });

  test('a bus of another mixer is refused', () {
    final foreign = Mixer().add('sfx');

    expect(() => audio.play('hit', bus: foreign), throwsArgumentError);
    expect(audio.voices, isEmpty);
  });

  test('a moved emitter is heard moving for that frame only', () {
    final emitter = Emitter(position: Vector3(0, 0, -10), doppler: 1);
    final a = audio.play('a', loop: true, at: emitter);
    final b = audio.play('b', loop: true, at: emitter);

    emitter.moveTo(Vector3(0, 0, -20), 1);
    audio.advance(1);
    expect(backend.levelOf(a.id)!.pitch, lessThan(1));
    expect(backend.levelOf(b.id)!.pitch, backend.levelOf(a.id)!.pitch);

    audio.advance(1);
    expect(backend.levelOf(a.id)!.pitch, 1);
    expect(backend.levelOf(b.id)!.pitch, 1);
  });

  test('a moved listener is heard moving for that frame only', () {
    final voice = audio.play(
      'a',
      loop: true,
      at: Emitter(position: Vector3(0, 0, -10)),
    );

    audio.listener.moveTo(Vector3(0, 0, -5), 1);
    audio.advance(1);
    expect(backend.levelOf(voice.id)!.pitch, greaterThan(1));

    audio.advance(1);
    expect(backend.levelOf(voice.id)!.pitch, 1);
  });

  test('time cannot run backwards', () {
    expect(() => audio.advance(-1), throwsArgumentError);
    expect(() => audio.advance(double.nan), throwsArgumentError);
  });
}

final class _Refusing implements AudioBackend {
  @override
  void start(
    int voice,
    String clip, {
    required bool loop,
    required Level level,
  }) => throw StateError('no such clip: $clip');

  @override
  void update(int voice, Level level) {}

  @override
  void stop(int voice) {}

  @override
  bool isPlaying(int voice) => false;
}
