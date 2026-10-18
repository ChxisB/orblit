import 'audio.dart';
import 'mixer.dart';
import 'spatial.dart';

/// A sound a sequence wants playing: which clip, how far into it the playhead
/// is, how loud, and where it comes from. Shaped like `orblit_sequence`'s
/// `SoundAt`, which this package does not import.
final class Cue {
  const Cue(this.clip, this.at, this.gain, {this.binding});

  final String clip;

  /// Seconds into the clip.
  final double at;
  final double gain;

  /// What the sound comes from, or null for a sound that has no place.
  final String? binding;
}

/// Plays the cues of a sequence as the playhead passes them.
///
/// A sequence states what should be sounding at each moment. This turns that
/// into starts and stops: a cue that appears starts a voice, one that goes
/// stops it, and one that stays follows its gain. A cue is started once, so a
/// clip that plays to its end before the sequence does is not started again.
final class CueBoard {
  CueBoard(
    this.audio, {
    this.bus,
    this.beds = const {},
    this.emitterFor,
    this.release = 0.05,
  });

  final Audio audio;
  final Bus? bus;

  /// Clips that are meant to be there whenever the playhead is, such as music
  /// or narration. After a seek they start part-way in. Any other clip is a
  /// one-shot, and a seek into the middle of it starts nothing.
  final Set<String> beds;

  /// Where a bound cue is heard from, or null to hear it from nowhere. Asked
  /// once, when the cue starts.
  final Emitter? Function(String binding)? emitterFor;

  /// Seconds to fade a cue out over when the sequence drops it, so a cut does
  /// not click.
  final double release;

  /// A cue seen and not yet gone. The voice is null for one a seek skipped.
  final Map<(String?, String), Voice?> _voices = {};

  /// Moves on to what the sequence says now. Pass [seeked] when the playhead
  /// jumped rather than ran. Two cues of one clip and binding are one voice, at
  /// the louder gain.
  // shortcut: a clip twice under one binding is a single voice, split them by
  // binding if a sequence needs two at once.
  void update(Iterable<Cue> cues, {bool seeked = false}) {
    final now = <(String?, String), Cue>{};
    for (final cue in cues) {
      final key = (cue.binding, cue.clip);
      final other = now[key];
      if (other == null || cue.gain > other.gain) now[key] = cue;
    }
    for (final key in _voices.keys.toList()) {
      if (now.containsKey(key)) continue;
      _voices.remove(key)?.stop(fadeSeconds: release);
    }
    for (final MapEntry(key: key, value: cue) in now.entries) {
      final known = _voices.containsKey(key);
      final bed = beds.contains(cue.clip);
      if (known && !(seeked && bed)) {
        _voices[key]?.volume = cue.gain;
        continue;
      }
      _voices[key]?.stop(fadeSeconds: release);
      _voices[key] = seeked && !bed ? null : _start(cue);
    }
  }

  /// Fades out every cue, as when the sequence ends.
  void stop() => update(const []);

  Voice _start(Cue cue) {
    final binding = cue.binding;
    return audio.play(
      cue.clip,
      bus: bus,
      volume: cue.gain,
      offset: cue.at,
      at: binding == null ? null : emitterFor?.call(binding),
    );
  }
}
