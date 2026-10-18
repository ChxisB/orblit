import 'backend.dart';

/// One call a backend received.
sealed class AudioCall {
  const AudioCall(this.voice);

  final int voice;
}

final class AudioStarted extends AudioCall {
  const AudioStarted(
    super.voice, {
    required this.clip,
    required this.loop,
    required this.level,
  });

  final String clip;
  final bool loop;
  final Level level;
}

final class AudioUpdated extends AudioCall {
  const AudioUpdated(super.voice, this.level);

  final Level level;
}

final class AudioStopped extends AudioCall {
  const AudioStopped(super.voice);
}

/// A backend that writes every call down and plays nothing. Every test of
/// sound in this repository runs against it. It throws [StateError] for a
/// call the [AudioBackend] contract rules out: a start of an id used before,
/// or an update or a stop of a voice that is not playing.
final class RecordedAudio implements AudioBackend {
  /// Every call, in the order it arrived.
  final List<AudioCall> calls = [];

  final Map<int, Level> _playing = {};
  final Set<int> _everStarted = {};

  /// How [voice] last sounded, or null when it is not playing.
  Level? levelOf(int voice) => _playing[voice];

  /// Ends [voice] as a clip does when it plays to its end.
  void finish(int voice) => _playing.remove(voice);

  @override
  void start(
    int voice,
    String clip, {
    required bool loop,
    required Level level,
  }) {
    if (_everStarted.contains(voice)) {
      throw StateError('Voice $voice was started before.');
    }
    _everStarted.add(voice);
    calls.add(AudioStarted(voice, clip: clip, loop: loop, level: level));
    _playing[voice] = level;
  }

  @override
  void update(int voice, Level level) {
    _mustBePlaying(voice);
    calls.add(AudioUpdated(voice, level));
    _playing[voice] = level;
  }

  @override
  void stop(int voice) {
    _mustBePlaying(voice);
    calls.add(AudioStopped(voice));
    _playing.remove(voice);
  }

  @override
  bool isPlaying(int voice) => _playing.containsKey(voice);

  /// The contract says a call on a voice that is not playing never happens,
  /// so the fake makes one a failure rather than a note in the log.
  void _mustBePlaying(int voice) {
    if (!isPlaying(voice)) throw StateError('Voice $voice is not playing.');
  }
}
