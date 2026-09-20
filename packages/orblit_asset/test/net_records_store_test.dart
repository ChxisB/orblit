@TestOn('vm')
library;

import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  late Directory temp;
  late String root;

  final hash = ContentHash.of([1, 2, 3]);
  final other = ContentHash.of([4, 5, 6]);
  final url = Uri.parse('https://cdn.example.com/game/robot.glb');

  setUp(() async {
    temp = await Directory.systemTemp.createTemp('orblit_records_');
    root = temp.path;
  });

  tearDown(() async {
    if (await temp.exists()) await temp.delete(recursive: true);
  });

  group('records kept in a directory', () {
    test('an empty directory is simply nothing known', () async {
      final records = DirectoryFetchRecords(root);
      expect(await records.get(url), isNull);
    });

    test('what was written is there on the next run', () async {
      final writing = DirectoryFetchRecords(root, settle: Duration.zero);
      await writing.put(url, FetchRecord(hash: hash, bytes: 3, etag: '"v1"'));
      await writing.close();

      final reading = DirectoryFetchRecords(root);
      final back = await reading.get(url);
      expect(back!.hash, hash);
      expect(back.etag, '"v1"');
    });

    test('forgetting one survives the round trip too', () async {
      final writing = DirectoryFetchRecords(root, settle: Duration.zero);
      await writing.put(url, FetchRecord(hash: hash, bytes: 3));
      await writing.put(
        Uri.parse('https://cdn.example.com/game/wall.ktx2'),
        FetchRecord(hash: other, bytes: 6),
      );
      await writing.remove(url);
      await writing.close();

      final reading = DirectoryFetchRecords(root);
      expect(await reading.get(url), isNull);
      expect(
        await reading.get(Uri.parse('https://cdn.example.com/game/wall.ktx2')),
        isNotNull,
      );
    });

    test('a corrupt file costs the cache, not the launch', () async {
      await File(
        [root, 'records.json'].join(Platform.pathSeparator),
      ).writeAsString('{ this is not json');

      final records = DirectoryFetchRecords(root);
      expect(await records.get(url), isNull);

      // And it recovers: writing over it works.
      await records.put(url, FetchRecord(hash: hash, bytes: 3));
      await records.close();
      expect(await DirectoryFetchRecords(root).get(url), isNotNull);
    });

    test('the file is only ever whole', () async {
      // Written to one side and renamed into place, so a reader never sees
      // half a set — the same reason the content store does it.
      final records = DirectoryFetchRecords(root, settle: Duration.zero);
      for (var at = 0; at < 50; at++) {
        await records.put(
          Uri.parse('https://cdn.example.com/game/$at.bin'),
          FetchRecord(hash: hash, bytes: at),
        );
      }
      await records.close();

      final text = await File(
        [root, 'records.json'].join(Platform.pathSeparator),
      ).readAsString();
      expect(FetchRecordsCodec.decode(text), hasLength(50));
    });

    test('an unwritable directory does not take the app down', () async {
      final records = DirectoryFetchRecords(
        [root, 'nothing', 'here'].join(Platform.pathSeparator),
        settle: Duration.zero,
      );
      // Creating it is allowed to work; what must not happen is a throw out
      // of put, which is called from the middle of a download.
      await records.put(url, FetchRecord(hash: hash, bytes: 3));
      await records.flush();
      expect(await records.get(url), isNotNull);
    });
  });

  group('writes are coalesced', () {
    test('a burst of changes costs one write', () async {
      final counted = _Counted(settle: const Duration(milliseconds: 20));
      for (var at = 0; at < 200; at++) {
        await counted.put(
          Uri.parse('https://cdn.example.com/game/$at.bin'),
          FetchRecord(hash: hash, bytes: at),
        );
      }
      await counted.flush();

      expect(
        counted.writes,
        1,
        reason: 'two hundred assets loading is one write, not two hundred',
      );
      expect(FetchRecordsCodec.decode(counted.text!), hasLength(200));
    });

    test('a later change is written too', () async {
      final counted = _Counted(settle: Duration.zero);
      await counted.put(url, FetchRecord(hash: hash, bytes: 3));
      await counted.flush();
      await counted.put(url, FetchRecord(hash: other, bytes: 6));
      await counted.flush();

      expect(counted.writes, 2);
      expect(FetchRecordsCodec.decode(counted.text!)[url]!.hash, other);
    });

    test('nothing is written when nothing changed', () async {
      final counted = _Counted();
      await counted.get(url);
      await counted.flush();

      expect(counted.writes, 0);
    });

    test('a store that cannot be written is not a crash', () async {
      final broken = _Broken(settle: Duration.zero);
      await broken.put(url, FetchRecord(hash: hash, bytes: 3));
      await broken.flush();

      // Still answers from memory; the cost was the next launch, not this one.
      expect((await broken.get(url))!.hash, hash);
    });

    test('a store that cannot be read starts empty', () async {
      final broken = _Broken();
      expect(await broken.get(url), isNull);
    });

    test('closing stops scheduling but not reading', () async {
      final counted = _Counted(settle: const Duration(milliseconds: 20));
      await counted.put(url, FetchRecord(hash: hash, bytes: 3));
      await counted.close();
      expect(counted.writes, 1);

      await counted.put(url, FetchRecord(hash: other, bytes: 6));
      await Future<void>.delayed(const Duration(milliseconds: 60));
      expect(counted.writes, 1, reason: 'closed means closed');
      expect((await counted.get(url))!.hash, other);
    });
  });
}

/// Buffered records over a string in memory, counting the writes.
class _Counted extends BufferedFetchRecords {
  _Counted({super.settle});

  String? text;
  var writes = 0;

  @override
  Future<String?> readText() async => text;

  @override
  Future<void> writeText(String wrote) async {
    writes++;
    text = wrote;
  }
}

/// Buffered records over a store that does not work.
class _Broken extends BufferedFetchRecords {
  _Broken({super.settle});

  @override
  Future<String?> readText() async => throw const FileSystemException('no');

  @override
  Future<void> writeText(String text) async =>
      throw const FileSystemException('no');
}
