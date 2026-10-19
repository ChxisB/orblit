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
