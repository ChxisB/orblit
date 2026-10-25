/// The Dart end of the check that the Dart and C# control surfaces agree.
///
///     dart run --enable-asserts tool/cross_peer.dart serve
///     dart run tool/cross_peer.dart drive ws://127.0.0.1:PORT/?token=TOKEN
///
/// `serve` runs a Dart server over two entities held in a map, prints the
/// line a client reads, and runs until stdin ends. `drive` runs the calls
/// below against a server and prints what came back as one line of JSON. The
/// C# tests in `Orblit.Remote.Tests` start each and compare the answers with
/// those of their own client and server.
library;

import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:orblit_remote/orblit_remote.dart';
import 'package:orblit_remote/src/remote_client.dart';

/// The calls both clients make, in order. The C# copy is `CrossTests.Script`.
const _script = <(String, Map<String, Object?>)>[
  ('world.entities', {}),
  (
    'world.entities',
    {
      'filter': ['Tag'],
    },
  ),
  ('entity.get', {'id': 1}),
  ('entity.get', {'id': 99}),
  ('world.snapshot', {}),
  ('entity.set', {'id': 1, 'component': 'Position', 'field': 'x', 'value': 7}),
  ('entity.set', {'id': 1, 'component': 'Position', 'field': 'x'}),
  ('entity.set', {'id': 5, 'component': 'Position', 'field': 'x', 'value': 7}),
  ('world.snapshot', {}),
  ('world.diff', {'a': 's1', 'b': 's2'}),
  ('world.diff', {'a': 1, 'b': 2}),
  ('world.diff', {'a': 's1', 'b': 's9'}),
  ('time.pause', {}),
  ('time.step', {'n': 3}),
  ('time.resume', {}),
  ('entity.get', {'id': 'one'}),
  ('log.tail', {}),
  ('no.such.verb', {}),
];

Future<void> main(List<String> arguments) async {
  stdout.encoding = utf8;
  switch (arguments) {
    case ['serve']:
      await _serve();
    case ['drive', final address]:
      await _drive(Uri.parse(address));
    default:
      stderr.writeln('usage: cross_peer.dart serve | drive ADDRESS');
      exitCode = 64;
  }
}

Future<void> _serve() async {
  final gate = TimeGate();
  final loop = Timer.periodic(const Duration(milliseconds: 2), (_) {
    gate.advance(0.016);
  });
  final server = await RemoteServer.start(
    RemoteHost(world: _MapWorld(), gate: gate),
    random: Random(7),
  );
  await stdin.drain<void>();
  loop.cancel();
  await server.close();
}

Future<void> _drive(Uri address) async {
  final client = await RemoteClient.connect(address);
  final seen = <Object?>[];
  for (final (method, params) in _script) {
    final entry = <String, Object?>{'method': method, 'params': params};
    try {
      entry['result'] = await client.call(method, params);
    } on RemoteError catch (error) {
      entry['error'] = {'code': error.code, 'message': error.message};
    }
    seen.add(entry);
  }
  await client.close();
  stdout.writeln(jsonEncode(seen));
}

final class _MapWorld implements WorldInspector {
  final Map<int, Map<String, Map<String, Object?>>> _held = {
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
    for (final entry in _held.entries)
      if (filter.every(entry.value.containsKey))
        (id: entry.key, components: entry.value.keys.toList()..sort()),
  ];

  @override
  Map<String, Map<String, Object?>>? read(int id) => _held[id] == null
      ? null
      : {
          for (final component in _held[id]!.entries)
            component.key: {...component.value},
        };

  @override
  void write(int id, String component, String field, num value) {
    final components =
        _held[id] ?? (throw StateError('Entity $id is not alive'));
    final fields = components[component];
    if (fields == null || !fields.containsKey(field)) {
      throw ArgumentError('$field is not in $component');
    }
    fields[field] = value.toDouble();
  }
}
