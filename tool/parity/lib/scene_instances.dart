import 'dart:convert';

import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

import 'fixture.dart';

const _lamp = 'props/lamp.oprefab';
const _street = 'props/street.oprefab';

/// The cases for materials: files and groups in, what each reads as and writes
/// back, and what each path resolves to.
List<Map<String, Object?>> materialCases() => [
  for (final scenario in _materialScenarios())
    {'op': 'materials', 'in': scenario, 'expect': _materialResult(scenario)},
];

/// The cases for instances: a scene, the prefabs it can reach and a list of
/// steps in, and the scene, prefabs and notes that come out.
List<Map<String, Object?>> instanceCases() => [
  for (final scenario in _instanceScenarios())
    {'op': 'instances', 'in': scenario, 'expect': _instanceResult(scenario)},
];

List<List<Object?>> _materialScenarios() => [
  [
    {
      'every.omat': {
        'values': {
          'shading': 'unlit',
          'blend': 'fade',
          'culling': 'none',
          'doubleSided': true,
          'baseColour': [1, 0.5, 0, 1],
          'metallic': 1,
          'roughness': 0.25,
          'sheenColour': [0.1, 0.2, 0.3],
          'tiling': [2, 3],
          'wrap': 'mirror',
          'filter': 'sharp',
          'blendMode': 'maskedDepth',
          'windStrength': 0.5,
        },
        'maps': {'baseColour': 'tex/a.png', 'normal': 'tex/n.png'},
      },
      'empty.omat': <String, Object?>{},
    },
    <String, Object?>{},
    ['every.omat', 'empty.omat'],
  ],
  [
    {
      'bad.omat': {
        'values': {
          'metallic': 'x',
          'roughness': true,
          'baseColour': [1, 2],
          'emissive': [1, 'a', 3],
          'tiling': [1, 2, 3],
          'blend': 'bogus',
          'doubleSided': 1,
          'shinyness': 1,
        },
        'maps': {'baseColour': 'a.png', 'nothing': 'b.png', 'normal': 4},
        'parent': 'other.omat',
      },
      'shape.omat': {
        'values': 3,
        'maps': <Object?>[],
        'parent': 5,
        'group': false,
      },
    },
    <String, Object?>{},
    ['bad.omat', 'shape.omat'],
  ],
  [
    {
      'base.omat': {
        'values': {
          'metallic': 0.2,
          'baseColour': [1, 1, 1, 1],
          'blend': 'fade',
        },
        'maps': {'baseColour': 'tex/base.png', 'normal': 'tex/n.png'},
      },
      'child.omat': {
        'parent': 'base.omat',
        'values': {'metallic': 0.8},
        'maps': {'baseColour': 'tex/child.png'},
      },
      'grand.omat': {
        'parent': 'child.omat',
        'values': {'roughness': 0.1},
      },
    },
    <String, Object?>{},
    ['base.omat', 'child.omat', 'grand.omat'],
  ],
  [
    {
      'a.omat': {
        'parent': 'b.omat',
        'values': {'metallic': 1},
      },
      'b.omat': {
        'parent': 'a.omat',
        'values': {'roughness': 1},
      },
      'lost.omat': {
        'parent': 'gone.omat',
        'values': {'metallic': 0.5},
      },
      'self.omat': {'parent': 'self.omat'},
    },
    <String, Object?>{},
    ['a.omat', 'b.omat', 'lost.omat', 'self.omat', 'missing.omat'],
  ],
  [
    {
      'base.omat': {
        'group': 'wet',
        'values': {'roughness': 0.9, 'metallic': 0},
        'maps': {'normal': 'tex/dry.png'},
      },
      'child.omat': {'parent': 'base.omat', 'group': 'dry'},
      'plain.omat': {'parent': 'base.omat'},
      'nogroup.omat': {'group': 'nothing'},
    },
    {
      'wet': {
        'values': {'roughness': 0.1},
        'maps': {'normal': 'tex/wet.png'},
      },
      'dry': {
        'values': {'roughness': 0.7, 'depthBias': 2},
      },
    },
    ['base.omat', 'child.omat', 'plain.omat', 'nogroup.omat'],
  ],
];

Map<String, Object?> _materialResult(List<Object?> scenario) {
  final docs = scenario[0]! as Map<String, Object?>;
  final groups = scenario[1]! as Map<String, Object?>;
  final paths = (scenario[2]! as List<Object?>).cast<String>();
  return attempt(() {
    final read = {
      for (final e in docs.entries)
        e.key: MaterialDocument.fromJson(e.value! as Map<String, Object?>),
    };
    final readGroups = {
      for (final e in groups.entries)
        e.key: MaterialDocument.fromJson(e.value! as Map<String, Object?>),
    };
    final library = MaterialLibrary(
      materials: {for (final e in read.entries) e.key: e.value.document},
      groups: {for (final e in readGroups.entries) e.key: e.value.document},
    );
    return {
      'docs': {for (final e in read.entries) e.key: _loaded(e.value)},
      'groups': {for (final e in readGroups.entries) e.key: _loaded(e.value)},
      'resolved': {
        for (final path in paths) path: _resolved(library.resolve(path)),
      },
    };
  });
}

Map<String, Object?> _loaded(MaterialLoad load) => {
  'problems': load.problems,
  'json': tree(load.document.toJson()),
};

Map<String, Object?> _resolved(ResolvedMaterial one) => {
  'values': tree(one.values),
  'maps': one.maps,
  'problems': one.problems,
};

SceneEntity _thing(
  String id, {
  String? parent,
  double x = 0,
  double y = 0,
  Map<String, SceneComponent> components = const {},
}) => SceneEntity(
  id: id,
  name: id,
  parent: parent,
  components: {
    SceneComponents.transform: TransformComponent(position: Vector3(x, y, 0)),
    ...components,
  },
);

SceneEntity _stub(
  String id,
  String asset, {
  String? parent,
  SceneDiff overrides = SceneDiff.none,
}) => SceneEntity(
  id: id,
  name: id,
  parent: parent,
  components: {
    SceneComponents.prefab: PrefabComponent(asset: asset, overrides: overrides),
  },
);

String _lampText({bool shade = false}) => PrefabDocument(
  name: 'Lamp',
  root: 'lamp',
  document: SceneDocument(
    name: 'Lamp',
    entities: [
      _thing('lamp'),
      _thing(
        'pole',
        parent: 'lamp',
        components: {
          SceneComponents.mesh: const MeshComponent(asset: 'models/pole.glb'),
        },
      ),
      _thing('bulb', parent: 'pole', y: 2),
      if (shade) _thing('shade', parent: 'bulb'),
    ],
  ),
).encode();

String _streetText() => PrefabDocument(
  name: 'Street',
  root: 'street',
  document: SceneDocument(
    name: 'Street',
    entities: [
      _thing('street'),
      _stub('lamp3', _lamp, parent: 'street'),
    ],
  ),
).encode();

// A prefab that contains an instance of itself.
String _loopText() => PrefabDocument(
  name: 'Loop',
  root: 'loop',
  document: SceneDocument(
    name: 'Loop',
    entities: [
      _thing('loop'),
      _stub('inner', 'props/loop.oprefab', parent: 'loop'),
    ],
  ),
).encode();

String _sceneText(List<SceneEntity> entities) =>
    SceneDocument(entities: entities).encode();

Map<String, Object?> _copy(
  String id,
  String name, {
  String? parent,
  double y = 0,
}) => {
  'id': id,
  'name': name,
  if (parent != null) 'parent': parent,
  'components': {
    'transform': {
      'position': [0.0, y, 0.0],
    },
    if (name == 'Pole')
      'mesh': const MeshComponent(asset: 'models/pole.glb').toJson(),
    'prefab': {'asset': _lamp},
  },
};

// Copies stamped in before instances were links, one with its bulb raised and
// a sign hung on it.
String _legacyText() => jsonEncode({
  'formatVersion': 4,
  'entities': [
    _copy('o1', 'Lamp'),
    _copy('o2', 'Pole', parent: 'o1'),
    _copy('o3', 'Bulb', parent: 'o2', y: 5),
    {'id': 'o4', 'name': 'Sign', 'parent': 'o1'},
  ],
});

Map<String, Object?> _step(String op, [Map<String, Object?> more = const {}]) =>
    {'op': op, ...more};

List<List<Object?>> _instanceScenarios() {
  final twoLamps = _sceneText([_stub('lamp1', _lamp), _stub('lamp2', _lamp)]);
  final both = {_lamp: _lampText(), _street: _streetText()};
  final lamps = {_lamp: _lampText()};
  final tweaked = [
    _step('expand'),
    _step('move', {'id': 'lamp1', 'x': 7.0, 'y': 0.0}),
    _step('move', {'id': 'lamp1/bulb', 'x': 0.0, 'y': 3.0}),
    _step('add', {'id': 'shade', 'parent': 'lamp1/bulb'}),
    _step('rename', {'id': 'lamp2/pole', 'name': 'Tall pole'}),
  ];
  return [
    [
      twoLamps,
      lamps,
      [_step('expand')],
    ],
    [
      twoLamps,
      lamps,
      [_step('expand'), _step('fold')],
    ],
    [
      twoLamps,
      lamps,
      [...tweaked, _step('fold'), _step('expand')],
    ],
    [
      twoLamps,
      lamps,
      [
        _step('expand'),
        _step('remove', {'id': 'lamp2/pole'}),
        _step('remove', {'id': 'lamp1/shade'}),
        _step('fold'),
      ],
    ],
    [
      twoLamps,
      lamps,
      [
        ...tweaked,
        _step('refresh', {'asset': _lamp, 'prefab': _lampText(shade: true)}),
      ],
    ],
    [
      twoLamps,
      lamps,
      [
        ...tweaked,
        _step('fold'),
        _step('swap', {'asset': _lamp, 'prefab': _lampText(shade: true)}),
        _step('expand'),
      ],
    ],
    [
      twoLamps,
      lamps,
      [
        ...tweaked,
        _step('apply', {'id': 'lamp1', 'asset': _lamp}),
        _step('fold'),
      ],
    ],
    [
      twoLamps,
      lamps,
      [
        ...tweaked,
        _step('revert', {'id': 'lamp1'}),
      ],
    ],
    [
      twoLamps,
      lamps,
      [
        ...tweaked,
        _step('unpack', {'id': 'lamp1'}),
        _step('fold'),
      ],
    ],
    [
      twoLamps,
      lamps,
      [
        _step('expand'),
        _step('revert', {'id': 'lamp9'}),
        _step('unpack', {'id': 'lamp9'}),
      ],
    ],
    [
      _sceneText([_thing('crate'), _stub('lamp1', _lamp)]),
      lamps,
      [
        _step('expand'),
        _step('apply', {'id': 'crate', 'asset': _lamp}),
      ],
    ],
    [
      _sceneText([_stub('street1', _street)]),
      both,
      [_step('expand')],
    ],
    [
      _sceneText([_stub('street1', _street), _stub('street2', _street)]),
      both,
      [
        _step('expand'),
        _step('move', {'id': 'street1/lamp3/bulb', 'x': 0.0, 'y': 5.0}),
        _step('fold'),
        _step('expand'),
      ],
    ],
    [
      _sceneText([_stub('street1', _street), _stub('street2', _street)]),
      both,
      [
        _step('expand'),
        _step('move', {'id': 'street1/lamp3/bulb', 'x': 0.0, 'y': 5.0}),
        _step('apply', {'id': 'street1/lamp3', 'asset': _lamp}),
      ],
    ],
    [
      _sceneText([_stub('street1', _street)]),
      both,
      [
        _step('expand'),
        _step('unpack', {'id': 'street1/lamp3'}),
      ],
    ],
    [
      _sceneText([_stub('street1', _street)]),
      both,
      [
        _step('expand'),
        _step('unpack', {'id': 'street1'}),
        _step('fold'),
      ],
    ],
    [
      _sceneText([_stub('street1', _street)]),
      both,
      [
        _step('expand'),
        _step('swap', {'asset': _lamp, 'prefab': _lampText(shade: true)}),
        _step('refresh', {'asset': _lamp, 'prefab': _lampText(shade: true)}),
      ],
    ],
    [
      _sceneText([_stub('lamp1', 'props/none.oprefab')]),
      lamps,
      [_step('expand'), _step('fold')],
    ],
    [
      _sceneText([_stub('lamp1', _lamp)]),
      {_lamp: _lampText(), 'props/loop.oprefab': _loopText()},
      [
        _step('expand'),
        _step('add', {'id': 'again', 'parent': 'lamp1'}),
        _step('fold'),
      ],
    ],
    [
      _sceneText([_stub('a', 'props/loop.oprefab')]),
      {'props/loop.oprefab': _loopText()},
      [_step('expand')],
    ],
    [
      _legacyText(),
      lamps,
      [_step('expand')],
    ],
    [
      _legacyText(),
      lamps,
      [_step('expand'), _step('fold')],
    ],
    [
      _legacyText(),
      <String, String>{},
      [_step('expand')],
    ],
    [
      _sceneText([
        _thing('post', x: 5),
        _thing('light', parent: 'post', y: 3),
        _thing('other'),
      ]),
      <String, String>{},
      [
        _step('make', {'id': 'post', 'asset': 'props/post.oprefab'}),
        _step('fold'),
        _step('expand'),
      ],
    ],
    [
      _sceneText([_thing('post', x: 5), _thing('light', parent: 'post', y: 3)]),
      <String, String>{},
      [
        _step('make', {
          'id': 'post',
          'asset': 'props/post.oprefab',
          'name': 'Post',
        }),
        _step('move', {'id': 'post/light', 'x': 0.0, 'y': 4.0}),
        _step('make', {'id': 'post/light', 'asset': 'props/other.oprefab'}),
      ],
    ],
    [
      _sceneText([_stub('lamp1', _lamp), _thing('holder')]),
      lamps,
      [
        _step('expand'),
        _step('move', {'id': 'lamp1/bulb', 'x': 0.0, 'y': 9.0}),
        _step('make', {'id': 'lamp1', 'asset': 'props/lamp2.oprefab'}),
        _step('fold'),
      ],
    ],
    [
      _sceneText([
        _thing('holder'),
        _stub('inner', 'props/loop.oprefab', parent: 'holder'),
      ]),
      {'props/loop.oprefab': _loopText()},
      [
        _step('make', {'id': 'holder', 'asset': 'props/loop.oprefab'}),
      ],
    ],
  ];
}

SceneDocument _moved(SceneDocument document, String id, double x, double y) =>
    document.withEntity(
      id,
      document[id]!.withComponent(
        SceneComponents.transform,
        TransformComponent(position: Vector3(x, y, 0)),
      ),
    );

double _number(Object? value) => (value! as num).toDouble();

Map<String, Object?> _instanceResult(List<Object?> scenario) {
  final prefabTexts = (scenario[1]! as Map<Object?, Object?>)
      .cast<String, String>();
  final steps = (scenario[2]! as List<Object?>).cast<Map<String, Object?>>();
  return attempt(() {
    final prefabs = {
      for (final e in prefabTexts.entries)
        e.key: PrefabDocument.decode(e.value).prefab,
    };
    PrefabDocument? source(String asset) => prefabs[asset];
    var document = SceneDocument.decode(scenario[0]! as String).document;
    final problems = <String>[];
    final renamed = <String, String>{};
    var next = 0;

    for (final step in steps) {
      final id = step['id'] as String?;
      final asset = step['asset'] as String?;
      switch (step['op']) {
        case 'expand':
          final load = expandInstances(document, source);
          document = load.document;
          problems.addAll(load.problems);
        case 'fold':
          document = foldInstances(document, source);
        case 'move':
          document = _moved(
            document,
            id!,
            _number(step['x']),
            _number(step['y']),
          );
        case 'rename':
          document = document.withEntity(
            id!,
            document[id]!.copyWith(name: step['name'] as String),
          );
        case 'add':
          document = document.withEntity(
            id!,
            _thing(id, parent: step['parent'] as String?),
          );
        case 'remove':
          document = document.withEntity(id!, null);
        case 'apply':
          final edit = applyInstance(document, id!, source: source);
          document = edit.document;
          prefabs[asset!] = edit.prefab;
          renamed.addAll(edit.renamed);
          problems.addAll(edit.problems);
        case 'revert':
          final load = revertInstance(document, id!, source);
          document = load.document;
          problems.addAll(load.problems);
        case 'unpack':
          final result = unpackInstance(
            document,
            id!,
            source: source,
            fresh: () => 'n${++next}',
          );
          document = result.document;
          renamed.addAll(result.renamed);
        case 'make':
          final edit = makePrefab(
            document,
            id!,
            asset: asset!,
            source: source,
            name: step['name'] as String?,
          );
          document = edit.document;
          prefabs[asset] = edit.prefab;
          renamed.addAll(edit.renamed);
          problems.addAll(edit.problems);
        case 'swap':
          prefabs[asset!] = PrefabDocument.decode(
            step['prefab']! as String,
          ).prefab;
        case 'refresh':
          final before = Map.of(prefabs);
          prefabs[asset!] = PrefabDocument.decode(
            step['prefab']! as String,
          ).prefab;
          final load = refreshInstances(
            document,
            asset,
            before: (one) => before[one],
            after: source,
          );
          document = load.document;
          problems.addAll(load.problems);
        default:
          throw StateError('no step ${step['op']}');
      }
    }

    return {
      'text': document.encode(),
      'problems': problems,
      'renamed': renamed,
      'prefabs': {
        for (final key in prefabs.keys.toList()..sort())
          key: prefabs[key]!.encode(),
      },
    };
  });
}
