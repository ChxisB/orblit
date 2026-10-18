import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_particles/orblit_particles.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

import 'document_view.dart';

/// The particles of a staged scene.
///
/// A [ParticleEmitterComponent] gives off particles from where its entity is,
/// for as long as the entity is shown. Each entity has its own clock. It
/// starts at 0 when the entity is first shown, and starts again if the entity
/// is hidden and shown. A change to the component keeps the clock, so an
/// effect being edited does not restart under the artist's hands.
///
/// Call [advance] once a frame and draw [populations]. Each emitter is one
/// population, keyed from [key] up, and is left out while it has no particle.
/// A particle is a cube that faces the camera, turned by its angle and as wide
/// as its size, so a size ramp that ends at 0 is how one fades. A population
/// has no alpha, no material and no flipbook, and the built-in cube is lit, so
/// smoke is a grey that shrinks and not a soft cloud.
///
/// [budget] is how many particles the whole scene may draw. The emitters that
/// started first keep theirs, and the later ones are cut to what is left.
///
/// A component that does not make an emitter, which only one built in code can
/// be, is left out and named in [rejected] until the scene changes.
final class OrblitParticles {
  OrblitParticles(this.view, {required this.key, this.budget = 20000});

  final OrblitDocumentView view;
  final int key;
  final int budget;

  final Map<String, _Live> _live = {};
  final Map<String, int> _keys = {};
  final Map<String, String> _rejected = {};
  List<OrblitPopulation> _drawn = const [];
  SceneDocument? _synced;
  int _revision = 0;

  /// What to draw this frame.
  List<OrblitPopulation> get populations => _drawn;

  /// Why an entity's emitter was left out, by entity id.
  Map<String, String> get rejected => Map.unmodifiable(_rejected);

  /// Moves every emitter's clock on by [seconds] and works out what is alive.
  void advance(double seconds) {
    if (!identical(view.document, _synced)) _reconcile(_emitters());
    final step = seconds.isFinite && seconds > 0 ? seconds : 0.0;
    final facing = _Facing(view.camera);
    final drawn = <OrblitPopulation>[];
    var left = budget;
    for (final MapEntry(key: id, value: live) in _live.entries) {
      live.advance(step, view.worldOf(id));
      if (left == 0) continue;
      final count = live.fill(facing, left);
      left -= count;
      if (count > 0) drawn.add(_populationOf(id, live, count));
    }
    _drawn = drawn;
  }

  /// The emitters of the shown entities, by id.
  Map<String, ParticleEmitterComponent> _emitters() {
    final found = <String, ParticleEmitterComponent>{};
    for (final entity in view.document.entities) {
      final emitter = entity[SceneComponents.particles];
      if (emitter is ParticleEmitterComponent && view.isShown(entity.id)) {
        found[entity.id] = emitter;
      }
    }
    return found;
  }

  void _reconcile(Map<String, ParticleEmitterComponent> wanted) {
    _synced = view.document;
    _rejected.clear();
    _live.removeWhere((id, _) => !wanted.containsKey(id));
    for (final MapEntry(key: id, value: component) in wanted.entries) {
      if (identical(_live[id]?.component, component)) continue;
      _apply(id, component);
    }
  }

  void _apply(String id, ParticleEmitterComponent component) {
    try {
      final emitter = _emitterOf(component);
      final capacity = math.min(component.maxParticles, budget);
      final live = _live[id];
      if (live == null) {
        _live[id] = _Live(component, emitter, capacity);
      } else {
        live.use(component, emitter, capacity);
      }
    } on ArgumentError catch (error) {
      _live.remove(id);
      _rejected[id] = '$error';
    }
  }

  OrblitPopulation _populationOf(String id, _Live live, int count) =>
      OrblitPopulation(
        key: key + _keys.putIfAbsent(id, () => _keys.length),
        transforms: Float32List.sublistView(live.transforms, 0, count * 16),
        colours: Float32List.sublistView(live.buffer.colours, 0, count * 3),
        minimum: live.minimum,
        maximum: live.maximum,
        fade: OrblitFade.none,
        revision: ++_revision,
        receiveShadows: false,
      );
}

Emitter _emitterOf(ParticleEmitterComponent c) => Emitter(
  seed: c.seed,
  shape: switch (c.shape) {
    EmitterShape.point => const PointShape(),
    EmitterShape.sphere => SphereShape(radius: c.radius),
    EmitterShape.cone => ConeShape(angle: c.angle, radius: c.radius),
    EmitterShape.box => BoxShape(c.extents),
  },
  rate: c.rate,
  bursts: [for (final burst in c.bursts) Burst(burst.time, burst.count)],
  duration: c.duration,
  lifetime: Range(c.lifetimeMin, c.lifetimeMax),
  speed: Range(c.speedMin, c.speedMax),
  size: c.size,
  colour: c.colour,
  gravity: c.gravity,
  drag: c.drag,
  noiseStrength: c.noiseStrength,
  noiseScale: c.noiseScale,
  spin: Range(c.spinMin, c.spinMax),
  space: switch (c.space) {
    EmitterSpace.world => ParticleSpace.world,
    EmitterSpace.local => ParticleSpace.local,
  },
  maxParticles: c.maxParticles,
);

/// One emitter that is running: its clock, where its entity has been, and the
/// buffers it is drawn from.
final class _Live {
  _Live(this.component, this.emitter, int capacity)
    : buffer = ParticleBuffer(capacity),
      transforms = Float32List(capacity * 16);

  ParticleEmitterComponent component;
  Emitter emitter;
  ParticleBuffer buffer;
  Float32List transforms;
  Vector3 minimum = Vector3.zero();
  Vector3 maximum = Vector3.zero();

  double _time = 0;
  final List<double> _times = [];
  final List<Matrix4> _worlds = [];

  /// Swaps in a changed component and keeps the clock and the history.
  void use(ParticleEmitterComponent next, Emitter made, int capacity) {
    component = next;
    emitter = made;
    if (buffer.capacity == capacity) return;
    buffer = ParticleBuffer(capacity);
    transforms = Float32List(capacity * 16);
  }

  void advance(double step, Matrix4 world) {
    _time += step;
    _remember(world);
  }

  /// Samples the emitter, writes a camera-facing transform for each particle
  /// and the box around them. Returns how many are alive, at most [limit].
  int fill(_Facing facing, int limit) {
    emitter.sample(_time, buffer, place: _worldAt, limit: limit);
    final count = buffer.count;
    final low = Vector3.all(double.infinity);
    final high = Vector3.all(double.negativeInfinity);
    var widest = 0.0;
    for (var i = 0; i < count; i++) {
      final size = buffer.sizes[i];
      widest = math.max(widest, size);
      final turn = buffer.angles[i];
      final cos = math.cos(turn);
      final sin = math.sin(turn);
      final o = i * 16;
      for (var a = 0; a < 3; a++) {
        final x = facing.x.storage[a];
        final y = facing.y.storage[a];
        final at = buffer.positions[i * 3 + a];
        transforms[o + a] = (x * cos + y * sin) * size;
        transforms[o + 4 + a] = (y * cos - x * sin) * size;
        transforms[o + 8 + a] = facing.z.storage[a] * size;
        transforms[o + 12 + a] = at;
        low.storage[a] = math.min(low.storage[a], at);
        high.storage[a] = math.max(high.storage[a], at);
      }
      transforms[o + 15] = 1;
      transforms[o + 3] = transforms[o + 7] = transforms[o + 11] = 0;
    }
    // A cube's corner is 0.87 of its width from its centre.
    minimum = low..sub(Vector3.all(widest));
    maximum = high..add(Vector3.all(widest));
    return count;
  }

  void _remember(Matrix4 world) {
    final moved = _worlds.isEmpty || !_same(_worlds.last, world);
    if (moved) {
      _times.add(_time);
      _worlds.add(world.clone());
    }
    // Keep the entry at or before the oldest birth that can still be alive,
    // since a particle born between two entries is placed by the earlier one.
    final oldest = _time - component.lifetimeMax;
    while (_times.length > 1 && _times[1] <= oldest) {
      _times.removeAt(0);
      _worlds.removeAt(0);
    }
  }

  // shortcut: stepped to the last frame's place, so a fast emitter leaves a
  // staircase. Blend the two entries around [time] when that shows.
  Matrix4 _worldAt(double time) {
    var low = 0;
    var high = _times.length - 1;
    while (low < high) {
      final mid = (low + high + 1) >> 1;
      if (_times[mid] <= time) {
        low = mid;
      } else {
        high = mid - 1;
      }
    }
    return _worlds[low];
  }

  static bool _same(Matrix4 a, Matrix4 b) {
    for (var i = 0; i < 16; i++) {
      if (a.storage[i] != b.storage[i]) return false;
    }
    return true;
  }
}

/// The axes a particle is built on so that it faces the camera, one for the
/// whole view.
final class _Facing {
  _Facing(OrblitCamera camera)
    : z = _towards(camera),
      x = Vector3.zero(),
      y = Vector3.zero() {
    x.setFrom(Vector3(0, 1, 0).cross(z));
    // Looking straight up or down, where "up" says nothing about turn.
    if (x.length2 < 1e-12) x.setValues(1, 0, 0);
    x.normalize();
    y.setFrom(z.cross(x));
  }

  final Vector3 x;
  final Vector3 y;
  final Vector3 z;

  static Vector3 _towards(OrblitCamera camera) {
    final back = camera.position - camera.target;
    if (back.length2 == 0) return Vector3(0, 0, 1);
    return back..normalize();
  }
}
