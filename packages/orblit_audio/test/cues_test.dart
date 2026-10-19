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

  List<String> log() => [
    for (final call in backend.calls)
      switch (call) {
        AudioStarted(:final clip) => 'start $clip',
        AudioStopped(:final voice) => 'stop $voice',
        AudioUpdated() => 'update',
      },
  ];

  test('cues start in the order the playhead reaches them', () {
    final board = CueBoard(audio);

    board.update([const Cue('door', 0, 1)]);
    board.update([const Cue('door', 0.1, 1), const Cue('voice', 0, 1)]);

    expect(log(), ['start door', 'start voice']);
  });

  test('a cue that stays follows its gain and starts nothing again', () {
    final board = CueBoard(audio);
    board.update([const Cue('door', 0, 1)]);

    board.update([const Cue('door', 0.1, 0.5)]);
    audio.advance(0);

    expect(log(), ['start door', 'update']);
    expect(backend.levelOf(0)!.gain, 0.5);
  });

  test('a clip that played to its end is not started again', () {
    final board = CueBoard(audio);
    board.update([const Cue('door', 0, 1)]);
    backend.finish(0);
    audio.advance(0);

    board.update([const Cue('door', 0.3, 1)]);

    expect(log(), ['start door']);
  });

  test('a cue the sequence drops fades out and may start again later', () {
    final board = CueBoard(audio, release: 0.1);
    board.update([const Cue('door', 0, 1)]);
    board.update(const []);
    audio.advance(0.2);

    board.update([const Cue('door', 0, 1)]);

    expect(log(), ['start door', 'stop 0', 'start door']);
  });

  test(
    'a seek into the middle of a one-shot starts nothing, then or later',
    () {
      final board = CueBoard(audio);

      board.update([const Cue('door', 2, 1)], seeked: true);
      board.update([const Cue('door', 2.1, 1)]);

      expect(log(), isEmpty);
    },
  );

  test('a seek into a bed starts it part-way in', () {
    final board = CueBoard(audio, beds: {'theme'});

    board.update([const Cue('theme', 12.5, 1)], seeked: true);

    final started = backend.calls.single as AudioStarted;
    expect(started.clip, 'theme');
    expect(started.offset, 12.5);
  });

  test('a seek puts a playing bed where the playhead now is', () {
    final board = CueBoard(audio, beds: {'theme'});
    board.update([const Cue('theme', 1, 1)]);

    board.update([const Cue('theme', 30, 1)], seeked: true);
    audio.advance(0.1);

    expect(log(), ['start theme', 'start theme', 'stop 0']);
    expect((backend.calls[1] as AudioStarted).offset, 30);
  });

  test('a cue with a binding is heard from where its entity is', () {
    final place = Emitter(position: Vector3(4, 0, 0));
    final board = CueBoard(
      audio,
      emitterFor: (binding) => binding == 'bell' ? place : null,
    );

    board.update([const Cue('ring', 0, 1, binding: 'bell')]);

    expect(audio.voices.single.emitter, same(place));
  });

  test('a cue with no binding has no place', () {
    final board = CueBoard(
      audio,
      emitterFor: (_) => Emitter(position: Vector3.zero()),
    );

    board.update([const Cue('narration', 0, 1)]);

    expect(audio.voices.single.emitter, isNull);
  });

  test('two cues of one clip are one voice at the louder gain', () {
    final board = CueBoard(audio);

    board.update([const Cue('wind', 0, 0.2), const Cue('wind', 0, 0.9)]);

    expect(backend.calls, hasLength(1));
    expect((backend.calls.single as AudioStarted).level.gain, 0.9);
  });

  test('stop fades out everything', () {
    final board = CueBoard(audio, release: 0);
    board.update([const Cue('a', 0, 1), const Cue('b', 0, 1)]);

    board.stop();

    expect(log(), ['start a', 'start b', 'stop 0', 'stop 1']);
  });
}
