@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

/// What every content store has to do, whatever it keeps its bytes in.
void behavesLikeAContentStore(ContentStore Function() make) {
  test('what is put can be got back by the hash put gives', () async {
    final store = make();
    final hash = await store.put(utf8.encode('abc'));
    expect(hash, ContentHash.of(utf8.encode('abc')));
    expect(await store.get(hash), utf8.encode('abc'));
    expect(await store.contains(hash), isTrue);
  });

  test('a hash nothing was put under is not there', () async {
    final store = make();
    final hash = ContentHash.of([1, 2, 3]);
    expect(await store.get(hash), isNull);
    expect(await store.contains(hash), isFalse);
  });

  test('nothing at all can be put and got back', () async {
    final store = make();
    final hash = await store.put(const []);
    expect(await store.get(hash), isEmpty);
    expect(await store.contains(hash), isTrue);
  });

  test('a removed entry is gone', () async {
    final store = make();
    final hash = await store.put([1, 2, 3]);
    await store.remove(hash);
    expect(await store.get(hash), isNull);
    expect(await store.contains(hash), isFalse);
  });

  test('removing what is not there is not an error', () async {
    final store = make();
    await store.remove(ContentHash.of([9]));
  });

  test('putting the same bytes twice gives the same hash', () async {
    final store = make();
    final first = await store.put([1, 2, 3]);
    final second = await store.put([1, 2, 3]);
    expect(second, first);
    expect(await store.get(first), [1, 2, 3]);
  });

  test('putting the same bytes many times at once is safe', () async {
    final store = make();
    final bytes = List.generate(4096, (i) => i % 251);
    final hashes = await Future.wait([
      for (var i = 0; i < 8; i++) store.put(bytes),
    ]);
    expect(hashes.toSet(), {ContentHash.of(bytes)});
    expect(await store.get(hashes.first), bytes);
  });

  test(
    'changing bytes after putting or getting them changes nothing',
    () async {
      final store = make();
      final given = [1, 2, 3];
      final hash = await store.put(given);
      given[0] = 9;

      final got = (await store.get(hash))!;
      got[1] = 9;
      expect(await store.get(hash), [1, 2, 3]);
    },
  );
}

void main() {
  group('a memory content store', () {
    behavesLikeAContentStore(MemoryContentStore.new);
  });

  group('a directory content store', () {
    late Directory temp;

    setUp(() async {
      temp = await Directory.systemTemp.createTemp('orblit_asset_store_');
    });

    tearDown(() => temp.delete(recursive: true));

    String pathOf(ContentHash hash) =>
        [temp.path, hash.shard, hash.hex].join(Platform.pathSeparator);

    List<File> filesUnder(String path) => Directory(path).existsSync()
        ? Directory(path).listSync(recursive: true).whereType<File>().toList()
        : const [];

    behavesLikeAContentStore(() => DirectoryContentStore(temp.path));

    test('files an entry under its shard, named by its hash', () async {
      final hash = await DirectoryContentStore(
        temp.path,
      ).put(utf8.encode('abc'));
      final file = File(pathOf(hash));
      expect(file.existsSync(), isTrue);
      expect(
        file.path,
        endsWith(
          '${Platform.pathSeparator}ba${Platform.pathSeparator}${hash.hex}',
        ),
      );
      expect(file.readAsBytesSync(), utf8.encode('abc'));
    });

    test(
      'a store whose directory is not there yet is empty, and put makes it',
      () async {
        final root = [temp.path, 'not', 'yet'].join(Platform.pathSeparator);
        final store = DirectoryContentStore(root);
        expect(await store.get(ContentHash.of([1])), isNull);

        final hash = await store.put([1]);
        expect(await store.get(hash), [1]);
      },
    );

    test(
      'many puts of the same bytes at once leave one entry and nothing half-written',
      () async {
        final store = DirectoryContentStore(temp.path);
        final bytes = List.generate(65536, (i) => i % 253);
        await Future.wait([for (var i = 0; i < 16; i++) store.put(bytes)]);

        final incoming = [temp.path, '.incoming'].join(Platform.pathSeparator);
        expect(filesUnder(incoming), isEmpty);
        final stored = filesUnder(temp.path);
        expect(stored, hasLength(1));
        expect(stored.single.readAsBytesSync(), bytes);
      },
    );

    test('two stores over one directory see each other\'s entries', () async {
      final writer = DirectoryContentStore(temp.path);
      final reader = DirectoryContentStore(temp.path);
      final hash = await writer.put([4, 5, 6]);
      expect(await reader.get(hash), [4, 5, 6]);
    });

    test(
      'bytes damaged on disk are noticed, removed and not handed back',
      () async {
        final store = DirectoryContentStore(temp.path);
        final hash = await store.put(utf8.encode('the right bytes'));
        File(pathOf(hash)).writeAsBytesSync(utf8.encode('the wrong bytes'));

        expect(
          await store.contains(hash),
          isTrue,
          reason: 'not read, so not checked',
        );
        expect(await store.get(hash), isNull);
        expect(File(pathOf(hash)).existsSync(), isFalse);
        expect(await store.contains(hash), isFalse);

        // And the next put of the right bytes puts them back.
        await store.put(utf8.encode('the right bytes'));
        expect(await store.get(hash), utf8.encode('the right bytes'));
      },
    );

    test('a file cut short on disk is noticed too', () async {
      final store = DirectoryContentStore(temp.path);
      final hash = await store.put(List.generate(1000, (i) => i % 256));
      File(pathOf(hash)).writeAsBytesSync(List.generate(500, (i) => i % 256));
      expect(await store.get(hash), isNull);
    });
  });
}
