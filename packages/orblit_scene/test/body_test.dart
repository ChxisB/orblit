import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

SceneDocument documentOf(List<SceneEntity> entities) =>
    SceneDocument(name: 'Scene', entities: entities);

SceneEntity crate(BodyComponent body) => SceneEntity(
  id: 'crate',
  name: 'Crate',
  components: {
    SceneComponents.transform: TransformComponent(),
    SceneComponents.body: body,
  },
);

BodyComponent bodyOf(SceneDocument document) =>
    document['crate']![SceneComponents.body]! as BodyComponent;

void main() {
  group('reading a body', () {
    test('says what it defaults to when the file says nothing', () {
      final body = BodyComponent.fromJson(const {});

      expect(body.shape, BodyShape.box);
      expect(body.size, Vector3.all(1));
      expect(body.radius, 0.5);
      expect(body.height, 2);
      expect(body.centre, Vector3.zero());
      expect(body.motion, BodyMotion.free);
      expect(body.mass, 1);
      expect(body.friction, 0.5);
      expect(body.restitution, 0);
      expect(body.linearDamping, 0.05);
      expect(body.angularDamping, 0.05);
      expect(body.layers, 1);
      expect(body.cares, BodyComponent.everyLayer);
      expect(body.startsAsleep, isFalse);
      expect(body.trigger, isFalse);
      expect(body.stay, isFalse);
      expect(body.surface, Vector3.zero());
    });

    test('is the body it was written as', () {
      final body = BodyComponent(
        shape: BodyShape.capsule,
        size: Vector3(1, 2, 3),
        radius: 0.3,
        height: 1.8,
        centre: Vector3(0, 0.9, 0),
        motion: BodyMotion.driven,
        mass: 80,
        friction: 0.9,
        restitution: 0.1,
        linearDamping: 0.2,
        angularDamping: 0.4,
        layers: 4,
        cares: 3,
        startsAsleep: true,
        trigger: true,
        stay: true,
        surface: Vector3(2, 0, -1),
      );
      final read = BodyComponent.fromJson(body.toJson());

      expect(read.toJson(), body.toJson());
      expect(read.shape, BodyShape.capsule);
      expect(read.centre, Vector3(0, 0.9, 0));
      expect(read.motion, BodyMotion.driven);
      expect(read.startsAsleep, isTrue);
      expect(read.trigger, isTrue);
      expect(read.stay, isTrue);
      expect(read.surface, Vector3(2, 0, -1));
    });

    test('a file from before triggers and belts reads as a plain body', () {
      final body = BodyComponent.fromJson(const {'mass': 5, 'asleep': true});

      expect(body.trigger, isFalse);
      expect(body.stay, isFalse);
      expect(body.surface, Vector3.zero());
    });

    test('a shape or a motion it does not know falls back', () {
      final body = BodyComponent.fromJson(const {
        'shape': 'cylinder',
        'motion': 'kinematic',
      });

      expect(body.shape, BodyShape.box);
      expect(body.motion, BodyMotion.free);
    });

    test('a layer mask is kept to thirty-two bits', () {
      final body = BodyComponent.fromJson(const {'layers': -1, 'cares': 2.0});

      expect(body.layers, BodyComponent.everyLayer);
      expect(body.cares, 2);
    });
  });

  group('changing a body', () {
    test('changes what it was asked to and keeps the rest', () {
      final before = BodyComponent(
        shape: BodyShape.capsule,
        size: Vector3(1, 2, 3),
        radius: 0.25,
        mass: 70,
        layers: 4,
        startsAsleep: true,
      );
      final after = before.copyWith(mass: 80, motion: BodyMotion.driven);

      expect(after.mass, 80);
      expect(after.motion, BodyMotion.driven);
      expect(after.toJson(), {
        ...before.toJson(),
        'mass': 80.0,
        'motion': 'driven',
      });
    });

    test('does not share its vectors with the body it came from', () {
      final before = BodyComponent(size: Vector3(1, 2, 3));
      final after = before.copyWith();
      after.size.x = 9;
      after.centre.y = 9;
      after.surface.z = 9;

      expect(before.size, Vector3(1, 2, 3));
      expect(before.centre, Vector3.zero());
      expect(before.surface, Vector3.zero());
    });

    test('turns a body into a trigger and a belt', () {
      final belt = BodyComponent(
        motion: BodyMotion.fixed,
      ).copyWith(trigger: true, stay: true, surface: Vector3(0, 0, 3));

      expect(belt.trigger, isTrue);
      expect(belt.stay, isTrue);
      expect(belt.surface, Vector3(0, 0, 3));
      expect(belt.copyWith(mass: 2).surface, Vector3(0, 0, 3));
    });
  });

  group('a body in a scene', () {
    test('keeps the size of a shape it is not using', () {
      final box = BodyComponent(size: Vector3(2, 1, 4));
      final ball = BodyComponent.fromJson({
        ...box.toJson(),
        'shape': BodyShape.sphere.name,
      });
      final boxAgain = BodyComponent.fromJson({
        ...ball.toJson(),
        'shape': BodyShape.box.name,
      });

      expect(boxAgain.size, Vector3(2, 1, 4));
    });

    test('is read back as a body, not an unknown component', () {
      final document = documentOf([crate(BodyComponent(mass: 20))]);
      final read = SceneDocument.decode(document.encode());

      expect(read.problems, isEmpty);
      expect(bodyOf(read.document).mass, 20);
      expect(read.document.encode(), document.encode());
    });

    test('is written after what it draws and before what it lights', () {
      final document = documentOf([
        SceneEntity(
          id: 'lamp',
          name: 'Lamp',
          components: {
            SceneComponents.light: const LightComponent(),
            SceneComponents.body: BodyComponent(),
            SceneComponents.mesh: const MeshComponent(),
          },
        ),
      ]);
      final written = document.encode();

      expect(written.indexOf('"mesh"'), lessThan(written.indexOf('"body"')));
      expect(written.indexOf('"body"'), lessThan(written.indexOf('"light"')));
    });

    test('a changed field is one operation, and it goes back', () {
      final a = documentOf([crate(BodyComponent())]);
      final b = documentOf([crate(BodyComponent(mass: 40))]);
      final diff = SceneDiff.between(a, b);

      expect(diff.operations.single, isA<SetField>());
      expect((diff.operations.single as SetField).field, 'mass');
      expect(diff.applyTo(a).encode(), b.encode());
      expect(diff.inverse.applyTo(b).encode(), a.encode());
    });

    test('turning on a trigger and a belt is two operations that go back', () {
      final a = documentOf([crate(BodyComponent())]);
      final b = documentOf([
        crate(BodyComponent(trigger: true, surface: Vector3(1, 0, 0))),
      ]);
      final diff = SceneDiff.between(a, b);

      expect(
        diff.operations.whereType<SetField>().map((op) => op.field),
        unorderedEquals(['trigger', 'surface']),
      );
      expect(diff.applyTo(a).encode(), b.encode());
      expect(diff.inverse.applyTo(b).encode(), a.encode());
    });
  });
}
