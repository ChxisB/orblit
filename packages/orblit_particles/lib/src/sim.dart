import 'dart:math' as math;

import 'package:orblit_noise/orblit_noise.dart';
import 'package:vector_math/vector_math_64.dart';

import 'buffer.dart';
import 'emitter.dart';
import 'random.dart';
import 'swirl.dart';

/// Where a segment meets a surface and which way that surface faces.
final class Hit {
  const Hit(this.point, this.normal);

  final Vector3 point;
  final Vector3 normal;
}

/// Answers whether the segment from [from] to [to] meets anything. The two
/// vectors are reused, so a caller must not keep them. Points are in the space
/// the particles move in, which is the world for [ParticleSpace.world].
typedef HitTest = Hit? Function(Vector3 from, Vector3 to);

// shortcut: a fixed gap off the surface so the next segment starts clear of it.
// Make it a field if a game works at a very different scale.
const double _skin = 1e-4;

final class _Live {
  _Live(this.id, this.born, this.life);

  final int id;
  final double born;
  final double life;
  final Vector3 position = Vector3.zero();
  final Vector3 velocity = Vector3.zero();
  double age = 0;
  bool dead = false;
}

/// An [Emitter] that keeps its particles and steps them forward, for what a
/// pure function cannot do: bounce off the world and be pushed about by
/// turbulence that depends on where a particle has been.
///
/// It is opt in. Two peers that step it with different time slices drift
/// apart, so it is for looks and not for anything a game decides on.
final class ParticleSim {
  ParticleSim(this.emitter, {this.hit, this.bounce, this.turbulence = 0})
    : _noise = GradientNoise(seed: emitter.seed) {
    if (bounce != null && !(bounce! >= 0 && bounce! <= 1)) {
      throw ArgumentError.value(bounce, 'bounce', 'must be 0 to 1');
    }
    if (!(turbulence >= 0 && turbulence.isFinite)) {
      throw ArgumentError.value(turbulence, 'turbulence', 'must be 0 or more');
    }
  }

  final Emitter emitter;
  final HitTest? hit;

  /// Share of speed a particle keeps off a surface, or null to die on it.
  final double? bounce;

  /// Metres per second squared of smooth push about.
  final double turbulence;

  final GradientNoise _noise;
  final List<_Live> _live = [];
  late final List<bool> _fired = List.filled(emitter.bursts.length, false);
  final Vector3 _to = Vector3.zero();
  final Vector3 _push = Vector3.zero();
  double _time = 0;
  int _next = 0;

  int get count => _live.length;

  /// Moves on by [dt] seconds. [place] is where the emitter is, as for
  /// `sample`.
  void step(double dt, {Placement? place}) {
    if (!(dt >= 0 && dt.isFinite)) {
      throw ArgumentError.value(dt, 'dt', 'must be 0 or more');
    }
    final old = _live.length;
    _time += dt;
    _birthAll(place);
    for (var i = 0; i < _live.length; i++) {
      final p = _live[i];
      _advance(p, i < old ? dt : _time - p.born);
    }
    _live.removeWhere((p) => p.dead);
    final excess = _live.length - emitter.maxParticles;
    if (excess > 0) _live.removeRange(0, excess);
  }

  /// Fills [out] with the particles alive now.
  void fill(ParticleBuffer out, {Placement? place}) {
    out.count = 0;
    final matrix = emitter.space == ParticleSpace.local
        ? place?.call(_time)
        : null;
    for (final p in _live) {
      if (out.count == out.capacity) break;
      final slot = out.count++;
      _to.setFrom(p.position);
      matrix?.transform3(_to);
      _to.copyIntoArray(out.positions, slot * 3);
      emitter.look(out, slot, p.id, p.age, p.life);
    }
  }

  void _birthAll(Placement? place) {
    final rate = emitter.rate;
    final end = emitter.duration ?? double.infinity;
    // Those born more than a longest life ago are dead on arrival, so a long
    // pause does not birth them only to drop them.
    _next = math.max(_next, ((_time - emitter.lifetime.max) * rate).floor());
    while (rate > 0 && _next / rate <= math.min(_time, end)) {
      _birth(_next, _next / rate, place);
      _next++;
    }
    for (var index = 0; index < emitter.bursts.length; index++) {
      final burst = emitter.bursts[index];
      if (_fired[index] || burst.time > _time) continue;
      _fired[index] = true;
      for (var number = 0; number < burst.count; number++) {
        _birth(Emitter.burstId(index, number), burst.time, place);
      }
    }
  }

  void _birth(int id, double born, Placement? place) {
    final p = _Live(id, born, emitter.lifeOf(id));
    emitter.birth(id, p.position, p.velocity);
    final matrix = emitter.space == ParticleSpace.world
        ? place?.call(born)
        : null;
    matrix?.transform3(p.position);
    matrix?.rotate3(p.velocity);
    _live.add(p);
  }

  void _advance(_Live p, double span) {
    p.age += span;
    if (p.age >= p.life) {
      p.dead = true;
      return;
    }
    p.velocity.addScaled(emitter.gravity, span);
    if (turbulence > 0) {
      final scale = emitter.noiseScale;
      final x = p.position.x * scale + unitAt(emitter.seed, p.id, 9) * 64;
      swirl(
        _noise,
        x,
        p.position.y * scale,
        p.position.z * scale + _time * scale,
        _push,
      );
      p.velocity.addScaled(_push, turbulence * span);
    }
    p.velocity.scale(math.exp(-emitter.drag * span));
    _to
      ..setFrom(p.position)
      ..addScaled(p.velocity, span);
    _land(p);
  }

  void _land(_Live p) {
    final found = hit?.call(p.position, _to);
    if (found == null) {
      p.position.setFrom(_to);
    } else if (bounce case final keep?) {
      final into = p.velocity.dot(found.normal);
      if (into < 0) p.velocity.addScaled(found.normal, -(1 + keep) * into);
      p.position
        ..setFrom(found.point)
        ..addScaled(found.normal, _skin);
    } else {
      p.dead = true;
    }
  }
}
