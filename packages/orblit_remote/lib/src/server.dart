import 'dart:async';
import 'dart:io';
import 'dart:math';

import 'verbs.dart';

const _maxSockets = 8;
const _maxPayload = 1 << 20;
const _loopbackHosts = {'127.0.0.1', 'localhost'};

/// The verbs of [RemoteVerbs], served over a loopback WebSocket behind a token.
///
/// The token is made at start and printed once. A connection is refused with a
/// 403 before the upgrade unless it carries the token in the `token` query, has
/// no `Origin` header, and names a loopback host. The first keeps strangers
/// out, and the other two keep a web page in the user's browser out, which can
/// reach a loopback port but cannot read the token.
///
/// Each connection is answered one request at a time, so a client that wants to
/// pause during a long `time.step` uses a second connection. At most eight
/// connections are open at once, and a frame over one mebibyte closes its
/// connection. When the last connection goes, or the server closes, time runs
/// again, so a debugger that vanishes cannot leave the game frozen.
final class RemoteServer {
  RemoteServer._(this._http, this._token, this._host);

  final HttpServer _http;
  final String _token;
  final RemoteHost _host;
  final Set<WebSocket> _sockets = {};
  bool _closing = false;

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
    final verbs = RemoteVerbs(host);
    final http = await HttpServer.bind(InternetAddress.loopbackIPv4, port);
    // A connection that never sends a request is dropped after about twice
    // this long, which was measured at 19 s for ten.
    http.idleTimeout = const Duration(seconds: 10);
    final server = RemoteServer._(
      http,
      _tokenFrom(random ?? Random.secure()),
      host,
    );
    server._serve(verbs);
    try {
      announce('orblit-remote ${server.uri}');
    } on Object {
      await server.close();
      rethrow;
    }
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

  /// Closes the port and every connection, and lets time run.
  Future<void> close() async {
    _closing = true;
    _host.gate?.resume();
    await _http.close(force: true);
    await Future.wait([
      for (final socket in _sockets.toList())
        socket.close(WebSocketStatus.goingAway),
    ]);
  }

  void _serve(RemoteVerbs verbs) {
    // A client that resets mid request must not take the game down, so every
    // error from a connection is dropped here.
    _http.listen(
      (request) => _accept(request, verbs).catchError((Object _) {}),
      onError: (Object _) {},
    );
  }

  Future<void> _accept(HttpRequest request, RemoteVerbs verbs) async {
    final status = _refusal(request);
    if (status != null) {
      request.response.statusCode = status;
      await request.response.close();
      return;
    }
    final WebSocket socket;
    try {
      socket = await WebSocketTransformer.upgrade(
        request,
        compression: CompressionOptions.compressionOff,
        maxPayloadLength: _maxPayload,
      );
    } on WebSocketException {
      return;
    }
    if (_closing) {
      await socket.close(WebSocketStatus.goingAway);
      return;
    }
    _sockets.add(socket);
    socket
        .asyncMap((data) => _answer(verbs, socket, data))
        .listen((_) {}, onError: (Object _) {}, onDone: () => _left(socket));
  }

  /// The status that turns [request] away, or null to let it upgrade.
  int? _refusal(HttpRequest request) {
    final headers = request.headers;
    if (!WebSocketTransformer.isUpgradeRequest(request) ||
        headers.value('origin') != null ||
        !_loopbackHosts.contains(headers.host) ||
        !_matches(_tokenOf(request.uri))) {
      return HttpStatus.forbidden;
    }
    return _sockets.length >= _maxSockets
        ? HttpStatus.serviceUnavailable
        : null;
  }

  void _left(WebSocket socket) {
    _sockets.remove(socket);
    if (_sockets.isEmpty) _host.gate?.resume();
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

/// The `token` query, or null when it is absent or not valid percent encoding,
/// which `queryParameters` throws on and which must read as a wrong token.
String? _tokenOf(Uri uri) {
  try {
    return uri.queryParameters['token'];
  } on FormatException {
    return null;
  }
}

String _tokenFrom(Random random) => [
  for (var i = 0; i < 16; i++)
    random.nextInt(256).toRadixString(16).padLeft(2, '0'),
].join();
