import 'dart:typed_data';

import 'package:orblit_scene/orblit_scene.dart' show Values;
import 'package:orblit_sequence/orblit_sequence.dart';

import 'blend.dart';

/// Where a blend is: which state, how far into it, and what it is fading in
/// over.
///
/// All of a blend's playing is here and nowhere else. The blend itself is
/// an asset that never changes, and the player holding one of these holds
/// nothing a place does not say, so a place is enough to carry on from:
/// written into a save file, a character comes back mid-stride; sent as
/// numbers, a character on another machine stands the same way; built by
/// hand, a test can say where a character is and ask what happens next,
/// without playing a frame to get there.
///
/// A fade is a place with another place under it. [from] is what [state] is
/// fading in over, which may itself still be fading in over something else,
/// when a change comes before the last one has finished. No more than
/// [deepest] are kept: past that the oldest is dropped, which it barely
/// shows by then.
class BlendPlace {
  const BlendPlace(
    this.state, {
    this.lap = 0,
    this.from,
    this.faded = 0,
    this.fade = 0,
    this.shape = Easing.smooth,
  });

  /// The most places one place holds, itself included.
  static const int deepest = 4;

  static const int _each = 5;

  /// How many numbers [toNumbers] writes, whatever the place: the arity of a
  /// `float64` component that carries one.
  static const int width = 1 + _each * deepest;

  /// The state being played.
  final String state;

  /// How far into it, in times through: nought on the way in, one at the
  /// end of the first time through, and two and a half halfway through the
  /// third lap of a loop. Times rather than seconds, because a state mixing
  /// a walk and a run is as long as the mix says, and that changes with
  /// the speed.
  final double lap;

  /// What [state] is fading in over, or null when it has the whole say.
  final BlendPlace? from;

  /// Seconds of the fade gone.
  final double faded;

  /// Seconds the fade takes in all.
  final double fade;

  /// How the fade eases.
  final Easing shape;

  /// How much say [state] has over [from]: nought as the fade starts and one
  /// at its end, and one when there is nothing to fade from.
  double get weight => from == null || !(fade > 0)
      ? 1
      : ease(shape, faded / fade).clamp(0.0, 1.0);

  /// Every state being played and how much say it has, newest first. The
  /// says add to one.
  List<(BlendPlace, double)> get shares {
    final out = <(BlendPlace, double)>[];
    var left = 1.0;
    for (BlendPlace? at = this; at != null && left > 0; at = at.from) {
      final say = at.weight;
      out.add((at, left * say));
      left *= 1 - say;
    }
    return out;
  }

  /// How many places this one holds, itself included.
  int get depth => 1 + (from?.depth ?? 0);

  /// Where playing goes on from after a change into [state].
  ///
  /// A [fade] of nothing is a cut, and leaves nothing behind. [inStep]
  /// starts the new state as far through its lap as this one is through
  /// its own, so a walk turning into a run keeps its feet.
  BlendPlace enter(
    String state, {
    double fade = 0,
    Easing shape = Easing.smooth,
    bool inStep = false,
  }) {
    final into = inStep ? lap - lap.floorToDouble() : 0.0;
    if (!(fade > 0) || !fade.isFinite) return BlendPlace(state, lap: into);
    return BlendPlace(
      state,
      lap: into,
      from: _keeping(deepest - 1),
      fade: fade,
      shape: shape,
    );
  }

  /// This place with no more than [places] in it.
  BlendPlace _keeping(int places) {
    final from = this.from;
    if (from == null) return this;
    if (places <= 1) return BlendPlace(state, lap: lap);
    final kept = from._keeping(places - 1);
    if (identical(kept, from)) return this;
    return BlendPlace(
      state,
      lap: lap,
      from: kept,
      faded: faded,
      fade: fade,
      shape: shape,
    );
  }

  Map<String, Object?> toJson() {
    final from = this.from;
    return Values.pruned({
      'state': state,
      'lap': lap,
      if (from != null) ...{
        'fade': fade,
        'faded': faded,
        'shape': shape == Easing.smooth ? null : shape.name,
        'from': from.toJson(),
      },
    });
  }

  /// A place out of decoded JSON, or null when it is not one.
  ///
  /// Read against [blend], because a save file outlives the blend it was
  /// saved with. A state the blend no longer has is dropped with whatever it
  /// was fading from, so a character saved mid-fade into a state that has
  /// since been taken out comes back in the state it was leaving; one saved
  /// in such a state does not come back at all, and the caller starts it
  /// afresh.
  static BlendPlace? fromJson(Object? raw, BlendDocument blend) =>
      _read(raw, blend, deepest);

  static BlendPlace? _read(Object? raw, BlendDocument blend, int places) {
    if (raw is! Map<String, Object?> || places < 1) return null;
    final state = Values.text(raw, 'state');
    if (state == null || blend.stateNamed(state) == null) return null;
    final lap = _lap(Values.maybeNumber(raw, 'lap'));
    final from = _read(raw['from'], blend, places - 1);
    final fade = Values.maybeNumber(raw, 'fade') ?? 0;
    if (from == null || !(fade > 0) || !fade.isFinite) {
      return BlendPlace(state, lap: lap);
    }
    final faded = Values.maybeNumber(raw, 'faded') ?? 0;
    return BlendPlace(
      state,
      lap: lap,
      from: from,
      fade: fade,
      faded: faded.isFinite ? faded.clamp(0.0, fade) : 0,
      shape: Values.named(Easing.values, raw['shape']) ?? Easing.smooth,
    );
  }

  /// The place as [width] numbers, for a component column.
  ///
  /// A state goes by its place in [blend]'s list rather than by name, so
  /// both ends need the same blend. For a save file, which may be read by a
  /// later build with its states moved about, [toJson] names them.
  ///
  /// The first number is how many places there are; then five for each,
  /// newest first: the state, the lap, the fade gone, the fade in all, and
  /// the easing.
  Float64List toNumbers(BlendDocument blend) {
    final out = Float64List(width);
    var count = 0;
    for (BlendPlace? at = this; at != null && count < deepest; at = at.from) {
      final base = 1 + count * _each;
      out[base] = blend.indexOf(at.state).toDouble();
      out[base + 1] = at.lap;
      out[base + 2] = at.faded;
      out[base + 3] = at.fade;
      out[base + 4] = at.shape.index.toDouble();
      count++;
    }
    out[0] = count.toDouble();
    return out;
  }

  /// A place out of [numbers] as [toNumbers] wrote them, or null when they
  /// are not one [blend] can play.
  static BlendPlace? fromNumbers(BlendDocument blend, List<double> numbers) {
    if (numbers.length < width || !numbers[0].isFinite) return null;
    final count = numbers[0].round().clamp(0, deepest);
    BlendPlace? built;
    for (var i = count - 1; i >= 0; i--) {
      final base = 1 + i * _each;
      final index = numbers[base];
      final known =
          index.isFinite &&
          index == index.roundToDouble() &&
          index >= 0 &&
          index < blend.states.length;
      if (!known) {
        // Nothing older than a state this blend has not got can be kept.
        built = null;
        continue;
      }
      final state = blend.states[index.toInt()].name;
      final lap = _lap(numbers[base + 1]);
      final fade = numbers[base + 3];
      if (built == null || !(fade > 0) || !fade.isFinite) {
        built = BlendPlace(state, lap: lap);
        continue;
      }
      final faded = numbers[base + 2];
      final shape = numbers[base + 4];
      built = BlendPlace(
        state,
        lap: lap,
        from: built,
        fade: fade,
        faded: faded.isFinite ? faded.clamp(0.0, fade) : 0,
        shape: shape.isFinite
            ? Easing.values[shape.round().clamp(0, Easing.values.length - 1)]
            : Easing.smooth,
      );
    }
    // Null when the newest state is one the blend has not got: no place at
    // all.
    return built;
  }

  static double _lap(double? raw) =>
      raw != null && raw.isFinite && raw >= 0 ? raw : 0;

  @override
  bool operator ==(Object other) =>
      other is BlendPlace &&
      other.state == state &&
      other.lap == lap &&
      other.faded == faded &&
      other.fade == fade &&
      other.shape == shape &&
      other.from == from;

  @override
  int get hashCode => Object.hash(state, lap, faded, fade, shape, from);

  @override
  String toString() {
    final from = this.from;
    if (from == null) return 'BlendPlace($state at $lap)';
    return 'BlendPlace($state at $lap, $faded of $fade s over $from)';
  }
}
