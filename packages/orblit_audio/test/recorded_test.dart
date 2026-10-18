import 'package:orblit_audio/orblit_audio.dart';
import 'package:test/test.dart';

void main() {
  test('the recording follows a voice from start to stop', () {
    final backend = RecordedAudio();

    backend.start(1, 'a', loop: false, level: const Level(gain: 0.5));
    expect(backend.isPlaying(1), isTrue);
    expect(backend.levelOf(1), const Level(gain: 0.5));

    backend.update(1, const Level(gain: 0.25));
    expect(backend.levelOf(1), const Level(gain: 0.25));

    backend.stop(1);
    expect(backend.isPlaying(1), isFalse);
    expect(backend.levelOf(1), isNull);
  });

  test('a call the contract rules out is a failure', () {
    final backend = RecordedAudio();

    expect(() => backend.update(1, const Level()), throwsStateError);
    expect(() => backend.stop(1), throwsStateError);

    backend.start(1, 'a', loop: false, level: const Level());
    backend.stop(1);

    expect(() => backend.update(1, const Level()), throwsStateError);
    expect(() => backend.stop(1), throwsStateError);
    expect(
      () => backend.start(1, 'a', loop: false, level: const Level()),
      throwsStateError,
    );
  });

  test('finish ends a voice without a stop call', () {
    final backend = RecordedAudio()
      ..start(1, 'a', loop: false, level: const Level());

    backend.finish(1);

    expect(backend.isPlaying(1), isFalse);
    expect(backend.calls, hasLength(1));
  });

  test('levels are equal when their numbers are', () {
    expect(const Level(gain: 0.5), const Level(gain: 0.5));
    expect(const Level(gain: 0.5), isNot(const Level(gain: 0.5, pan: 0.1)));
    expect(const Level(pitch: 2), isNot(const Level()));
  });
}
