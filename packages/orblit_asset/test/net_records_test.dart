import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  final hash = ContentHash.of([1, 2, 3]);
  final other = ContentHash.of([4, 5, 6]);
  final url = Uri.parse('https://cdn.example.com/game/robot.glb');

  group('one record', () {
    test('survives the round trip whole', () {
      final when = DateTime.utc(2026, 9, 20, 11, 30);
      final record = FetchRecord(
        hash: hash,
        bytes: 3,
        etag: '"v1"',
        fetched: when,
      );

      final back = FetchRecord.fromJson(record.toJson())!;
      expect(back.hash, hash);
      expect(back.bytes, 3);
      expect(back.etag, '"v1"');
      expect(back.fetched, when);
    });

    test('keeps the time in UTC, wherever it was written', () {
      final local = DateTime.utc(2026, 9, 20, 11, 30).toLocal();
      final back = FetchRecord.fromJson(
        FetchRecord(hash: hash, bytes: 1, fetched: local).toJson(),
      )!;
      expect(back.fetched!.isUtc, isTrue);
      expect(back.fetched!.toUtc(), local.toUtc());
    });

    test('a confirmation moves the time and nothing else', () {
      final record = FetchRecord(
        hash: hash,
        bytes: 3,
        etag: '"v1"',
        fetched: DateTime.utc(2026, 1, 1),
      );
      final now = DateTime.utc(2026, 9, 20);
      final again = record.confirmedAt(now);

      expect(again.hash, hash);
      expect(again.bytes, 3);
      expect(again.etag, '"v1"');
      expect(again.fetched, now);
    });

    test('a record with no tag and no time is still a record', () {
      final back = FetchRecord.fromJson(
        FetchRecord(hash: hash, bytes: 0).toJson(),
      );
      expect(back, isNotNull);
      expect(back!.etag, isNull);
      expect(back.fetched, isNull);
    });

    test('nonsense is no record rather than a crash at startup', () {
      expect(FetchRecord.fromJson(null), isNull);
      expect(FetchRecord.fromJson('a string'), isNull);
      expect(FetchRecord.fromJson(<String, Object?>{}), isNull);
      expect(
        FetchRecord.fromJson({'hash': 'not a hash', 'bytes': 1}),
        isNull,
        reason: 'a hash that will not parse is not one',
      );
      expect(
        FetchRecord.fromJson({'hash': hash.hex, 'bytes': -1}),
        isNull,
        reason: 'a negative length is not a length',
      );
      expect(
        FetchRecord.fromJson({'hash': hash.hex}),
        isNull,
        reason: 'without a length there is nothing to show progress against',
      );
    });

    test('a time that will not parse costs the time, not the record', () {
      final back = FetchRecord.fromJson({
        'hash': hash.hex,
        'bytes': 3,
        'fetched': 'last Tuesday',
      });
      expect(back, isNotNull);
      expect(back!.hash, hash);
      expect(back.fetched, isNull);
    });
  });

  group('the whole set', () {
    test('survives the round trip', () {
      final records = {
        url: FetchRecord(hash: hash, bytes: 3, etag: '"v1"'),
        Uri.parse('https://cdn.example.com/game/wall.ktx2'): FetchRecord(
          hash: other,
          bytes: 6,
        ),
      };

      expect(
        FetchRecordsCodec.decode(FetchRecordsCodec.encode(records)),
        records,
      );
    });

    test('writes the same bytes every time for the same records', () {
      final one = {
        Uri.parse('https://cdn.example.com/b'): FetchRecord(
          hash: hash,
          bytes: 1,
        ),
        Uri.parse('https://cdn.example.com/a'): FetchRecord(
          hash: other,
          bytes: 2,
        ),
      };
      final two = {
        Uri.parse('https://cdn.example.com/a'): FetchRecord(
          hash: other,
          bytes: 2,
        ),
        Uri.parse('https://cdn.example.com/b'): FetchRecord(
          hash: hash,
          bytes: 1,
        ),
      };

      expect(FetchRecordsCodec.encode(one), FetchRecordsCodec.encode(two));
    });

    test('one bad entry costs that entry, not the cache', () {
      // The whole point of a cache that reads this way: a truncated write or
      // a single corrupt line should mean one asset downloads again, not
      // that everything the app has ever fetched does.
      final text = FetchRecordsCodec.encode({
        url: FetchRecord(hash: hash, bytes: 3),
        Uri.parse('https://cdn.example.com/game/broken.bin'): FetchRecord(
          hash: other,
          bytes: 6,
        ),
      }).replaceFirst(other.hex, 'not a hash');

      final back = FetchRecordsCodec.decode(text);
      expect(back.keys, [url]);
    });

    test('a newer format is not guessed at', () {
      final text = FetchRecordsCodec.encode({
        url: FetchRecord(hash: hash, bytes: 3),
      }).replaceFirst('"formatVersion":1', '"formatVersion":2');

      expect(FetchRecordsCodec.decode(text), isEmpty);
    });

    test('anything that is not a record set at all reads as empty', () {
      expect(FetchRecordsCodec.decode(''), isEmpty);
      expect(FetchRecordsCodec.decode('}{ truncated'), isEmpty);
      expect(FetchRecordsCodec.decode('[1, 2, 3]'), isEmpty);
      expect(FetchRecordsCodec.decode('{"formatVersion":1}'), isEmpty);
      expect(
        FetchRecordsCodec.decode('{"formatVersion":1,"records":7}'),
        isEmpty,
      );
    });
  });

  group('records held in memory', () {
    test('remember, forget and clear', () async {
      final records = MemoryFetchRecords();
      expect(await records.get(url), isNull);

      await records.put(url, FetchRecord(hash: hash, bytes: 3));
      expect((await records.get(url))!.hash, hash);

      await records.put(url, FetchRecord(hash: other, bytes: 6));
      expect(
        (await records.get(url))!.hash,
        other,
        reason: 'a URL has one current version, not a history',
      );

      await records.remove(url);
      expect(await records.get(url), isNull);

      await records.put(url, FetchRecord(hash: hash, bytes: 3));
      await records.clear();
      expect(await records.get(url), isNull);
    });

    test('does not hold on to the map it was handed', () async {
      final given = {url: FetchRecord(hash: hash, bytes: 3)};
      final records = MemoryFetchRecords(given);
      await records.remove(url);

      expect(given, hasLength(1), reason: 'the caller keeps its own map');
    });
  });
}
