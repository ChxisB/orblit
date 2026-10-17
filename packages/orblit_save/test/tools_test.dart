import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

import 'support.dart';

void main() {
  late Directory dir;
  late HistoryStore store;
  late SaveTools tools;
  final live = <String>{};
  var clockMs = 0;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('orblit_tools_test');
    store = HistoryStore.files(dir.path);
    live.clear();
    clockMs = 900000;
    tools = SaveTools(
      store: store,
      isLive: live.contains,
      clock: () => DateTime.fromMillisecondsSinceEpoch(clockMs++, isUtc: true),
    );
  });
  tearDown(() => dir.deleteSync(recursive: true));

  DateTime at(int ms) => DateTime.fromMillisecondsSinceEpoch(ms, isUtc: true);

  Future<List<int>> bytesOfKey(String key) async =>
      (await store.read(key))!.bytes;

  Future<int> versions(String key) async =>
      (await store.history.list(key)).length;

  group('HistoryStore', () {
    test('a write is the current save and a version', () async {
      await store.write('ana', recordOf([1], at: 1000));

      expect(await bytesOfKey('ana'), [1]);
      expect(await store.exists('ana'), isTrue);
      expect(await store.keys(), ['ana']);
      expect(await versions('ana'), 1);
    });

    test('a queue writing through it fills the history', () async {
      final queue = SaveQueue.spawn(
        HistoryStore.files(dir.path),
        clock: fixedClock,
        onFailure: (key, error) => fail('$key failed: $error'),
      );
      for (var i = 0; i < 5; i++) {
        queue.save('ana', bytesOf([i]), const Autosave());
      }
      expect(await queue.close(), isEmpty);

      expect(await bytesOfKey('ana'), [4]);
      expect(await store.history.keys(), ['ana']);
      expect(await versions('ana'), greaterThanOrEqualTo(1));
    });

    test(
      'a history that fails fails the write after the save is kept',
      () async {
        await Directory('${dir.path}/history').create();
        File('${dir.path}/history/entries').writeAsStringSync('in the way');

        await expectLater(store.write('ana', recordOf([1])), throwsA(anything));

        expect(await bytesOfKey('ana'), [1]);
      },
    );
  });

  group('restore', () {
    setUp(() async {
      await store.write('ana', recordOf([1], at: 1000));
      await store.write('ana', recordOf([2], at: 2000));
    });

    test('makes an older version the current save', () async {
      await tools.restore('ana', at(1000));

      final record = (await store.read('ana'))!;
      expect(record.bytes, [1]);
      expect(record.reason, Custom('restore'));
      expect(record.at.millisecondsSinceEpoch, 900001);
    });

    test('keeps the save it replaced as pre-restore', () async {
      await tools.restore('ana', at(1000));

      final kept = (await store.history.list(
        'ana',
      )).where((v) => v.reason == Custom('pre-restore')).single;
      expect((await store.history.load(kept)).bytes, [2]);
      expect(kept.at.millisecondsSinceEpoch, 900000);
    });

    test('refuses a key a player is using and writes nothing', () async {
      live.add('ana');

      await expectLater(tools.restore('ana', at(1000)), throwsStateError);

      expect(await bytesOfKey('ana'), [2]);
      expect(await versions('ana'), 2);
    });

    test('refuses a time with no version', () async {
      await expectLater(tools.restore('ana', at(1500)), throwsStateError);
      await expectLater(tools.restore('bo', at(1000)), throwsStateError);

      expect(await bytesOfKey('ana'), [2]);
      expect(await versions('ana'), 2);
    });

    test('brings back a save that is gone', () async {
      File('${dir.path}/ana.save').deleteSync();

      await tools.restore('ana', at(2000));

      expect(await bytesOfKey('ana'), [2]);
      expect(
        (await store.history.list(
          'ana',
        )).where((v) => v.reason == Custom('pre-restore')),
        isEmpty,
      );
    });
  });

  group('restore all', () {
    setUp(() async {
      await store.write('ana', recordOf([1], at: 1000));
      await store.write('ana', recordOf([2], at: 3000));
      await store.write('bo', recordOf([5], at: 1000));
      await store.write('bo', recordOf([6], at: 3000));
      await store.write('cy', recordOf([7], at: 1000));
      await store.write('dee', recordOf([8], at: 3000));
    });

    test('the plan lists what would change and writes nothing', () async {
      final plan = await tools.restorePlan(at(2000));

      expect(plan.changes.map((e) => e.key), unorderedEquals(['ana', 'bo']));
      expect(plan.changes.every((e) => e.at == at(1000)), isTrue);
      expect(plan.live, isEmpty);
      expect(await bytesOfKey('ana'), [2]);
      expect(await bytesOfKey('bo'), [6]);
      expect(await versions('ana'), 2);
      expect(await versions('bo'), 2);
    });

    test(
      'leaves out a save already at that version and one with none old enough',
      () async {
        final plan = await tools.restorePlan(at(2000));

        expect(plan.changes.map((e) => e.key), isNot(contains('cy')));
        expect(plan.changes.map((e) => e.key), isNot(contains('dee')));
      },
    );

    test('reports a key in use and does not plan it', () async {
      live.add('bo');

      final plan = await tools.restorePlan(at(2000));

      expect(plan.live, ['bo']);
      expect(plan.changes.map((e) => e.key), ['ana']);
    });

    test('applying a plan writes every change', () async {
      await tools.applyRestore(await tools.restorePlan(at(2000)));

      expect(await bytesOfKey('ana'), [1]);
      expect(await bytesOfKey('bo'), [5]);
      expect(await bytesOfKey('cy'), [7]);
      expect(await bytesOfKey('dee'), [8]);
    });

    test(
      'a key that went live after the plan stops it and stays as it was',
      () async {
        final plan = await tools.restorePlan(at(2000));
        live.addAll(['ana', 'bo']);

        await expectLater(tools.applyRestore(plan), throwsStateError);

        expect(await bytesOfKey('ana'), [2]);
        expect(await bytesOfKey('bo'), [6]);
      },
    );

    test('picks the newest version at or before the time', () async {
      await store.write('fay', recordOf([1], at: 1000));
      await store.write('fay', recordOf([2], at: 2000));
      await store.write('fay', recordOf([3], at: 3000));

      final plan = await tools.restorePlan(at(2000));

      final fay = plan.changes.singleWhere((e) => e.key == 'fay');
      expect(fay.at, at(2000));
    });

    test('a time before every version plans nothing', () async {
      final plan = await tools.restorePlan(at(10));

      expect(plan.changes, isEmpty);
      expect(plan.live, isEmpty);
    });
  });

  group('edit', () {
    setUp(() => store.write('ana', recordOf([1, 2, 3], at: 1000)));

    test('saves what the change makes of the bytes, for the reason', () async {
      await tools.edit(
        'ana',
        (bytes) => Uint8List.fromList([for (final b in bytes) b * 2]),
        Custom('gm fix'),
      );

      final record = (await store.read('ana'))!;
      expect(record.bytes, [2, 4, 6]);
      expect(record.reason, Custom('gm fix'));
      expect(record.at.millisecondsSinceEpoch, 900000);
      expect(await versions('ana'), 2);
    });

    test('refuses a key a player is using', () async {
      live.add('ana');

      await expectLater(
        tools.edit('ana', (bytes) => bytesOf([9]), Custom('x')),
        throwsStateError,
      );

      expect(await bytesOfKey('ana'), [1, 2, 3]);
    });

    test('refuses a key with no save', () async {
      await expectLater(
        tools.edit('bo', (bytes) => bytesOf([9]), Custom('x')),
        throwsStateError,
      );
      expect(await store.exists('bo'), isFalse);
    });

    test('a change that throws leaves the save as it was', () async {
      await expectLater(
        tools.edit(
          'ana',
          (bytes) => throw const FormatException('no'),
          Custom('x'),
        ),
        throwsFormatException,
      );

      expect(await bytesOfKey('ana'), [1, 2, 3]);
      expect(await versions('ana'), 1);
    });
  });
}
