import 'dart:convert';

import 'verbs.dart';

/// The version the tool server reports. A test keeps it equal to the pubspec.
const toolServerVersion = '0.2.0';

const _protocolVersions = ['2025-06-18', '2025-03-26', '2024-11-05'];
const _defaultLimit = 20;
const _diffLimit = 200;

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
  });

  final String description;
  final Map<String, Object?> properties;
  final List<String> required;
  final bool writes;
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
      'Pauses the game and passes `n` frames (default 1) of a sixtieth of a '
      'second. Answers the frames passed. The game runs again when this '
      'session ends.',
      {
        'n': {'type': 'integer', 'description': 'Frames, at least 1'},
      },
      _step,
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

  /// The answer to one message, as JSON text. Null for a notification, a
  /// message with no id, which is never answered.
  Future<String?> handle(String message) async {
    final Object? request;
    try {
      request = jsonDecode(message);
    } on FormatException {
      return _failure(null, RemoteError.parse, 'The message is not JSON');
    }
    if (request is! Map<String, Object?> || request['method'] is! String) {
      return _failure(
        null,
        RemoteError.invalidRequest,
        'A message is one object with a method',
      );
    }
    final id = request['id'];
    try {
      final result = await _dispatch(
        request['method']! as String,
        request['params'],
      );
      return id == null ? null : _reply({'id': id, 'result': result});
    } on RemoteError catch (error) {
      return id == null ? null : _failure(id, error.code, error.message);
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
    for (final entry in _tools.entries)
      if (allowWrite || !entry.value.writes)
        {
          'name': entry.key,
          'description': entry.value.description,
          'inputSchema': {
            'type': 'object',
            'properties': entry.value.properties,
            'required': entry.value.required,
          },
          'annotations': {'readOnlyHint': !entry.value.writes},
        },
  ];

  Future<Object?> _callTool(Object? params) async {
    final name = params is Map<String, Object?> ? params['name'] : null;
    final tool = _tools[name];
    if (tool == null || (tool.writes && !allowWrite)) {
      throw RemoteError(RemoteError.invalidParams, 'Unknown tool $name');
    }
    final args = (params! as Map<String, Object?>)['arguments'] ?? {};
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

  Future<_Content> _step(_Args args) async =>
      _text(await _call('time.step', {'n': args['n'] ?? 1}));

  Future<_Content> _set(_Args args) async {
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
