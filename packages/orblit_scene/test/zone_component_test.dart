import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

SceneDocument documentOf(ZoneComponent? zone) => SceneDocument(
  name: 'Scene',
  entities: [
    SceneEntity(
      id: 'pool',
      name: 'Pool',
      components: {
        SceneComponents.transform: TransformComponent(),
        SceneComponents.body: BodyComponent(motion: BodyMotion.fixed),
        if (zone != null) SceneComponents.zone: zone,
      },
    ),
  ],
);

void main() {
  group('reading a zone', () {
    test('changes nothing when the file says nothing', () {
      final zone = ZoneComponent.fromJson(const {});

      expect(zone.gravity, isNull);
      expect(zone.linearDamping, isNull);
      expect(zone.angularDamping, isNull);
      expect(zone.priority, 0);
    });

    test('is the zone it was written as', () {
      final zone = ZoneComponent(
        gravity: Vector3(0, -1.5, 0),
        linearDamping: 2,
        angularDamping: 3,
        priority: 4,
      );
      final read = ZoneComponent.fromJson(zone.toJson());

      expect(read.toJson(), zone.toJson());
      expect(read.gravity, Vector3(0, -1.5, 0));
      expect(read.linearDamping, 2);
      expect(read.angularDamping, 3);
      expect(read.priority, 4);
    });

    test('leaves a field out of the file when it is left out of the zone', () {
      final zone = ZoneComponent(linearDamping: 2);

      expect(zone.toJson(), {'linearDamping': 2.0, 'priority': 0});
    });

    test('keeps a gravity of nought, which holds a body in place', () {
      final zone = ZoneComponent(gravity: Vector3.zero());
      final read = ZoneComponent.fromJson(zone.toJson());

      expect(read.gravity, Vector3.zero());
    });

    test('takes a gravity that is not a list for no gravity', () {
      final zone = ZoneComponent.fromJson(const {'gravity': 'down'});

      expect(zone.gravity, isNull);
    });

    test('rounds a priority written as a fraction', () {
      expect(ZoneComponent.fromJson(const {'priority': 2.6}).priority, 3);
    });
  });

  group('changing a zone', () {
    test('changes what it was asked to and keeps the rest', () {
      final before = ZoneComponent(gravity: Vector3(0, 9, 0), priority: 1);
      final after = before.copyWith(linearDamping: 1, priority: 2);

      expect(after.gravity, Vector3(0, 9, 0));
      expect(after.linearDamping, 1);
      expect(after.priority, 2);
    });

    test('does not share its gravity with the zone it came from', () {
      final before = ZoneComponent(gravity: Vector3(0, 9, 0));
      final after = before.copyWith();
      after.gravity!.y = 0;

      expect(before.gravity, Vector3(0, 9, 0));
    });

    test('does not share the vector it was built from', () {
      final up = Vector3(0, 9, 0);
      final zone = ZoneComponent(gravity: up);
      up.y = 0;

      expect(zone.gravity, Vector3(0, 9, 0));
    });
  });

  group('a zone in a scene', () {
    test('is read back as a zone, not an unknown component', () {
      final document = documentOf(ZoneComponent(gravity: Vector3(0, 9, 0)));
      final read = SceneDocument.decode(document.encode());

      expect(read.problems, isEmpty);
      expect(
        read.document['pool']![SceneComponents.zone],
        isA<ZoneComponent>(),
      );
      expect(read.document.encode(), document.encode());
    });

    test('is written after the motion and before the light', () {
      final order = SceneComponents.order;

      expect(order.indexOf(SceneComponents.zone), order.indexOf('motion') + 1);
      expect(order.indexOf('light'), order.indexOf(SceneComponents.zone) + 1);
    });

    test('adding one is a change that goes back', () {
      final a = documentOf(null);
      final b = documentOf(ZoneComponent(linearDamping: 4));
      final diff = SceneDiff.between(a, b);

      expect(diff.operations, isNotEmpty);
      expect(diff.applyTo(a).encode(), b.encode());
      expect(diff.inverse.applyTo(b).encode(), a.encode());
    });

    test('a changed field is one operation, and it goes back', () {
      final a = documentOf(ZoneComponent(priority: 1));
      final b = documentOf(ZoneComponent(priority: 2));
      final diff = SceneDiff.between(a, b);

      expect(diff.operations.single, isA<SetField>());
      expect((diff.operations.single as SetField).field, 'priority');
      expect(diff.applyTo(a).encode(), b.encode());
      expect(diff.inverse.applyTo(b).encode(), a.encode());
    });
  });
}
