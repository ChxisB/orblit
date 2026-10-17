import 'dart:io';

import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

import 'support.dart';

void main() {
  late Directory dir;
  late SaveHistory history;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('orblit_history_test');
    history = SaveHistory('${dir.path}/history');
  });
  tearDown(() => dir.deleteSync(recursive: true));

  List<File> blobs() => Directory(
    '${dir.path}/history/blobs',
  ).listSync().whereType<File>().where((f) => f.path.endsWith('.gz')).toList();

  File onlyBlob() => blobs().single;

  // A blob under a minute old is kept, so a test that expects one gone ages it.
  void ageBlobs() {
    for (final blob in blobs()) {
      blob.setLastModifiedSync(DateTime.fromMillisecondsSinceEpoch(0));
    }
  }

  group('record and list', () {
    test('lists the versions of a key newest first', () async {
      await history.record('ana', recordOf([1], at: 1000));
      await history.record(
        'ana',
        recordOf([2], reason: Custom('pre-restore'), at: 3000),
      );
      await history.record(
        'ana',
        recordOf([3], reason: const Leave(), at: 2000),
      );

      final versions = await history.list('ana');

      expect(versions.map((v) => v.at.millisecondsSinceEpoch), [
        3000,
        2000,
        1000,
      ]);
      expect(versions.map((v) => v.reason), [
        Custom('pre-restore'),
        const Leave(),
        const Autosave(),
      ]);
      expect(versions.every((v) => v.key == 'ana'), isTrue);
    });

    test('loads the bytes, reason and time of a version', () async {
      await history.record(
        'ana',
        recordOf([4, 5, 6], reason: const Shutdown(), at: 777),
      );

      final record = await history.load((await history.list('ana')).single);

      expect(record.bytes, [4, 5, 6]);
      expect(record.reason, const Shutdown());
      expect(record.at.millisecondsSinceEpoch, 777);
    });

    test('an identical save adds a version and no copy', () async {
      await history.record('ana', recordOf([1, 2], at: 1000));
      ageBlobs();
      await history.record('ana', recordOf([1, 2], at: 2000));
      await history.record('bo', recordOf([1, 2], at: 3000));

      expect(
        onlyBlob().lastModifiedSync().millisecondsSinceEpoch,
        0,
        reason: 'the copy was not written again',
      );
      expect(await history.list('ana'), hasLength(2));
      expect(blobs(), hasLength(1));

      await history.record('ana', recordOf([1, 3], at: 4000));

      expect(blobs(), hasLength(2));
    });

    test('two saves in one millisecond with other bytes both stay', () async {
      await history.record('ana', recordOf([1], at: 1000));
      await history.record('ana', recordOf([2], at: 1000));

      expect(await history.list('ana'), hasLength(2));
    });

    test('versions of one millisecond come back in one fixed order', () async {
      for (var i = 0; i < 8; i++) {
        await history.record('ana', recordOf([i], at: 1000));
      }

      final hashes = [for (final v in await history.list('ana')) v.hash];

      expect(hashes, [...hashes]..sort());
    });

    test('keys are the ones with a version', () async {
      await history.record('ana', recordOf([1]));
      await history.record('bo', recordOf([2]));

      expect(await history.keys(), unorderedEquals(['ana', 'bo']));
    });

    test(
      'an empty history has no keys and a key without versions has none',
      () async {
        expect(await history.keys(), isEmpty);
        expect(await history.list('ana'), isEmpty);
      },
    );

    test('a key that is not a save key is refused', () async {
      expect(() => history.list('../x'), throwsArgumentError);
      expect(() => history.record('a/b', recordOf([1])), throwsArgumentError);
    });
  });

  group('damage', () {
    test('a damaged entry names its file', () async {
      await history.record('ana', recordOf([1], at: 1000));
      final entry = Directory(
        '${dir.path}/history/entries/ana',
      ).listSync().whereType<File>().single;
      entry.writeAsStringSync('{"format":1}');

      await expectLater(
        history.list('ana'),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            allOf(contains(entry.path), contains('damaged')),
          ),
        ),
      );
    });

    test('an entry from a later format is refused', () async {
      await history.record('ana', recordOf([1], at: 1000));
      final entry = Directory(
        '${dir.path}/history/entries/ana',
      ).listSync().whereType<File>().single;
      entry.writeAsStringSync(
        entry.readAsStringSync().replaceFirst('"format":1', '"format":2'),
      );

      await expectLater(history.list('ana'), throwsFormatException);
    });

    test('a missing copy is reported', () async {
      await history.record('ana', recordOf([1], at: 1000));
      onlyBlob().deleteSync();

      await expectLater(
        history.load((await history.list('ana')).single),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('missing'),
          ),
        ),
      );
    });

    test('a copy that is not gzip is reported', () async {
      await history.record('ana', recordOf([1], at: 1000));
      onlyBlob().writeAsBytesSync([1, 2, 3]);

      await expectLater(
        history.load((await history.list('ana')).single),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('damaged'),
          ),
        ),
      );
    });

    test('a copy that holds other bytes is reported', () async {
      await history.record('ana', recordOf([1], at: 1000));
      await history.record('bo', recordOf([2], at: 1000));
      final ana = (await history.list('ana')).single;
      final bo = (await history.list('bo')).single;
      File('${dir.path}/history/blobs/${ana.hash}.gz').writeAsBytesSync(
        File('${dir.path}/history/blobs/${bo.hash}.gz').readAsBytesSync(),
      );

      await expectLater(
        history.load(ana),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('wrong hash'),
          ),
        ),
      );
    });
  });

  test('the default spacing changes at a day, a week and a month', () {
    expect(
      defaultSpacing(const Duration(hours: 23, minutes: 59)),
      Duration.zero,
    );
    expect(defaultSpacing(const Duration(days: 1)), const Duration(hours: 1));
    expect(
      defaultSpacing(const Duration(days: 6, hours: 23)),
      const Duration(hours: 1),
    );
    expect(defaultSpacing(const Duration(days: 7)), const Duration(days: 1));
    expect(
      defaultSpacing(const Duration(days: 29, hours: 23)),
      const Duration(days: 1),
    );
    expect(defaultSpacing(const Duration(days: 30)), isNull);
  });

  group('prune', () {
    final now = DateTime.fromMillisecondsSinceEpoch(
      100 * Duration.millisecondsPerDay,
      isUtc: true,
    );

    Future<void> saveAt(String key, Duration age, int byte) => history.record(
      key,
      recordOf([byte], at: now.millisecondsSinceEpoch - age.inMilliseconds),
    );

    Future<List<Duration>> ages(String key) async => [
      for (final v in await history.list(key)) now.difference(v.at),
    ];

    test('keeps what the default says and drops the rest', () async {
      const kept = [
        Duration.zero,
        Duration(hours: 1),
        Duration(hours: 23),
        Duration(hours: 25),
        Duration(hours: 26, minutes: 30),
        Duration(days: 8),
        Duration(days: 9, hours: 1),
      ];
      const dropped = [
        Duration(hours: 25, minutes: 30),
        Duration(days: 8, hours: 2),
        Duration(days: 31),
        Duration(days: 40),
      ];
      var byte = 0;
      for (final age in [...kept, ...dropped]) {
        await saveAt('ana', age, byte++);
      }

      final removed = await history.prune(defaultSpacing, now);

      expect(removed, dropped.length);
      expect(await ages('ana'), kept);
    });

    test('keeps versions that are exactly the spacing apart', () async {
      await saveAt('ana', const Duration(hours: 25), 1);
      await saveAt('ana', const Duration(hours: 26), 2);
      await saveAt('ana', const Duration(hours: 27), 3);

      expect(await history.prune(defaultSpacing, now), 0);
    });

    test('leaves a file that is not a copy alone', () async {
      await saveAt('ana', Duration.zero, 1);
      final stray = File('${dir.path}/history/blobs/notes.txt')
        ..writeAsStringSync('keep me')
        ..setLastModifiedSync(DateTime.fromMillisecondsSinceEpoch(0));

      await history.prune(defaultSpacing, now);

      expect(stray.existsSync(), isTrue);
    });

    test('keeps every version of one key for a day', () async {
      for (var minute = 0; minute < 50; minute++) {
        await saveAt('ana', Duration(minutes: minute * 20), minute);
      }
      final inDay = (await ages(
        'ana',
      )).where((a) => a < const Duration(days: 1));

      await history.prune(defaultSpacing, now);

      expect(
        (await ages('ana')).where((a) => a < const Duration(days: 1)),
        inDay,
      );
    });

    test('treats each key on its own', () async {
      await saveAt('ana', const Duration(days: 8), 1);
      await saveAt('bo', const Duration(days: 8, hours: 2), 2);

      await history.prune(defaultSpacing, now);

      expect(await ages('ana'), hasLength(1));
      expect(await ages('bo'), hasLength(1));
    });

    test('a spacing that drops everything empties the history', () async {
      await saveAt('ana', Duration.zero, 1);
      await saveAt('bo', const Duration(hours: 1), 2);
      ageBlobs();

      final removed = await history.prune((_) => null, now);

      expect(removed, 2);
      expect(await history.list('ana'), isEmpty);
      expect(blobs(), isEmpty);
    });

    test('removes a copy once no version names it', () async {
      await saveAt('ana', const Duration(days: 40), 1);
      await saveAt('ana', Duration.zero, 2);
      ageBlobs();

      await history.prune(defaultSpacing, now);

      expect(blobs(), hasLength(1));
      expect((await history.load((await history.list('ana')).single)).bytes, [
        2,
      ]);
    });

    test('keeps a copy that a kept version of another key shares', () async {
      await saveAt('ana', const Duration(days: 40), 1);
      await saveAt('bo', Duration.zero, 1);
      ageBlobs();

      await history.prune(defaultSpacing, now);

      expect(blobs(), hasLength(1));
      expect((await history.load((await history.list('bo')).single)).bytes, [
        1,
      ]);
    });

    test('keeps a young copy that no version names yet', () async {
      await saveAt('ana', Duration.zero, 1);
      File('${dir.path}/history/blobs/${'a' * 64}.gz').writeAsBytesSync([0]);
      final old = File('${dir.path}/history/blobs/${'b' * 64}.gz')
        ..writeAsBytesSync([0]);
      old.setLastModifiedSync(now.subtract(const Duration(minutes: 5)));
      File(
        '${dir.path}/history/blobs/${'a' * 64}.gz',
      ).setLastModifiedSync(now.subtract(const Duration(seconds: 10)));

      await history.prune(defaultSpacing, now);

      expect(
        File('${dir.path}/history/blobs/${'a' * 64}.gz').existsSync(),
        isTrue,
      );
      expect(old.existsSync(), isFalse);
    });

    test('an empty history prunes nothing', () async {
      expect(await history.prune(defaultSpacing, now), 0);
    });
  });
}
