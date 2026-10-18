import 'backend.dart';
import 'mixer.dart';
import 'spatial.dart';

/// A change of volume over time.
final class _Fade {
  _Fade({required this.from, required this.to, required this.length});

  final double from;
  final double to;
  final double length;

  double elapsed = 0;

  bool get isDone => elapsed >= length;

  double get volume => isDone ? to : from + (to - from) * (elapsed / length);
}

/// One clip playing on one bus. [Audio.play] makes it.
final class Voice {
  Voice._({
    required this.id,
    required this.clip,
    required this.bus,
    required this.loop,
    required this.emitter,
    required double volume,
    required double pitch,
    required AudioBackend backend,
  }) : _volume = checkedGain(volume, 'volume'),
       _pitch = _checkedPitch(pitch),
       _backend = backend;

  final int id;
  final String clip;
  final Bus bus;
  final bool loop;

  /// Null for a sound with no place, such as music or an interface click.
  final Emitter? emitter;
  final AudioBackend _backend;

  double _volume;
  double _pitch;
  bool _playing = true;
  _Fade? _fade;

  /// Seconds left before the voice ends, once [stop] has been given a fade.
  /// A fade of the volume cannot cancel it.
  double? _stopIn;
  late Level _sent;

  bool get isPlaying => _playing;

  /// The voice's own gain, before its buses and its distance.
  double get volume => _volume;

  set volume(double value) {
    _volume = checkedGain(value, 'volume');
    _fade = null;
  }

  /// A playback rate. 1 is the clip as recorded.
  double get pitch => _pitch;

  set pitch(double value) => _pitch = _checkedPitch(value);

  /// Moves the volume to [volume] in a straight line over [seconds].
  void fadeTo(double volume, {required double seconds}) {
    final target = checkedGain(volume, 'volume');
    final length = checkedSeconds(seconds, 'seconds');
    if (_playing) _startFade(target, length);
  }

  /// Ends the voice now, or after fading to silence over [fadeSeconds]. The
  /// end holds even if the volume is set or faded meanwhile.
  void stop({double fadeSeconds = 0}) {
    final length = checkedSeconds(fadeSeconds, 'fadeSeconds');
    if (length == 0) return _end();
    _stopIn = length;
    _startFade(0, length);
  }

  /// What this voice sounds like to [listener].
  Level levelFor(Listener listener) {
    final heard = emitter?.heardBy(listener) ?? const Level();
    return Level(
      gain: _volume * bus.gain * heard.gain,
      pitch: _pitch * heard.pitch,
      pan: heard.pan,
    );
  }

  void _startFade(double target, double seconds) {
    if (seconds == 0) {
      _volume = target;
      _fade = null;
    } else {
      _fade = _Fade(from: _volume, to: target, length: seconds);
    }
  }

  void _begin(Listener listener) {
    _sent = levelFor(listener);
    _backend.start(id, clip, loop: loop, level: _sent);
  }

  void _advance(double seconds, Listener listener) {
    _stepFade(seconds);
    if (_playing && !_backend.isPlaying(id)) _playing = false;
    if (!_playing) return;
    final level = levelFor(listener);
    if (level == _sent) return;
    _sent = level;
    _backend.update(id, level);
  }

  void _stepFade(double seconds) {
    final fade = _fade;
    if (fade != null) {
      fade.elapsed += seconds;
      _volume = fade.volume;
      if (fade.isDone) _fade = null;
    }
    final left = _stopIn;
    if (left == null) return;
    _stopIn = left - seconds;
    if (left <= seconds) _end();
  }

  /// A clip that already played to its end is not stopped a second time.
  void _end() {
    if (_backend.isPlaying(id)) _backend.stop(id);
    _playing = false;
  }
}

double _checkedPitch(double value) {
  if (!value.isFinite || value <= 0) {
    throw ArgumentError.value(value, 'pitch', 'must be above zero');
  }
  return value;
}

/// The sound calls a game makes. Nothing is heard until a backend is behind
/// it, and nothing changes in the backend between two calls to [advance]
/// except a start or a stop.
final class Audio {
  Audio(AudioBackend backend) : _backend = backend;

  final AudioBackend _backend;
  final Mixer mixer = Mixer();
  final Listener listener = Listener();
  final List<Voice> _voices = [];
  int _nextId = 0;

  /// The voices that are playing.
  List<Voice> get voices =>
      List.unmodifiable(_voices.where((voice) => voice.isPlaying));

  /// Plays [clip] on [bus], or on the master bus. A voice with an [at] is
  /// heard from where that emitter is.
  Voice play(
    String clip, {
    Bus? bus,
    bool loop = false,
    double volume = 1,
    double pitch = 1,
    Emitter? at,
  }) {
    if (bus != null && !mixer.owns(bus)) {
      throw ArgumentError.value(bus, 'bus', 'is not a bus of this mixer');
    }
    final voice = Voice._(
      id: _nextId++,
      clip: clip,
      bus: bus ?? mixer.master,
      loop: loop,
      emitter: at,
      volume: volume,
      pitch: pitch,
      backend: _backend,
    );
    voice._begin(listener);
    _voices.add(voice);
    return voice;
  }

  /// Moves every fade on by [seconds], then tells the backend what changed
  /// since the last call: a bus, the listener, an emitter or a fade.
  void advance(double seconds) {
    if (!(seconds >= 0)) {
      throw ArgumentError.value(seconds, 'seconds', 'must be zero or more');
    }
    for (final voice in _voices) {
      voice._advance(seconds, listener);
    }
    // After every voice has used them, since an emitter can be shared.
    listener.settle();
    for (final voice in _voices) {
      voice.emitter?.settle();
    }
    _voices.removeWhere((voice) => !voice.isPlaying);
  }
}
