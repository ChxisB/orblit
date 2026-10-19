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
  int _next = 0;
  bool _open = true;

  /// Connects to [address], the `ws://` line a game announces, token included.
  ///
  /// [deadline] bounds each call. The game's own limit is ten seconds a verb,
  /// so the default sits above it. Throws [RemoteError.unavailable] when the
  /// game cannot be reached or refuses the token.
  static Future<RemoteClient> connect(
    Uri address, {
    Duration deadline = const Duration(seconds: 30),
  }) async {
    try {
      final socket = await WebSocket.connect('$address');
      return RemoteClient._(socket, deadline);
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
  Future<Object?> call(String method, Map<String, Object?> params) {
    if (!_open) return Future.error(_gone());
    final id = ++_next;
    final answer = _waiting[id] = Completer();
    _socket.add(
      jsonEncode({
        'jsonrpc': '2.0',
        'id': id,
        'method': method,
        'params': params,
      }),
    );
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
