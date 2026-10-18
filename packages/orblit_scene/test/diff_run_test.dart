import 'dart:math';

import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

/// A run of adds or removes is applied in one pass, which has to land on the
/// document the same operations give one at a time.
void main() {
  Map<String, Object?> entity(String id, {String? parent}) =>
      SceneEntity(id: id, name: id, parent: parent).toJson();

  SceneDocument one(SceneDocument document, List<SceneOp> operations) {
    var current = document;
    for (final op in operations) {
      current = op.applyTo(current);
    }
    return current;
  }

  List<String> ids(SceneDocument document) => [
    for (final e in document.entities) e.id,
  ];

  SceneDocument world(List<String> names) => SceneDocument(
    entities: [for (final id in names) SceneEntity(id: id, name: id)],
  );

  test('adds in a chain keep their order and sit at the head', () {
    final diff = SceneDiff([
      AddEntity(entity('a')),
      AddEntity(entity('b'), after: 'a'),
      AddEntity(entity('c'), after: 'b'),
    ]);

    expect(ids(diff.applyTo(world(['x', 'y']))), ['a', 'b', 'c', 'x', 'y']);
  });

  test('an add after something missing goes to the end', () {
    final diff = SceneDiff([
      AddEntity(entity('a'), after: 'nothing'),
      AddEntity(entity('b'), after: 'nothing'),
    ]);

    expect(ids(diff.applyTo(world(['x']))), ['x', 'a', 'b']);
  });

  test('an add of an id that is there is left alone', () {
    final diff = SceneDiff([
      AddEntity(entity('x', parent: 'y')),
      AddEntity(entity('a')),
      AddEntity(entity('a')),
    ]);

    final result = diff.applyTo(world(['x']));

    expect(ids(result), ['a', 'x']);
    expect(result['x']!.parent, isNull);
  });

  test('removes take out only what they name', () {
    final diff = SceneDiff([
      RemoveEntity(entity('a')),
      RemoveEntity(entity('missing')),
      RemoveEntity(entity('c')),
    ]);

    expect(ids(diff.applyTo(world(['a', 'b', 'c']))), ['b']);
  });

  test('a run in the middle leaves the operations around it in order', () {
    final diff = SceneDiff([
      AddEntity(entity('a')),
      AddEntity(entity('b'), after: 'a'),
      RemoveEntity(entity('x')),
      RemoveEntity(entity('y')),
      AddEntity(entity('c')),
    ]);

    expect(ids(diff.applyTo(world(['x', 'y', 'z']))), ['c', 'a', 'b', 'z']);
  });

  test('random runs land where one at a time does', () {
    final random = Random(15);
    const names = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
    for (var round = 0; round < 300; round++) {
      final start = world([
        for (final name in names)
          if (random.nextBool()) name,
      ]);
      final operations = <SceneOp>[
        for (var i = 0; i < random.nextInt(14); i++)
          if (random.nextInt(3) == 0)
            RemoveEntity(entity(names[random.nextInt(names.length)]))
          else
            AddEntity(
              entity(names[random.nextInt(names.length)]),
              after: random.nextInt(4) == 0
                  ? null
                  : random.nextInt(5) == 0
                  ? 'missing'
                  : names[random.nextInt(names.length)],
            ),
      ];

      final batched = SceneDiff(operations).applyTo(start);

      expect(
        ids(batched),
        ids(one(start, operations)),
        reason: 'round $round: $operations',
      );
    }
  });
}
