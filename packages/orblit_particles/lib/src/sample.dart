import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_noise/orblit_noise.dart';
import 'package:vector_math/vector_math_64.dart';

import 'buffer.dart';
import 'emitter.dart';
import 'random.dart';
import 'swirl.dart';

/// The frame an emitter makes at a time.
extension EmitterSampling on Emitter {
  /// Fills [out] with every particle alive [time] seconds after the emitter
  /// began.
  ///
  /// Nothing is kept between calls. A particle is found from its id and its
  /// age, so a seek, a pause and a replay all give the same frame. [limit]
  /// lowers the cap below [maxParticles], for a scene that shares a budget.
  void sample(double time, ParticleBuffer out, {Placement? place, int? limit}) {
    out.count = 0;
    if (!(time >= 0 && time.isFinite)) return;
    final cap = math.min(
      math.min(limit ?? maxParticles, maxParticles),
      out.capacity,
    );
    if (cap > 0) _Frame(this, time, out, place).fill(cap);
  }
}

const double _negligibleDrag = 1e-6;

typedef _Visit = void Function(int id, double born, double life);

final class _Frame {
  _Frame(this.emitter, this.time, this.out, this.place)
    : _noise = GradientNoise(seed: emitter.seed);

  final Emitter emitter;
  final double time;
  final ParticleBuffer out;
  final Placement? place;
  final GradientNoise _noise;
  final Vector3 _start = Vector3.zero();
  final Vector3 _position = Vector3.zero();
  final Vector3 _velocity = Vector3.zero();
  final Vector3 _push = Vector3.zero();

  void fill(int cap) {
    var alive = 0;
    _each((id, born, life) => alive++);
    var cutoff = double.infinity;
    var ties = 0;
    if (alive > cap) {
      final ages = Float64List(alive);
      var next = 0;
      _each((id, born, life) => ages[next++] = time - born);
      ages.sort();
      cutoff = ages[cap - 1];
      ties = cap - ages.where((age) => age < cutoff).length;
    }
    _each((id, born, life) {
      final age = time - born;
      if (age > cutoff || (age == cutoff && ties-- <= 0)) return;
      _write(id, born, life);
    });
  }

  /// Offers every particle that could be alive. Those born long enough ago to
  /// be dead are never looked at.
  void _each(_Visit visit) {
    final rate = emitter.rate;
    if (rate > 0) {
      final until = math.min(time, emitter.duration ?? time);
      final first = math.max(0, ((time - emitter.lifetime.max) * rate).floor());
      for (var id = first; id <= (until * rate).floor(); id++) {
        _offer(id, id / rate, visit);
      }
    }
    for (var index = 0; index < emitter.bursts.length; index++) {
      final burst = emitter.bursts[index];
      if (burst.time > time || burst.time + emitter.lifetime.max <= time) {
        continue;
      }
      for (var number = 0; number < burst.count; number++) {
        _offer(Emitter.burstId(index, number), burst.time, visit);
      }
    }
  }

  void _offer(int id, double born, _Visit visit) {
    final life = emitter.lifeOf(id);
    final age = time - born;
    if (age >= 0 && age < life) visit(id, born, life);
  }

  void _write(int id, double born, double life) {
    final age = time - born;
    final world = emitter.space == ParticleSpace.world;
    emitter.birth(id, _position, _velocity);
    if (world) _place(born);
    _start.setFrom(_position);
    _fly(age);
    _wander(id, age);
    if (!world) _place(time);
    final slot = out.count++;
    _position.copyIntoArray(out.positions, slot * 3);
    emitter.look(out, slot, id, age, life);
  }

  void _place(double at) {
    final matrix = place?.call(at);
    if (matrix == null) return;
    matrix.transform3(_position);
    matrix.rotate3(_velocity);
  }

  /// Moves the particle on from its birth by [age] seconds, in the closed form
  /// of constant gravity and drag, so no step is taken.
  void _fly(double age) {
    final gravity = emitter.gravity;
    final drag = emitter.drag;
    // Below this the closed form loses its digits to cancellation, and the
    // drag-free path is off by less than a hair.
    if (drag < _negligibleDrag) {
      _position
        ..addScaled(_velocity, age)
        ..addScaled(gravity, 0.5 * age * age);
      return;
    }
    final slide = (1 - math.exp(-drag * age)) / drag;
    _position
      ..addScaled(gravity, (age - slide) / drag)
      ..addScaled(_velocity, slide);
  }

  /// A smooth push about, which depends on where the particle is and not on
  /// where it has been. The push a newborn would get is taken off, so a
  /// particle starts on the shape and drifts from there.
  void _wander(int id, double age) {
    final strength = emitter.noiseStrength;
    if (strength == 0) return;
    _swirl(id, _position, age);
    _position.addScaled(_push, strength);
    _swirl(id, _start, 0);
    _position.addScaled(_push, -strength);
  }

  void _swirl(int id, Vector3 at, double age) {
    final scale = emitter.noiseScale;
    swirl(
      _noise,
      at.x * scale + unitAt(emitter.seed, id, 9) * 64,
      at.y * scale,
      at.z * scale + age * scale,
      _push,
    );
  }
}
