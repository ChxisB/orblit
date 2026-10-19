import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'verbs.dart';

/// One connection to a running game's control surface. Calls are paired with
/// their answers by id, so several can wait at once.
///
/// It uses only dart:io, so a process that links it loads no native code.
final class RemoteClient {
  RemoteClient._(this._socket, this._deadline) {
    _socket.listen(_answered, onError: (Object _) {}, onDone: _closed);
  }

  final WebSocket _socket;
  final Duration _deadline;
  final Map<int, Completer<Object?>> _waiting = {};
  final Completer<void> _done = Completer();
  int _next = 0;
  bool _open = true;

  /// Completes when the connection ends, whichever side ended it.
  Future<void> get closed => _done.future;

  /// Connects to [address], the `ws://` line a game announces, token included.
  ///
  /// [deadline] bounds each call. A game waits up to ten seconds on one
  /// callback and restarts that wait for every frame a `time.step` passes, so
  /// a long step can outlast the default. [connectTimeout] bounds the wait for
  /// the upgrade. Throws [ArgumentError] for either at zero or less, and
  /// [RemoteError.unavailable] when the game cannot be reached, does not
  /// answer, or refuses the token.
  static Future<RemoteClient> connect(
    Uri address, {
    Duration deadline = const Duration(seconds: 30),
    Duration connectTimeout = const Duration(seconds: 10),
  }) async {
    for (final (name, span) in [
      ('deadline', deadline),
      ('connectTimeout', connectTimeout),
    ]) {
      if (span <= Duration.zero) {
        throw ArgumentError.value(span, name, 'is not above 0');
      }
    }
    try {
      final socket = await WebSocket.connect(
        '$address',
      ).timeout(connectTimeout);
      return RemoteClient._(socket, deadline);
    } on TimeoutException {
      throw RemoteError(
        RemoteError.unavailable,
        '${address.host}:${address.port} did not answer in '
        '${connectTimeout.inSeconds} s.',
      );
    } on WebSocketException {
      // The library's text repeats the address, and the address holds the
      // token, so the message is written here instead.
      throw const RemoteError(
        RemoteError.unavailable,
        'The game refused the connection. Check the address and its token.',
      );
    } on IOException {
      throw RemoteError(
        RemoteError.unavailable,
        'Nothing is listening at ${address.host}:${address.port}.',
      );
    }
  }

  /// The result of [method], or a [RemoteError] with the game's code. Throws
  /// [RemoteError.unavailable] once the connection has closed.
  Future<Object?> call(String method, Map<String, Object?> params) async {
    if (!_open) throw _gone();
    final id = ++_next;
    // Encoded before the waiter exists, because a value JSON cannot carry
    // throws here and would leave the waiter in the map until close.
    final message = jsonEncode({
      'jsonrpc': '2.0',
      'id': id,
      'method': method,
      'params': params,
    });
    final answer = _waiting[id] = Completer();
    _socket.add(message);
    return answer.future.timeout(
      _deadline,
      onTimeout: () {
        _waiting.remove(id);
        throw RemoteError(
          RemoteError.timedOut,
          'No answer to $method in ${_deadline.inSeconds} s',
        );
      },
    );
  }

  Future<void> close() => _socket.close();

  void _answered(Object? data) {
    final Object? reply;
    try {
      reply = jsonDecode(data! as String);
    } on Object {
      return;
    }
    if (reply is! Map<String, Object?>) return;
    final waiting = _waiting.remove(reply['id']);
    if (waiting == null) return;
    final error = reply['error'];
    if (error is Map<String, Object?>) {
      waiting.completeError(
        RemoteError(
          error['code'] is int ? error['code']! as int : RemoteError.internal,
          '${error['message']}',
        ),
      );
    } else {
      waiting.complete(reply['result']);
    }
  }

  void _closed() {
    _open = false;
    _done.complete();
    final waiting = _waiting.values.toList();
    _waiting.clear();
    for (final call in waiting) {
      call.completeError(_gone());
    }
  }

  RemoteError _gone() => const RemoteError(
    RemoteError.unavailable,
    'The game closed the connection',
  );
}
