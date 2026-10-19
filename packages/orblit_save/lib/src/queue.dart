import 'dart:async';
import 'dart:isolate';
import 'dart:typed_data';

import 'reason.dart';
import 'record.dart';
import 'store.dart';

/// Takes saves from the tick and writes them to a [SaveStore] somewhere else.
///
/// [save] returns at once. A read sees the newest save before it reaches the
/// store. A write that fails is reported to the caller's `onFailure` and the
/// save is kept in memory, so reads stay right and [flush] tries again.
final class SaveQueue {
  SaveQueue._(this._store, this._clock, this._onFailure);

  /// A queue whose writes happen in a worker isolate. [store] is copied into
  /// it, so it must hold nothing that cannot be sent, and a [MemorySaveStore]
  /// would be a second, separate store. A worker that ends early starts again
  /// and writes the saves that had no answer.
  factory SaveQueue.spawn(
    SaveStore store, {
    required DateTime Function() clock,
    required void Function(String key, Object error) onFailure,
  }) {
    final queue = SaveQueue._(store, clock, onFailure);
    queue._sink = _IsolateSink(store, queue._answer, () => queue._open.values);
    return queue;
  }

  /// A queue whose writes happen on this isolate, one at a time. For tests and
  /// for a store that does no heavy work.
  factory SaveQueue.inline(
    SaveStore store, {
    required DateTime Function() clock,
    required void Function(String key, Object error) onFailure,
  }) {
    final queue = SaveQueue._(store, clock, onFailure);
    queue._sink = _InlineSink(store, queue._answer);
    return queue;
  }

  final SaveStore _store;
  final DateTime Function() _clock;
  final void Function(String key, Object error) _onFailure;
  late final _Sink _sink;

  int _nextId = 0;

  /// Saves sent and not answered, oldest first.
  final Map<int, _Entry> _open = {};

  /// The newest save of each key that the store may not hold yet: sent, or
  /// failed.
  final Map<String, _Entry> _held = {};

  Completer<void>? _idle;

  /// How many saves are waiting for the store.
  int get pending => _open.length;

  /// Queues [bytes] under [key]. The bytes are copied.
  void save(String key, Uint8List bytes, SaveReason reason) {
    checkKey(key);
    final record = SaveRecord(bytes: bytes, reason: reason, at: _clock());
    _send(_Entry(_nextId++, key, record));
  }

  void _send(_Entry entry) {
    _open[entry.id] = entry;
    _held[entry.key] = entry;
    _sink.send(entry);
  }

  void _answer(int id, Object? error) {
    final entry = _open.remove(id);
    if (entry == null) return; // answered twice after a restart
    final held = _held[entry.key];
    if (error == null) {
      if (held != null && held.id <= id) _held.remove(entry.key);
    } else {
      _onFailure(entry.key, error);
    }
    if (_open.isEmpty) {
      _idle?.complete();
      _idle = null;
    }
  }

  /// The newest save under [key], including one not yet written.
  Future<SaveRecord?> read(String key) async =>
      _held[key]?.record ?? await _store.read(key);

  Future<bool> exists(String key) async =>
      _held.containsKey(key) || await _store.exists(key);

  /// Every key with a save, including one not yet written.
  Future<Set<String>> keys() async => {..._held.keys, ...await _store.keys()};

  /// Writes again every save that failed, waits until none is waiting, and
  /// returns the keys that are still not on the store.
  Future<Set<String>> flush() async {
    for (final entry in _held.values.toList()) {
      if (!_open.containsKey(entry.id)) _send(entry);
    }
    if (_open.isNotEmpty) await (_idle ??= Completer<void>()).future;
    return _held.keys.toSet();
  }

  /// Flushes, then stops the worker and waits for it to end. The queue is not
  /// used after this.
  Future<Set<String>> close() async {
    final stuck = await flush();
    await _sink.close();
    return stuck;
  }

  /// Ends the worker isolate as a crash would. For tests.
  void killWorker() => _sink.kill();

  /// How many worker isolates have been started. For tests.
  int get workerStarts => _sink.workerStarts;
}

final class _Entry {
  _Entry(this.id, this.key, this.record);

  final int id;
  final String key;
  final SaveRecord record;
}

abstract interface class _Sink {
  int get workerStarts;

  void send(_Entry entry);

  Future<void> close();

  void kill();
}

typedef _Answer = void Function(int id, Object? error);

final class _InlineSink implements _Sink {
  _InlineSink(this._store, this._answer);

  final SaveStore _store;
  final _Answer _answer;
  Future<void> _tail = Future.value();

  @override
  void send(_Entry entry) {
    _tail = _tail
        .then((_) => _store.write(entry.key, entry.record))
        .then(
          (_) => _answer(entry.id, null),
          onError: (Object e) => _answer(entry.id, e),
        );
  }

  @override
  int get workerStarts => 0;

  @override
  Future<void> close() async {}

  @override
  void kill() {}
}

final class _Boot {
  const _Boot(this.replies, this.store);

  final SendPort replies;
  final SaveStore store;
}

Future<void> _work(_Boot boot) async {
  final inbox = ReceivePort();
  boot.replies.send(inbox.sendPort);
  await for (final message in inbox) {
    if (message case [
      final int id,
      final String key,
      final SaveRecord record,
    ]) {
      try {
        await boot.store.write(key, record);
        boot.replies.send([id, null]);
      } on Object catch (e) {
        boot.replies.send([id, e.toString()]);
      }
    } else {
      break; // null is the stop message
    }
  }
  inbox.close();
}

final class _IsolateSink implements _Sink {
  _IsolateSink(this._store, this._answer, this._open) {
    _replies.listen(_onReply);
    _exits.listen((_) => _onExit());
    _start();
  }

  final SaveStore _store;
  final _Answer _answer;
  final Iterable<_Entry> Function() _open;
  final ReceivePort _replies = ReceivePort();
  final ReceivePort _exits = ReceivePort();
  Isolate? _isolate;
  SendPort? _worker;
  final Completer<void> _exited = Completer<void>();
  bool _closing = false;

  @override
  int workerStarts = 0;

  void _start() {
    workerStarts++;
    // A failure to start an isolate is not recoverable and should be loud.
    unawaited(
      Isolate.spawn(
        _work,
        _Boot(_replies.sendPort, _store),
        onExit: _exits.sendPort,
      ).then((isolate) => _isolate = isolate),
    );
  }

  void _onReply(Object? message) {
    switch (message) {
      case SendPort() when _closing:
        message.send(null);
      case SendPort():
        _worker = message;
        for (final entry in _open().toList()) {
          send(entry);
        }
      case [final int id, final Object? error]:
        _answer(id, error);
    }
  }

  void _onExit() {
    _worker = null;
    _isolate = null;
    if (!_closing) {
      _start();
      return;
    }
    _replies.close();
    _exits.close();
    _exited.complete();
  }

  @override
  void send(_Entry entry) => _worker?.send([entry.id, entry.key, entry.record]);

  @override
  Future<void> close() {
    _closing = true;
    _worker?.send(null);
    return _exited.future;
  }

  @override
  void kill() => _isolate?.kill(priority: Isolate.immediate);
}
