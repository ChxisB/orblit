import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:orblit_core/orblit_core.dart';
import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

/// One connection, with a request counter so each answer is paired by id.
final class Client {
  Client(this._socket) {
    _socket.listen((data) {
      final reply = jsonDecode(data as String) as Map<String, Object?>;
      _waiting.remove(reply['id'])!.complete(reply);
    });
  }

  final WebSocket _socket;
  final Map<Object?, Completer<Map<String, Object?>>> _waiting = {};
  int _next = 0;

  Future<Object?> call(String method, [Map<String, Object?>? params]) async {
    final id = ++_next;
    final answer = _waiting[id] = Completer();
    _socket.add(
      jsonEncode({
        'jsonrpc': '2.0',
        'id': id,
        'method': method,
        'params': ?params,
      }),
    );
    final reply = await answer.future;
    if (reply.containsKey('error')) throw StateError('${reply['error']}');
    return reply['result'];
  }

  Future<void> close() => _socket.close();
}

/// The first line of the answer to a hand written upgrade request, so a header
/// or a query that [WebSocket.connect] would not send can be sent.
Future<String> rawStatus(
  Uri uri, {
  String? host,
  String? query,
  Map<String, String> extra = const {},
}) async {
  final socket = await Socket.connect(uri.host, uri.port);
  final headers = {
    'Host': host ?? '${uri.host}:${uri.port}',
    'Connection': 'Upgrade',
    'Upgrade': 'websocket',
    'Sec-WebSocket-Version': '13',
    'Sec-WebSocket-Key': 'dGhlIHNhbXBsZSBub25jZQ==',
    ...extra,
  };
  socket.write(
    'GET /?${query ?? uri.query} HTTP/1.1\r\n'
    '${headers.entries.map((e) => '${e.key}: ${e.value}\r\n').join()}\r\n',
  );
  final reply = await utf8.decoder.bind(socket).first;
  socket.destroy();
  return reply.split('\r\n').first;
}

void main() {
  late World world;
  late TransformComponents transforms;
  late TimeGate gate;
  late RemoteServer server;
  late List<String> announced;
  late Timer loop;
  late int passed;
  late int a;
  late int b;

  setUp(() async {
    world = World();
    transforms = world.registerTransforms();
    a = world.createEntity();
    world.add(a, transforms.local, transform(x: 4));
    b = world.createEntity();
    world.add(b, transforms.local, transform(y: 1));
    gate = TimeGate();
    passed = 0;
    // The game's own loop, which is the one place the gate is consulted.
    loop = Timer.periodic(const Duration(milliseconds: 2), (_) {
      final delta = gate.advance(0.016);
      if (delta > 0) passed++;
      world.tick(delta);
    });
    announced = [];
    server = await RemoteServer.start(
      RemoteHost(
        world: CoreWorldInspector(world, [transforms.local]),
        gate: gate,
      ),
      random: Random(7),
      announce: announced.add,
    );
  });

  tearDown(() async {
    loop.cancel();
    await server.close();
    world.dispose();
  });

  test('the announced line is the address with the token', () {
    expect(announced, ['orblit-remote ${server.uri}']);
    expect(server.uri.host, '127.0.0.1');
    expect(
      server.uri.queryParameters['token'],
      matches(RegExp(r'^[0-9a-f]{32}$')),
    );
  });

  test(
    'a script pauses, steps ten ticks, reads one entity and sets another',
    () async {
      final client = Client(await WebSocket.connect('${server.uri}'));
      await client.call('time.pause');
      final ticksBefore = passed;
      final stepped = await client.call('time.step', {'n': 10});
      expect(stepped, {'paused': true, 'stepped': 10});
      expect(passed - ticksBefore, 10);

      final read =
          await client.call('entity.get', {'id': a}) as Map<String, Object?>;
      final fields =
          (read['components']!
                  as Map<String, Object?>)['orblit.LocalTransform']!
              as Map<String, Object?>;
      expect(fields['x'], 4);

      final before =
          await client.call('world.snapshot') as Map<String, Object?>;
      await client.call('entity.set', {
        'id': b,
        'component': 'orblit.LocalTransform',
        'field': 'y',
        'value': 9,
      });
      final after = await client.call('world.snapshot') as Map<String, Object?>;
      final diff = await client.call('world.diff', {
        'a': before['id'],
        'b': after['id'],
      });
      expect(diff, [
        {'path': 'entities/$b/orblit.LocalTransform/y', 'from': 1, 'to': 9},
      ]);
      await client.close();
    },
  );

  test('a wrong token, no token and a plain request are refused', () async {
    final wrong = server.uri.replace(queryParameters: {'token': 'nope'});
    await expectLater(
      WebSocket.connect('$wrong'),
      throwsA(isA<WebSocketException>()),
    );
    final none = server.uri.replace(queryParameters: <String, String>{});
    await expectLater(
      WebSocket.connect('$none'),
      throwsA(isA<WebSocketException>()),
    );

    final http = HttpClient();
    final request = await http.getUrl(server.uri.replace(scheme: 'http'));
    final response = await request.close();
    expect(response.statusCode, HttpStatus.forbidden);
    await response.drain<void>();
    http.close();
  });

  test('a token that is not valid percent encoding is refused', () async {
    expect(await rawStatus(server.uri, query: 'token=%ff'), contains('403'));
    expect(await rawStatus(server.uri), contains('101'));
  });

  test('a request from a web page or for another host is refused', () async {
    await expectLater(
      WebSocket.connect(
        '${server.uri}',
        headers: {'Origin': 'http://evil.example'},
      ),
      throwsA(isA<WebSocketException>()),
    );
    expect(await rawStatus(server.uri, host: 'evil.example'), contains('403'));
    expect(
      await rawStatus(server.uri, host: 'localhost:${server.uri.port}'),
      contains('101'),
    );
  });

  test('a ninth connection is turned away', () async {
    final open = [
      for (var i = 0; i < 8; i++) await WebSocket.connect('${server.uri}'),
    ];
    await expectLater(
      WebSocket.connect('${server.uri}'),
      throwsA(isA<WebSocketException>()),
    );
    for (final socket in open) {
      await socket.close();
    }
  });

  test('a frame over a mebibyte closes its connection', () async {
    final socket = await WebSocket.connect('${server.uri}');
    final closed = socket.listen((_) {}).asFuture<void>();
    socket.add('x' * (2 << 20));
    await closed;
    // The SDK reports an oversized frame as a protocol error.
    expect(
      socket.closeCode,
      anyOf(WebSocketStatus.protocolError, WebSocketStatus.messageTooBig),
    );
  });

  test('requests on one connection are answered in order', () async {
    final socket = await WebSocket.connect('${server.uri}');
    final order = <int>[];
    final two = Completer<void>();
    socket.listen((data) {
      order.add(
        (jsonDecode(data as String) as Map<String, Object?>)['id']! as int,
      );
      if (order.length == 2) two.complete();
    });
    socket.add('{"id":1,"method":"time.step","params":{"n":40}}');
    socket.add('{"id":2,"method":"world.entities"}');
    await two.future;
    expect(order, [1, 2]);
    await socket.close();
  });

  test('time runs again when the last client leaves', () async {
    final socket = await WebSocket.connect('${server.uri}');
    final client = Client(socket);
    await client.call('time.pause');
    expect(gate.paused, isTrue);
    await client.close();
    for (var i = 0; i < 100 && gate.paused; i++) {
      await Future<void>.delayed(const Duration(milliseconds: 10));
    }
    expect(gate.paused, isFalse);
  });

  test('time runs again when the server closes', () async {
    gate.pause();
    await server.close();
    expect(gate.paused, isFalse);
  });

  test('a failing announce leaves no port open', () async {
    late final Uri uri;
    await expectLater(
      RemoteServer.start(
        RemoteHost(gate: TimeGate()),
        announce: (line) {
          uri = Uri.parse(line.split(' ').last);
          throw StateError('no console');
        },
      ),
      throwsStateError,
    );
    await expectLater(
      Socket.connect(uri.host, uri.port),
      throwsA(isA<SocketException>()),
    );
  });

  test('close ends a connection that is still open', () async {
    final socket = await WebSocket.connect('${server.uri}');
    final closed = socket.listen((_) {}).asFuture<void>();
    await server.close();
    await closed;
    expect(socket.closeCode, WebSocketStatus.goingAway);
  });

  test('close does not wait for a client that never reads', () async {
    final socket = await WebSocket.connect('${server.uri}');
    final watch = Stopwatch()..start();
    await server.close();
    expect(watch.elapsed, lessThan(const Duration(seconds: 4)));
    await socket.close();
  });

  test('the render verbs answer over the socket', () async {
    final rendering = await RemoteServer.start(
      RemoteHost(
        draws: () => [
          {'id': 'object:1', 'x': double.nan},
        ],
        material: (key) => {'key': key},
      ),
      random: Random(8),
      announce: (_) {},
    );
    final client = Client(await WebSocket.connect('${rendering.uri}'));
    expect(await client.call('view.draws'), {
      'count': 1,
      'draws': [
        {'id': 'object:1', 'x': 'NaN'},
      ],
      'truncated': false,
    });
    expect(await client.call('view.material', {'key': 3}), {'key': 3});
    await client.close();
    await rendering.close();
  });

  test('a release build refuses to start the server', () async {
    final probe = ['run', 'test/release_probe.dart'];
    final off = await Process.run(Platform.resolvedExecutable, probe);
    expect('${off.stdout}'.trim(), 'refused', reason: '${off.stderr}');
    final on = await Process.run(Platform.resolvedExecutable, [
      'run',
      '--enable-asserts',
      'test/release_probe.dart',
    ]);
    expect('${on.stdout}'.trim(), 'started', reason: '${on.stderr}');
  }, timeout: const Timeout(Duration(minutes: 3)));
}
