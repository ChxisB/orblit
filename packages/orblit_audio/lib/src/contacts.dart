import 'audio.dart';
import 'mixer.dart';
import 'spatial.dart';

/// The clip two surfaces make when they meet. A null [b] is any surface, so
/// ("glass", null) covers glass against everything the table does not name.
final class ContactRule {
  const ContactRule(this.a, this.b, this.clip);

  final String a;
  final String? b;
  final String clip;

  bool _fits(String x, String y) =>
      (a == x && (b == null || b == y)) || (a == y && (b == null || b == x));
}

/// The sound of two things touching: a clip chosen by what each is made of,
/// as loud as the touch was hard.
final class ContactSounds {
  ContactSounds(
    this.audio, {
    required this.rules,
    this.bus,
    this.quiet = 0.5,
    this.loud = 10,
    this.limit = 16,
  }) {
    if (!(quiet >= 0 && loud > quiet && loud.isFinite)) {
      throw ArgumentError('Need 0 <= quiet < loud, finite.');
    }
  }

  final Audio audio;

  /// Tried in order, a rule naming both surfaces before one naming a single
  /// surface, whatever the order in the list.
  final List<ContactRule> rules;
  final Bus? bus;

  /// The strength below which a touch is silent, and the one at and above which
  /// it is full volume. The unit is the caller's, such as the newton-seconds a
  /// physics touch event carries.
  final double quiet;
  final double loud;

  /// How many contact sounds may play at once. A pile of crates landing in one
  /// frame is a handful of sounds and not a hundred.
  final int limit;

  final List<Voice> _playing = [];

  /// Plays what [a] and [b] sound like meeting at [strength], from [at].
  /// Nothing plays for a pair no rule covers, a touch below [quiet], or when
  /// [limit] sounds are already going.
  // shortcut: volume is linear in strength, give it a curve if a hard hit and
  // a harder one sound alike.
  void hit(String a, String b, {required double strength, Emitter? at}) {
    _playing.removeWhere((voice) => !voice.isPlaying);
    if (strength < quiet || _playing.length >= limit) return;
    final clip = _clipFor(a, b);
    if (clip == null) return;
    final volume = ((strength - quiet) / (loud - quiet)).clamp(0.0, 1.0);
    _playing.add(audio.play(clip, bus: bus, volume: volume, at: at));
  }

  String? _clipFor(String a, String b) {
    for (final rule in rules) {
      if (rule.b != null && rule._fits(a, b)) return rule.clip;
    }
    for (final rule in rules) {
      if (rule.b == null && rule._fits(a, b)) return rule.clip;
    }
    return null;
  }
}
