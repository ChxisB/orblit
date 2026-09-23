import 'package:vector_math/vector_math_64.dart';

import 'easing.dart';

/// How a value gets from one keyframe to the next.
enum Hold {
  /// It does not. The value jumps at the second key and holds until the one
  /// after. What a visibility flag, a material swap or a subtitle line wants.
  step,

  /// Straight there, at a constant rate. Corners at every key, which is
  /// exactly right for anything mechanical and wrong for anything alive.
  linear,

  /// Eased at both ends of each span, so the value arrives and leaves at
  /// rest. The default: it is what somebody means by "animate this".
  smooth,

  /// Eased with the *shape* the key names, so one span can snap and the next
  /// can settle.
  shaped,

  /// Along a curve that leaves this key, and arrives at the next, at the
  /// slopes they name.
  ///
  /// A key that names no slope has one worked out from its neighbours: the
  /// line from the key before it to the key after, so a curve through four
  /// keys flows through the middle two instead of stopping at each. The
  /// first and last keys, and any key that is a peak or a trough, are left at
  /// rest, so the curve never overshoots a value somebody chose.
  ///
  /// What an imported cubic spline is, and what a curve editor's handles set.
  /// A value that cannot curve, a flag, travels as [linear] does.
  curve,
}

/// One value at one moment.
class Key<T> {
  const Key(
    this.at,
    this.value, {
    this.hold = Hold.smooth,
    this.shape,
    this.slopeIn,
    this.slopeOut,
  });

  /// Seconds from the start of the sequence.
  final double at;
  final T value;

  /// How the value travels from *this* key to the next. Held on the earlier
  /// key rather than the later one, because a span belongs to the key that
  /// starts it — inserting a key at the end should not change how the one
  /// before it behaves.
  final Hold hold;

  /// Which easing [Hold.shaped] uses. Ignored otherwise.
  final Easing? shape;

  /// How fast the value is changing as it arrives here, in units a second,
  /// when the span before this key is a [Hold.curve]. Null works one out.
  final T? slopeIn;

  /// How fast it is changing as it leaves, when [hold] is [Hold.curve]. Null
  /// works one out.
  ///
  /// Separate from [slopeIn] so a key can be a corner: a ball's height
  /// arrives at the floor falling and leaves it rising.
  final T? slopeOut;
}

/// How two values of one kind are mixed.
///
/// Separate from the values because mixing is not always what an operator
/// would do: two rotations are mixed by turning between them, not by
/// averaging four numbers, and two flags are not mixed at all.
abstract class Mixer<T> {
  const Mixer();

  /// [a] at nought, [b] at one.
  T lerp(T a, T b, double t);

  /// Several at once, each with a weight. Used where clips overlap.
  T mix(List<T> values, List<double> weights);
}

/// A mixer for values that can travel along a curve: ones that can be added
/// and scaled, which is every kind here but a flag.
abstract interface class CurveMixer<T> implements Mixer<T> {
  /// A value that is not changing.
  T get still;

  /// The slope a curve through [at] should have there, given the keys either
  /// side of it and the [seconds] between them.
  T through(T before, T at, T after, double seconds);

  /// The value [t] of the way from [a] to [b] over [seconds], leaving [a] at
  /// the slope [leave] and arriving at [b] at the slope [arrive].
  T curve(T a, T leave, T b, T arrive, double seconds, double t);
}

/// The four weights of a cubic Hermite curve at [t]: for the start value, the
/// start slope, the end value and the end slope.
({double a, double leave, double b, double arrive}) _hermite(double t) {
  final t2 = t * t;
  final t3 = t2 * t;
  return (
    a: 2 * t3 - 3 * t2 + 1,
    leave: t3 - 2 * t2 + t,
    b: -2 * t3 + 3 * t2,
    arrive: t3 - t2,
  );
}

/// A peak or a trough is left flat. Anything else takes the line from its
/// neighbours, which is the slope that passes through without a kink.
double _clamped(double before, double at, double after, double seconds) {
  if ((at - before) * (after - at) <= 0) return 0;
  return seconds <= 0 ? 0 : (after - before) / seconds;
}

class DoubleMixer extends Mixer<double> implements CurveMixer<double> {
  const DoubleMixer();

  @override
  double get still => 0;

  @override
  double through(double before, double at, double after, double seconds) =>
      _clamped(before, at, after, seconds);

  @override
  double curve(
    double a,
    double leave,
    double b,
    double arrive,
    double seconds,
    double t,
  ) {
    final w = _hermite(t);
    return w.a * a +
        w.leave * leave * seconds +
        w.b * b +
        w.arrive * arrive * seconds;
  }

  @override
  double lerp(double a, double b, double t) => a + (b - a) * t;

  @override
  double mix(List<double> values, List<double> weights) {
    var total = 0.0;
    var sum = 0.0;
    for (var i = 0; i < values.length; i++) {
      sum += values[i] * weights[i];
      total += weights[i];
    }
    return total == 0 ? 0 : sum / total;
  }
}

class Vector3Mixer extends Mixer<Vector3> implements CurveMixer<Vector3> {
  const Vector3Mixer();

  @override
  Vector3 get still => Vector3.zero();

  /// Worked out one axis at a time, so a key that is the top of a jump is
  /// flat in height without also stopping the run forwards.
  @override
  Vector3 through(Vector3 before, Vector3 at, Vector3 after, double seconds) =>
      Vector3(
        _clamped(before.x, at.x, after.x, seconds),
        _clamped(before.y, at.y, after.y, seconds),
        _clamped(before.z, at.z, after.z, seconds),
      );

  @override
  Vector3 curve(
    Vector3 a,
    Vector3 leave,
    Vector3 b,
    Vector3 arrive,
    double seconds,
    double t,
  ) {
    final w = _hermite(t);
    return a.scaled(w.a)
      ..addScaled(leave, w.leave * seconds)
      ..addScaled(b, w.b)
      ..addScaled(arrive, w.arrive * seconds);
  }

  @override
  Vector3 lerp(Vector3 a, Vector3 b, double t) => a + (b - a) * t;

  @override
  Vector3 mix(List<Vector3> values, List<double> weights) {
    final sum = Vector3.zero();
    var total = 0.0;
    for (var i = 0; i < values.length; i++) {
      sum.addScaled(values[i], weights[i]);
      total += weights[i];
    }
    return total == 0 ? Vector3.zero() : sum / total;
  }
}

class QuaternionMixer extends Mixer<Quaternion>
    implements CurveMixer<Quaternion> {
  const QuaternionMixer();

  @override
  Quaternion get still => Quaternion(0, 0, 0, 0);

  /// The line from the key before to the key after, both written the same
  /// way round as [at]. A quaternion and its negative are one rotation, and a
  /// slope taken between two written opposite ways points nowhere useful.
  ///
  /// Not flattened at peaks, because a rotation has no peak: it is the four
  /// numbers together that mean something, not any one of them.
  @override
  Quaternion through(
    Quaternion before,
    Quaternion at,
    Quaternion after,
    double seconds,
  ) {
    if (seconds <= 0) return still;
    final from = _alike(at, before);
    final to = _alike(at, after);
    return Quaternion(
      (to.x - from.x) / seconds,
      (to.y - from.y) / seconds,
      (to.z - from.z) / seconds,
      (to.w - from.w) / seconds,
    );
  }

  /// Four numbers along four curves, then made a rotation again. What glTF
  /// specifies for a cubic spline on a rotation, which matters because that
  /// is where most curved rotations come from.
  @override
  Quaternion curve(
    Quaternion a,
    Quaternion leave,
    Quaternion b,
    Quaternion arrive,
    double seconds,
    double t,
  ) {
    // The short way round, as [lerp] does, and the slope turned with it.
    final flip = _dot(a, b) < 0 ? -1.0 : 1.0;
    final w = _hermite(t);
    final m0 = w.leave * seconds;
    final m1 = w.arrive * seconds * flip;
    final bw = w.b * flip;
    final out = Quaternion(
      w.a * a.x + m0 * leave.x + bw * b.x + m1 * arrive.x,
      w.a * a.y + m0 * leave.y + bw * b.y + m1 * arrive.y,
      w.a * a.z + m0 * leave.z + bw * b.z + m1 * arrive.z,
      w.a * a.w + m0 * leave.w + bw * b.w + m1 * arrive.w,
    );
    if (out.length2 == 0) return a.clone();
    out.normalize();
    return out;
  }

  static double _dot(Quaternion a, Quaternion b) =>
      a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

  /// [q], negated if that is what it takes to be written the same way round
  /// as [like].
  static Quaternion _alike(Quaternion like, Quaternion q) =>
      _dot(like, q) < 0 ? Quaternion(-q.x, -q.y, -q.z, -q.w) : q;

  @override
  Quaternion lerp(Quaternion a, Quaternion b, double t) {
    // The short way round. Without the sign check a turn of a hundred and
    // eighty-one degrees goes the other hundred and seventy-nine.
    final flipped = _alike(a, b);
    final out = Quaternion(
      a.x + (flipped.x - a.x) * t,
      a.y + (flipped.y - a.y) * t,
      a.z + (flipped.z - a.z) * t,
      a.w + (flipped.w - a.w) * t,
    );
    out.normalize();
    return out;
  }

  @override
  Quaternion mix(List<Quaternion> values, List<double> weights) {
    if (values.isEmpty) return Quaternion.identity();
    // Accumulated pairwise rather than summed, because the weighted sum of
    // four quaternions is only a rotation by accident.
    var out = values.first;
    var carried = weights.first;
    for (var i = 1; i < values.length; i++) {
      final total = carried + weights[i];
      if (total <= 0) continue;
      out = lerp(out, values[i], weights[i] / total);
      carried = total;
    }
    return out;
  }
}

/// Flags do not blend. Whichever clip has the most say decides.
class BoolMixer extends Mixer<bool> {
  const BoolMixer();

  @override
  bool lerp(bool a, bool b, double t) => t < 0.5 ? a : b;

  @override
  bool mix(List<bool> values, List<double> weights) {
    var best = false;
    var most = -1.0;
    for (var i = 0; i < values.length; i++) {
      if (weights[i] > most) {
        most = weights[i];
        best = values[i];
      }
    }
    return best;
  }
}

const doubleMixer = DoubleMixer();
const vector3Mixer = Vector3Mixer();
const quaternionMixer = QuaternionMixer();
const boolMixer = BoolMixer();

/// A value over time: keys, and the rule for getting between them.
///
/// Sampling is a pure function of the moment asked for. Nothing here
/// remembers where the playhead was, which is what makes scrubbing backwards
/// give the same answer as playing forwards to the same place.
class Channel<T> {
  Channel(this.keys, this.mixer)
    : assert(keys.isNotEmpty, 'a channel with no keys has no value');

  /// In ascending order of [Key.at]. Not sorted here: a channel is built once
  /// and sampled constantly, and sorting on every sample would be the most
  /// expensive thing in a sequence.
  final List<Key<T>> keys;
  final Mixer<T> mixer;

  double get start => keys.first.at;
  double get end => keys.last.at;

  /// The value at [at]. Before the first key it is the first key's value and
  /// after the last it is the last's — a channel does not extrapolate,
  /// because a curve run off its end is a value nobody chose.
  T at(double at) {
    if (at <= keys.first.at) return keys.first.value;
    if (at >= keys.last.at) return keys.last.value;

    var low = 0;
    var high = keys.length - 1;
    while (high - low > 1) {
      final middle = (low + high) ~/ 2;
      if (keys[middle].at <= at) {
        low = middle;
      } else {
        high = middle;
      }
    }

    final from = keys[low];
    final to = keys[high];
    if (from.hold == Hold.step) return from.value;

    final span = to.at - from.at;
    // Two keys at the same moment: the second wins, which is how a cut is
    // written.
    if (span <= 0) return to.value;

    final part = (at - from.at) / span;
    final curves = mixer;
    if (from.hold == Hold.curve && curves is CurveMixer<T>) {
      return curves.curve(
        from.value,
        from.slopeOut ?? _slopeAt(low, curves),
        to.value,
        to.slopeIn ?? _slopeAt(high, curves),
        span,
        part,
      );
    }
    final shaped = switch (from.hold) {
      Hold.linear || Hold.curve => part,
      Hold.smooth => ease(Easing.inOut, part),
      Hold.shaped => ease(from.shape ?? Easing.inOut, part),
      Hold.step => 0.0,
    };
    return mixer.lerp(from.value, to.value, shaped);
  }

  /// The slope worked out for the key at [index], for a key that names none.
  ///
  /// Public because a curve editor draws it: a handle nobody has dragged
  /// still has a direction, and it should be the one the curve really takes.
  T slopeAt(int index) {
    final curves = mixer;
    if (curves is! CurveMixer<T>) {
      throw StateError('a ${T.toString()} channel has no slopes');
    }
    return _slopeAt(index, curves);
  }

  T _slopeAt(int index, CurveMixer<T> curves) {
    // The ends start and finish at rest: a curve run past its last key is a
    // value nobody chose, and so is a slope that assumes one.
    if (index <= 0 || index >= keys.length - 1) return curves.still;
    final before = keys[index - 1];
    final after = keys[index + 1];
    return curves.through(
      before.value,
      keys[index].value,
      after.value,
      after.at - before.at,
    );
  }
}
