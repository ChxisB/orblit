import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_cells/orblit_cells.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

void main() {
  // The point stands in the middle of cell (0, 0). Cells are ten across.
  const middle = 5.0;
  final grid = Grid(10);

  late Duration now;
  late SceneDocument scene;
  late List<Cell> asked;
  late List<CellLoad> loads;
  late Map<Cell, Object?> files;
  late Duration cost;

  setUp(() {
    now = Duration.zero;
    scene = SceneDocument();
    asked = [];
    loads = [];
    files = {};
    cost = Duration.zero;
  });

  Uint8List file(List<SceneEntity> entities) =>
      utf8.encode(SceneDocument(entities: entities).encode());

  Uint8List tree(String name) => file([
    SceneEntity(id: 'root', name: name),
    SceneEntity(id: 'leaf', name: name, parent: 'root'),
  ]);

  CellStreamer streamer({
    int radius = 1,
    int inFlight = 2,
    Duration budget = const Duration(milliseconds: 4),
  }) => CellStreamer(
    window: CellWindow(grid, loadRadius: radius),
    inFlight: inFlight,
    budget: budget,
    retryAfter: const Duration(seconds: 5),
    clock: () => now,
    onLoad: loads.add,
    fetch: (cell) async {
      asked.add(cell);
      final found = files[cell];
      if (found is Exception) throw found;
      return found as Uint8List?;
    },
    apply: (diff) {
      now += cost;
      scene = diff.applyTo(scene);
    },
  );

  // Lets the fetches finish, which they do between frames.
  Future<void> between() => Future<void>.delayed(Duration.zero);

  // Steps until nothing is waiting, as a game would over a few frames.
  Future<void> settle(CellStreamer s, {double x = middle}) async {
    for (var i = 0; i < 40; i++) {
      s.step(x, middle);
      await between();
    }
  }

  test('puts a cell in with its cell on every id and keeps the tree', () async {
    files[(x: 0, z: 0)] = tree('here');
    final s = streamer(radius: 0);

    await settle(s);

    expect(scene.entities.map((e) => e.id), ['cell_0_0:root', 'cell_0_0:leaf']);
    expect(scene['cell_0_0:leaf']!.parent, 'cell_0_0:root');
    expect(scene['cell_0_0:root']!.parent, isNull);
  });

  test('two cells with the same ids do not clash', () async {
    files[(x: 0, z: 0)] = tree('a');
    files[(x: 1, z: 0)] = tree('b');
    final s = streamer(radius: 1);

    await settle(s);

    expect(scene.length, 4);
    expect(scene['cell_1_0:root']!.name, 'b');
  });

  test('asks for the cell under the point first', () async {
    final s = streamer(radius: 2, inFlight: 1);

    s.step(middle, middle);

    expect(asked, [(x: 0, z: 0)]);
  });

  test('never has more than inFlight cells asked for or waiting', () async {
    final s = streamer(radius: 2, inFlight: 2);

    s.step(middle, middle);
    expect(s.pending, 2);
    await between();
    expect(s.pending, 2, reason: 'the fetched ones wait to be applied');
    s.step(middle, middle);
    await between();
    expect(asked.toSet(), hasLength(asked.length), reason: 'none asked twice');
  });

  group('the budget', () {
    test('stops after the cell that used it up', () async {
      for (final cell in grid.around((x: 0, z: 0), 1)) {
        files[cell] = tree('c');
      }
      cost = const Duration(milliseconds: 3);
      final s = streamer(radius: 1, inFlight: 9);
      s.step(middle, middle);
      await between();

      s.step(middle, middle);

      // 3 ms is under the 4 ms budget, so a second goes; 6 ms is not.
      expect(s.lastStep.loaded, 2);
      expect(s.lastStep.spent, const Duration(milliseconds: 6));
    });

    test(
      'does one cell in a frame even when one costs more than all',
      () async {
        files[(x: 0, z: 0)] = tree('big');
        cost = const Duration(milliseconds: 50);
        final s = streamer(radius: 0);
        s.step(middle, middle);
        await between();

        s.step(middle, middle);

        expect(s.lastStep.loaded, 1);
      },
    );

    test('a budget of zero is refused', () {
      expect(() => streamer(budget: Duration.zero), throwsArgumentError);
    });
  });

  group('leaving', () {
    test(
      'drops the cells past the margin and takes their entities out',
      () async {
        files[(x: 0, z: 0)] = tree('start');
        final s = streamer(radius: 0);
        await settle(s);
        expect(scene.length, 2);

        // Cell 2 is past the margin of one cell.
        await settle(s, x: 25);

        expect(s.isLoaded((x: 0, z: 0)), isFalse);
        expect(
          scene.entities.where((e) => e.id.startsWith('cell_0_0:')),
          isEmpty,
        );
      },
    );

    test('keeps a cell that is in the margin', () async {
      files[(x: 0, z: 0)] = tree('start');
      final s = streamer(radius: 0);
      await settle(s);

      await settle(s, x: 15);

      expect(s.isLoaded((x: 0, z: 0)), isTrue);
      expect(scene.length, 2);
    });

    test('keeps a pinned cell wherever the point goes', () async {
      files[(x: 0, z: 0)] = tree('start');
      final s = streamer(radius: 0);
      await settle(s);

      for (var i = 0; i < 5; i++) {
        s.step(95, middle, pinned: {(x: 0, z: 0)});
        await between();
      }

      expect(s.isLoaded((x: 0, z: 0)), isTrue);
    });

    test('throws away a cell that arrives after the point has left', () async {
      files[(x: 0, z: 0)] = tree('late');
      final s = streamer(radius: 0, inFlight: 1);
      s.step(middle, middle);

      // Fetched, but the point is nowhere near it by the next frame.
      await between();
      s.step(95, middle);

      expect(s.isLoaded((x: 0, z: 0)), isFalse);
      expect(scene.length, 0);
    });
  });

  group('a cell that is not there', () {
    test('is loaded as empty and not asked for again', () async {
      final s = streamer(radius: 0);

      await settle(s);

      expect(s.isLoaded((x: 0, z: 0)), isTrue);
      expect(asked, [(x: 0, z: 0)]);
      expect(scene.length, 0);
    });

    test(
      'that fails to read is reported and asked for after the wait',
      () async {
        files[(x: 0, z: 0)] = Exception('offline');
        final s = streamer(radius: 0);

        s.step(middle, middle);
        await between();
        expect(loads.single.error, isA<Exception>());

        now += const Duration(seconds: 1);
        s.step(middle, middle);
        await between();
        expect(asked, hasLength(1), reason: 'too soon to try again');

        files[(x: 0, z: 0)] = tree('back');
        now += const Duration(seconds: 5);
        await settle(s);
        expect(scene.length, 2);
      },
    );

    test('that is not a scene is reported and left out', () async {
      files[(x: 0, z: 0)] = Uint8List.fromList(utf8.encode('not a scene'));
      final s = streamer(radius: 0);

      await settle(s);

      expect(loads.single.error, isA<SceneFormatException>());
      expect(s.isLoaded((x: 0, z: 0)), isFalse);
      expect(scene.length, 0);
    });
  });

  test('reports what each cell cost', () async {
    final bytes = tree('profiled');
    files[(x: 0, z: 0)] = bytes;
    cost = const Duration(milliseconds: 2);
    final s = streamer(radius: 0);

    await settle(s);

    final load = loads.single;
    expect(load.cell, (x: 0, z: 0));
    expect(load.bytes, bytes.length);
    expect(load.entities, 2);
    expect(load.apply, const Duration(milliseconds: 2));
    expect(load.error, isNull);
  });

  test('a point that is not a number leaves everything as it is', () async {
    files[(x: 0, z: 0)] = tree('stays');
    final s = streamer(radius: 0);
    await settle(s);

    s.step(double.nan, 0);

    expect(s.isLoaded((x: 0, z: 0)), isTrue);
    expect(scene.length, 2);
  });
}
