import 'dart:convert';

import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

const String lampAsset = 'props/lamp.oprefab';
const String streetAsset = 'props/street.oprefab';

SceneEntity thing(
  String id, {
  String? name,
  String? parent,
  double x = 0,
  double y = 0,
  Map<String, SceneComponent> components = const {},
}) => SceneEntity(
  id: id,
  name: name ?? id,
  parent: parent,
  components: {
    SceneComponents.transform: TransformComponent(position: Vector3(x, y, 0)),
    ...components,
  },
);

SceneEntity stub(
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

/// A lamp post: a pole with a bulb on top.
PrefabDocument lamp({bool shade = false}) => PrefabDocument(
  name: 'Lamp',
  root: 'lamp',
  document: SceneDocument(
    name: 'Lamp',
    entities: [
      thing('lamp', name: 'Lamp'),
      thing(
        'pole',
        name: 'Pole',
        parent: 'lamp',
        components: {
          SceneComponents.mesh: const MeshComponent(asset: 'models/pole.glb'),
        },
      ),
      thing('bulb', name: 'Bulb', parent: 'pole', y: 2),
      if (shade) thing('shade', name: 'Shade', parent: 'bulb'),
    ],
  ),
);

/// A street with a lamp on it, for prefabs inside prefabs.
PrefabDocument street() => PrefabDocument(
  name: 'Street',
  root: 'street',
  document: SceneDocument(
    name: 'Street',
    entities: [
      thing('street', name: 'Street'),
      stub('lamp3', lampAsset, parent: 'street'),
    ],
  ),
);

PrefabSource library(Map<String, PrefabDocument> prefabs) =>
    (asset) => prefabs[asset];

SceneDocument scene(List<SceneEntity> entities) =>
    SceneDocument(entities: entities);

SceneDocument opened(SceneDocument document, PrefabSource source) {
  final load = expandInstances(document, source);
  expect(load.problems, isEmpty);
  return load.document;
}

SceneDocument movedTo(SceneDocument document, String id, double x, double y) =>
    document.withEntity(
      id,
      document[id]!.withComponent(
        SceneComponents.transform,
        TransformComponent(position: Vector3(x, y, 0)),
      ),
    );

Vector3 positionOf(SceneDocument document, String id) =>
    (document[id]![SceneComponents.transform]! as TransformComponent).position;

PrefabComponent linkOf(SceneDocument document, String id) =>
    document[id]![SceneComponents.prefab]! as PrefabComponent;

void main() {
  group('a path names something inside an instance', () {
    test('one id for each document crossed', () {
      expect(EntityPath.join('street1', 'lamp3'), 'street1/lamp3');
      expect(EntityPath.segments('street1/lamp3/bulb'), [
        'street1',
        'lamp3',
        'bulb',
      ]);
      expect(EntityPath.instanceOf('street1/lamp3/bulb'), 'street1');
      expect(EntityPath.instanceOf('crate'), isNull);
    });

    test('the root of an instance has one name, the instance\'s', () {
      expect(EntityPath.localTo('lamp1', 'lamp1', root: 'lamp'), 'lamp');
      expect(EntityPath.localTo('lamp1', 'lamp1/bulb', root: 'lamp'), 'bulb');
      expect(EntityPath.localTo('lamp1', 'lamp10/bulb', root: 'lamp'), isNull);
      expect(EntityPath.inside('lamp1', 'lamp', root: 'lamp'), 'lamp1');
      expect(EntityPath.inside('lamp1', 'bulb', root: 'lamp'), 'lamp1/bulb');
    });

    test('what it is inside, nearest first', () {
      expect(EntityPath.enclosing('street1/lamp3/bulb'), [
        'street1/lamp3',
        'street1',
      ]);
      expect(EntityPath.enclosing('crate'), isEmpty);
      expect(EntityPath.within('lamp1', 'lamp1'), isTrue);
      expect(EntityPath.within('lamp1', 'lamp1/bulb'), isTrue);
      expect(EntityPath.within('lamp1', 'lamp10'), isFalse);
    });
  });

  group('a prefab file', () {
    test('reads back what it wrote', () {
      final prefab = lamp();
      final load = PrefabDocument.decode(prefab.encode());
      expect(load.problems, isEmpty);
      expect(load.prefab.root, 'lamp');
      expect(load.prefab.name, 'Lamp');
      expect(load.prefab.encode(), prefab.encode());
    });

    test('one from before instances were links still opens', () {
      final load = PrefabDocument.decode(
        jsonEncode({
          'kind': PrefabDocument.marker,
          'formatVersion': 1,
          'sceneFormatVersion': 4,
          'name': 'Crate',
          'root': 'c1',
          'objects': [
            {
              'id': 'c1',
              'name': 'Crate',
              'components': {
                'transform': {
                  'position': [0.0, 0.0, 0.0],
                },
              },
            },
            {'id': 'c2', 'name': 'Lid', 'parent': 'c1'},
          ],
        }),
      );
      expect(load.prefab.rootEntity.name, 'Crate');
      expect(load.prefab.document.childrenOf('c1').single.id, 'c2');
    });

    test('something that is not one is refused', () {
      expect(
        () => PrefabDocument.decode('{"kind": "orblit.scene"}'),
        throwsA(isA<SceneFormatException>()),
      );
      expect(
        () => PrefabDocument.decode('not json'),
        throwsA(isA<SceneFormatException>()),
      );
    });

    test('an instance on its root is a link and its overrides', () {
      final link = PrefabComponent(
        asset: lampAsset,
        overrides: SceneDiff([SetVisible('bulb', from: true, to: false)]),
      );
      final back = SceneComponents.read(
        SceneComponents.prefab,
        jsonDecode(jsonEncode(link.toJson())) as Map<String, Object?>,
      );
      expect(jsonEncode(back.toJson()), jsonEncode(link.toJson()));
      expect(link.toJson().containsKey('state'), isFalse);
    });
  });

  group('an instance is a link and a diff', () {
    final source = library({lampAsset: lamp(), streetAsset: street()});

    test('opening puts every part in, under a path into the instance', () {
      final document = opened(scene([stub('lamp1', lampAsset)]), source);

      expect(document.entities.map((e) => e.id), [
        'lamp1',
        'lamp1/pole',
        'lamp1/bulb',
      ]);
      expect(document['lamp1/pole']!.parent, 'lamp1');
      expect(document['lamp1/bulb']!.parent, 'lamp1/pole');
      expect(document['lamp1']!.name, 'lamp1');
      expect(linkOf(document, 'lamp1').state, PrefabState.open);
      expect(document['lamp1']!.has(SceneComponents.transform), isTrue);
    });

    test('an untouched instance folds back to exactly the link', () {
      final saved = scene([stub('lamp1', lampAsset)]);
      final folded = foldInstances(opened(saved, source), source);
      expect(folded.encode(), saved.encode());
    });

    test(
      'placed twice and edited in one place, the edit is in that one only',
      () {
        var document = opened(
          scene([stub('lamp1', lampAsset), stub('lamp2', lampAsset)]),
          source,
        );
        document = movedTo(document, 'lamp1/bulb', 0, 3);

        final saved = foldInstances(document, source);
        final again = opened(
          SceneDocument.decode(saved.encode()).document,
          source,
        );

        expect(positionOf(again, 'lamp1/bulb').y, 3);
        expect(positionOf(again, 'lamp2/bulb').y, 2);
        expect(linkOf(saved, 'lamp2').overrides.isEmpty, isTrue);
        final overrides = linkOf(saved, 'lamp1').overrides.operations;
        expect(overrides, hasLength(1));
        expect((overrides.single as SetField).id, 'bulb');
      },
    );

    test('the prefab\'s own change shows in both, keeping each one\'s', () {
      var document = opened(
        scene([stub('lamp1', lampAsset), stub('lamp2', lampAsset)]),
        source,
      );
      document = movedTo(document, 'lamp1/bulb', 0, 3);
      final saved = foldInstances(document, source);

      final changed = library({lampAsset: lamp(shade: true)});
      final again = opened(saved, changed);

      expect(again['lamp1/shade']!.parent, 'lamp1/bulb');
      expect(again['lamp2/shade']!.parent, 'lamp2/bulb');
      expect(positionOf(again, 'lamp1/bulb').y, 3);
    });

    test('the file holds the diff, not the parts', () {
      var document = opened(
        scene([stub('lamp1', lampAsset), stub('lamp2', lampAsset)]),
        source,
      );
      document = movedTo(document, 'lamp1', 5, 0);
      final text = foldInstances(document, source).encode();

      expect(text, isNot(contains('lamp1/')));
      expect(text, isNot(contains('"Pole"')));
      final entities = (jsonDecode(text) as Map)['entities'] as List;
      expect(entities, hasLength(2));
    });

    test('where it stands is an override on the prefab\'s root', () {
      var document = opened(scene([stub('lamp1', lampAsset)]), source);
      document = movedTo(document, 'lamp1', 5, 0);
      final saved = foldInstances(document, source);

      final op = linkOf(saved, 'lamp1').overrides.operations.single;
      expect((op as SetField).id, 'lamp');
      expect(positionOf(opened(saved, source), 'lamp1').x, 5);
    });

    test('a removed part stays removed, and an added one stays added', () {
      var document = opened(scene([stub('lamp1', lampAsset)]), source);
      document = document
          .withEntity('lamp1/bulb', null)
          .withEntity('lamp1/sign', thing('lamp1/sign', parent: 'lamp1/pole'));

      final again = opened(foldInstances(document, source), source);
      expect(again.contains('lamp1/bulb'), isFalse);
      expect(again['lamp1/sign']!.parent, 'lamp1/pole');
    });

    test('something hung on a part belongs to the scene, by path', () {
      var document = opened(scene([stub('lamp1', lampAsset)]), source);
      document = document.withEntity(
        'flag',
        thing('flag', parent: 'lamp1/bulb'),
      );

      final saved = foldInstances(document, source);
      expect(saved['flag']!.parent, 'lamp1/bulb');
      expect(linkOf(saved, 'lamp1').overrides.isEmpty, isTrue);

      final read = SceneDocument.decode(saved.encode());
      expect(read.problems, isEmpty);
      expect(read.document['flag']!.parent, 'lamp1/bulb');
      expect(opened(read.document, source)['flag']!.parent, 'lamp1/bulb');
    });

    test('hung on a part the prefab no longer has, it moves to the '
        'nearest thing still there', () {
      final document = scene([
        stub('lamp1', lampAsset),
        thing('flag', parent: 'lamp1/bulb'),
      ]);
      final load = expandInstances(
        document,
        library({
          lampAsset: PrefabDocument(
            name: 'Lamp',
            root: 'lamp',
            document: SceneDocument(entities: [thing('lamp')]),
          ),
        }),
      );
      expect(load.document['flag']!.parent, 'lamp1');
      expect(load.problems, isNotEmpty);
    });
  });

  group('a prefab inside a prefab', () {
    final source = library({lampAsset: lamp(), streetAsset: street()});

    test('opens all the way down, one id per document crossed', () {
      final document = opened(scene([stub('street1', streetAsset)]), source);
      expect(document['street1/lamp3']!.parent, 'street1');
      expect(document['street1/lamp3/bulb']!.parent, 'street1/lamp3/pole');
      expect(linkOf(document, 'street1/lamp3').state, PrefabState.open);
    });

    test('an edit deep inside is kept in the outer instance\'s terms', () {
      var document = opened(scene([stub('street1', streetAsset)]), source);
      document = movedTo(document, 'street1/lamp3/bulb', 0, 4);

      final saved = foldInstances(document, source);
      expect(saved.entities, hasLength(1));
      final op = linkOf(saved, 'street1').overrides.operations.single;
      expect((op as SetField).id, 'lamp3/bulb');
      expect(positionOf(opened(saved, source), 'street1/lamp3/bulb').y, 4);
    });

    test('a change to the inner prefab reaches every street', () {
      final saved = scene([stub('street1', streetAsset)]);
      final again = opened(
        saved,
        library({lampAsset: lamp(shade: true), streetAsset: street()}),
      );
      expect(again['street1/lamp3/shade']!.parent, 'street1/lamp3/bulb');
    });

    test('one that contains itself stays closed instead of looping', () {
      final loop = PrefabDocument(
        name: 'Loop',
        root: 'loop',
        document: SceneDocument(
          entities: [
            thing('loop'),
            stub('inner', 'loop.oprefab', parent: 'loop'),
          ],
        ),
      );
      final load = expandInstances(
        scene([stub('x', 'loop.oprefab')]),
        library({'loop.oprefab': loop}),
      );
      expect(load.document['x/inner'], isNotNull);
      expect(linkOf(load.document, 'x/inner').state, PrefabState.folded);
      expect(load.problems.single, contains('itself'));
    });
  });

  group('a prefab that cannot be read', () {
    test('leaves the link as it was, overrides and all', () {
      final overrides = SceneDiff([SetVisible('bulb', from: true, to: false)]);
      final document = scene([
        stub('lamp1', lampAsset, overrides: overrides),
        thing('flag', parent: 'lamp1/bulb'),
      ]);
      final load = expandInstances(document, library({}));

      expect(load.problems.single, contains(lampAsset));
      expect(load.document.encode(), document.encode());
      expect(
        foldInstances(load.document, library({})).encode(),
        document.encode(),
      );
    });

    test('a change to a part it no longer has is let go, and said', () {
      final load = expandInstances(
        scene([
          stub(
            'lamp1',
            lampAsset,
            overrides: SceneDiff([SetVisible('gone', from: true, to: false)]),
          ),
        ]),
        library({lampAsset: lamp()}),
      );
      expect(load.document.contains('lamp1/pole'), isTrue);
      expect(load.problems.single, contains('no longer has'));
    });
  });

  group('a copy from before instances were links', () {
    Map<String, Object?> copied(
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
        'prefab': {'asset': lampAsset},
      },
    };

    SceneDocument legacy() => SceneDocument.decode(
      jsonEncode({
        'formatVersion': 4,
        'entities': [
          copied('o1', 'Lamp'),
          copied('o2', 'Pole', parent: 'o1'),
          // Somebody raised this one's bulb.
          copied('o3', 'Bulb', parent: 'o2', y: 5),
          // And hung a sign on it.
          {'id': 'o4', 'name': 'Sign', 'parent': 'o1'},
        ],
      }),
    ).document;

    test('is relinked, keeping what was changed about it', () {
      final load = expandInstances(legacy(), library({lampAsset: lamp()}));
      final document = load.document;

      expect(linkOf(document, 'o1').state, PrefabState.open);
      expect(document['o1/pole']!.parent, 'o1');
      expect(document['o1/bulb']!.parent, 'o1/pole');
      expect(document['o1/pole']!.has(SceneComponents.prefab), isFalse);
      expect(positionOf(document, 'o1/bulb').y, 5);
      expect(document['o4']!.parent, 'o1');
      expect(load.problems.single, contains('now linked'));
    });

    test('and saves as a link with only that change in it', () {
      final source = library({lampAsset: lamp()});
      final saved = foldInstances(
        expandInstances(legacy(), source).document,
        source,
      );

      expect(saved.entities.map((e) => e.id), ['o1', 'o4']);
      final op = linkOf(saved, 'o1').overrides.operations.single;
      expect((op as SetField).id, 'bulb');
    });

    test('stays a copy while its prefab cannot be read', () {
      final document = legacy();
      final load = expandInstances(document, library({}));
      expect(load.document.encode(), document.encode());
      expect(linkOf(load.document, 'o2').state, PrefabState.stamped);
    });
  });

  group('making a prefab', () {
    SceneDocument post() => scene([
      thing(
        'post',
        name: 'Post',
        x: 5,
        components: {
          SceneComponents.mesh: const MeshComponent(asset: 'models/pole.glb'),
        },
      ),
      thing('light', name: 'Light', parent: 'post', y: 3),
    ]);

    test('makes a thing, not a thing at a place, and links it', () {
      final made = makePrefab(
        post(),
        'post',
        asset: 'props/post.oprefab',
        source: library({}),
      );

      expect(made.prefab.root, 'post');
      expect(positionOf(made.prefab.document, 'post').x, 0);
      expect(made.renamed, {'light': 'post/light'});

      final document = made.document;
      expect(linkOf(document, 'post').state, PrefabState.open);
      expect(document['post/light']!.parent, 'post');
      expect(positionOf(document, 'post').x, 5);
      expect(made.problems, isEmpty);

      final saved = foldInstances(
        document,
        library({'props/post.oprefab': made.prefab}),
      );
      expect(saved.entities, hasLength(1));
      expect(
        (linkOf(saved, 'post').overrides.operations.single as SetField).field,
        'position',
      );
    });

    test('an instance inside stays an instance, written folded', () {
      final source = library({lampAsset: lamp()});
      final document = opened(
        scene([thing('corner'), stub('lamp1', lampAsset, parent: 'corner')]),
        source,
      );
      final made = makePrefab(
        document,
        'corner',
        asset: 'props/corner.oprefab',
        source: source,
      );

      expect(made.prefab.document.entities.map((e) => e.id), [
        'corner',
        'lamp1',
      ]);
      expect(made.document['corner/lamp1/bulb'], isNotNull);
      expect(made.renamed['lamp1/bulb'], 'corner/lamp1/bulb');
    });

    test('is refused for something inside an instance, or that would '
        'contain itself', () {
      final source = library({lampAsset: lamp()});
      final document = opened(
        scene([thing('corner'), stub('lamp1', lampAsset, parent: 'corner')]),
        source,
      );
      expect(
        () => makePrefab(
          document,
          'lamp1/pole',
          asset: 'props/pole.oprefab',
          source: source,
        ),
        throwsA(isA<PrefabException>()),
      );
      expect(
        () => makePrefab(document, 'corner', asset: lampAsset, source: source),
        throwsA(isA<PrefabException>()),
      );
    });
  });

  group('applying, reverting and unpacking', () {
    final source = library({lampAsset: lamp(), streetAsset: street()});

    SceneDocument twoLamps() {
      var document = opened(
        scene([stub('lamp1', lampAsset), stub('lamp2', lampAsset)]),
        source,
      );
      document = movedTo(document, 'lamp1', 7, 0);
      document = movedTo(document, 'lamp1/bulb', 0, 3);
      document = document.withEntity(
        'shade',
        thing('shade', name: 'Shade', parent: 'lamp1/bulb'),
      );
      return document.withEntity(
        'lamp2/pole',
        document['lamp2/pole']!.copyWith(name: 'Tall pole'),
      );
    }

    test('apply writes this instance into the prefab, and every other '
        'instance keeps its own changes', () {
      final applied = applyInstance(twoLamps(), 'lamp1', source: source);
      final prefab = applied.prefab;

      expect(prefab.root, 'lamp');
      expect(prefab.document['shade']!.parent, 'bulb');
      expect(positionOf(prefab.document, 'bulb').y, 3);
      expect(positionOf(prefab.document, 'lamp').x, 0);
      expect(applied.renamed, {'shade': 'lamp1/shade'});

      final document = applied.document;
      expect(document.contains('shade'), isFalse);
      expect(document['lamp1/shade']!.parent, 'lamp1/bulb');
      expect(positionOf(document, 'lamp1').x, 7);
      expect(document['lamp2/shade']!.parent, 'lamp2/bulb');
      expect(positionOf(document, 'lamp2/bulb').y, 3);
      expect(document['lamp2/pole']!.name, 'Tall pole');

      final after = library({lampAsset: prefab});
      final saved = foldInstances(document, after);
      final ops = linkOf(saved, 'lamp1').overrides.operations;
      expect(ops.single, isA<SetField>());
      expect((ops.single as SetField).id, 'lamp');
    });

    test('apply reaches the lamps inside every street', () {
      var document = opened(
        scene([stub('lamp1', lampAsset), stub('street1', streetAsset)]),
        source,
      );
      document = movedTo(document, 'lamp1/bulb', 0, 6);
      final applied = applyInstance(document, 'lamp1', source: source);
      expect(positionOf(applied.document, 'street1/lamp3/bulb').y, 6);
    });

    test('revert drops the changes and keeps where it stands', () {
      final document = twoLamps().withEntity(
        'flag',
        thing('flag', parent: 'lamp1/pole'),
      );
      final load = revertInstance(document, 'lamp1', source);
      final reverted = load.document;

      expect(positionOf(reverted, 'lamp1/bulb').y, 2);
      expect(positionOf(reverted, 'lamp1').x, 7);
      expect(reverted['flag']!.parent, 'lamp1/pole');
      expect(reverted['lamp2/pole']!.name, 'Tall pole');
      // A thing hung on the bulb from outside is the scene's, and stays.
      expect(reverted['shade']!.parent, 'lamp1/bulb');
    });

    test('unpack gives the parts ids of their own', () {
      var next = 0;
      final result = unpackInstance(
        twoLamps(),
        'lamp1',
        source: source,
        fresh: () => 'n${++next}',
      );
      final document = result.document;

      expect(document['lamp1']!.has(SceneComponents.prefab), isFalse);
      expect(result.renamed, {'lamp1/pole': 'n1', 'lamp1/bulb': 'n2'});
      expect(document['n1']!.parent, 'lamp1');
      expect(document['n2']!.parent, 'n1');
      expect(document['shade']!.parent, 'n2');
      expect(positionOf(document, 'n2').y, 3);

      final saved = foldInstances(document, source);
      expect(saved.contains('n2'), isTrue);
    });

    test('unpacking a street leaves its lamps as instances', () {
      final document = opened(scene([stub('street1', streetAsset)]), source);
      final result = unpackInstance(
        document,
        'street1',
        source: source,
        fresh: () => 'n1',
      );

      expect(linkOf(result.document, 'n1').state, PrefabState.open);
      expect(result.document['n1/bulb']!.parent, 'n1/pole');
      final saved = foldInstances(result.document, source);
      expect(saved.entities.map((e) => e.id), ['street1', 'n1']);
    });

    test('unpacking one whose root is another prefab leaves it that one', () {
      final fancy = PrefabDocument(
        name: 'Fancy lamp',
        root: 'base',
        document: SceneDocument(
          entities: [
            stub('base', lampAsset),
            thing('sign', parent: 'base'),
          ],
        ),
      );
      final both = library({lampAsset: lamp(), 'fancy.oprefab': fancy});
      final document = opened(scene([stub('f1', 'fancy.oprefab')]), both);
      expect(document['f1/base/bulb'], isNotNull);
      expect(document['f1/sign']!.parent, 'f1');

      final result = unpackInstance(
        document,
        'f1',
        source: both,
        fresh: () => 'n1',
      );
      expect(linkOf(result.document, 'f1').asset, lampAsset);
      expect(result.document['f1/bulb']!.parent, 'f1/pole');
      expect(result.document['n1']!.parent, 'f1');

      final saved = foldInstances(result.document, both);
      expect(saved.entities.map((e) => e.id), ['f1', 'n1']);
    });

    test('unpacking against a prefab that has lost its root still unpacks', () {
      final document = twoLamps();
      final broken = PrefabDocument(
        name: 'Lamp',
        root: 'gone',
        document: lamp().document,
      );
      var next = 0;
      final result = unpackInstance(
        document,
        'lamp1',
        source: library({lampAsset: broken}),
        fresh: () => 'n${++next}',
      );
      expect(result.document['lamp1']!.has(SceneComponents.prefab), isFalse);
      expect(result.document['n2']!.parent, 'n1');
      expect(
        () => applyInstance(
          document,
          'lamp1',
          source: library({lampAsset: broken}),
        ),
        throwsA(isA<PrefabException>()),
      );
    });

    test('a part moved out of its instance folds back under its root', () {
      final document = twoLamps();
      final moved = document.withEntity(
        'lamp1/bulb',
        document['lamp1/bulb']!.copyWith(parent: 'lamp2/bulb'),
      );
      final saved = foldInstances(moved, source);
      expect(saved.contains('lamp1/bulb'), isFalse);
      final back = opened(saved, source);
      expect(back['lamp1/bulb']!.parent, 'lamp1');
    });
  });
}
