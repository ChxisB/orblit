import 'audio.dart';
import 'mixer.dart';

/// A piece of music that loops, and the beat it keeps.
final class MusicTrack {
  MusicTrack(this.clip, {this.bpm = 120, this.beatsPerBar = 4}) {
    if (!(bpm.isFinite && bpm > 0)) {
      throw ArgumentError.value(bpm, 'bpm', 'must be above zero');
    }
    if (beatsPerBar < 1) {
      throw ArgumentError.value(
        beatsPerBar,
        'beatsPerBar',
        'must be 1 or more',
      );
    }
  }

  final String clip;
  final double bpm;
  final int beatsPerBar;

  double get beat => 60 / bpm;
  double get bar => beat * beatsPerBar;
}

/// When a change of music happens.
enum MusicWhen { now, nextBeat, nextBar }

/// One row of the transition table: going [to] a track, from [from] or, when
/// that is null, from any track.
final class MusicMove {
  const MusicMove({
    required this.to,
    this.from,
    this.when = MusicWhen.now,
    this.fade = 0,
  });

  final String? from;
  final String to;
  final MusicWhen when;

  /// Seconds the old track fades out over while the new one fades in.
  final double fade;
}

/// The music of a game: one track looping at a time, changed by a table, with
/// stingers over the top.
///
/// Time is the sampled kind. [advance] says how long has passed, so a change
/// that waits for the beat fires on the first call that crosses it, and a test
/// drives it with whatever steps it likes.
final class Music {
  Music(
    this.audio, {
    required this.tracks,
    this.moves = const [],
    this.bus,
    this.duck = 0.3,
  });

  final Audio audio;
  final Map<String, MusicTrack> tracks;
  final List<MusicMove> moves;
  final Bus? bus;

  /// What a track is turned down to while a stinger plays.
  final double duck;

  String? _playing;
  Voice? _bed;

  /// Seconds the current track has been playing.
  double _position = 0;
  MusicMove? _pending;
  final List<Voice> _stingers = [];

  /// The track playing now, or null before the first.
  String? get playing => _playing;

  /// The track a change is waiting to start, or null.
  String? get waitingFor => _pending?.to;

  /// Goes to [track] by the first row that fits, else a cut. Going to the track
  /// that is already waited for does nothing, and going to the one that is
  /// playing cancels a change that was waiting.
  void go(String track) {
    if (!tracks.containsKey(track)) {
      throw ArgumentError.value(track, 'track', 'there is no such track');
    }
    if (track == _playing) {
      _pending = null;
      return;
    }
    if (track == _pending?.to) return;
    final move = _moveTo(track);
    if (move.when == MusicWhen.now || _playing == null) {
      _begin(move, late: 0);
    } else {
      _pending = move;
    }
  }

  /// Fades the track out over [fade] seconds and leaves nothing playing. A
  /// change that was waiting is dropped, and the next [go] starts at once.
  void stop({double fade = 0}) {
    _pending = null;
    _bed?.stop(fadeSeconds: fade);
    _bed = null;
    _playing = null;
  }

  // shortcut: the duck and the recovery fade for a fixed time, make them
  // fields when a game wants to tune them.
  static const double _duckFade = 0.1;
  static const double _recoverFade = 0.5;

  /// Plays [clip] once over the music and turns the track down until it ends.
  void stinger(String clip) {
    _stingers.add(audio.play(clip, bus: bus));
    _bed?.fadeTo(duck, seconds: _duckFade);
  }

  /// Moves on by [seconds]: the beat, a change waiting for it, and a stinger
  /// that has ended.
  void advance(double seconds) {
    final before = _position;
    _position += seconds;
    if (_pending case final move?) {
      final unit = _unit(move.when);
      final boundary = ((before / unit).floor() + 1) * unit;
      if (_position >= boundary) _begin(move, late: _position - boundary);
    }
    final wasDucked = _stingers.isNotEmpty;
    _stingers.removeWhere((voice) => !voice.isPlaying);
    if (wasDucked && _stingers.isEmpty) _bed?.fadeTo(1, seconds: _recoverFade);
  }

  MusicMove _moveTo(String track) {
    for (final move in moves) {
      if (move.to == track && move.from == _playing) return move;
    }
    for (final move in moves) {
      if (move.to == track && move.from == null) return move;
    }
    return MusicMove(to: track);
  }

  double _unit(MusicWhen when) {
    final track = tracks[_playing]!; // go() only waits when a track plays.
    return when == MusicWhen.nextBar ? track.bar : track.beat;
  }

  /// Starts [move]'s track. [late] is how far past its beat this is, so the
  /// new track begins where it would have if it had started on time.
  void _begin(MusicMove move, {required double late}) {
    _pending = null;
    final old = _bed;
    old?.stop(fadeSeconds: move.fade);
    _playing = move.to;
    _position = late;
    final level = _stingers.isEmpty ? 1.0 : duck;
    final bed = audio.play(
      tracks[move.to]!.clip,
      bus: bus,
      loop: true,
      volume: move.fade > 0 ? 0 : level,
      offset: late,
    );
    if (move.fade > 0) bed.fadeTo(level, seconds: move.fade);
    _bed = bed;
  }
}
