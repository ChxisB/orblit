import 'dart:convert';
import 'dart:io';

import 'package:orblit_remote/orblit_remote.dart';
import 'package:orblit_remote/src/tools.dart';
import 'package:test/test.dart';

typedef _Seen = List<({String method, Map<String, Object?> params})>;

/// A tool server over a fake game. [answers] maps a verb to its result, and
/// every call is kept in [seen].
({ToolServer server, _Seen seen}) _make({
  Map<String, Object?> answers = const {},
  bool allowWrite = false,
}) {
  final seen = <({String method, Map<String, Object?> params})>[];
  final server = ToolServer((method, params) async {
    seen.add((method: method, params: params));
    final answer = answers[method];
    if (answer is RemoteError) throw answer;
    return answer;
  }, allowWrite: allowWrite);
  return (server: server, seen: seen);
}

Future<Map<String, Object?>> _ask(
  ToolServer server,
  String method, [
  Map<String, Object?>? params,
]) async {
  final reply = await server.handle(
    jsonEncode({
      'jsonrpc': '2.0',
      'id': 1,
      'method': method,
      'params': ?params,
    }),
  );
  return jsonDecode(reply!) as Map<String, Object?>;
}

Map<String, Object?> _result(Map<String, Object?> reply) =>
    reply['result']! as Map<String, Object?>;

List<Map<String, Object?>> _content(Map<String, Object?> reply) =>
    (_result(reply)['content']! as List<Object?>).cast();

Object? _text(Map<String, Object?> reply) =>
    jsonDecode(_content(reply).single['text']! as String);

Future<Map<String, Object?>> _tool(
  ToolServer server,
  String name, [
  Map<String, Object?> arguments = const {},
]) => _ask(server, 'tools/call', {'name': name, 'arguments': arguments});

void main() {
  group('protocol', () {
    test('answers a version it knows with the same one', () async {
      final reply = await _ask(_make().server, 'initialize', {
        'protocolVersion': '2025-03-26',
      });
      expect(_result(reply)['protocolVersion'], '2025-03-26');
      expect(_result(reply)['capabilities'], {'tools': <String, Object?>{}});
      expect(
        (_result(reply)['serverInfo']! as Map<String, Object?>)['name'],
        'orblit-remote',
      );
    });

    test('answers the newest version for one it does not know', () async {
      final reply = await _ask(_make().server, 'initialize', {
        'protocolVersion': '1999-01-01',
      });
      expect(_result(reply)['protocolVersion'], '2025-06-18');
    });

    test('reports the version in the pubspec', () {
      final pubspec = File('pubspec.yaml').readAsStringSync();
      expect(pubspec, contains('version: $toolServerVersion\n'));
    });

    test('never answers a notification', () async {
      final reply = await _make().server.handle(
        jsonEncode({'jsonrpc': '2.0', 'method': 'notifications/initialized'}),
      );
      expect(reply, isNull);
    });

    test('answers ping with an empty result', () async {
      expect(_result(await _ask(_make().server, 'ping')), isEmpty);
    });

    test('answers a message that is not JSON with a parse error', () async {
      final reply =
          jsonDecode((await _make().server.handle('{nope'))!)
              as Map<String, Object?>;
      expect((reply['error']! as Map<String, Object?>)['code'], -32700);
    });

    test('answers an unknown method with -32601', () async {
      final reply = await _ask(_make().server, 'resources/list');
      expect((reply['error']! as Map<String, Object?>)['code'], -32601);
    });
  });

  group('ids, notifications and batches', () {
    String message(Map<String, Object?> body) =>
        jsonEncode({'jsonrpc': '2.0', ...body});

    test('a tools/call with no id is never run', () async {
      final made = _make(answers: {'perf.summary': {}});
      final reply = await made.server.handle(
        message({
          'method': 'tools/call',
          'params': {'name': 'perf'},
        }),
      );
      expect(reply, isNull);
      expect(made.seen, isEmpty);
    });

    test('an id of null is a notification', () async {
      final reply = await _make().server.handle(
        message({'id': null, 'method': 'ping'}),
      );
      expect(reply, isNull);
    });

    test('an id that is not a number or text is refused', () async {
      for (final id in [
        1.5,
        {'a': 1},
        [1],
        true,
      ]) {
        final reply =
            jsonDecode(
                  (await _make().server.handle(
                    message({'id': id, 'method': 'ping'}),
                  ))!,
                )
                as Map<String, Object?>;
        expect((reply['error']! as Map<String, Object?>)['code'], -32600);
        expect(reply['id'], isNull);
      }
    });

    test('a request with no method answers under its own id', () async {
      final reply =
          jsonDecode((await _make().server.handle(message({'id': 5})))!)
              as Map<String, Object?>;
      expect(reply['id'], 5);
      expect((reply['error']! as Map<String, Object?>)['code'], -32600);
    });

    test('a batch is answered in order and leaves out notifications', () async {
      final reply = await _make().server.handle(
        jsonEncode([
          {'jsonrpc': '2.0', 'id': 1, 'method': 'ping'},
          {'jsonrpc': '2.0', 'method': 'notifications/initialized'},
          {'jsonrpc': '2.0', 'id': 'b', 'method': 'ping'},
        ]),
      );
      final answers = (jsonDecode(reply!) as List<Object?>)
          .cast<Map<String, Object?>>();
      expect(answers.map((a) => a['id']), [1, 'b']);
    });

    test('a batch of notifications is not answered', () async {
      final reply = await _make().server.handle(
        jsonEncode([
          {'jsonrpc': '2.0', 'method': 'notifications/initialized'},
        ]),
      );
      expect(reply, isNull);
    });

    test('an empty batch is an invalid request', () async {
      final reply =
          jsonDecode((await _make().server.handle('[]'))!)
              as Map<String, Object?>;
      expect((reply['error']! as Map<String, Object?>)['code'], -32600);
    });
  });

  group('the tool list', () {
    List<String> names(Map<String, Object?> reply) => [
      for (final tool in (_result(reply)['tools']! as List<Object?>))
        (tool! as Map<String, Object?>)['name']! as String,
    ];

    test('has six read tools and no set', () async {
      final reply = await _ask(_make().server, 'tools/list');
      expect(names(reply), [
        'observe',
        'step',
        'snapshot',
        'diff',
        'screenshot',
        'perf',
      ]);
    });

    test('adds set when writes are allowed', () async {
      final reply = await _ask(_make(allowWrite: true).server, 'tools/list');
      expect(names(reply), hasLength(7));
      expect(names(reply), contains('set'));
    });

    test('refuses set when writes are not allowed', () async {
      final made = _make();
      final reply = await _tool(made.server, 'set', {
        'id': 1,
        'component': 'Position',
        'field': 'x',
        'value': 5,
      });
      expect((reply['error']! as Map<String, Object?>)['code'], -32602);
      expect(made.seen, isEmpty);
    });

    test('says step and set change the game, and the rest do not', () async {
      final reply = await _ask(_make(allowWrite: true).server, 'tools/list');
      final hints = {
        for (final tool in (_result(reply)['tools']! as List<Object?>))
          (tool! as Map<String, Object?>)['name']:
              ((tool as Map<String, Object?>)['annotations']!
                  as Map<String, Object?>)['readOnlyHint'],
      };
      expect(hints['step'], isFalse);
      expect(hints['set'], isFalse);
      expect(hints['observe'], isTrue);
      expect(hints['snapshot'], isTrue);
    });

    test('refuses a tool it does not have', () async {
      final reply = await _tool(_make().server, 'delete_everything');
      expect((reply['error']! as Map<String, Object?>)['code'], -32602);
    });
  });

  group('the tools', () {
    test('observe cuts a long list to the limit', () async {
      final made = _make(
        answers: {
          'world.entities': [
            for (var id = 0; id < 30; id++)
              {
                'id': id,
                'components': ['Position'],
              },
          ],
        },
      );
      final shown = await _tool(made.server, 'observe', {'limit': 3});
      final body = _text(shown)! as Map<String, Object?>;
      expect(body['count'], 30);
      expect(body['entities'], hasLength(3));
      expect(body['truncated'], isTrue);
    });

    test('observe shows twenty rows with no limit', () async {
      final made = _make(
        answers: {
          'world.entities': [
            for (var id = 0; id < 30; id++)
              {'id': id, 'components': <String>[]},
          ],
        },
      );
      final body =
          _text(await _tool(made.server, 'observe'))! as Map<String, Object?>;
      expect(body['entities'], hasLength(20));
    });

    test('observe passes a filter on to the game', () async {
      final made = _make(answers: {'world.entities': <Object?>[]});
      await _tool(made.server, 'observe', {
        'filter': ['Tag'],
      });
      expect(made.seen.single.params, {
        'filter': ['Tag'],
      });
    });

    test('observe with an id reads that entity in full', () async {
      final made = _make(
        answers: {
          'entity.get': {
            'id': 2,
            'components': {
              'Tag': {'n': 9},
            },
          },
        },
      );
      final body =
          _text(await _tool(made.server, 'observe', {'id': 2}))!
              as Map<String, Object?>;
      expect(body['components'], isNotNull);
      expect(made.seen.single.method, 'entity.get');
    });

    test('observe tells the assistant a limit of zero is wrong', () async {
      final made = _make(answers: {'world.entities': <Object?>[]});
      final reply = await _tool(made.server, 'observe', {'limit': 0});
      expect(_result(reply)['isError'], isTrue);
      expect(_content(reply).single['text'], contains('limit'));
    });

    test('a tool that takes no arguments works without the field', () async {
      final made = _make(
        answers: {
          'perf.summary': {'frame': 1},
        },
      );
      final server = made.server;
      final without = await _ask(server, 'tools/call', {'name': 'perf'});
      expect(_result(without)['isError'], isFalse);
      final nulled = await _ask(server, 'tools/call', {
        'name': 'perf',
        'arguments': null,
      });
      expect(_result(nulled)['isError'], isFalse);
    });

    test('step refuses a count outside 1 to 300', () async {
      final made = _make(
        answers: {
          'time.step': {'stepped': 1},
        },
      );
      for (final n in [0, 301, -1, 'x', 2.5]) {
        final reply = await _tool(made.server, 'step', {'n': n});
        expect(_result(reply)['isError'], isTrue, reason: '$n');
      }
      expect(made.seen, isEmpty);
      await _tool(made.server, 'step', {'n': 300});
      expect(made.seen.single.params, {'n': 300});
    });

    test('set refuses a value JSON cannot carry', () async {
      final made = _make(allowWrite: true);
      final reply = await made.server.handle(
        '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":'
        '{"name":"set","arguments":{"id":1,"component":"a","field":"b",'
        '"value":1e999}}}',
      );
      final result =
          (jsonDecode(reply!) as Map<String, Object?>)['result']!
              as Map<String, Object?>;
      expect(result['isError'], isTrue);
      expect(jsonEncode(result['content']), contains('finite'));
      expect(made.seen, isEmpty);
    });

    test('step passes one frame by default', () async {
      final made = _make(
        answers: {
          'time.step': {'stepped': 1},
        },
      );
      await _tool(made.server, 'step');
      expect(made.seen.single.params, {'n': 1});
    });

    test('set writes through when allowed', () async {
      final made = _make(allowWrite: true);
      final reply = await _tool(made.server, 'set', {
        'id': 1,
        'component': 'Position',
        'field': 'x',
        'value': 5,
      });
      expect(_result(reply)['isError'], isFalse);
      expect(_text(reply), made.seen.single.params);
      expect(made.seen.single.method, 'entity.set');
      expect(made.seen.single.params, {
        'id': 1,
        'component': 'Position',
        'field': 'x',
        'value': 5,
      });
    });

    test('snapshot answers the id and the counts, not the content', () async {
      final made = _make(
        answers: {
          'world.snapshot': {
            'id': 's1',
            'entities': {'1': <String, Object?>{}, '2': <String, Object?>{}},
            'extras': {'physics': <String, Object?>{}},
          },
        },
      );
      expect(_text(await _tool(made.server, 'snapshot')), {
        'id': 's1',
        'entities': 2,
        'extras': ['physics'],
      });
    });

    test('diff cuts a long list of changes', () async {
      final made = _make(
        answers: {
          'world.diff': [
            for (var i = 0; i < 500; i++) {'path': 'p$i', 'from': 0, 'to': 1},
          ],
        },
      );
      final body =
          _text(await _tool(made.server, 'diff', {'a': 's1', 'b': 's2'}))!
              as Map<String, Object?>;
      expect(body['count'], 500);
      expect(body['changes'], hasLength(200));
      expect(body['truncated'], isTrue);
    });

    test('screenshot answers an image block', () async {
      final made = _make(
        answers: {
          'view.screenshot': {'png': 'AAAA'},
        },
      );
      final block = _content(await _tool(made.server, 'screenshot')).single;
      expect(block, {'type': 'image', 'data': 'AAAA', 'mimeType': 'image/png'});
    });

    test('a refusal from the game is an error result, not a crash', () async {
      final made = _make(
        answers: {
          'entity.get': const RemoteError(-32001, 'Entity 9 is not alive'),
        },
      );
      final reply = await _tool(made.server, 'observe', {'id': 9});
      expect(_result(reply)['isError'], isTrue);
      expect(_content(reply).single['text'], contains('Entity 9 is not alive'));
    });

    test('an answer in an unknown shape is an error result', () async {
      final made = _make(answers: {'world.snapshot': 'nonsense'});
      final reply = await _tool(made.server, 'snapshot');
      expect(_result(reply)['isError'], isTrue);
    });
  });
}
