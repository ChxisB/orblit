import 'dart:typed_data';

import 'package:orblit_core/orblit_core.dart';
import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

void main() {
  late World world;
  late ComponentType position;
  late ComponentType tag;
  late ComponentType health;
  late TransformComponents transforms;
  late CoreWorldInspector inspector;
  late int a;
  late int b;

  setUp(() {
    world = World();
    position = world.registerComponent(
      'Position',
      kind: ComponentKind.float32,
      arity: 3,
    );
    tag = world.registerComponent('Tag', kind: ComponentKind.uint8, arity: 2);
    health = world.registerComponent('Health', kind: ComponentKind.int32);
    transforms = world.registerTransforms();
    a = world.createEntity();
    world.add(a, position, Float32List.fromList([1, 2, 3]));
    world.add(a, transforms.local, transform(x: 4));
    b = world.createEntity();
    world.add(b, position);
    world.add(b, tag);
    world.add(b, health, Int32List.fromList([10]));
    inspector = CoreWorldInspector(
      world,
      [position, tag, health, transforms.local],
      manifest: [
        {
          'name': 'Position',
          'fields': ['x', 'y', 'z'],
        },
        {
          'name': 'Health',
          'fields': ['hp'],
        },
      ],
    );
  });

  tearDown(() => world.dispose());

  group('entities', () {
    test('list every entity with its component names, sorted', () {
      final rows = inspector.entities([]);
      expect(rows.map((row) => row.id), [a, b]);
      expect(rows[0].components, ['Position', 'orblit.LocalTransform']);
      expect(rows[1].components, ['Health', 'Position', 'Tag']);
    });

    test('are narrowed by the components asked for', () {
      expect(inspector.entities(['Tag']).map((row) => row.id), [b]);
      expect(inspector.entities(['Position']), hasLength(2));
      expect(inspector.entities(['Position', 'Tag']), hasLength(1));
    });

    test('refuse a component nobody told the inspector about', () {
      expect(() => inspector.entities(['Nope']), throwsArgumentError);
    });

    test('show a component the inspector was not given as its id', () {
      final other = world.registerComponent('Other', kind: ComponentKind.int32);
      world.add(a, other);
      final row = inspector.entities([]).first;
      expect(row.components, contains('#${other.id}'));
    });
  });

  group('read', () {
    test('names the fields of a described component', () {
      final read = inspector.read(a)!;
      expect(read['Position'], {'x': 1.0, 'y': 2.0, 'z': 3.0});
      expect(read['orblit.LocalTransform']!['x'], 4.0);
      expect(read['orblit.LocalTransform']!['rotationW'], 1.0);
      expect(read['orblit.LocalTransform'], hasLength(10));
    });

    test('says how an undescribed component is laid out', () {
      expect(inspector.read(b)!['Tag'], {'undescribed': 'uint8 x2'});
      expect(inspector.read(b)!['Health'], {'hp': 10});
    });

    test('leaves out a component the entity lacks', () {
      expect(inspector.read(a)!.containsKey('Tag'), isFalse);
    });

    test('answers null for an entity that is gone', () {
      world.destroyEntity(a);
      expect(inspector.read(a), isNull);
    });

    test('shows a value a float cannot hold as text', () {
      world.float32Of(a, position)![0] = double.nan;
      expect(inspector.read(a)!['Position']!['x'], 'NaN');
    });
  });

  group('write', () {
    test('sets one field and leaves the others', () {
      inspector.write(a, 'Position', 'y', 9);
      expect(world.float32Of(a, position), [1, 9, 3]);
      expect(inspector.read(a)!['Position'], {'x': 1.0, 'y': 9.0, 'z': 3.0});
    });

    test('sets a field of a built-in transform', () {
      inspector.write(a, 'orblit.LocalTransform', 'scaleY', 2);
      expect(world.float32Of(a, transforms.local)![8], 2);
    });

    test('sets a whole number in an integer column', () {
      inspector.write(b, 'Health', 'hp', 7);
      expect(inspector.read(b)!['Health'], {'hp': 7});
    });

    test('refuses a fraction in an integer column', () {
      expect(
        () => inspector.write(b, 'Health', 'hp', 2.5),
        throwsArgumentError,
      );
      expect(inspector.read(b)!['Health'], {'hp': 10});
    });

    test('refuses a field of a component with no names', () {
      expect(() => inspector.write(b, 'Tag', 'x', 1), throwsArgumentError);
    });

    test('refuses a field nobody named and a component the entity lacks', () {
      expect(() => inspector.write(a, 'Position', 'w', 1), throwsArgumentError);
      expect(() => inspector.write(a, 'Health', 'hp', 1), throwsArgumentError);
      expect(() => inspector.write(a, 'Nope', 'x', 1), throwsArgumentError);
    });

    test('refuses an entity that is gone', () {
      world.destroyEntity(a);
      expect(() => inspector.write(a, 'Position', 'x', 1), throwsStateError);
    });
  });

  test('a description that does not match the layout is refused', () {
    expect(
      () => CoreWorldInspector(
        world,
        [position],
        manifest: [
          {
            'name': 'Position',
            'fields': ['x', 'y'],
          },
        ],
      ),
      throwsArgumentError,
    );
  });
}
