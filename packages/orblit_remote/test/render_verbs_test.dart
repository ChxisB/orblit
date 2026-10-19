import 'dart:async';
import 'dart:convert';

import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

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

Object? resultOf(Map<String, Object?> reply) => reply['result'];

const _passRows = [
  {'name': 'shadow', 'milliseconds': 0.4, 'draws': 3},
  {'name': 'colour', 'milliseconds': 1.9, 'draws': 12},
];

final _drawRows = [
  {'id': 'object:1', 'mesh': null, 'material': 7, 'instances': 1},
  {'id': 'object:2', 'mesh': 'rock.glb', 'material': 7, 'instances': 1},
  {'id': 'population:3', 'mesh': null, 'material': null, 'instances': 500},
];

RemoteVerbs hosted({
  Duration timeout = const Duration(seconds: 10),
  List<Map<String, Object?>>? draws,
}) => RemoteVerbs(
  RemoteHost(
    timeout: timeout,
    passes: () async => _passRows,
    draws: () => draws ?? _drawRows,
    material: (key) => key == 7 ? {'key': 7, 'roughness': 0.5} : null,
  ),
);

void main() {
  group('view.passes', () {
    test('answers a row for each pass the host reports', () async {
      expect(resultOf(await call(hosted(), 'view.passes')), _passRows);
    });

    test('waits only as long as the host timeout', () async {
      final hung = RemoteVerbs(
        RemoteHost(
          timeout: const Duration(milliseconds: 20),
          passes: () => Completer<List<Map<String, Object?>>>().future,
        ),
      );
      expect(codeOf(await call(hung, 'view.passes')), RemoteError.timedOut);
    });
  });

  group('view.draws', () {
    test('answers the count and the rows', () async {
      expect(resultOf(await call(hosted(), 'view.draws')), {
        'count': 3,
        'draws': _drawRows,
        'truncated': false,
      });
    });

    test('answers the first hundred rows unless asked for more', () async {
      final many = hosted(
        draws: [
          for (var i = 0; i < 150; i++) {'id': 'object:$i'},
        ],
      );
      final result =
          resultOf(await call(many, 'view.draws'))! as Map<String, Object?>;
      expect(result['count'], 150);
      expect(result['draws'], hasLength(100));
      expect(result['truncated'], isTrue);
    });

    test('cuts at the limit and says so', () async {
      final result =
          resultOf(await call(hosted(), 'view.draws', {'limit': 2}))!
              as Map<String, Object?>;
      expect(result['count'], 3);
      expect(result['draws'], _drawRows.take(2));
      expect(result['truncated'], isTrue);
    });

    test('a limit of zero is the count alone', () async {
      expect(resultOf(await call(hosted(), 'view.draws', {'limit': 0})), {
        'count': 3,
        'draws': <Object?>[],
        'truncated': true,
      });
    });

    test('refuses a limit that is below zero or not a number', () async {
      for (final limit in [-1, 'ten', 1.5]) {
        expect(
          codeOf(await call(hosted(), 'view.draws', {'limit': limit})),
          RemoteError.invalidParams,
          reason: '$limit',
        );
      }
    });

    test('says a float JSON cannot write as text', () async {
      final broken = hosted(
        draws: [
          {'id': 'object:1', 'x': double.nan, 'far': double.infinity},
        ],
      );
      final result =
          resultOf(await call(broken, 'view.draws'))! as Map<String, Object?>;
      expect(result['draws'], [
        {'id': 'object:1', 'x': 'NaN', 'far': 'Infinity'},
      ]);
    });
  });

  group('view.draw', () {
    test('answers the row with that id', () async {
      expect(
        resultOf(await call(hosted(), 'view.draw', {'id': 'object:2'})),
        _drawRows[1],
      );
    });

    test('an id nothing has is not found', () async {
      expect(
        codeOf(await call(hosted(), 'view.draw', {'id': 'object:99'})),
        RemoteError.notFound,
      );
    });

    test('needs an id as text', () async {
      expect(
        codeOf(await call(hosted(), 'view.draw', {'id': 2})),
        RemoteError.invalidParams,
      );
      expect(
        codeOf(await call(hosted(), 'view.draw')),
        RemoteError.invalidParams,
      );
    });
  });

  group('view.material', () {
    test('answers what the host holds for the key', () async {
      expect(resultOf(await call(hosted(), 'view.material', {'key': 7})), {
        'key': 7,
        'roughness': 0.5,
      });
    });

    test('a key the host has no material for is not found', () async {
      expect(
        codeOf(await call(hosted(), 'view.material', {'key': 8})),
        RemoteError.notFound,
      );
    });

    test('needs a whole number key', () async {
      expect(
        codeOf(await call(hosted(), 'view.material', {'key': 'brass'})),
        RemoteError.invalidParams,
      );
    });
  });

  test('a read only session still answers the render verbs', () async {
    final verbs = RemoteVerbs(
      RemoteHost(readOnly: true, draws: () => _drawRows),
    );
    expect(
      resultOf(await call(verbs, 'view.draws')),
      isA<Map<String, Object?>>(),
    );
  });

  group('a host that gave no renderer', () {
    test('answers unavailable for each verb', () async {
      final bare = RemoteVerbs(const RemoteHost());
      for (final method in [
        'view.passes',
        'view.draws',
        'view.draw',
        'view.material',
      ]) {
        expect(
          codeOf(await call(bare, method, {'id': 'object:1', 'key': 1})),
          RemoteError.unavailable,
          reason: method,
        );
      }
    });

    test('a callback that throws by itself is internal', () async {
      final broken = RemoteVerbs(
        RemoteHost(
          draws: () => throw StateError('scene is gone'),
          material: (_) => throw ArgumentError('bad key'),
        ),
      );
      for (final entry in {
        'view.draws': <String, Object?>{},
        'view.draw': {'id': 'object:1'},
        'view.material': {'key': 1},
      }.entries) {
        expect(
          codeOf(await call(broken, entry.key, entry.value)),
          RemoteError.internal,
          reason: entry.key,
        );
      }
    });
  });
}
