import 'dart:io';
import 'dart:math';

import 'verbs.dart';

/// The verbs of [RemoteVerbs], served over a loopback WebSocket behind a token.
///
/// The token is made at start and printed once. A connection that does not
/// carry it in the `token` query is refused before the upgrade.
final class RemoteServer {
  RemoteServer._(this._http, this._token);

  final HttpServer _http;
  final String _token;
  final Set<WebSocket> _sockets = {};

  /// Opens the port on the loopback address and calls [announce] with the line
  /// a client reads, `orblit-remote ws://127.0.0.1:PORT/?token=TOKEN`.
  ///
  /// [random] makes the token, and defaults to a secure source. Throws
  /// [StateError] unless asserts are on, which is the case in a debug build and
  /// not in a profile or release one.
  static Future<RemoteServer> start(
    RemoteHost host, {
    Random? random,
    int port = 0,
    void Function(String line) announce = print,
  }) async {
    var debug = false;
    assert(() {
      debug = true;
      return true;
    }());
    if (!debug) {
      throw StateError('The control surface runs in a debug build only');
    }
    final http = await HttpServer.bind(InternetAddress.loopbackIPv4, port);
    final server = RemoteServer._(http, _tokenFrom(random ?? Random.secure()));
    server._serve(RemoteVerbs(host));
    announce('orblit-remote ${server.uri}');
    return server;
  }

  /// The address a client connects to, token included.
  Uri get uri => Uri(
    scheme: 'ws',
    host: _http.address.address,
    port: _http.port,
    path: '/',
    queryParameters: {'token': _token},
  );

  /// Closes the port and every connection.
  Future<void> close() async {
    await _http.close(force: true);
    await Future.wait([
      for (final socket in _sockets.toList())
        socket.close(WebSocketStatus.goingAway),
    ]);
  }

  void _serve(RemoteVerbs verbs) {
    _http.listen((request) async {
      final token = request.uri.queryParameters['token'];
      if (!WebSocketTransformer.isUpgradeRequest(request) || !_matches(token)) {
        request.response.statusCode = HttpStatus.forbidden;
        await request.response.close();
        return;
      }
      final WebSocket socket;
      try {
        socket = await WebSocketTransformer.upgrade(request);
      } on WebSocketException {
        return;
      }
      _sockets.add(socket);
      socket.listen(
        (data) => _answer(verbs, socket, data),
        onDone: () => _sockets.remove(socket),
      );
    });
  }

  Future<void> _answer(
    RemoteVerbs verbs,
    WebSocket socket,
    Object? data,
  ) async {
    if (data is! String) return;
    final reply = await verbs.handle(data);
    if (reply != null && socket.readyState == WebSocket.open) socket.add(reply);
  }

  /// Compares in time that does not depend on where the texts first differ.
  bool _matches(String? given) {
    if (given == null || given.length != _token.length) return false;
    var difference = 0;
    for (var i = 0; i < given.length; i++) {
      difference |= given.codeUnitAt(i) ^ _token.codeUnitAt(i);
    }
    return difference == 0;
  }
}

String _tokenFrom(Random random) => [
  for (var i = 0; i < 16; i++)
    random.nextInt(256).toRadixString(16).padLeft(2, '0'),
].join();
