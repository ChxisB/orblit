import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

/// The differences as plain maps, because a record compares its fields by
/// identity and the values here are maps.
List<Map<String, Object?>> shown(List<Difference> found) => [
  for (final d in found) {'path': d.path, 'from': d.from, 'to': d.to},
];

void main() {
  group('diffJson', () {
    test('finds nothing between equal documents', () {
      final doc = {
        'a': [1, 2, 3],
        'b': {'c': 'x'},
      };
      expect(diffJson(doc, doc), isEmpty);
      expect(diffJson({'n': 1}, {'n': 1.0}), isEmpty);
    });

    test('names the path of a number that changed', () {
      final one = {
        'entities': {
          '3': {
            'Position': {'x': 1, 'y': 2},
          },
        },
      };
      final two = {
        'entities': {
          '3': {
            'Position': {'x': 1, 'y': 5},
          },
        },
      };
      expect(shown(diffJson(one, two)), [
        {'path': 'entities/3/Position/y', 'from': 2, 'to': 5},
      ]);
    });

    test('names an entity that was added and one that was removed', () {
      final one = {
        'entities': {
          '1': {'Tag': 1},
          '2': {'Tag': 2},
        },
      };
      final two = {
        'entities': {
          '2': {'Tag': 2},
          '3': {'Tag': 3},
        },
      };
      expect(shown(diffJson(one, two)), [
        {
          'path': 'entities/1',
          'from': {'Tag': 1},
          'to': null,
        },
        {
          'path': 'entities/3',
          'from': null,
          'to': {'Tag': 3},
        },
      ]);
    });

    test('compares lists element by element and by length', () {
      expect(
        shown(
          diffJson(
            {
              'a': [1, 2, 3],
            },
            {
              'a': [1, 9],
            },
          ),
        ),
        [
          {'path': 'a/1', 'from': 2, 'to': 9},
          {'path': 'a/2', 'from': 3, 'to': null},
        ],
      );
      expect(
        shown(
          diffJson(
            {
              'a': [1],
            },
            {
              'a': [1, 2],
            },
          ),
        ),
        [
          {'path': 'a/1', 'from': null, 'to': 2},
        ],
      );
    });

    test('reports a change of kind once, at the place it happened', () {
      expect(
        shown(
          diffJson(
            {
              'a': {'b': 1},
            },
            {'a': 5},
          ),
        ),
        [
          {
            'path': 'a',
            'from': {'b': 1},
            'to': 5,
          },
        ],
      );
    });
  });

  group('SnapshotStore', () {
    test('finds a snapshot by the id it was given', () {
      final store = SnapshotStore()..add({'n': 1});
      final first = store.lastId;
      store.add({'n': 2});
      expect(first, isNot(store.lastId));
      expect(store.find(first), {'n': 1});
      expect(store.find(store.lastId), {'n': 2});
    });

    test('drops the oldest when it is full', () {
      final store = SnapshotStore(capacity: 2)..add({'n': 1});
      final first = store.lastId;
      store
        ..add({'n': 2})
        ..add({'n': 3});
      expect(store.find(first), isNull);
      expect(store.find(store.lastId), {'n': 3});
    });

    test('answers null for an id it never gave', () {
      expect(SnapshotStore().find('s99'), isNull);
    });

    test('has no last id before the first snapshot', () {
      expect(() => SnapshotStore().lastId, throwsStateError);
      expect(() => SnapshotStore(capacity: 0), throwsArgumentError);
    });
  });
}
