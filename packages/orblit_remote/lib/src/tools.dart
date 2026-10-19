import 'dart:convert';

import 'verbs.dart';

/// The version the tool server reports. A test keeps it equal to the pubspec.
const toolServerVersion = '0.3.0';

const _protocolVersions = ['2025-06-18', '2025-03-26', '2024-11-05'];
const _defaultLimit = 20;
const _diffLimit = 200;

// One call must finish inside the client's deadline of thirty seconds, and a
// game may run at ten frames a second.
const _maxFrames = 300;

/// Calls one verb of the control surface and answers its result. Throws
/// [RemoteError] when the game refuses the call or cannot be reached.
typedef RemoteCall =
    Future<Object?> Function(String method, Map<String, Object?> params);

typedef _Args = Map<String, Object?>;
typedef _Content = List<Map<String, Object?>>;

final class _Tool {
  const _Tool(
    this.description,
    this.properties,
    this.run, {
    this.required = const [],
    this.writes = false,
    this.readOnlyHint = true,
  });

  final String description;
  final Map<String, Object?> properties;
  final List<String> required;

  /// Gated behind the write flag.
  final bool writes;

  /// What a client may read as "this changes nothing". `step` is not gated,
  /// but it runs game code and holds time, so it does not claim it.
  final bool readOnlyHint;
  final Future<_Content> Function(_Args args) run;
}

/// The control surface as named tools for an assistant, over the tool
/// protocol. One message in, one answer out. It knows nothing of stdin or of
/// sockets, so a test can hand it a fake [RemoteCall].
///
/// The one tool that writes, `set`, is left out of the list and refused when
/// [allowWrite] is false.
final class ToolServer {
  ToolServer(this._call, {required this.allowWrite});

  final RemoteCall _call;
  final bool allowWrite;

  late final Map<String, _Tool> _tools = {
    'observe': _Tool(
      'Lists entities with their component names, and answers the count and '
      'the first `limit` rows (default $_defaultLimit). With `id`, answers '
      'that one entity with every field of every component.',
      {
        'id': {'type': 'integer', 'description': 'One entity to read in full'},
        'filter': {
          'type': 'array',
          'items': {'type': 'string'},
          'description': 'Keep entities that have all of these components',
        },
        'limit': {'type': 'integer', 'description': 'Rows to answer'},
      },
      _observe,
    ),
    'step': _Tool(
      'Pauses the game and passes `n` frames (default 1, at most '
      '$_maxFrames), each one step delta of the game, a sixtieth of a second '
      'unless it chose another. Answers the frames passed. The game stays '
      'paused until this session ends.',
      {
        'n': {'type': 'integer', 'minimum': 1, 'maximum': _maxFrames},
      },
      _step,
      readOnlyHint: false,
    ),
    'set': _Tool(
      'Writes to the running game. Sets one field of one component on an '
      'entity to a number, and answers what it wrote.',
      {
        'id': {'type': 'integer'},
        'component': {'type': 'string'},
        'field': {'type': 'string'},
        'value': {'type': 'number'},
      },
      _set,
      required: ['id', 'component', 'field', 'value'],
      writes: true,
      readOnlyHint: false,
    ),
    'snapshot': _Tool(
      'Records every entity and the host extras under a new id, and answers '
      'the id with counts, not the content. The last eight are kept.',
      {},
      _snapshot,
    ),
    'diff': _Tool(
      'Compares two snapshot ids and answers the count and the first '
      '$_diffLimit paths that differ. It is only as good as the two '
      'snapshots, and misses what neither one held.',
      {
        'a': {'type': 'string', 'description': 'The earlier snapshot id'},
        'b': {'type': 'string', 'description': 'The later snapshot id'},
      },
      _diff,
      required: ['a', 'b'],
    ),
    'screenshot': _Tool(
      'The render view as one PNG. It is a single frame, so it says nothing '
      'about motion, and a paused game shows the same picture each time.',
      {},
      _screenshot,
    ),
    'perf': _Tool(
      'The phase times the game reports, as it reports them.',
      {},
      _perf,
    ),
  };

  // The one place the write gate is read. The list and the call both use it,
  // so a tool cannot be listed and refused, or hidden and callable.
  late final Map<String, _Tool> _visible = {
    for (final entry in _tools.entries)
      if (allowWrite || !entry.value.writes) entry.key: entry.value,
  };

  /// The answer to one message, as JSON text. Null when nothing is owed: a
  /// notification, or a batch of them. A message with no id is never run,
  /// since it could not be answered, and no notification this server knows
  /// needs an action.
  Future<String?> handle(String message) async {
    final Object? decoded;
    try {
      decoded = jsonDecode(message);
    } on FormatException {
      return _failure(null, RemoteError.parse, 'The message is not JSON');
    }
    if (decoded is! List<Object?>) return _one(decoded);
    if (decoded.isEmpty) {
      return _failure(
        null,
        RemoteError.invalidRequest,
        'A batch holds at least one message',
      );
    }
    final answers = [for (final item in decoded) await _one(item)];
    final owed = answers.nonNulls.toList();
    return owed.isEmpty ? null : '[${owed.join(',')}]';
  }

  Future<String?> _one(Object? request) async {
    if (request is! Map<String, Object?>) {
      return _failure(
        null,
        RemoteError.invalidRequest,
        'A message is one object with a method',
      );
    }
    final id = request['id'];
    if (id != null && id is! int && id is! String) {
      return _failure(
        null,
        RemoteError.invalidRequest,
        'An id is a whole number or text',
      );
    }
    final method = request['method'];
    if (method is! String) {
      return _failure(id, RemoteError.invalidRequest, 'A message has a method');
    }
    if (id == null) return null;
    try {
      final result = await _dispatch(method, request['params']);
      return _reply({'id': id, 'result': result});
    } on RemoteError catch (error) {
      return _failure(id, error.code, error.message);
    }
  }

  Future<Object?> _dispatch(String method, Object? params) async {
    switch (method) {
      case 'initialize':
        return _initialize(params);
      case 'ping':
        return <String, Object?>{};
      case 'tools/list':
        return {'tools': _list()};
      case 'tools/call':
        return _callTool(params);
    }
    throw RemoteError(RemoteError.noMethod, 'No method named $method');
  }

  Map<String, Object?> _initialize(Object? params) {
    final asked = params is Map<String, Object?>
        ? params['protocolVersion']
        : null;
    return {
      'protocolVersion': _protocolVersions.contains(asked)
          ? asked
          : _protocolVersions.first,
      'capabilities': {'tools': <String, Object?>{}},
      'serverInfo': {'name': 'orblit-remote', 'version': toolServerVersion},
    };
  }

  List<Map<String, Object?>> _list() => [
    for (final entry in _visible.entries)
      {
        'name': entry.key,
        'description': entry.value.description,
        'inputSchema': {
          'type': 'object',
          'properties': entry.value.properties,
          'required': entry.value.required,
        },
        'annotations': {'readOnlyHint': entry.value.readOnlyHint},
      },
  ];

  Future<Object?> _callTool(Object? params) async {
    final call = params is Map<String, Object?>
        ? params
        : const <String, Object?>{};
    final tool = _visible[call['name']];
    if (tool == null) {
      throw RemoteError(
        RemoteError.invalidParams,
        'Unknown tool ${call['name']}',
      );
    }
    final args = call['arguments'] ?? const <String, Object?>{};
    if (args is! _Args) {
      throw const RemoteError(
        RemoteError.invalidParams,
        'Arguments are an object of names',
      );
    }
    try {
      return {'content': await tool.run(args), 'isError': false};
    } on RemoteError catch (error) {
      return _problem('${error.code}: ${error.message}');
    } on Object catch (error) {
      // A game that answers in a shape this server does not know is the
      // assistant's problem to read, not a reason to end the session.
      return _problem('The game answered something unexpected: $error');
    }
  }

  // shortcut: the cuts happen here, after the game has answered in full. Give
  // world.entities a limit when a world is big enough for that to matter.
  Future<_Content> _observe(_Args args) async {
    final id = args['id'];
    if (id != null) return _text(await _call('entity.get', {'id': id}));
    final filter = args['filter'];
    final rows =
        await _call('world.entities', {'filter': ?filter}) as List<Object?>;
    final limit = args['limit'] ?? _defaultLimit;
    if (limit is! int || limit < 1) {
      throw const RemoteError(
        RemoteError.invalidParams,
        'limit must be a whole number above 0',
      );
    }
    return _text({
      'count': rows.length,
      'entities': rows.take(limit).toList(),
      'truncated': rows.length > limit,
    });
  }

  Future<_Content> _step(_Args args) async {
    final n = args['n'] ?? 1;
    if (n is! int || n < 1 || n > _maxFrames) {
      throw const RemoteError(
        RemoteError.invalidParams,
        'n is a whole number from 1 to $_maxFrames. Call step again for more.',
      );
    }
    return _text(await _call('time.step', {'n': n}));
  }

  Future<_Content> _set(_Args args) async {
    final value = args['value'];
    // JSON cannot carry these, so the call would fail while it was written.
    if (value is num && !value.isFinite) {
      throw const RemoteError(
        RemoteError.invalidParams,
        'value must be a finite number',
      );
    }
    final written = {
      for (final name in const ['id', 'component', 'field', 'value'])
        name: args[name],
    };
    await _call('entity.set', written);
    return _text(written);
  }

  Future<_Content> _snapshot(_Args _) async {
    final kept = await _call('world.snapshot', {}) as Map<String, Object?>;
    return _text({
      'id': kept['id'],
      'entities': (kept['entities']! as Map<String, Object?>).length,
      'extras': (kept['extras']! as Map<String, Object?>).keys.toList(),
    });
  }

  Future<_Content> _diff(_Args args) async {
    final changes =
        await _call('world.diff', {'a': args['a'], 'b': args['b']})
            as List<Object?>;
    return _text({
      'count': changes.length,
      'changes': changes.take(_diffLimit).toList(),
      'truncated': changes.length > _diffLimit,
    });
  }

  Future<_Content> _screenshot(_Args _) async {
    final shot = await _call('view.screenshot', {}) as Map<String, Object?>;
    return [
      {'type': 'image', 'data': shot['png'], 'mimeType': 'image/png'},
    ];
  }

  Future<_Content> _perf(_Args _) async =>
      _text(await _call('perf.summary', {}));

  _Content _text(Object? value) => [
    {'type': 'text', 'text': value is String ? value : jsonEncode(value)},
  ];

  Map<String, Object?> _problem(String message) => {
    'content': _text(message),
    'isError': true,
  };

  String _reply(Map<String, Object?> body) =>
      jsonEncode({'jsonrpc': '2.0', ...body});

  String _failure(Object? id, int code, String message) => _reply({
    'id': id,
    'error': {'code': code, 'message': message},
  });
}
