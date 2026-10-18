import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

SceneDocument documentOf(ParticleEmitterComponent? emitter) => SceneDocument(
  name: 'Scene',
  entities: [
    SceneEntity(
      id: 'fire',
      name: 'Fire',
      components: {
        SceneComponents.transform: TransformComponent(),
        if (emitter != null) SceneComponents.particles: emitter,
      },
    ),
  ],
);

ParticleEmitterComponent read(Map<String, Object?> json) =>
    ParticleEmitterComponent.fromJson(json);

void main() {
  group('reading an emitter', () {
    test('takes the defaults when the file says nothing', () {
      final emitter = read(const {});

      expect(emitter.shape, EmitterShape.point);
      expect(emitter.rate, 10);
      expect(emitter.lifetimeMin, 1);
      expect(emitter.lifetimeMax, 2);
      expect(emitter.size, [0.1]);
      expect(emitter.colour, [1, 1, 1]);
      expect(emitter.duration, isNull);
      expect(emitter.space, EmitterSpace.world);
      expect(emitter.maxParticles, 1000);
      expect(emitter.bursts, isEmpty);
    });

    test('is the emitter it was written as', () {
      final emitter = ParticleEmitterComponent(
        seed: 7,
        shape: EmitterShape.cone,
        radius: 0.2,
        angle: 0.6,
        extents: Vector3(1, 2, 3),
        rate: 0,
        bursts: const [EmitterBurst(0.5, 12), EmitterBurst(2, 3)],
        duration: 4,
        lifetimeMin: 0.4,
        lifetimeMax: 0.9,
        speedMin: 3,
        speedMax: 6,
        spinMin: -1,
        spinMax: 1,
        size: const [0.05, 0.02, 0],
        colour: const [1, 0.9, 0.4, 1, 0.3, 0.1],
        gravity: Vector3(0, -9.8, 0),
        drag: 0.3,
        noiseStrength: 0.1,
        noiseScale: 2,
        space: EmitterSpace.local,
        maxParticles: 200,
      );

      final again = read(emitter.toJson());

      expect(again.toJson(), emitter.toJson());
      expect(again.bursts.last.count, 3);
      expect(again.extents, Vector3(1, 2, 3));
      expect(again.gravity, Vector3(0, -9.8, 0));
      expect(again.space, EmitterSpace.local);
    });

    test('leaves out a duration that is not set', () {
      expect(
        ParticleEmitterComponent().toJson().containsKey('duration'),
        false,
      );
      expect(read(const {'duration': 3}).duration, 3);
    });

    test('keeps a duration of nought, which is no births', () {
      expect(read(const {'duration': 0}).duration, 0);
    });

    test('reads a bad number as the default for that number', () {
      final bad = <String, Object?>{
        'rate': -1,
        'radius': double.nan,
        'angle': 4,
        'drag': -2,
        'noiseStrength': -1,
        'noiseScale': 0,
        'maxParticles': 0,
        'duration': -5,
        'shape': 'blob',
        'space': 'moon',
      };

      final emitter = read(bad);

      expect(emitter.rate, 10);
      expect(emitter.radius, 0.5);
      expect(emitter.angle, 0.4);
      expect(emitter.drag, 0);
      expect(emitter.noiseStrength, 0);
      expect(emitter.noiseScale, 1);
      expect(emitter.maxParticles, 1000);
      expect(emitter.duration, isNull);
      expect(emitter.shape, EmitterShape.point);
      expect(emitter.space, EmitterSpace.world);
    });

    test('reads a seed or gravity that is not finite as the default', () {
      final emitter = read({
        'seed': double.infinity,
        'gravity': [0, double.nan, 0],
      });

      expect(emitter.seed, 0);
      expect(emitter.gravity, Vector3.zero());
    });

    test('reads a range that is not a range as the default range', () {
      final cases = <List<Object?>>[
        [2, 1],
        [0, 1],
        [1],
        ['a', 'b'],
        [1, double.nan],
      ];
      for (final life in cases) {
        final emitter = read({'lifetime': life});
        expect(
          [emitter.lifetimeMin, emitter.lifetimeMax],
          [1, 2],
          reason: '$life',
        );
      }
      expect(
        read(const {
          'speed': [3, 1],
        }).speedMin,
        1,
      );
      expect(
        read(const {
          'spin': [-2, 2],
        }).spinMin,
        -2,
      );
    });

    test('reads a ramp that is not whole as the default ramp', () {
      expect(read(const {'size': <Object?>[]}).size, [0.1]);
      expect(
        read(const {
          'size': [0.1, -1],
        }).size,
        [0.1],
      );
      expect(
        read(const {
          'colour': [1, 1],
        }).colour,
        [1, 1, 1],
      );
      expect(
        read(const {
          'colour': [1, 0, 0, 0, 0, 1],
        }).colour,
        hasLength(6),
      );
    });

    test('drops a burst that is not one and keeps the rest', () {
      final emitter = read(const {
        'bursts': [
          {'time': 1, 'count': 5},
          {'time': -1, 'count': 5},
          {'time': 1, 'count': 0},
          {'time': 1, 'count': 70000},
          'nonsense',
          {'time': 2, 'count': 7},
        ],
      });

      expect(emitter.bursts.map((b) => b.count), [5, 7]);
    });

    test('reads extents with a negative side as the default box', () {
      expect(
        read(const {
          'extents': [1, -1, 1],
        }).extents,
        Vector3.all(0.5),
      );
    });
  });

  group('changing an emitter', () {
    test('changes what it was asked to and keeps the rest', () {
      final before = ParticleEmitterComponent(rate: 5, drag: 0.5);

      final after = before.copyWith(rate: 20, shape: EmitterShape.sphere);

      expect(after.rate, 20);
      expect(after.shape, EmitterShape.sphere);
      expect(after.drag, 0.5);
    });

    test('does not share its vectors with what it was built from', () {
      final g = Vector3(0, -9.8, 0);
      final emitter = ParticleEmitterComponent(gravity: g);
      g.y = 0;
      emitter.copyWith().gravity.y = 5;

      expect(emitter.gravity, Vector3(0, -9.8, 0));
    });
  });

  group('an emitter in a scene', () {
    test('is read back as an emitter, not an unknown component', () {
      final document = documentOf(
        ParticleEmitterComponent(bursts: const [EmitterBurst(1, 4)]),
      );
      final back = SceneDocument.decode(document.encode());

      expect(back.problems, isEmpty);
      expect(
        back.document['fire']![SceneComponents.particles],
        isA<ParticleEmitterComponent>(),
      );
      expect(back.document.encode(), document.encode());
    });

    test('is written after the canvas and before the data', () {
      final order = SceneComponents.order;

      expect(
        order.indexOf(SceneComponents.particles),
        order.indexOf('canvas') + 1,
      );
      expect(
        order.indexOf('data'),
        order.indexOf(SceneComponents.particles) + 1,
      );
    });

    test('adding one is a change that goes back', () {
      final a = documentOf(null);
      final b = documentOf(ParticleEmitterComponent(rate: 40));
      final diff = SceneDiff.between(a, b);

      expect(diff.applyTo(a).encode(), b.encode());
      expect(diff.inverse.applyTo(b).encode(), a.encode());
    });

    test('a changed field is one operation, and it goes back', () {
      final a = documentOf(ParticleEmitterComponent(rate: 1));
      final b = documentOf(ParticleEmitterComponent(rate: 2));
      final diff = SceneDiff.between(a, b);

      expect((diff.operations.single as SetField).field, 'rate');
      expect(diff.applyTo(a).encode(), b.encode());
      expect(diff.inverse.applyTo(b).encode(), a.encode());
    });
  });
}
