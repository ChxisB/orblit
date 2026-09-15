import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

const emptyHash =
    'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855';
const abcHash =
    'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad';

void main() {
  group('a content hash', () {
    test('is the SHA-256 of the bytes, as published', () {
      expect(ContentHash.of(const []).hex, emptyHash);
      expect(ContentHash.of(utf8.encode('abc')).hex, abcHash);
    });

    test('from a stream is the same as from the bytes, however cut', () async {
      final bytes = utf8.encode('the quick brown fox jumps over the lazy dog');
      final whole = ContentHash.of(bytes);

      expect(await ContentHash.ofStream(Stream.value(bytes)), whole);
      expect(
        await ContentHash.ofStream(
          Stream.fromIterable([
            bytes.sublist(0, 1),
            bytes.sublist(1, 17),
            const [],
            bytes.sublist(17),
          ]),
        ),
        whole,
      );
    });

    test('of an empty stream is the hash of nothing', () async {
      expect((await ContentHash.ofStream(const Stream.empty())).hex, emptyHash);
    });

    test('is the same for the same bytes and different for different ones', () {
      expect(ContentHash.of([1, 2, 3]), ContentHash.of([1, 2, 3]));
      expect(
        ContentHash.of([1, 2, 3]).hashCode,
        ContentHash.of([1, 2, 3]).hashCode,
      );
      expect(ContentHash.of([1, 2, 3]), isNot(ContentHash.of([1, 2, 4])));
    });

    test('its shard is its first two digits', () {
      expect(ContentHash.of(utf8.encode('abc')).shard, 'ba');
    });

    test('reads back what it writes', () {
      final hash = ContentHash.of(utf8.encode('abc'));
      expect(ContentHash.parse(hash.hex), hash);
      expect(hash.toString(), abcHash);
    });

    test('upper-case digits are read as the same hash', () {
      final upper = ContentHash.parse(abcHash.toUpperCase());
      expect(upper.hex, abcHash);
      expect(upper, ContentHash.parse(abcHash));
    });
  });

  group('a content hash is refused when', () {
    test('it is the wrong length', () {
      expect(
        () => ContentHash.parse(abcHash.substring(1)),
        throwsA(
          isA<FormatException>().having(
            (error) => error.message,
            'message',
            contains('63 characters'),
          ),
        ),
      );
      expect(() => ContentHash.parse(''), throwsFormatException);
    });

    test('it has something in it that is not a hexadecimal digit', () {
      final bad = '${abcHash.substring(0, 10)}g${abcHash.substring(11)}';
      expect(
        () => ContentHash.parse(bad),
        throwsA(
          isA<FormatException>()
              .having((error) => error.message, 'message', contains('"g"'))
              .having((error) => error.offset, 'offset', 10),
        ),
      );
      expect(
        () => ContentHash.parse('${abcHash.substring(1)}:'),
        throwsFormatException,
      );
    });
  });
}
