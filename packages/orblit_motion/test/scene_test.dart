import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

SceneEntity entity(String id, {String? parent, bool prefab = false}) =>
    SceneEntity(
      id: id,
      name: id,
      parent: parent,
      components: {
        SceneComponents.transform: TransformComponent(),
        if (prefab)
          SceneComponents.prefab: const PrefabComponent(asset: 'lamp.oscene'),
      },
    );

/// A street with a lamp placed in it, the lamp's bulb, and a door that
/// belongs to the scene itself.
SceneDocument street() => SceneDocument(
  entities: [
    entity('street1', prefab: true),
    entity('street1/lamp3', parent: 'street1', prefab: true),
    entity('street1/lamp3/bulb', parent: 'street1/lamp3'),
    entity('door'),
  ],
);

List<SetField> lampOps(ClipFrame frame, SceneDocument document) =>
    sceneOpsFor(frame, document, ClipScope.inScene(document, 'street1/lamp3'));

void main() {
  group('a scope', () {
    test("played on an instance names its parts by the prefab's ids", () {
      const scope = ClipScope('street1/lamp3', instance: 'street1/lamp3');
      expect(scope.resolve(''), 'street1/lamp3');
      expect(scope.resolve('bulb'), 'street1/lamp3/bulb');
      expect(scope.resolve('bulb/glow'), 'street1/lamp3/bulb/glow');

      expect(scope.targetOf('street1/lamp3'), '');
      expect(scope.targetOf('street1/lamp3/bulb'), 'bulb');
      expect(scope.targetOf('street1/lamp3/bulb/glow'), 'bulb/glow');
    });

    test('has no name for what is outside the instance', () {
      const scope = ClipScope('street1/lamp3', instance: 'street1/lamp3');
      expect(scope.targetOf('street1'), isNull);
      expect(scope.targetOf('door'), isNull);
      expect(scope.targetOf('street1/lamp30/bulb'), isNull);
    });

    test('played on a part names the prefab root by its id, when known', () {
      const scope = ClipScope(
        'street1/lamp3/bulb',
        instance: 'street1/lamp3',
        root: 'lamp',
      );
      expect(scope.resolve(''), 'street1/lamp3/bulb');
      expect(scope.resolve('lamp'), 'street1/lamp3');
      expect(scope.resolve('flame'), 'street1/lamp3/flame');
      expect(scope.targetOf('street1/lamp3'), 'lamp');
      expect(scope.targetOf('street1/lamp3/bulb'), '');

      const unknown = ClipScope(
        'street1/lamp3/bulb',
        instance: 'street1/lamp3',
      );
      expect(unknown.targetOf('street1/lamp3'), isNull);
    });

    test("played outside any instance names things by the scene's ids", () {
      const scope = ClipScope('door');
      expect(scope.resolve(''), 'door');
      expect(scope.resolve('street1/lamp3'), 'street1/lamp3');
      expect(scope.targetOf('door'), '');
      expect(scope.targetOf('street1/lamp3/bulb'), 'street1/lamp3/bulb');
    });

    test('is worked out from where the owner is in the scene', () {
      final document = street();

      final lamp = ClipScope.inScene(document, 'street1/lamp3');
      expect(lamp.instance, 'street1/lamp3');

      final bulb = ClipScope.inScene(document, 'street1/lamp3/bulb');
      expect(bulb.instance, 'street1/lamp3');
      expect(bulb.resolve(''), 'street1/lamp3/bulb');

      final door = ClipScope.inScene(document, 'door');
      expect(door.instance, isNull);
    });
  });

  group('a frame in a scene', () {
    test('is the changes that make the scene look like it', () {
      final document = street();
      final frame = ClipFrame(0.5)
        ..values[''] = {
          'transform.position': Vector3(1, 2, 3),
          'transform.rotation': Quaternion.axisAngle(
            Vector3(0, 1, 0),
            radians(30),
          ),
        }
        ..values['bulb'] = {'transform.scale': Vector3(2, 2, 2)};

      final ops = lampOps(frame, document);

      expect(ops, hasLength(3));
      final position = ops[0];
      expect(position.id, 'street1/lamp3');
      expect(position.type, 'transform');
      expect(position.field, 'position');
      expect(position.from, [0, 0, 0]);
      expect(position.to, [1, 2, 3]);

      // A transform is written in degrees, so a rotation becomes them.
      final rotation = ops[1];
      expect(rotation.field, 'rotation');
      final degrees = rotation.to! as List<double>;
      expect(degrees[0], closeTo(0, 1e-9));
      expect(degrees[1], closeTo(30, 1e-9));
      expect(degrees[2], closeTo(0, 1e-9));

      expect(ops[2].id, 'street1/lamp3/bulb');
      expect(ops[2].field, 'scale');
    });

    test('can be put back exactly', () {
      final document = street();
      final frame = ClipFrame(0)
        ..values['bulb'] = {'transform.position': Vector3(0, 3, 0)};
      final ops = lampOps(frame, document);

      var shown = document;
      for (final op in ops) {
        shown = op.applyTo(shown);
      }
      final bulb = shown['street1/lamp3/bulb']![SceneComponents.transform];
      expect((bulb! as TransformComponent).position, Vector3(0, 3, 0));

      var back = shown;
      for (final op in ops.reversed) {
        back = op.inverse.applyTo(back);
      }
      expect(
        back['street1/lamp3/bulb']!.toJson(),
        document['street1/lamp3/bulb']!.toJson(),
      );
    });

    test('leaves out what the scene already has, or does not have at all', () {
      final document = street();
      final frame = ClipFrame(0)
        ..values['bulb'] = {
          // Where the bulb already is.
          'transform.position': Vector3.zero(),
          // A lamp without a light: nothing to dim.
          'light.power': 20.0,
          // Not a component and a field at all.
          'power': 20.0,
        }
        // A part this lamp does not have.
        ..values['flame'] = {'transform.scale': Vector3(2, 2, 2)};

      expect(lampOps(frame, document), isEmpty);
    });

    test('leaves bones to whoever draws the model', () {
      final document = street();
      final frame = ClipFrame(0)
        ..bones[''] = {'hips': BoneLocal(position: Vector3(0, 1, 0))};
      expect(sceneOpsFor(frame, document, const ClipScope('door')), isEmpty);
    });

    test('writes a rotation anywhere else as its four numbers', () {
      final document = SceneDocument(
        entities: [
          const SceneEntity(
            id: 'door',
            name: 'Door',
            components: {
              'hinge': UnknownComponent('hinge', {
                'turn': [0, 0, 0, 1],
              }),
            },
          ),
        ],
      );
      final frame = ClipFrame(0)
        ..values[''] = {
          'hinge.turn': Quaternion.axisAngle(Vector3(0, 1, 0), radians(90)),
        };
      final ops = sceneOpsFor(frame, document, const ClipScope('door'));
      final turn = ops.single.to! as List<double>;
      expect(turn, hasLength(4));
      expect(turn[1], closeTo(0.7071067811865476, 1e-12));
    });
  });
}
