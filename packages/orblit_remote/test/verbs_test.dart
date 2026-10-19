import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

/// A world held in a map, so a verb can be tried with no native code.
final class FakeWorld implements WorldInspector {
  final Map<int, Map<String, Map<String, Object?>>> held = {
    1: {
      'Position': {'x': 1, 'y': 2},
    },
    2: {
      'Position': {'x': 3, 'y': 4},
      'Tag': {'n': 9},
    },
  };

  @override
  List<EntityRow> entities(List<String> filter) => [
    for (final entry in held.entries)
      if (filter.every(entry.value.containsKey))
        (id: entry.key, components: entry.value.keys.toList()..sort()),
  ];

  @override
  Map<String, Map<String, Object?>>? read(int id) {
    final components = held[id];
    // A copy, as the real inspector builds one, so a snapshot cannot alias it.
    return components == null
        ? null
        : (jsonDecode(jsonEncode(components)) as Map<String, Object?>).map(
            (k, v) => MapEntry(k, v! as Map<String, Object?>),
          );
  }

  @override
  void write(int id, String component, String field, num value) {
    final components = held[id];
    if (components == null) throw StateError('Entity $id is not alive');
    final fields = components[component];
    if (fields == null || !fields.containsKey(field)) {
      throw ArgumentError.value(field, 'field', 'is not in $component');
    }
    fields[field] = value;
  }
}

Future<Map<String, Object?>> call(
  RemoteVerbs verbs,
  String method, [
  Object? params,
]) async {
  final reply = await verbs.handle(
    jsonEncode({
      'jsonrpc': '2.0',
      'id': 1,
      'method': method,
      'params': ?params,
    }),
  );
  return jsonDecode(reply!) as Map<String, Object?>;
}

int codeOf(Map<String, Object?> reply) =>
    (reply['error']! as Map<String, Object?>)['code']! as int;

void main() {
  late FakeWorld world;
  late RemoteVerbs verbs;

  setUp(() {
    world = FakeWorld();
    verbs = RemoteVerbs(RemoteHost(world: world));
  });

  group('world and entities', () {
    test(
      'world.entities lists every entity, or those with the components',
      () async {
        final all = await call(verbs, 'world.entities');
        expect(all['result'], [
          {
            'id': 1,
            'components': ['Position'],
          },
          {
            'id': 2,
            'components': ['Position', 'Tag'],
          },
        ]);
        final tagged = await call(verbs, 'world.entities', {
          'filter': ['Tag'],
        });
        expect((tagged['result']! as List<Object?>).length, 1);
      },
    );

    test('entity.get answers a live entity and refuses a dead one', () async {
      final live = await call(verbs, 'entity.get', {'id': 1});
      expect(live['result'], {
        'id': 1,
        'components': {
          'Position': {'x': 1, 'y': 2},
        },
      });
      expect(
        codeOf(await call(verbs, 'entity.get', {'id': 7})),
        RemoteError.notFound,
      );
    });

    test('entity.set changes one field and refuses a bad one', () async {
      await call(verbs, 'entity.set', {
        'id': 1,
        'component': 'Position',
        'field': 'y',
        'value': 8,
      });
      expect(world.held[1]!['Position']!['y'], 8);
      final bad = await call(verbs, 'entity.set', {
        'id': 1,
        'component': 'Position',
        'field': 'nope',
        'value': 8,
      });
      expect(codeOf(bad), RemoteError.invalidParams);
      final text = await call(verbs, 'entity.set', {
        'id': 1,
        'component': 'Position',
        'field': 'y',
        'value': 'high',
      });
      expect(codeOf(text), RemoteError.invalidParams);
      final dead = await call(verbs, 'entity.set', {
        'id': 7,
        'component': 'Position',
        'field': 'y',
        'value': 1,
      });
      expect(codeOf(dead), RemoteError.notFound);
    });

    test('a read only session refuses a write and still reads', () async {
      final locked = RemoteVerbs(
        RemoteHost(world: world, readOnly: true, input: (_) {}),
      );
      final set = await call(locked, 'entity.set', {
        'id': 1,
        'component': 'Position',
        'field': 'y',
        'value': 8,
      });
      expect(codeOf(set), RemoteError.readOnly);
      expect(world.held[1]!['Position']!['y'], 2);
      expect(
        codeOf(
          await call(locked, 'input.send', {'event': <String, Object?>{}}),
        ),
        RemoteError.readOnly,
      );
      expect(
        (await call(locked, 'entity.get', {'id': 1})).containsKey('result'),
        isTrue,
      );
    });
  });

  group('snapshots', () {
    test('a diff names exactly the field that changed', () async {
      final before =
          (await call(verbs, 'world.snapshot'))['result']!
              as Map<String, Object?>;
      world.held[2]!['Position']!['y'] = 40;
      final after =
          (await call(verbs, 'world.snapshot'))['result']!
              as Map<String, Object?>;
      final diff = await call(verbs, 'world.diff', {
        'a': before['id'],
        'b': after['id'],
      });
      expect(diff['result'], [
        {'path': 'entities/2/Position/y', 'from': 4, 'to': 40},
      ]);
    });

    test('a snapshot carries the extras the host gave', () async {
      final host = RemoteHost(
        world: world,
        extras: {
          'physics': () => {'bodies': 3},
        },
      );
      final snap = await call(RemoteVerbs(host), 'world.snapshot');
      expect((snap['result']! as Map<String, Object?>)['extras'], {
        'physics': {'bodies': 3},
      });
    });

    test('a snapshot is a copy of what the host gave', () async {
      final shared = <String, Object?>{'bodies': 3};
      final host = RemoteHost(world: world, extras: {'physics': () => shared});
      final copying = RemoteVerbs(host);
      final first =
          (await call(copying, 'world.snapshot'))['result']!
              as Map<String, Object?>;
      shared['bodies'] = 4;
      final second =
          (await call(copying, 'world.snapshot'))['result']!
              as Map<String, Object?>;
      final diff = await call(copying, 'world.diff', {
        'a': first['id'],
        'b': second['id'],
      });
      expect(diff['result'], [
        {'path': 'extras/physics/bodies', 'from': 3, 'to': 4},
      ]);
    });

    test('a float JSON cannot write is kept as its name', () async {
      final host = RemoteHost(
        world: world,
        extras: {
          'physics': () => {'speed': double.nan},
        },
      );
      final snap = await call(RemoteVerbs(host), 'world.snapshot');
      expect((snap['result']! as Map<String, Object?>)['extras'], {
        'physics': {'speed': 'NaN'},
      });
    });

    test('a snapshot that cannot be written is not kept', () async {
      var broken = true;
      final host = RemoteHost(
        world: world,
        extras: {
          'physics': () => {'body': broken ? Object() : 1},
        },
      );
      final flaky = RemoteVerbs(host);
      expect(codeOf(await call(flaky, 'world.snapshot')), RemoteError.internal);
      broken = false;
      final snap = await call(flaky, 'world.snapshot');
      expect((snap['result']! as Map<String, Object?>)['id'], 's1');
    });

    test('a diff of a snapshot nobody took is refused', () async {
      final diff = await call(verbs, 'world.diff', {'a': 's1', 'b': 's2'});
      expect(codeOf(diff), RemoteError.notFound);
    });
  });

  group('time', () {
    test('pause and resume move the gate', () async {
      final gate = TimeGate();
      final timed = RemoteVerbs(RemoteHost(gate: gate));
      expect((await call(timed, 'time.pause'))['result'], {'paused': true});
      expect(gate.paused, isTrue);
      expect((await call(timed, 'time.resume'))['result'], {'paused': false});
      expect(gate.paused, isFalse);
    });

    test('step answers once the loop has passed the frames', () async {
      final gate = TimeGate();
      final timed = RemoteVerbs(RemoteHost(gate: gate));
      final reply = call(timed, 'time.step', {'n': 3});
      expect(gate.pending, 3);
      for (var i = 0; i < 3; i++) {
        gate.advance(0.5);
      }
      expect((await reply)['result'], {'paused': true, 'stepped': 3});
    });

    test('step gives up when no loop passes the frames', () async {
      final gate = TimeGate();
      final timed = RemoteVerbs(
        RemoteHost(gate: gate, timeout: const Duration(milliseconds: 20)),
      );
      final reply = await call(timed, 'time.step', {'n': 2});
      expect(codeOf(reply), RemoteError.timedOut);
      expect(gate.pending, 0);
      expect(gate.paused, isTrue);
    });

    test('step keeps waiting while frames keep passing', () async {
      final gate = TimeGate();
      final timed = RemoteVerbs(
        RemoteHost(gate: gate, timeout: const Duration(milliseconds: 200)),
      );
      final reply = call(timed, 'time.step', {'n': 4});
      for (var i = 0; i < 4; i++) {
        await Future<void>.delayed(const Duration(milliseconds: 60));
        gate.advance(0.5);
      }
      expect((await reply)['result'], {'paused': true, 'stepped': 4});
    });

    test('step says how many frames passed when it is cut short', () async {
      final gate = TimeGate();
      final timed = RemoteVerbs(RemoteHost(gate: gate));
      final reply = call(timed, 'time.step', {'n': 5});
      gate.advance(0.5);
      gate.advance(0.5);
      gate.pause();
      expect((await reply)['result'], {'paused': true, 'stepped': 2});
    });

    test('step refuses fewer than one frame', () async {
      final timed = RemoteVerbs(RemoteHost(gate: TimeGate()));
      expect(
        codeOf(await call(timed, 'time.step', {'n': 0})),
        RemoteError.invalidParams,
      );
    });
  });

  group('the other parts', () {
    test('log.tail answers the last lines, fifty by default', () async {
      final log = LogTail()
        ..add('one')
        ..add('two')
        ..add('three');
      final logged = RemoteVerbs(RemoteHost(log: log));
      expect((await call(logged, 'log.tail', {'n': 2}))['result'], [
        'two',
        'three',
      ]);
      expect((await call(logged, 'log.tail'))['result'], [
        'one',
        'two',
        'three',
      ]);
      expect(
        codeOf(await call(logged, 'log.tail', {'n': -1})),
        RemoteError.invalidParams,
      );
    });

    test('input.send hands the event to the game', () async {
      final seen = <Map<String, Object?>>[];
      final sent = RemoteVerbs(RemoteHost(input: seen.add));
      await call(sent, 'input.send', {
        'event': {'key': 'w'},
      });
      expect(seen, [
        {'key': 'w'},
      ]);
      expect(
        codeOf(await call(sent, 'input.send', {'event': 'w'})),
        RemoteError.invalidParams,
      );
    });

    test('view.screenshot answers the PNG as base64', () async {
      final shot = RemoteVerbs(
        RemoteHost(
          screenshot: () async => Uint8List.fromList([137, 80, 78, 71]),
        ),
      );
      expect((await call(shot, 'view.screenshot'))['result'], {
        'png': 'iVBORw==',
      });
    });

    test('perf.summary answers what the host reports', () async {
      final timed = RemoteVerbs(RemoteHost(perf: () => {'tickMs': 1.5}));
      expect((await call(timed, 'perf.summary'))['result'], {'tickMs': 1.5});
    });

    test(
      'a verb needs the part it reads, and says so when it is missing',
      () async {
        final bare = RemoteVerbs(const RemoteHost());
        for (final method in [
          'world.entities',
          'time.pause',
          'view.screenshot',
          'log.tail',
          'perf.summary',
        ]) {
          expect(
            codeOf(await call(bare, method)),
            RemoteError.unavailable,
            reason: method,
          );
        }
      },
    );

    test(
      'a callback that fails by itself is internal, not a bad request',
      () async {
        final broken = RemoteVerbs(
          RemoteHost(
            input: (_) => throw ArgumentError('the game broke'),
            screenshot: () async => throw StateError('no surface'),
            perf: () => throw StateError('no clock'),
          ),
        );
        final sent = await call(broken, 'input.send', {
          'event': <String, Object?>{},
        });
        expect(codeOf(sent), RemoteError.internal);
        expect(
          codeOf(await call(broken, 'view.screenshot')),
          RemoteError.internal,
        );
        expect(
          codeOf(await call(broken, 'perf.summary')),
          RemoteError.internal,
        );
      },
    );

    test('a callback that never answers times out', () async {
      final hung = RemoteVerbs(
        RemoteHost(
          timeout: const Duration(milliseconds: 20),
          input: (_) => Completer<void>().future,
          screenshot: () => Completer<Uint8List>().future,
        ),
      );
      final sent = await call(hung, 'input.send', {
        'event': <String, Object?>{},
      });
      expect(codeOf(sent), RemoteError.timedOut);
      expect(codeOf(await call(hung, 'view.screenshot')), RemoteError.timedOut);
    });

    test('an input sink that answers later is waited for', () async {
      final seen = <Map<String, Object?>>[];
      final slow = RemoteVerbs(
        RemoteHost(
          input: (event) async {
            await Future<void>.delayed(const Duration(milliseconds: 10));
            seen.add(event);
          },
        ),
      );
      final reply = await call(slow, 'input.send', {
        'event': {'key': 'w'},
      });
      expect(reply['result'], isNull);
      expect(seen, [
        {'key': 'w'},
      ]);
    });

    test('a timeout of zero is refused', () {
      expect(
        () => RemoteVerbs(const RemoteHost(timeout: Duration.zero)),
        throwsArgumentError,
      );
    });

    test('a host callback that throws is answered, not rethrown', () async {
      final broken = RemoteVerbs(
        RemoteHost(
          world: world,
          perf: () => throw const FormatException('boom'),
        ),
      );
      expect(codeOf(await call(broken, 'perf.summary')), RemoteError.internal);
      expect(
        (await call(broken, 'entity.get', {'id': 1})).containsKey('result'),
        isTrue,
      );
    });
  });

  group('requests', () {
    test('an unknown method is refused', () async {
      expect(codeOf(await call(verbs, 'world.fly')), RemoteError.noMethod);
    });

    test('text that is not JSON is answered with a null id', () async {
      final reply =
          jsonDecode((await verbs.handle('{'))!) as Map<String, Object?>;
      expect(codeOf(reply), RemoteError.parse);
      expect(reply['id'], isNull);
    });

    test(
      'a request that is not an object, or has a bad id, is refused',
      () async {
        for (final bad in ['[1]', '7', '{"id":1.5,"method":"log.tail"}']) {
          final reply =
              jsonDecode((await verbs.handle(bad))!) as Map<String, Object?>;
          expect(codeOf(reply), RemoteError.invalidRequest, reason: bad);
        }
      },
    );

    test('params as a list are refused', () async {
      expect(
        codeOf(await call(verbs, 'entity.get', [1])),
        RemoteError.invalidParams,
      );
    });

    test('a notification is run and never answered', () async {
      final reply = await verbs.handle(
        '{"jsonrpc":"2.0","method":"entity.set","params":{"id":1,'
        '"component":"Position","field":"x","value":5}}',
      );
      expect(reply, isNull);
      expect(world.held[1]!['Position']!['x'], 5);
    });
  });
}
