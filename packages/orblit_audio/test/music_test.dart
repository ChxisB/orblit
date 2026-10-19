import 'package:orblit_audio/orblit_audio.dart';
import 'package:test/test.dart';

void main() {
  late RecordedAudio backend;
  late Audio audio;

  setUp(() {
    backend = RecordedAudio();
    audio = Audio(backend);
  });

  // 120 bpm in 4/4: a beat is half a second and a bar is two.
  Music make({List<MusicMove> moves = const []}) => Music(
    audio,
    tracks: {
      'calm': MusicTrack('calm.ogg'),
      'fight': MusicTrack('fight.ogg', bpm: 60, beatsPerBar: 3),
    },
    moves: moves,
  );

  List<String> starts() => [
    for (final call in backend.calls)
      if (call is AudioStarted) call.clip,
  ];

  test('the first track starts at once, looping', () {
    final music = make()..go('calm');

    final started = backend.calls.single as AudioStarted;
    expect(started.clip, 'calm.ogg');
    expect(started.loop, isTrue);
    expect(music.playing, 'calm');
  });

  test('a change on the next beat waits for the beat, and starts on it', () {
    final music = make(
      moves: [const MusicMove(to: 'fight', when: MusicWhen.nextBeat)],
    )..go('calm');
    music.advance(0.2);

    music.go('fight');
    music.advance(0.2);
    expect(music.playing, 'calm');
    expect(music.waitingFor, 'fight');

    music.advance(0.2);

    expect(music.playing, 'fight');
    expect(music.waitingFor, isNull);
    expect(starts(), ['calm.ogg', 'fight.ogg']);
  });

  test('the new track starts as far in as the step went past the beat', () {
    final music = make(
      moves: [const MusicMove(to: 'fight', when: MusicWhen.nextBeat)],
    )..go('calm');
    music.advance(0.4);
    music.go('fight');

    music.advance(0.35);

    final started = backend.calls.whereType<AudioStarted>().last;
    expect(started.offset, closeTo(0.25, 1e-9));
  });

  test('a change on the next bar waits out the bar', () {
    final music = make(
      moves: [const MusicMove(to: 'fight', when: MusicWhen.nextBar)],
    )..go('calm');
    music.advance(0.1);
    music.go('fight');

    music.advance(1.8);
    expect(music.playing, 'calm');
    music.advance(0.2);

    expect(music.playing, 'fight');
  });

  test('a beat landed on exactly is not the next beat', () {
    final music = make(
      moves: [const MusicMove(to: 'fight', when: MusicWhen.nextBeat)],
    )..go('calm');
    music.advance(0.5);
    music.go('fight');

    music.advance(0.4);
    expect(music.playing, 'calm');
    music.advance(0.1);

    expect(music.playing, 'fight');
  });

  test(
    'stopping fades the track out, drops a waiting change and starts anew',
    () {
      final music = make(
        moves: [const MusicMove(to: 'fight', when: MusicWhen.nextBar)],
      )..go('calm');
      music.go('fight');

      music.stop(fade: 1);
      audio.advance(0.5);
      expect(backend.levelOf(0)!.gain, closeTo(0.5, 1e-9));
      audio.advance(0.5);
      expect(backend.levelOf(0), isNull);
      expect(music.playing, isNull);
      expect(music.waitingFor, isNull);

      music.advance(5);
      expect(starts(), ['calm.ogg']);
      music.go('fight');
      expect(starts(), ['calm.ogg', 'fight.ogg']);
    },
  );

  test('a fade brings the new track in while the old one goes out', () {
    final music = make(moves: [const MusicMove(to: 'fight', fade: 1)])
      ..go('calm');

    music.go('fight');
    audio.advance(0.5);

    expect(backend.calls.whereType<AudioStarted>().last.level.gain, 0);
    expect(backend.levelOf(0)!.gain, closeTo(0.5, 1e-9));
    expect(backend.levelOf(1)!.gain, closeTo(0.5, 1e-9));
    audio.advance(0.5);
    expect(backend.levelOf(0), isNull);
    expect(backend.levelOf(1)!.gain, 1);
  });

  test('a row from one track beats a row from any, and no row is a cut', () {
    final music = make(
      moves: [
        const MusicMove(to: 'fight', fade: 2),
        const MusicMove(from: 'calm', to: 'fight', fade: 1),
      ],
    )..go('calm');

    music.go('fight');
    audio.advance(1);

    expect(backend.levelOf(1)!.gain, 1);
    expect(backend.levelOf(0), isNull);
  });

  test('going back to the track that plays cancels a change that waits', () {
    final music = make(
      moves: [const MusicMove(to: 'fight', when: MusicWhen.nextBeat)],
    )..go('calm');
    music.go('fight');

    music.go('calm');
    music.advance(5);

    expect(music.playing, 'calm');
    expect(starts(), ['calm.ogg']);
  });

  test('a stinger ducks the track and gives it back when it ends', () {
    final music = make()..go('calm');

    music.stinger('sting.ogg');
    audio.advance(0.1);
    expect(backend.levelOf(0)!.gain, closeTo(0.3, 1e-9));

    backend.finish(1);
    audio.advance(0);
    music.advance(0.1);
    audio.advance(0.5);

    expect(backend.levelOf(0)!.gain, 1);
  });

  test('a track that starts under a stinger starts ducked', () {
    final music = make()..go('calm');
    music.stinger('sting.ogg');

    music.go('fight');

    expect(backend.calls.whereType<AudioStarted>().last.level.gain, 0.3);
  });

  test('a track the table does not know is an error', () {
    expect(() => make().go('dance'), throwsArgumentError);
  });

  test('a track needs a beat that exists', () {
    expect(() => MusicTrack('a.ogg', bpm: 0), throwsArgumentError);
    expect(() => MusicTrack('a.ogg', beatsPerBar: 0), throwsArgumentError);
  });
}
