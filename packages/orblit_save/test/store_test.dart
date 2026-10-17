import 'dart:io';

import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

import 'support.dart';

void main() {
  late Directory dir;

  setUp(() => dir = Directory.systemTemp.createTempSync('orblit_save_test'));
  tearDown(() => dir.deleteSync(recursive: true));

  final stores = <String, SaveStore Function()>{
    'MemorySaveStore': MemorySaveStore.new,
    'FileSaveStore': () => FileSaveStore('${dir.path}/saves'),
  };

  for (final MapEntry(key: name, value: make) in stores.entries) {
    group(name, () {
      test('reads back what was written, with its reason and time', () async {
        final store = make();
        await store.write(
          'ana',
          recordOf([1, 2, 3], reason: Custom('pre-restore'), at: 4321),
        );

        final record = (await store.read('ana'))!;

        expect(record.bytes, [1, 2, 3]);
        expect(record.reason, Custom('pre-restore'));
        expect(record.at.millisecondsSinceEpoch, 4321);
        expect(record.at.isUtc, isTrue);
      });

      test('a missing key reads as null and does not exist', () async {
        final store = make();

        expect(await store.read('ana'), isNull);
        expect(await store.exists('ana'), isFalse);
        expect(await store.keys(), isEmpty);
      });

      test('a second write replaces the first', () async {
        final store = make();
        await store.write('ana', recordOf([1]));
        await store.write('ana', recordOf([2, 2]));

        expect((await store.read('ana'))!.bytes, [2, 2]);
        expect(await store.exists('ana'), isTrue);
        expect(await store.keys(), ['ana']);
      });

      test('an empty save is a save', () async {
        final store = make();
        await store.write('ana', recordOf([]));

        expect((await store.read('ana'))!.bytes, isEmpty);
      });

      test('lists every key', () async {
        final store = make();
        await store.write('ana', recordOf([1]));
        await store.write('bo.2', recordOf([2]));

        expect(await store.keys(), unorderedEquals(['ana', 'bo.2']));
      });

      test('a key that is not a plain name is refused', () async {
        final store = make();
        final bad = ['', '.hidden', '../x', 'a/b', r'a\b', 'a b', 'x' * 129];

        for (final key in bad) {
          await expectLater(
            store.write(key, recordOf([1])),
            throwsArgumentError,
            reason: key,
          );
        }
        await expectLater(store.read('../x'), throwsArgumentError);
      });
    });
  }

  group('FileSaveStore', () {
    String path(String key) => '${dir.path}/saves/$key.save';

    test('a write cut off halfway leaves the old save readable', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1, 2, 3]));
      // What a crash between the first byte and the rename leaves behind.
      File('${path('ana')}.tmp').writeAsBytesSync([0x4F, 0x53, 0x41]);

      expect((await store.read('ana'))!.bytes, [1, 2, 3]);
      expect(await store.keys(), ['ana']);
    });

    test('a write that cannot start leaves the old save as it was', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1, 2, 3]));
      Directory('${path('ana')}.tmp').createSync();

      await expectLater(
        store.write('ana', recordOf([9])),
        throwsA(isA<FileSystemException>()),
      );

      expect((await store.read('ana'))!.bytes, [1, 2, 3]);
    });

    test('a write after a cut off one replaces the leftover', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1]));
      File('${path('ana')}.tmp').writeAsBytesSync(List.filled(500, 7));

      await store.write('ana', recordOf([9]));

      expect((await store.read('ana'))!.bytes, [9]);
      expect(File('${path('ana')}.tmp').existsSync(), isFalse);
    });

    test('a write leaves no temporary file', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1]));

      expect(Directory('${dir.path}/saves').listSync().map((e) => e.path), [
        path('ana'),
      ]);
    });

    test('a damaged file throws and names the key', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1, 2, 3]));
      final file = File(path('ana'));

      for (final bad in <List<int>>[
        [],
        [1, 2, 3],
        [0x4F, 0x53, 0x41, 0x56, 255, 0, 0, 0],
        [0x4F, 0x53, 0x41, 0x56, 2, 0, 0, 0, 0x7B, 0x7D],
        [0x58, 0x53, 0x41, 0x56, 0, 0, 0, 0],
      ]) {
        file.writeAsBytesSync(bad);
        await expectLater(
          store.read('ana'),
          throwsA(
            isA<FormatException>().having(
              (e) => e.message,
              'message',
              contains('"ana"'),
            ),
          ),
          reason: '$bad',
        );
      }
    });

    test('a file that is whole but has the wrong magic is refused', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1, 2, 3]));
      final file = File(path('ana'));
      final bytes = file.readAsBytesSync();
      bytes[0] = bytes[0] ^ 0xFF;
      file.writeAsBytesSync(bytes);

      await expectLater(store.read('ana'), throwsFormatException);
    });

    test('a file of a later format is refused', () async {
      final store = FileSaveStore('${dir.path}/saves');
      await store.write('ana', recordOf([1]));
      final file = File(path('ana'));
      final bytes = file.readAsBytesSync();
      final text = String.fromCharCodes(bytes);
      file.writeAsStringSync(text.replaceFirst('"format":1', '"format":2'));

      await expectLater(store.read('ana'), throwsFormatException);
    });
  });
}
