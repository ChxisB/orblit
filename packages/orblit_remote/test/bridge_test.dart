import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:orblit_core/orblit_core.dart';
import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

/// The binary as a child process, spoken to one JSON line at a time. A line
/// on its stdout that is not JSON fails the test, so stdout is proved to carry
/// messages and nothing else.
final class Bridge {
  Bridge._(this._process, this._lines);

  final Process _process;
  final StreamIterator<String> _lines;
  int _next = 0;

  static Future<Bridge> start(
    List<String> arguments, {
    Map<String, String> environment = const {},
  }) async {
    final process = await Process.start(Platform.resolvedExecutable, [
      'bin/orblit_remote.dart',
      ...arguments,
    ], environment: environment);
    // Notes go to stderr, and a full pipe would stall the child.
    unawaited(process.stderr.drain<void>());
    final lines = StreamIterator(
      process.stdout.transform(utf8.decoder).transform(const LineSplitter()),
    );
    return Bridge._(process, lines);
  }

  void notify(String method) => _send({'method': method});

  void sendBytes(List<int> bytes) => _process.stdin.add(bytes);

  /// The next line the program wrote, whatever it answers.
  Future<Map<String, Object?>> next() async {
    if (!await _lines.moveNext()) throw StateError('The bridge ended');
    return jsonDecode(_lines.current) as Map<String, Object?>;
  }

  Future<int> get exitCode => _process.exitCode;

  Future<Map<String, Object?>> ask(
    String method, [
    Map<String, Object?> params = const {},
  ]) async {
    _send({'id': ++_next, 'method': method, 'params': params});
    if (!await _lines.moveNext()) throw StateError('The bridge ended');
    final reply = jsonDecode(_lines.current) as Map<String, Object?>;
    expect(reply['id'], _next);
    return reply;
  }

  Future<Map<String, Object?>> tool(
    String name, [
    Map<String, Object?> arguments = const {},
  ]) => ask('tools/call', {'name': name, 'arguments': arguments});

  /// The decoded text of the one block a tool answered.
  Future<Object?> text(
    String name, [
    Map<String, Object?> arguments = const {},
  ]) async {
    final reply = await tool(name, arguments);
    final result = reply['result']! as Map<String, Object?>;
    expect(result['isError'], isFalse, reason: '$result');
    final block = (result['content']! as List<Object?>).single!;
    return jsonDecode((block as Map<String, Object?>)['text']! as String);
  }

  Future<int> finish() async {
    await _process.stdin.close();
    final code = await _process.exitCode;
    await _lines.cancel();
    return code;
  }

  void kill() => _process.kill();

  void _send(Map<String, Object?> message) =>
      _process.stdin.writeln(jsonEncode({'jsonrpc': '2.0', ...message}));
}

Future<List<String>> _toolNames(Bridge bridge) async {
  final reply = await bridge.ask('tools/list');
  final tools = (reply['result']! as Map<String, Object?>)['tools']!;
  return [
    for (final tool in tools as List<Object?>)
      (tool! as Map<String, Object?>)['name']! as String,
  ];
}

void main() {
  late World world;
  late TransformComponents transforms;
  late TimeGate gate;
  late RemoteServer server;
  late Timer loop;
  late int a;
  Bridge? bridge;

  setUp(() async {
    world = World();
    transforms = world.registerTransforms();
    a = world.createEntity();
    world.add(a, transforms.local, transform(x: 4));
    world.add(world.createEntity(), transforms.local, transform(y: 1));
    gate = TimeGate();
    loop = Timer.periodic(const Duration(milliseconds: 2), (_) {
      world.tick(gate.advance(0.016));
    });
    server = await RemoteServer.start(
      RemoteHost(
        world: CoreWorldInspector(world, [transforms.local]),
        gate: gate,
      ),
      random: Random(5),
      announce: (_) {},
    );
  });

  tearDown(() async {
    bridge?.kill();
    bridge = null;
    loop.cancel();
    await server.close();
    world.dispose();
  });

  test(
    'a session reads the count, steps, sets a value and shows the diff',
    () async {
      final run = bridge = await Bridge.start([
        '--allow-write',
        '${server.uri}',
      ]);
      final hello = await run.ask('initialize', {
        'protocolVersion': '2025-06-18',
      });
      expect(
        (hello['result']! as Map<String, Object?>)['protocolVersion'],
        isNotNull,
      );
      run.notify('notifications/initialized');
      expect(await _toolNames(run), contains('set'));

      final seen = await run.text('observe') as Map<String, Object?>;
      expect(seen['count'], 2);

      final stepped = await run.text('step', {'n': 3}) as Map<String, Object?>;
      expect(stepped['stepped'], 3);

      final before = await run.text('snapshot') as Map<String, Object?>;
      await run.text('set', {
        'id': a,
        'component': 'orblit.LocalTransform',
        'field': 'x',
        'value': 9,
      });
      final after = await run.text('snapshot') as Map<String, Object?>;
      final diff =
          await run.text('diff', {'a': before['id'], 'b': after['id']})
              as Map<String, Object?>;
      expect(diff['count'], greaterThan(0));
      expect(jsonEncode(diff['changes']), contains('x'));

      expect(await run.finish(), 0);
    },
    timeout: const Timeout(Duration(seconds: 60)),
  );

  test(
    'without --allow-write there is no set tool and a call to it is refused',
    () async {
      final run = bridge = await Bridge.start(['${server.uri}']);
      expect(await _toolNames(run), isNot(contains('set')));
      final reply = await run.tool('set', {
        'id': a,
        'component': 'orblit.LocalTransform',
        'field': 'x',
        'value': 9,
      });
      expect((reply['error']! as Map<String, Object?>)['code'], -32602);
      expect(await run.finish(), 0);
    },
    timeout: const Timeout(Duration(seconds: 60)),
  );

  test('a host that is not this machine is refused with 64', () async {
    for (final address in [
      'ws://example.com:80/?token=x',
      'ws://127.0.0.1@evil.example:80/?token=x',
      'ws://[::1]:80/?token=x',
      'ws://127.0.0.1/?token=x',
    ]) {
      final result = await Process.run(Platform.resolvedExecutable, [
        'bin/orblit_remote.dart',
        address,
      ]);
      expect(result.exitCode, 64, reason: address);
      expect(result.stdout, isEmpty);
    }
  });

  test('an unknown flag is refused rather than ignored', () async {
    final result = await Process.run(Platform.resolvedExecutable, [
      'bin/orblit_remote.dart',
      '--allow-writes',
      '${server.uri}',
    ]);
    expect(result.exitCode, 64);
  });

  test('a wrong token exits with 69 and prints no token', () async {
    final wrong = server.uri.replace(queryParameters: {'token': 'tok3nXYZ'});
    final result = await Process.run(Platform.resolvedExecutable, [
      'bin/orblit_remote.dart',
      '$wrong',
    ]);
    expect(result.exitCode, 69);
    expect(result.stderr, isNot(contains('tok3nXYZ')));
    expect(result.stderr, contains('refused'));
    expect(result.stdout, isEmpty);
  });

  test('the address can come from the environment', () async {
    final run = bridge = await Bridge.start(
      [],
      environment: {'ORBLIT_REMOTE_ADDRESS': 'orblit-remote ${server.uri}'},
    );
    expect(await _toolNames(run), isNotEmpty);
    expect(await run.finish(), 0);
  });

  test('the program exits with 69 when the game goes away', () async {
    final run = bridge = await Bridge.start(['${server.uri}']);
    expect(await _toolNames(run), isNotEmpty);
    await server.close();
    expect(await run.exitCode.timeout(const Duration(seconds: 15)), 69);
  }, timeout: const Timeout(Duration(seconds: 60)));

  test(
    'a game that is read only still refuses set with --allow-write',
    () async {
      final locked = await RemoteServer.start(
        RemoteHost(
          world: CoreWorldInspector(world, [transforms.local]),
          gate: gate,
          readOnly: true,
        ),
        random: Random(6),
        announce: (_) {},
      );
      final run = bridge = await Bridge.start([
        '--allow-write',
        '${locked.uri}',
      ]);
      final reply = await run.tool('set', {
        'id': a,
        'component': 'orblit.LocalTransform',
        'field': 'x',
        'value': 9,
      });
      final result = reply['result']! as Map<String, Object?>;
      expect(result['isError'], isTrue);
      expect(jsonEncode(result['content']), contains('-32002'));
      expect(await run.finish(), 0);
      await locked.close();
    },
    timeout: const Timeout(Duration(seconds: 60)),
  );

  test(
    'a stray byte on stdin is a parse error and the session goes on',
    () async {
      final run = bridge = await Bridge.start(['${server.uri}']);
      run.sendBytes([0xff, 0xfe, 0x0a]);
      final bad = await run.next();
      expect((bad['error']! as Map<String, Object?>)['code'], -32700);
      expect(await _toolNames(run), isNotEmpty);
      expect(await run.finish(), 0);
    },
    timeout: const Timeout(Duration(seconds: 60)),
  );

  test('a bad address exits with a usage note and no stdout', () async {
    final result = await Process.run(Platform.resolvedExecutable, [
      'bin/orblit_remote.dart',
      'http://nowhere',
    ]);
    expect(result.exitCode, 64);
    expect(result.stdout, isEmpty);
    expect(result.stderr, contains('usage'));
  });

  test('a game that is not running exits with 69', () async {
    final probe = await ServerSocket.bind(InternetAddress.loopbackIPv4, 0);
    final port = probe.port;
    await probe.close();
    final result = await Process.run(Platform.resolvedExecutable, [
      'bin/orblit_remote.dart',
      'ws://127.0.0.1:$port/?token=x',
    ]);
    expect(result.exitCode, 69);
    expect(result.stdout, isEmpty);
  });
}
