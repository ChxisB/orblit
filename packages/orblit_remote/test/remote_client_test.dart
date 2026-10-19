import 'dart:async';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:orblit_remote/orblit_remote.dart';
import 'package:orblit_remote/src/remote_client.dart';
import 'package:test/test.dart';

void main() {
  late RemoteServer server;
  late TimeGate gate;
  late Timer loop;
  late Completer<Uint8List> hung;

  setUp(() async {
    gate = TimeGate();
    loop = Timer.periodic(const Duration(milliseconds: 2), (_) {
      gate.advance(0.016);
    });
    hung = Completer();
    server = await RemoteServer.start(
      RemoteHost(
        gate: gate,
        perf: () => {'frame': 1},
        // A game that never hands over the frame, so a call can stay open.
        screenshot: () => hung.future,
        timeout: const Duration(milliseconds: 300),
      ),
      random: Random(3),
      announce: (_) {},
    );
  });

  tearDown(() async {
    loop.cancel();
    await server.close();
  });

  test('pairs an answer with its call', () async {
    final client = await RemoteClient.connect(server.uri);
    expect(await client.call('perf.summary', {}), {'frame': 1});
    await client.close();
  });

  test('two calls in flight each get their own answer', () async {
    final client = await RemoteClient.connect(server.uri);
    final both = await Future.wait([
      client.call('time.step', {'n': 2}),
      client.call('perf.summary', {}),
    ]);
    expect((both[0]! as Map<String, Object?>)['stepped'], 2);
    expect(both[1], {'frame': 1});
    await client.close();
  });

  test('a refusal arrives as a RemoteError with the code', () async {
    final client = await RemoteClient.connect(server.uri);
    await expectLater(
      client.call('entity.get', {'id': 1}),
      throwsA(
        isA<RemoteError>().having(
          (e) => e.code,
          'code',
          RemoteError.unavailable,
        ),
      ),
    );
    await client.close();
  });

  test(
    'a call waiting when the game closes fails, and so does the next',
    () async {
      final client = await RemoteClient.connect(server.uri);
      final waiting = client.call('view.screenshot', {});
      await Future<void>.delayed(const Duration(milliseconds: 50));
      await server.close();
      await expectLater(
        waiting,
        throwsA(
          isA<RemoteError>().having(
            (e) => e.code,
            'code',
            RemoteError.unavailable,
          ),
        ),
      );
      await expectLater(
        client.call('perf.summary', {}),
        throwsA(isA<RemoteError>()),
      );
    },
  );

  test('a call the game never answers ends at the deadline', () async {
    final client = await RemoteClient.connect(
      server.uri,
      deadline: const Duration(milliseconds: 100),
    );
    await expectLater(
      client.call('view.screenshot', {}),
      throwsA(
        isA<RemoteError>().having((e) => e.code, 'code', RemoteError.timedOut),
      ),
    );
    await client.close();
  });

  test('a bad token is refused and the message hides it', () async {
    final bad = server.uri.replace(queryParameters: {'token': 'wrong'});
    await expectLater(
      RemoteClient.connect(bad),
      throwsA(
        isA<RemoteError>().having(
          (e) => e.message,
          'message',
          allOf(contains('refused'), isNot(contains('wrong'))),
        ),
      ),
    );
  });

  test('a port nobody listens on is unavailable', () async {
    final probe = await ServerSocket.bind(InternetAddress.loopbackIPv4, 0);
    final port = probe.port;
    await probe.close();
    await expectLater(
      RemoteClient.connect(Uri.parse('ws://127.0.0.1:$port/?token=x')),
      throwsA(isA<RemoteError>()),
    );
  });
}
