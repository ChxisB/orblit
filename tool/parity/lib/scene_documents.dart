import 'dart:convert';
import 'dart:math';

import 'package:orblit_scene/orblit_scene.dart';

import 'fixture.dart';

/// A copy of [value] with every number made a different one, every flag
/// flipped and every text kept, so a reader that ignores a key still shows.
Object? changed(Object? value) => switch (value) {
  final int i => i * 2 + 1,
  final double d => d * 2 + 1,
  final bool b => !b,
  final Map<String, Object?> m => {
    for (final e in m.entries) e.key: changed(e.value),
  },
  final List<Object?> l => [for (final e in l) changed(e)],
  _ => value,
};

/// A component of [type] at its defaults, as the JSON that holds it.
Map<String, Object?> fullOf(String type) =>
    SceneComponents.read(type, {}).toJson();

/// The cases for the file format: a scene's text in, what the reader kept and
/// what it writes out.
List<Map<String, Object?>> documentCases() => [
  for (final text in [..._currentTexts(), ..._oldTexts(), ..._brokenTexts()])
    _documentCase(text),
];

/// The cases for diffs: two scenes in, the operations between them and what
/// they do forward and back.
List<Map<String, Object?>> diffCases() {
  final cases = <Map<String, Object?>>[];
  for (var seed = 0; seed < 60; seed++) {
    final random = Random(seed);
    final a = _document(random, size: 2 + seed % 8);
    var b = a;
    for (var i = 0; i < 1 + seed % 4; i++) {
      b = _edit(random, b);
    }
    cases.add(_diffCase(a, b));
  }
  for (var seed = 0; seed < 12; seed++) {
    cases.add(
      _diffCase(
        _document(Random(seed), size: 6),
        _document(Random(seed + 100), size: 6),
      ),
    );
  }
  return cases;
}

/// The cases for reading a diff out of JSON that is not quite one.
List<Map<String, Object?>> operationCases() {
  final random = Random(5);
  final a = _document(random, size: 6);
  var b = a;
  for (var i = 0; i < 4; i++) {
    b = _edit(random, b);
  }
  final real =
      _roundTripped(SceneDiff.between(a, b).toJson())! as List<Object?>;
  final first = real.first as Map<String, Object?>;
  return [
    for (final raw in <Object?>[
      real,
      real.sublist(1),
      [...real, 7, null, 'text'],
      [
        {...first, 'op': 'bogus'},
        ...real.skip(1),
      ],
      [
        {...first, 'id': 'nothing-has-this-id'},
      ],
      [
        {'op': 'name', 'id': 'nothing-has-this-id', 'from': 'a', 'to': 'b'},
      ],
      {'op': 'name'},
      const <Object?>[],
      'text',
      null,
    ])
      _operationCase(a, raw),
  ];
}

/// The cases for moving one entity forward, as a clipboard or a prefab does.
List<Map<String, Object?>> entityCases() {
  final entities = <(Map<String, Object?>, int)>[
    ({'id': 'sun', 'kind': 'light', 'lightType': 'sun', 'power': 1000.0}, 1),
    ({'id': 'light', 'kind': 'light'}, 1),
    ({'id': 'bulb', 'kind': 'light', 'lightType': 'point', 'power': 60.0}, 1),
    ({'id': 'crate', 'kind': 'mesh', 'mesh': 'models/crate.glb'}, 2),
    (
      {
        'id': 'crate',
        'kind': 'mesh',
        'position': [1.0, 2.0, 3.0],
      },
      3,
    ),
    ({'id': 'portal', 'kind': 'wormhole'}, 3),
    ({'id': 'props', 'kind': 'group'}, 3),
    ({'name': 'no id', 'kind': 'mesh'}, 3),
    (
      {
        'id': 'post',
        'name': 'Post',
        'components': {
          'prefab': {'asset': 'props/lamp.oprefab'},
        },
      },
      4,
    ),
    (
      {
        'id': 'crate',
        'name': 'Crate',
        'components': {'mesh': const MeshComponent().toJson()},
      },
      SceneDocument.formatVersion,
    ),
    ({'id': 'old', 'kind': 'light'}, 0),
  ];
  return [for (final (json, version) in entities) _entityCase(json, version)];
}

Map<String, Object?> _documentCase(String text) => {
  'op': 'document',
  'in': [text],
  'expect': attempt(() {
    final load = SceneDocument.decode(text);
    return {'problems': load.problems, 'text': load.document.encode()};
  }),
};

Map<String, Object?> _diffCase(SceneDocument a, SceneDocument b) {
  final diff = SceneDiff.between(a, b);
  final ops = _roundTripped(diff.toJson());
  return {
    'op': 'diff',
    'in': [a.encode(), b.encode()],
    'expect': {
      'ops': tree(ops),
      'forward': diff.applyTo(a).encode(),
      'back': diff.inverse.applyTo(b).encode(),
      'reread': SceneDiff.fromJson(ops).applyTo(a).encode(),
    },
  };
}

Map<String, Object?> _operationCase(SceneDocument document, Object? raw) => {
  'op': 'operations',
  'in': [document.encode(), raw],
  'expect': attempt(() {
    final diff = SceneDiff.fromJson(_roundTripped(raw));
    return {
      'ops': tree(_roundTripped(diff.toJson())),
      'applied': diff.applyTo(document).encode(),
    };
  }),
};

// Dart reads the input after a trip through JSON, as it would read a file:
// a map literal typed by inference is not the map a decoder makes.
Map<String, Object?> _entityCase(Map<String, Object?> json, int version) => {
  'op': 'entity',
  'in': [json, version],
  'expect': attempt(() {
    final notes = <String>[];
    final moved = SceneMigrations.entity(
      _roundTripped(json)! as Map<String, Object?>,
      version: version,
      notes: notes,
    );
    return {'entity': tree(moved), 'notes': notes};
  }),
};

Object? _roundTripped(Object? value) => jsonDecode(jsonEncode(value));

String _old(int version, Map<String, Object?> rest) =>
    jsonEncode({'formatVersion': version, ...rest});

List<String> _currentTexts() => [
  SceneDocument().encode(),
  SceneDocument(name: 'Named').encode(),
  SceneDocument(
    settings: SceneSettings.fromJson({
      'sky': '#223344',
      'ambient': 12000.0,
      'time': {'hour': 18.5, 'cycle': true, 'hoursPerSecond': 0.25},
      'layerNames': ['World', '', 'Player'],
    }),
  ).encode(),
  for (var seed = 0; seed < 8; seed++)
    _document(Random(seed), size: 3 + seed * 2).encode(),
  SceneDocument(
    entities: [
      for (final type in _types)
        SceneEntity(
          id: type,
          name: type,
          components: {type: SceneComponents.read(type, {})},
        ),
      const SceneEntity(
        id: 'hidden',
        name: 'Hidden',
        visible: false,
        components: {
          'buoyancy': UnknownComponent('buoyancy', {
            'displacement': 3.5,
            'notes': 'from a newer editor',
          }),
        },
      ),
    ],
  ).encode(),
];

List<String> _oldTexts() => [
  _old(1, {
    'name': 'Old',
    'entities': [
      {
        'name': 'Key',
        'components': ['Light', 'Transform'],
      },
      {
        'name': 'Eye',
        'components': ['Camera'],
      },
      {
        'name': 'Crate',
        'components': ['MeshRenderer'],
      },
    ],
  }),
  _old(1, {
    'objects': [
      {'id': 'sun', 'kind': 'light', 'lightType': 'sun', 'power': 1000.0},
      {'id': 'light', 'kind': 'light'},
      {'id': 'bulb', 'kind': 'light', 'lightType': 'point', 'power': 60.0},
    ],
  }),
  _old(2, {
    'objects': <Object?>[],
    'fog': {
      'colour': '#405060',
      'density': 0.4,
      'height': 12.0,
      'falloff': 0.3,
      'mist': 0.5,
      'mistSize': 20.0,
      'mistSpeed': 0.25,
    },
  }),
  _old(2, {
    'objects': <Object?>[],
    'fog': {'density': 0.2},
  }),
  _old(2, {
    'objects': [
      {'id': 'crate', 'kind': 'mesh'},
    ],
    'fog': {'density': 0.0},
  }),
  _old(2, {
    'objects': [
      {'id': 'weather', 'kind': 'mesh'},
    ],
    'fog': {'density': 0.3},
  }),
  _old(3, {
    'name': 'Courtyard',
    'sky': '#223344',
    'ambient': 12000.0,
    'time': {'hour': 18.5, 'cycle': true, 'hoursPerSecond': 0.25},
    'objects': [
      {
        'id': 'floor',
        'name': 'Floor',
        'kind': 'mesh',
        'position': [0.0, -1.0, 0.0],
        'scale': [30.0, 1.0, 30.0],
        'colour': '#8899AA',
        'mesh': 'models/floor.glb',
        'material': 'materials/stone.omat',
        'castShadows': false,
        'sway': 0.25,
        'data': ['scripts/floor.js'],
      },
      {'id': 'box', 'kind': 'shape'},
      {
        'id': 'spot',
        'kind': 'light',
        'lightType': 'spot',
        'power': 60.0,
        'spotSize': 30.0,
        'colour': '#FFEEDD',
        'castShadows': false,
      },
      {'id': 'props', 'kind': 'group'},
      {'id': 'crate', 'kind': 'mesh', 'parent': 'props', 'visible': false},
      {'id': 'portal', 'kind': 'wormhole'},
    ],
  }),
  _old(3, {'name': 'Nothing'}),
  _old(4, {
    'name': 'Street',
    'entities': [
      {
        'id': 'post',
        'name': 'Post',
        'components': {
          'prefab': {'asset': 'props/lamp.oprefab'},
        },
      },
      {
        'id': 'sign',
        'name': 'Sign',
        'parent': 'post',
        'components': {'mesh': const MeshComponent().toJson()},
      },
    ],
  }),
];

List<String> _brokenTexts() => [
  'not a scene',
  '[1, 2, 3]',
  '{"entities": []}',
  _old(SceneDocument.formatVersion + 1, {'entities': <Object?>[]}),
  _old(SceneDocument.formatVersion, {'name': 'No list'}),
  _old(SceneDocument.formatVersion, {'entities': 'not a list'}),
  _old(SceneDocument.formatVersion, {
    'entities': [
      {'id': 'a', 'name': 'A'},
      {'name': 'no id of its own'},
      {'id': 'a', 'name': 'the second a'},
      7,
      {'id': 'b', 'name': 'B'},
    ],
  }),
  _old(SceneDocument.formatVersion, {
    'entities': [
      {'id': 'child', 'name': 'Lamp', 'parent': 'gone'},
      {'id': 'inner', 'name': 'Inner', 'parent': 'ghost/part'},
      {'id': 'post', 'name': 'Post'},
      {'id': 'sign', 'name': 'Sign', 'parent': 'post/lamp'},
    ],
  }),
  _old(SceneDocument.formatVersion, {
    'entities': [
      {'id': 'a', 'name': 'A', 'parent': 'b'},
      {'id': 'b', 'name': 'B', 'parent': 'a'},
      {'id': 'c', 'name': 'C', 'parent': 'c'},
    ],
  }),
  _old(SceneDocument.formatVersion, {
    'sky': 'not a colour',
    'layerNames': ['World', 7, null, 'Player'],
    'entities': <Object?>[],
  }),
  _old(SceneDocument.formatVersion, {
    'layerNames': [for (var i = 0; i < 40; i++) 'Layer $i'],
    'entities': <Object?>[],
  }),
];

const _types = [
  SceneComponents.transform,
  SceneComponents.mesh,
  SceneComponents.material,
  SceneComponents.body,
  SceneComponents.joint,
  SceneComponents.motion,
  SceneComponents.zone,
  SceneComponents.vehicle,
  SceneComponents.wheel,
  SceneComponents.light,
  SceneComponents.camera,
  SceneComponents.terrain,
  SceneComponents.sprite,
  SceneComponents.weather,
  SceneComponents.particles,
  SceneComponents.soundSource,
  SceneComponents.data,
];

SceneComponent _component(Random random, String type) {
  final full = fullOf(type);
  return SceneComponents.read(
    type,
    random.nextBool() ? full : changed(full)! as Map<String, Object?>,
  );
}

SceneEntity _entity(Random random, String id, String? parent) => SceneEntity(
  id: id,
  name: 'Thing ${random.nextInt(9)}',
  parent: parent,
  visible: random.nextInt(4) != 0,
  components: {
    for (final type in _types)
      if (random.nextInt(4) == 0) type: _component(random, type),
  },
);

// Parented only to something already placed, so a generated document has no
// loop for decode to cut and its text reads back as it was written.
SceneDocument _document(Random random, {required int size}) {
  final entities = <SceneEntity>[];
  for (var i = 0; i < size; i++) {
    final parent = entities.isEmpty || random.nextBool()
        ? null
        : entities[random.nextInt(entities.length)].id;
    entities.add(_entity(random, 'e$i', parent));
  }
  final document = SceneDocument(
    name: 'Scene ${random.nextInt(5)}',
    entities: entities,
  );
  final text = document.encode();
  if (SceneDocument.decode(text).document.encode() != text) {
    throw StateError('A generated scene does not read back as written.');
  }
  return document;
}

SceneDocument _edit(Random random, SceneDocument document) {
  if (document.entities.isEmpty) {
    return document.withEntity('n0', _entity(random, 'n0', null));
  }
  final entity = document.entities[random.nextInt(document.length)];
  return switch (random.nextInt(9)) {
    0 => document.withEntity(
      entity.id,
      entity.copyWith(name: 'Renamed ${random.nextInt(9)}'),
    ),
    1 => document.withEntity(
      entity.id,
      entity.copyWith(visible: !entity.visible),
    ),
    2 => _reparented(random, document, entity),
    3 => _added(random, document),
    4 => _removed(document, entity),
    5 => _toggled(random, document, entity),
    6 => _nudged(random, document, entity),
    7 => _swapped(random, document),
    _ => document.copyWith(
      settings: document.settings.copyWith(
        ambient: random.nextInt(5) * 1000.0,
        timeOfDay: random.nextInt(24).toDouble(),
        layerNames: random.nextBool() ? const ['World', '', 'Player'] : null,
      ),
    ),
  };
}

SceneDocument _reparented(
  Random random,
  SceneDocument document,
  SceneEntity entity,
) {
  final others = [
    for (final other in document.entities)
      if (!_inside(document, other.id, entity.id)) other.id,
  ];
  final parent = others.isEmpty || random.nextBool()
      ? null
      : others[random.nextInt(others.length)];
  return document.withEntity(
    entity.id,
    parent == null
        ? entity.copyWith(clearParent: true)
        : entity.copyWith(parent: parent),
  );
}

// Whether [id] is [ancestor] or somewhere beneath it.
bool _inside(SceneDocument document, String id, String ancestor) {
  for (String? at = id; at != null; at = document[at]?.parent) {
    if (at == ancestor) return true;
  }
  return false;
}

SceneDocument _added(Random random, SceneDocument document) {
  final id = 'n${random.nextInt(1000)}';
  if (document.contains(id)) return document;
  final parent = random.nextBool()
      ? null
      : document.entities[random.nextInt(document.length)].id;
  final entities = [...document.entities]
    ..insert(random.nextInt(document.length + 1), _entity(random, id, parent));
  return document.copyWith(entities: entities);
}

// Only something nothing hangs from, so no child is left with a missing parent.
SceneDocument _removed(SceneDocument document, SceneEntity entity) =>
    document.entities.any((other) => other.parent == entity.id)
    ? document
    : document.withEntity(entity.id, null);

SceneDocument _toggled(
  Random random,
  SceneDocument document,
  SceneEntity entity,
) {
  final type = _types[random.nextInt(_types.length)];
  final components = {...entity.components};
  if (components.remove(type) == null) {
    components[type] = _component(random, type);
  }
  return document.withEntity(
    entity.id,
    entity.copyWith(components: components),
  );
}

SceneDocument _nudged(
  Random random,
  SceneDocument document,
  SceneEntity entity,
) {
  if (entity.components.isEmpty) return document;
  final type = entity.components.keys.elementAt(
    random.nextInt(entity.components.length),
  );
  final json = entity.components[type]!.toJson();
  if (json.isEmpty) return document;
  final key = json.keys.elementAt(random.nextInt(json.length));
  final next = SceneComponents.read(type, {...json, key: changed(json[key])});
  return document.withEntity(
    entity.id,
    entity.copyWith(components: {...entity.components, type: next}),
  );
}

SceneDocument _swapped(Random random, SceneDocument document) {
  final entities = [...document.entities];
  final i = random.nextInt(entities.length);
  final j = random.nextInt(entities.length);
  final one = entities[i];
  entities[i] = entities[j];
  entities[j] = one;
  return document.copyWith(entities: entities);
}
