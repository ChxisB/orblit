import 'dart:async';
import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'inspector.dart';
import 'log_tail.dart';
import 'snapshot.dart';
import 'time_gate.dart';

/// A request the surface refuses, with the JSON-RPC code a client reads.
final class RemoteError implements Exception {
  const RemoteError(this.code, this.message);

  static const parse = -32700;
  static const invalidRequest = -32600;
  static const noMethod = -32601;
  static const invalidParams = -32602;
  static const internal = -32603;
  static const notFound = -32001;
  static const readOnly = -32002;
  static const timedOut = -32003;
  static const unavailable = -32004;

  final int code;
  final String message;
}

/// The parts of a running game the surface can reach. Each is optional, and a
/// verb that needs a part the game did not give answers [RemoteError.unavailable].
final class RemoteHost {
  const RemoteHost({
    this.world,
    this.gate,
    this.log,
    this.perf,
    this.input,
    this.screenshot,
    this.passes,
    this.draws,
    this.material,
    this.extras = const {},
    this.readOnly = false,
    this.timeout = const Duration(seconds: 10),
  });

  final WorldInspector? world;
  final TimeGate? gate;
  final LogTail? log;

  /// The host's phase times, as JSON.
  final Map<String, Object?> Function()? perf;

  /// Passes one input event, as JSON, to the game.
  final FutureOr<void> Function(Map<String, Object?> event)? input;

  /// The render view as PNG bytes.
  final Future<Uint8List> Function()? screenshot;

  /// The last frame's passes in the order they ran, as rows of `name`,
  /// `milliseconds` and `draws`. What the renderer reports.
  final Future<List<Map<String, Object?>>> Function()? passes;

  /// What the scene submitted to draw, one row each. A row carries a text `id`
  /// that is unique among the rows. Culling and batching come after the scene,
  /// so this is not what the renderer drew. [passes] is the check on it.
  ///
  /// A plain function runs to the end on the game's isolate, so [timeout] does
  /// not cover it. Keep it quick.
  final List<Map<String, Object?>> Function()? draws;

  /// One material by its key, or null for a key the scene does not hold. Like
  /// [draws], it is not covered by [timeout].
  final Map<String, Object?>? Function(int key)? material;

  /// Further state kept beside the entities in a snapshot, by name, as JSON.
  /// The physics world's own snapshot is native memory with no JSON form, so a
  /// host puts what it wants compared here, such as contacts or stats.
  final Map<String, Map<String, Object?> Function()> extras;

  /// Refuses `entity.set` and `input.send`. Time control and snapshots stay,
  /// because they change no value by themselves.
  final bool readOnly;

  /// How long a verb waits on the game before it answers
  /// [RemoteError.timedOut]: for the next stepped frame, the input sink, the
  /// screenshot and [passes]. It must be above zero.
  final Duration timeout;
}

typedef _Run = Future<Object?> Function(Map<String, Object?> params);

/// The verbs of the control surface, as one JSON-RPC request in and one
/// answer out. It knows nothing of sockets, so a test can call it directly.
final class RemoteVerbs {
  /// Throws [ArgumentError] for a [RemoteHost.timeout] of zero or less. A
  /// const host cannot check it, so the owner of the clock does.
  RemoteVerbs(this._host) {
    if (_host.timeout <= Duration.zero) {
      throw ArgumentError.value(_host.timeout, 'timeout', 'is not above 0');
    }
  }

  final RemoteHost _host;
  final SnapshotStore _snapshots = SnapshotStore();

  late final Map<String, ({bool write, _Run run})> _table = {
    'world.entities': (write: false, run: _entities),
    'entity.get': (write: false, run: _get),
    'entity.set': (write: true, run: _set),
    'time.pause': (write: false, run: _pause),
    'time.resume': (write: false, run: _resume),
    'time.step': (write: false, run: _step),
    'world.snapshot': (write: false, run: _snapshot),
    'world.diff': (write: false, run: _diff),
    'input.send': (write: true, run: _send),
    'view.screenshot': (write: false, run: _screenshot),
    'view.passes': (write: false, run: _passes),
    'view.draws': (write: false, run: _draws),
    'view.draw': (write: false, run: _draw),
    'view.material': (write: false, run: _material),
    'log.tail': (write: false, run: _tail),
    'perf.summary': (write: false, run: _perf),
  };

  /// The answer to one request, as the JSON text to send back. Null for a
  /// notification, a request with no id, which is never answered.
  ///
  /// shortcut: one request at a time, not a batch. Add arrays when a client
  /// needs several answers in one round trip.
  Future<String?> handle(String message) async {
    final Map<String, Object?> request;
    try {
      request = _parse(message);
    } on RemoteError catch (error) {
      return _failure(null, error);
    }
    final id = request['id'];
    try {
      final result = await _call(request);
      if (id == null) return null;
      return jsonEncode({'jsonrpc': '2.0', 'id': id, 'result': result});
    } on RemoteError catch (error) {
      return id == null ? null : _failure(id, error);
    } catch (error) {
      // A host callback can throw anything, and the debugger must outlive it.
      final wrapped = RemoteError(RemoteError.internal, '$error');
      return id == null ? null : _failure(id, wrapped);
    }
  }

  Map<String, Object?> _parse(String message) {
    final Object? request;
    try {
      request = jsonDecode(message);
    } on FormatException {
      throw const RemoteError(RemoteError.parse, 'The request is not JSON');
    }
    final id = request is Map<String, Object?> ? request['id'] : null;
    if (request is! Map<String, Object?> || (id is! int? && id is! String?)) {
      throw const RemoteError(
        RemoteError.invalidRequest,
        'A request is one object with a method and an int or text id',
      );
    }
    return request;
  }

  Future<Object?> _call(Map<String, Object?> request) async {
    final verb = _table[request['method']];
    if (verb == null) {
      throw RemoteError(
        RemoteError.noMethod,
        'No method named ${request['method']}',
      );
    }
    if (verb.write && _host.readOnly) {
      throw const RemoteError(
        RemoteError.readOnly,
        'This session is read only',
      );
    }
    final params = request['params'] ?? const <String, Object?>{};
    if (params is! Map<String, Object?>) {
      throw const RemoteError(
        RemoteError.invalidParams,
        'Params are an object of names, not a list',
      );
    }
    try {
      return await verb.run(params);
    } on ArgumentError catch (error) {
      throw RemoteError(RemoteError.invalidParams, '$error');
    } on StateError catch (error) {
      throw RemoteError(RemoteError.notFound, error.message);
    }
  }

  String _failure(Object? id, RemoteError error) => jsonEncode({
    'jsonrpc': '2.0',
    'id': id,
    'error': {'code': error.code, 'message': error.message},
  });

  T _need<T>(T? part, String name) =>
      part ??
      (throw RemoteError(
        RemoteError.unavailable,
        'This game did not give the surface $name',
      ));

  Future<Object?> _entities(Map<String, Object?> params) async {
    final world = _need(_host.world, 'a world');
    return [
      for (final row in world.entities(_names(params['filter'])))
        {'id': row.id, 'components': row.components},
    ];
  }

  Future<Object?> _get(Map<String, Object?> params) async {
    final id = _int(params, 'id');
    final components = _need(_host.world, 'a world').read(id);
    if (components == null) {
      throw RemoteError(RemoteError.notFound, 'Entity $id is not alive');
    }
    return {'id': id, 'components': components};
  }

  Future<Object?> _set(Map<String, Object?> params) async {
    _need(_host.world, 'a world').write(
      _int(params, 'id'),
      _string(params, 'component'),
      _string(params, 'field'),
      _num(params, 'value'),
    );
    return null;
  }

  Future<Object?> _pause(Map<String, Object?> _) async {
    _need(_host.gate, 'a time gate').pause();
    return {'paused': true};
  }

  Future<Object?> _resume(Map<String, Object?> _) async {
    _need(_host.gate, 'a time gate').resume();
    return {'paused': false};
  }

  // The deadline restarts whenever a frame passes, so a long step on a slow
  // game finishes and a loop that never calls the gate is caught.
  //
  // shortcut: two clients stepping at once share one count, so `stepped` can
  // be high for one of them. Give each step its own count when that matters.
  Future<Object?> _step(Map<String, Object?> params) async {
    final gate = _need(_host.gate, 'a time gate');
    final frames = _int(params, 'n');
    final before = gate.passed;
    final done = gate.step(frames);
    var left = gate.pending;
    while (true) {
      try {
        await done.timeout(_host.timeout);
        break;
      } on TimeoutException {
        if (gate.pending >= left) {
          gate.pause();
          throw RemoteError(
            RemoteError.timedOut,
            'The game passed no frame in ${_host.timeout.inMilliseconds} ms. '
            'Its loop must call world.tick(gate.advance(delta)).',
          );
        }
        left = gate.pending;
      }
    }
    return {'paused': true, 'stepped': min(frames, gate.passed - before)};
  }

  // F5 is broken on purpose: a snapshot is kept so that a later diff can name
  // it, and the client needs its id back with the content.
  Future<Object?> _snapshot(Map<String, Object?> _) async {
    final world = _need(_host.world, 'a world');
    final entities = <String, Object?>{};
    for (final row in world.entities(const [])) {
      final components = world.read(row.id);
      if (components != null) entities['${row.id}'] = components;
    }
    final raw = <String, Object?>{
      'entities': entities,
      'extras': {
        for (final extra in _host.extras.entries)
          extra.key: await _fromHost(extra.value),
      },
    };
    // The round trip copies the snapshot, so a host that reuses its maps
    // cannot change a kept one, and it fails here, before anything is kept.
    final snapshot = _plain(raw)! as Map<String, Object?>;
    _snapshots.add(snapshot);
    return {'id': _snapshots.lastId, ...snapshot};
  }

  Future<Object?> _diff(Map<String, Object?> params) async => [
    for (final change in diffJson(_kept(params, 'a'), _kept(params, 'b')))
      {'path': change.path, 'from': change.from, 'to': change.to},
  ];

  Future<Object?> _send(Map<String, Object?> params) async {
    final event = params['event'];
    if (event is! Map<String, Object?>) {
      throw ArgumentError.value(event, 'event', 'must be an object');
    }
    final sink = _need(_host.input, 'an input sink');
    await _fromHost(() => sink(event));
    return null;
  }

  Future<Object?> _screenshot(Map<String, Object?> _) async {
    final source = _need(_host.screenshot, 'a screenshot source');
    return {'png': base64Encode(await _fromHost(source))};
  }

  Future<Object?> _passes(Map<String, Object?> _) async =>
      _plain(await _fromHost(_need(_host.passes, 'a pass list')));

  Future<Object?> _draws(Map<String, Object?> params) async {
    final limit = params['limit'] == null ? 100 : _int(params, 'limit');
    if (limit < 0) throw ArgumentError.value(limit, 'limit', 'is below 0');
    final rows = await _fromHost(_need(_host.draws, 'a draw list'));
    return _plain({
      'count': rows.length,
      'draws': rows.take(limit).toList(),
      'truncated': rows.length > limit,
    });
  }

  Future<Object?> _draw(Map<String, Object?> params) async {
    final id = _string(params, 'id');
    final rows = await _fromHost(_need(_host.draws, 'a draw list'));
    final row = rows.where((row) => row['id'] == id).firstOrNull;
    if (row == null) throw RemoteError(RemoteError.notFound, 'No draw $id');
    return _plain(row);
  }

  Future<Object?> _material(Map<String, Object?> params) async {
    final key = _int(params, 'key');
    final read = _need(_host.material, 'a material list');
    final row = await _fromHost(() => read(key));
    if (row == null) {
      throw RemoteError(RemoteError.notFound, 'No material with key $key');
    }
    return _plain(row);
  }

  Future<Object?> _tail(Map<String, Object?> params) async {
    final count = params['n'] == null ? 50 : _int(params, 'n');
    return _need(_host.log, 'a log').tail(count);
  }

  Future<Object?> _perf(Map<String, Object?> _) async =>
      _fromHost(_need(_host.perf, 'a perf summary'));

  /// Runs a callback the game supplied. It can hang, and its own bug must not
  /// read as a bad request, so an argument or state error from it is internal.
  Future<T> _fromHost<T>(FutureOr<T> Function() call) async {
    try {
      return await Future.sync(call).timeout(_host.timeout);
    } on TimeoutException {
      throw RemoteError(
        RemoteError.timedOut,
        'The game did not answer in ${_host.timeout.inMilliseconds} ms',
      );
    } on ArgumentError catch (error) {
      throw RemoteError(RemoteError.internal, '$error');
    } on StateError catch (error) {
      throw RemoteError(RemoteError.internal, error.message);
    }
  }

  Map<String, Object?> _kept(Map<String, Object?> params, String name) {
    final id = _string(params, name);
    return _snapshots.find(id) ??
        (throw RemoteError(RemoteError.notFound, 'No snapshot named $id'));
  }
}

/// A float JSON cannot write is its name, as the inspector reports it.
Object _finite(Object? value) =>
    value is double ? '$value' : throw JsonUnsupportedObjectError(value);

/// Rows from the renderer can hold the NaN that is being looked for, so it
/// is text here and not an internal error from the encoder.
Object? _plain(Object? rows) =>
    jsonDecode(jsonEncode(rows, toEncodable: _finite));

List<String> _names(Object? value) {
  if (value == null) return const [];
  if (value is List<Object?> && value.every((name) => name is String)) {
    return value.cast<String>();
  }
  throw ArgumentError.value(value, 'filter', 'must be a list of names');
}

int _int(Map<String, Object?> params, String name) {
  final value = params[name];
  if (value is int) return value;
  throw ArgumentError.value(value, name, 'must be a whole number');
}

num _num(Map<String, Object?> params, String name) {
  final value = params[name];
  if (value is num) return value;
  throw ArgumentError.value(value, name, 'must be a number');
}

String _string(Map<String, Object?> params, String name) {
  final value = params[name];
  if (value is String) return value;
  throw ArgumentError.value(value, name, 'must be text');
}
