import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_particles/orblit_particles.dart'
    show Emitter, EmitterSampling, ParticleBuffer;
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

SceneEntity emitterAt(
  String id,
  ParticleEmitterComponent emitter, {
  double x = 0,
  bool visible = true,
}) => SceneEntity(
  id: id,
  name: id,
  visible: visible,
  components: {
    SceneComponents.transform: TransformComponent(position: Vector3(x, 0, 0)),
    SceneComponents.particles: emitter,
  },
);

/// Ten born a second, each living five and going nowhere, so a count says how
/// long the clock has run and a place says where the entity was.
final still = ParticleEmitterComponent(
  lifetimeMin: 5,
  lifetimeMax: 5,
  speedMin: 0,
  speedMax: 0,
);

OrblitDocumentView viewOf(List<SceneEntity> entities) =>
    OrblitDocumentView(SceneDocument(entities: entities));

void change(OrblitDocumentView view, SceneEntity entity) =>
    view.replace(view.document.withEntity(entity.id, entity));

List<double> xs(OrblitPopulation population) => [
  for (var i = 0; i < population.count; i++) population.transforms[i * 16 + 12],
];

void main() {
  test('an emitter is one population, keyed from the key', () {
    final view = viewOf([emitterAt('a', still)]);
    final particles = OrblitParticles(view, key: 700);

    particles.advance(3);

    expect(particles.populations, hasLength(1));
    expect(particles.populations.single.key, 700);
    expect(particles.populations.single.count, 31);
    expect(particles.populations.single.mesh, isNull);
  });

  test('a default component is drawn as a default emitter', () {
    final view = viewOf([emitterAt('a', ParticleEmitterComponent())]);
    final particles = OrblitParticles(view, key: 0);
    final wanted = ParticleBuffer(1000);

    particles.advance(1.7);
    Emitter().sample(1.7, wanted);

    final drawn = particles.populations.single;
    expect(drawn.count, wanted.count);
    for (var i = 0; i < wanted.count; i++) {
      expect(drawn.transforms[i * 16 + 12], wanted.positions[i * 3]);
      expect(drawn.transforms[i * 16 + 13], wanted.positions[i * 3 + 1]);
      expect(drawn.transforms[i * 16 + 14], wanted.positions[i * 3 + 2]);
    }
  });

  test('a particle faces the camera at its size', () {
    final view = viewOf([emitterAt('a', still)]);
    view.through = OrblitCamera(
      position: Vector3(0, 0, 10),
      target: Vector3.zero(),
    );
    final particles = OrblitParticles(view, key: 0)..advance(1);

    final t = particles.populations.single.transforms;
    Vector3 column(int c) => Vector3(t[c], t[c + 1], t[c + 2]);
    expect(column(8).x, closeTo(0, 1e-6));
    expect(column(8).y, closeTo(0, 1e-6));
    expect(column(8).z, closeTo(0.1, 1e-6));
    expect(column(0).length, closeTo(0.1, 1e-6));
    expect(column(4).length, closeTo(0.1, 1e-6));
    expect(column(0).dot(column(4)), closeTo(0, 1e-6));
  });

  test('looking straight down still gives a turned square', () {
    final view = viewOf([emitterAt('a', still)]);
    view.through = OrblitCamera(
      position: Vector3(0, 10, 0),
      target: Vector3.zero(),
    );
    final particles = OrblitParticles(view, key: 0)..advance(1);

    final t = particles.populations.single.transforms;
    expect(t.every((v) => v.isFinite), isTrue);
    expect(t[9], closeTo(0.1, 1e-6));
  });

  test('the box holds every particle with its size', () {
    final view = viewOf([
      emitterAt(
        'a',
        ParticleEmitterComponent(
          shape: EmitterShape.sphere,
          radius: 3,
          size: const [0.4],
        ),
      ),
    ]);
    final particles = OrblitParticles(view, key: 0)..advance(2);

    final drawn = particles.populations.single;
    for (var i = 0; i < drawn.count; i++) {
      for (var a = 0; a < 3; a++) {
        final at = drawn.transforms[i * 16 + 12 + a];
        expect(at - 0.4, greaterThanOrEqualTo(drawn.minimum.storage[a] - 1e-6));
        expect(at + 0.4, lessThanOrEqualTo(drawn.maximum.storage[a] + 1e-6));
      }
    }
  });

  group('the clock', () {
    test('starts again when the entity is hidden and shown', () {
      final view = viewOf([emitterAt('a', still)]);
      final particles = OrblitParticles(view, key: 0)..advance(3);
      expect(particles.populations.single.count, 31);

      change(view, emitterAt('a', still, visible: false));
      particles.advance(1);
      expect(particles.populations, isEmpty);

      change(view, emitterAt('a', still));
      particles.advance(1);
      expect(particles.populations.single.count, 11);
    });

    test('is kept when the component is edited', () {
      final view = viewOf([emitterAt('a', still)]);
      final particles = OrblitParticles(view, key: 0)..advance(3);

      change(view, emitterAt('a', still.copyWith(rate: 20)));
      particles.advance(0);

      expect(particles.populations.single.count, 61);
    });

    test('does not move for a step that is not a time', () {
      final view = viewOf([emitterAt('a', still)]);
      final particles = OrblitParticles(view, key: 0)..advance(1);

      for (final step in [double.nan, -1.0, double.infinity]) {
        particles.advance(step);
        expect(particles.populations.single.count, 11, reason: '$step');
      }
    });

    test('moves on a revision each frame', () {
      final view = viewOf([emitterAt('a', still)]);
      final particles = OrblitParticles(view, key: 0)..advance(1);
      final first = particles.populations.single.revision;

      particles.advance(0);

      expect(particles.populations.single.revision, greaterThan(first));
    });
  });

  group('where the entity has been', () {
    test('a world particle stays where it was born', () {
      final view = viewOf([emitterAt('a', still)]);
      final particles = OrblitParticles(view, key: 0)..advance(1);
      change(view, emitterAt('a', still, x: 10));
      particles.advance(1);
      particles.advance(1);

      final where = xs(particles.populations.single);
      expect(where.where((x) => (x - 0).abs() < 1e-6), hasLength(20));
      expect(where.where((x) => (x - 10).abs() < 1e-6), hasLength(11));
    });

    test('a local particle goes with the entity', () {
      final local = still.copyWith(space: EmitterSpace.local);
      final view = viewOf([emitterAt('a', local)]);
      final particles = OrblitParticles(view, key: 0)..advance(2);
      change(view, emitterAt('a', local, x: 10));
      particles.advance(1);

      expect(xs(particles.populations.single).every((x) => x == 10), isTrue);
    });

    test('an entity that never moves keeps one place', () {
      final view = viewOf([emitterAt('a', still, x: 4)]);
      final particles = OrblitParticles(view, key: 0);
      for (var i = 0; i < 100; i++) {
        particles.advance(0.1);
      }

      expect(xs(particles.populations.single).every((x) => x == 4), isTrue);
    });
  });

  group('a crowd', () {
    test('is cut to the budget, the later emitters first', () {
      final view = viewOf([emitterAt('a', still), emitterAt('b', still)]);
      final particles = OrblitParticles(view, key: 0, budget: 40)..advance(3);

      expect(particles.populations.map((p) => p.count), [31, 9]);
      expect(particles.populations.map((p) => p.key).toSet(), hasLength(2));
    });

    test('has no population for an emitter with nothing alive', () {
      final view = viewOf([
        emitterAt('a', still.copyWith(rate: 0)),
        emitterAt('b', still),
      ]);
      final particles = OrblitParticles(view, key: 0)..advance(1);

      expect(particles.populations, hasLength(1));
    });

    test('is not drawn from an entity with no emitter', () {
      final view = viewOf([SceneEntity(id: 'a', name: 'a', components: {})]);
      final particles = OrblitParticles(view, key: 0)..advance(1);

      expect(particles.populations, isEmpty);
    });
  });

  test('a component that makes no emitter is named and left out', () {
    final view = viewOf([
      emitterAt('bad', ParticleEmitterComponent(drag: -1)),
      emitterAt('good', still),
    ]);
    final particles = OrblitParticles(view, key: 0)..advance(1);

    expect(particles.populations, hasLength(1));
    expect(particles.rejected.keys, ['bad']);
    expect(particles.rejected['bad'], contains('drag'));

    change(view, emitterAt('bad', still));
    particles.advance(0);

    expect(particles.rejected, isEmpty);
    expect(particles.populations, hasLength(2));
  });

  test('the view\'s camera is the one it looks through', () {
    final view = viewOf([]);
    final lens = OrblitCamera(
      position: Vector3(1, 2, 3),
      target: Vector3.zero(),
    );

    view.through = lens;

    expect(view.camera, same(lens));
  });
}
