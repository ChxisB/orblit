import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

import 'support.dart';

/// A store that holds each write until the test lets it through, or fails it.
final class _Gated implements SaveStore {
  final MemorySaveStore inner = MemorySaveStore();
  final List<Completer<void>> gates = [];
  bool fail = false;
  bool open = false;
  int written = 0;

  @override
  Future<void> write(String key, SaveRecord record) async {
    if (!open) {
      final gate = Completer<void>();
      gates.add(gate);
      await gate.future;
    }
    if (fail) throw StateError('disk full');
    await inner.write(key, record);
    written++;
  }

  void let() {
    for (final gate in gates.where((g) => !g.isCompleted)) {
      gate.complete();
    }
    gates.clear();
  }

  @override
  Future<SaveRecord?> read(String key) => inner.read(key);

  @override
  Future<bool> exists(String key) => inner.exists(key);

  @override
  Future<List<String>> keys() => inner.keys();
}

void main() {
  final failures = <String>[];
  late Directory dir;
  late FileSaveStore files;

  setUp(() {
    failures.clear();
    dir = Directory.systemTemp.createTempSync('orblit_save_queue');
    files = FileSaveStore('${dir.path}/saves');
  });
  tearDown(() => dir.deleteSync(recursive: true));

  void noteFailure(String key, Object error) => failures.add(key);

  group('an inline queue', () {
    test('a read straight after save returns the new bytes', () async {
      final store = _Gated();
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Autosave());
      final done = queue.flush();

      queue.save('ana', bytesOf([2]), const Leave());
      final seen = await queue.read('ana');

      expect(seen!.bytes, [2]);
      expect(seen.reason, const Leave());
      expect(await store.inner.read('ana'), isNull);
      expect(await queue.exists('ana'), isTrue);

      store.open = true;
      store.let();
      await done;
      await queue.flush();
      expect((await store.inner.read('ana'))!.bytes, [2]);
    });

    test('an answer for an older save does not hide a newer one', () async {
      final store = _Gated();
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Autosave());
      queue.save('ana', bytesOf([2]), const Autosave());

      await Future<void>.delayed(Duration.zero);
      store.gates.first.complete();
      await Future<void>.delayed(Duration.zero);
      await Future<void>.delayed(Duration.zero);

      expect((await store.inner.read('ana'))!.bytes, [1]);
      expect((await queue.read('ana'))!.bytes, [2]);
      expect(queue.pending, 1);

      store.open = true;
      store.let();
      await queue.flush();
      expect((await store.inner.read('ana'))!.bytes, [2]);
    });

    test('a flush does not write a save that is already on its way', () async {
      final store = _Gated();
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Autosave());
      final done = queue.flush();
      await Future<void>.delayed(Duration.zero);

      store.open = true;
      store.let();
      await done;
      await Future<void>.delayed(Duration.zero);
      await Future<void>.delayed(Duration.zero);

      expect(store.written, 1);
    });

    test('writes happen one at a time, in the order they were saved', () async {
      final store = _Gated();
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('a', bytesOf([1]), const Autosave());
      queue.save('b', bytesOf([2]), const Autosave());
      await Future<void>.delayed(Duration.zero);

      expect(store.gates, hasLength(1), reason: 'b waits for a');

      store.gates.first.complete();
      await Future<void>.delayed(Duration.zero);
      await Future<void>.delayed(Duration.zero);

      expect(store.gates, hasLength(2), reason: 'b starts once a is done');
      expect(store.written, 1);

      store.open = true;
      store.let();
      await queue.flush();
    });

    test('the bytes are copied when saved', () async {
      final queue = SaveQueue.inline(
        MemorySaveStore(),
        clock: fixedClock,
        onFailure: noteFailure,
      );
      final bytes = bytesOf([1, 2]);
      queue.save('ana', bytes, const Autosave());
      bytes[0] = 99;

      await queue.flush();

      expect((await queue.read('ana'))!.bytes, [1, 2]);
    });

    test('the record carries the clock and the reason', () async {
      final store = MemorySaveStore();
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Shutdown());
      await queue.flush();

      final record = (await store.read('ana'))!;

      expect(record.at, fixedClock());
      expect(record.reason, const Shutdown());
    });

    test('the newest of two saves is what lands', () async {
      final store = _Gated();
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Autosave());
      queue.save('ana', bytesOf([2]), const Autosave());
      expect(queue.pending, 2);

      store.open = true;
      store.let();
      expect(await queue.flush(), isEmpty);

      expect(queue.pending, 0);
      expect((await store.inner.read('ana'))!.bytes, [2]);
    });

    test('a failed write is reported, kept and tried again by flush', () async {
      final store = _Gated()
        ..open = true
        ..fail = true;
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Autosave());

      expect(await queue.flush(), {'ana'});
      expect(failures, ['ana']);
      expect((await queue.read('ana'))!.bytes, [1]);
      expect(await store.inner.exists('ana'), isFalse);

      expect(await queue.flush(), {'ana'}, reason: 'it tried again');
      expect(failures, ['ana', 'ana']);

      store.fail = false;
      expect(await queue.flush(), isEmpty);
      expect((await store.inner.read('ana'))!.bytes, [1]);
    });

    test('a newer save that works replaces an older one that failed', () async {
      final store = _Gated()
        ..open = true
        ..fail = true;
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );
      queue.save('ana', bytesOf([1]), const Autosave());
      await queue.flush();

      store.fail = false;
      queue.save('ana', bytesOf([2]), const Autosave());

      expect(await queue.flush(), isEmpty);
      expect((await store.inner.read('ana'))!.bytes, [2]);
    });

    test('a key that is not a plain name is refused at save', () {
      final queue = SaveQueue.inline(
        MemorySaveStore(),
        clock: fixedClock,
        onFailure: noteFailure,
      );

      expect(
        () => queue.save('../x', bytesOf([1]), const Autosave()),
        throwsArgumentError,
      );
      expect(queue.pending, 0);
    });

    test('flush with nothing queued returns at once', () async {
      final queue = SaveQueue.inline(
        MemorySaveStore(),
        clock: fixedClock,
        onFailure: noteFailure,
      );

      expect(await queue.flush(), isEmpty);
    });

    test('reads fall through to the store for a key never queued', () async {
      final store = MemorySaveStore();
      await store.write('ana', recordOf([7]));
      final queue = SaveQueue.inline(
        store,
        clock: fixedClock,
        onFailure: noteFailure,
      );

      expect((await queue.read('ana'))!.bytes, [7]);
      expect(await queue.exists('ana'), isTrue);
      expect(await queue.read('bo'), isNull);
      expect(await queue.exists('bo'), isFalse);
    });
  });

  group('a queue with a worker isolate', () {
    SaveQueue spawn() =>
        SaveQueue.spawn(files, clock: fixedClock, onFailure: noteFailure);

    test('writes a save to the file store', () async {
      final queue = spawn();
      queue.save('ana', bytesOf([1, 2, 3]), Custom('first'));

      expect(await queue.close(), isEmpty);

      final record = (await files.read('ana'))!;
      expect(record.bytes, [1, 2, 3]);
      expect(record.reason, Custom('first'));
      expect(record.at, fixedClock());
      expect(queue.workerStarts, 1, reason: 'one worker, and it ended');
    });

    test('a queue closed before its worker is up still ends it', () async {
      final queue = spawn();

      expect(await queue.close(), isEmpty);

      expect(queue.workerStarts, 1);
    });

    test('a read straight after save returns the new bytes', () async {
      final queue = spawn();
      queue.save('ana', bytesOf([5]), const Autosave());

      expect((await queue.read('ana'))!.bytes, [5]);

      await queue.close();
    });

    test('ten saves all land when the worker ends first', () async {
      final queue = spawn();
      queue.killWorker();
      for (var i = 0; i < 10; i++) {
        queue.save('p$i', bytesOf([i]), const Autosave());
      }

      expect(await queue.close(), isEmpty);

      for (var i = 0; i < 10; i++) {
        expect((await files.read('p$i'))!.bytes, [i], reason: 'p$i');
      }
    });

    test('ten saves all land when the worker ends part way', () async {
      final queue = spawn();
      for (var i = 0; i < 5; i++) {
        queue.save('p$i', bytesOf([i]), const Autosave());
      }
      await Future<void>.delayed(const Duration(milliseconds: 50));
      queue.killWorker();
      for (var i = 5; i < 10; i++) {
        queue.save('p$i', bytesOf([i]), const Autosave());
      }

      expect(await queue.close(), isEmpty);

      expect(await files.keys(), hasLength(10));
    });

    test('a worker that ends twice is started twice', () async {
      final queue = spawn();
      queue.save('a', bytesOf([1]), const Autosave());
      await queue.flush();

      expect(queue.workerStarts, 1);
      queue.killWorker();
      await Future<void>.delayed(const Duration(milliseconds: 100));
      queue.killWorker();
      await Future<void>.delayed(const Duration(milliseconds: 100));
      queue.save('b', bytesOf([2]), const Autosave());

      expect(await queue.close(), isEmpty);
      expect(await files.keys(), unorderedEquals(['a', 'b']));
      expect(queue.workerStarts, 3, reason: 'two restarts, none after close');
    });

    test('the newest of many saves of one key is what lands', () async {
      final queue = spawn();
      for (var i = 0; i < 50; i++) {
        queue.save('ana', Uint8List.fromList([i]), const Autosave());
      }

      await queue.close();

      expect((await files.read('ana'))!.bytes, [49]);
    });

    test('a write that fails is reported and kept', () async {
      final queue = SaveQueue.spawn(
        FileSaveStore('${dir.path}/file-not-dir/inside'),
        clock: fixedClock,
        onFailure: noteFailure,
      );
      File('${dir.path}/file-not-dir').writeAsStringSync('in the way');
      queue.save('ana', bytesOf([1]), const Autosave());

      expect(await queue.flush(), {'ana'});

      expect(failures, isNotEmpty);
      expect((await queue.read('ana'))!.bytes, [1]);
      await queue.close();
    });
  });
}
