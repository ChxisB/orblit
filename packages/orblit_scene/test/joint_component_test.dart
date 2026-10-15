import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  group('a joint component', () {
    test('is an unlimited hinge when the file says nothing', () {
      final joint = JointComponent.fromJson(const {});
      expect(joint.kind, JointKind.hinge);
      expect(joint.limits, isEmpty);
      expect(joint.swing, 45);
      expect(joint.strength, 0);
      expect(joint.breakingForce, 0);
      expect(joint.collide, isFalse);
    });

    test('survives the round trip through a scene file', () {
      final door = JointComponent(
        limits: const {JointAxis.aboutX: JointRange(-10, 95)},
        speed: 30,
        strength: 5,
        breakingTorque: 400,
      );
      final document = SceneDocument(
        name: 'Scene',
        entities: [
          SceneEntity(
            id: 'hinge',
            name: 'Hinge',
            components: {
              SceneComponents.transform: TransformComponent(),
              SceneComponents.joint: door,
            },
          ),
        ],
      );

      final back = SceneDocument.decode(document.encode()).document;
      final joint = back['hinge']![SceneComponents.joint]! as JointComponent;
      expect(joint.toJson(), door.toJson());
      expect(joint.limits[JointAxis.aboutX]!.high, 95);
    });

    test('is written after the body and before the motion', () {
      final order = SceneComponents.order;
      expect(order.indexOf(SceneComponents.joint), order.indexOf('body') + 1);
      expect(order.indexOf('motion'), order.indexOf(SceneComponents.joint) + 1);
    });

    test('keeps the limits its kind does not read', () {
      final hinge = JointComponent(
        limits: const {JointAxis.aboutX: JointRange(0, 90)},
      );
      final back = JointComponent.fromJson(
        hinge.copyWith(kind: JointKind.slider).toJson(),
      ).copyWith(kind: JointKind.hinge);
      expect(back.toJson(), hinge.toJson());
      expect(back.copyWith(kind: JointKind.slider).axes, [JointAxis.alongX]);
    });

    test('skips a limit it cannot read and keeps the rest', () {
      final joint = JointComponent.fromJson(const {
        'kind': 'sixAxis',
        'limits': {
          'alongY': [0, 1],
          'aboutZ': [5],
          'aboutY': 'wide',
          'sideways': [0, 1],
        },
      });
      expect(joint.kind, JointKind.sixAxis);
      expect(joint.limits.keys, [JointAxis.alongY]);
    });

    test('changes one limit at a time', () {
      final joint = JointComponent(kind: JointKind.sixAxis)
          .limit(JointAxis.alongY, const JointRange.at(0))
          .limit(JointAxis.aboutZ, const JointRange(-5, 5));
      expect(joint.limits[JointAxis.alongY]!.locked, isTrue);
      expect(joint.free(JointAxis.alongY).limits.keys, [JointAxis.aboutZ]);
      expect(
        () => joint.limits[JointAxis.alongX] = const JointRange(0, 1),
        throwsUnsupportedError,
      );
    });
  });

  group('a joint that gives', () {
    test('has no give when the file says nothing', () {
      final joint = JointComponent.fromJson(const {});
      expect(joint.springs, isEmpty);
      expect(joint.softHertz, 0);
      expect(joint.goalHertz, 0);
      expect(joint.ratio, 1);
      expect(joint.over, isEmpty);
      expect(joint.path, isEmpty);
      expect(joint.follow, isFalse);
    });

    test('survives the round trip through a scene file', () {
      final machine = JointComponent(
        kind: JointKind.path,
        limits: const {JointAxis.alongX: JointRange(0, 4)},
        springs: const {JointAxis.alongX: JointGive(2, ratio: 0.5, rest: 1.5)},
        softHertz: 3,
        softRatio: 0.7,
        goal: 40,
        goalHertz: 6,
        goalRatio: 0.9,
        ratio: -0.5,
        axisB: Vector3(0, 0, 1),
        over: [Vector3(0, 3, 0), Vector3(2, 3, 0)],
        path: [Vector3(0, 0, 0), Vector3(1, -1, 0), Vector3(3, -1, 0)],
        follow: true,
      );
      final back = JointComponent.fromJson(machine.toJson());
      expect(back.toJson(), machine.toJson());
      expect(
        back.springs[JointAxis.alongX],
        const JointGive(2, ratio: 0.5, rest: 1.5),
      );
      expect(back.path, hasLength(3));
      expect(back.path[2].x, 3);
      expect(back.over[1].y, 3);
      expect(back.axisB.z, 1);
      expect(back.follow, isTrue);
    });

    test('reads the new kinds by name', () {
      for (final kind in ['gear', 'rack', 'pulley', 'path']) {
        expect(JointComponent.fromJson({'kind': kind}).kind.name, kind);
      }
    });

    test('says which ways each new kind reads', () {
      expect(JointComponent(kind: JointKind.path).axes, [JointAxis.alongX]);
      for (final kind in [JointKind.gear, JointKind.rack, JointKind.pulley]) {
        expect(JointComponent(kind: kind).axes, isEmpty);
      }
    });

    test('skips a spring with no frequency and keeps the rest', () {
      final joint = JointComponent.fromJson(const {
        'springs': {
          'alongX': [0, 1, 0],
          'aboutX': [4],
          'aboutY': 'stiff',
        },
      });
      expect(joint.springs.keys, [JointAxis.aboutX]);
      expect(joint.springs[JointAxis.aboutX]!.ratio, 1);
      expect(joint.springs[JointAxis.aboutX]!.rest, isNull);
    });

    test('drops a half-written point', () {
      final joint = JointComponent.fromJson(const {
        'path': [0, 0, 0, 1, 2],
      });
      expect(joint.path, hasLength(1));
    });

    test('copies and changes one spring at a time', () {
      final joint = JointComponent()
          .spring(JointAxis.aboutX, const JointGive(5))
          .spring(JointAxis.alongY, const JointGive(1, rest: 2));
      expect(joint.springs.keys, [JointAxis.aboutX, JointAxis.alongY]);
      expect(joint.unspring(JointAxis.aboutX).springs.keys, [JointAxis.alongY]);
      expect(
        () => joint.springs[JointAxis.alongZ] = const JointGive(1),
        throwsUnsupportedError,
      );
    });

    test('does not share its points with the vector it was given', () {
      final point = Vector3(1, 2, 3);
      final joint = JointComponent(path: [point]);
      point.x = 9;
      expect(joint.path.single.x, 1);
    });
  });

  group('what a joint holds', () {
    // world
    //   door        body
    //   arm         body
    //     elbow     joint
    //     forearm   body
    //       wrist   joint
    //       hand    (no body)
    //         grip  joint
    //   loose       joint, nothing above it with a body
    const parents = <String, String?>{
      'door': null,
      'arm': null,
      'elbow': 'arm',
      'forearm': 'arm',
      'wrist': 'forearm',
      'hand': 'forearm',
      'grip': 'hand',
      'loose': null,
    };
    const bodies = {'door', 'arm', 'forearm'};
    ({String? body, String? holder}) endsOf(String id) => JointComponent.endsOf(
      id,
      parentOf: (id) => parents[id],
      hasBody: bodies.contains,
    );

    test('is its own body, held by the world when nothing is above it', () {
      expect(endsOf('door'), (body: 'door', holder: null));
    });

    test('is the body it hangs under, held by the one above that', () {
      expect(endsOf('wrist'), (body: 'forearm', holder: 'arm'));
      expect(endsOf('grip'), (body: 'forearm', holder: 'arm'));
    });

    test('on a body, holds that body to the one above it', () {
      expect(endsOf('forearm'), (body: 'forearm', holder: 'arm'));
    });

    test('under a body with nothing above, holds it to the world', () {
      expect(endsOf('elbow'), (body: 'arm', holder: null));
    });

    test('holds nothing with no body at or above it', () {
      expect(endsOf('loose'), (body: null, holder: null));
    });

    test('survives a parent loop', () {
      final ends = JointComponent.endsOf(
        'a',
        parentOf: (id) => id == 'a' ? 'b' : 'a',
        hasBody: (id) => id == 'b',
      );
      expect(ends, (body: 'b', holder: null));
    });
  });
}
