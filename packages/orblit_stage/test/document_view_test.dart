import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_light/orblit_light.dart' show LightType, Tint;
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_weather/orblit_weather.dart'
    show WeatherCondition, WeatherState;
import 'package:vector_math/vector_math_64.dart';

SceneEntity thing(
  String id, {
  String? parent,
  bool visible = true,
  Map<String, SceneComponent> components = const {},
}) => SceneEntity(
  id: id,
  name: id,
  parent: parent,
  visible: visible,
  components: components,
);

SceneEntity crate(String id, {String? parent, Vector3? at}) => thing(
  id,
  parent: parent,
  components: {
    SceneComponents.transform: TransformComponent(position: at),
    SceneComponents.mesh: const MeshComponent(),
  },
);

/// What the two ways of getting there have to agree on.
///
/// A view moved by a diff and a view built from the finished document are the
/// same scene or the incremental path is a bug — and it is the kind of bug
/// that shows up as one object in the wrong place an hour into somebody's
/// session, with nothing to point at.
void expectSameAsRebuilt(OrblitDocumentView moved, SceneDocument wanted) {
  final built = OrblitDocumentView(wanted);

  String shape(OrblitDocumentView view) => [
    for (final object in view.scene.objects)
      'object ${object.transform.storage.join(",")} '
          '${object.mesh} ${object.visible} ${object.colour}',
    for (final light in view.scene.lights)
      'light ${light.kind} ${light.intensity} ${light.position}',
    for (final layer in view.scene.sprites)
      'sprites ${layer.transform.storage.join(",")} ${layer.blend}',
    'sky ${view.scene.sky.colour} ${view.scene.sky.ambient}',
    'fog ${view.scene.fog.density} ${view.scene.fog.colour}',
    'rain ${view.scene.precipitation.amount}',
    'camera ${view.scene.camera.position} ${view.scene.camera.target}',
  ].join('\n');

  expect(shape(moved), shape(built));
}

void main() {
  group('what a document becomes', () {
    test('a mesh entity is an object, and a light entity is a light', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            crate('crate'),
            thing(
              'sun',
              components: {
                SceneComponents.light: const LightComponent(power: 2),
              },
            ),
          ],
        ),
      );

      expect(view.scene.objects, hasLength(1));
      expect(view.scene.lights, hasLength(1));
      expect(view.scene.lights.single.kind, OrblitLightKind.directional);
    });

    test('a lamp that is both is both, which the old shape could not say', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            thing(
              'lamp',
              components: {
                SceneComponents.mesh: const MeshComponent(),
                SceneComponents.light: const LightComponent(
                  kind: LightType.point,
                ),
              },
            ),
          ],
        ),
      );

      expect(view.scene.objects, hasLength(1));
      expect(view.scene.lights, hasLength(1));
      // Two roles of one entity never share a key, whatever the renderer does
      // with the two lists.
      expect(
        view.scene.objects.single.key,
        isNot(view.scene.lights.single.key),
      );
    });

    test('a child is placed by its parent as well as by itself', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            thing(
              'group',
              components: {
                SceneComponents.transform: TransformComponent(
                  position: Vector3(10, 0, 0),
                ),
              },
            ),
            crate('crate', parent: 'group', at: Vector3(0, 0, 5)),
          ],
        ),
      );

      expect(
        view.scene.objects.single.transform.getTranslation(),
        Vector3(10, 0, 5),
      );
    });

    test('hiding a group hides what is inside it', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            thing('group', visible: false),
            crate('crate', parent: 'group'),
          ],
        ),
      );

      expect(view.scene.objects.single.visible, isFalse);
    });

    test('a hidden light is left out rather than sent dark', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            thing(
              'sun',
              visible: false,
              components: {SceneComponents.light: const LightComponent()},
            ),
          ],
        ),
      );

      expect(view.scene.lights, isEmpty);
    });

    test('the order the lists come out in is the document order', () {
      final view = OrblitDocumentView(
        SceneDocument(entities: [crate('a'), crate('b'), crate('c')]),
      );

      final keys = view.scene.objects.map((o) => o.key).toList();
      view.replace(
        SceneDocument(entities: [crate('c'), crate('a'), crate('b')]),
      );

      expect(view.scene.objects.map((o) => o.key), [keys[2], keys[0], keys[1]]);
    });

    test('weather becomes the air, and what is falling out of it', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            thing(
              'weather',
              components: {
                SceneComponents.weather: WeatherComponent(
                  condition: WeatherCondition.storm,
                  air: WeatherState.of(
                    WeatherCondition.storm,
                  ).copyWith(rain: 0.8),
                ),
              },
            ),
          ],
        ),
      );

      expect(view.scene.fog.density, greaterThan(0));
      expect(view.scene.precipitation.amount, 0.8);
    });

    test('a scene with no weather in it has clear air', () {
      final view = OrblitDocumentView(SceneDocument(entities: [crate('a')]));
      expect(view.scene.fog.density, 0);
      expect(view.scene.precipitation.amount, 0);
    });

    test('project paths are rooted, and absolute ones are left alone', () {
      final view = OrblitDocumentView(
        SceneDocument(
          entities: [
            thing(
              'a',
              components: {
                SceneComponents.mesh: const MeshComponent(
                  asset: 'models/a.glb',
                ),
              },
            ),
            thing(
              'b',
              components: {
                SceneComponents.mesh: const MeshComponent(asset: '/tmp/b.glb'),
              },
            ),
          ],
        ),
        projectRoot: '/projects/one',
      );

      expect(view.scene.objects.first.mesh, '/projects/one/models/a.glb');
      expect(view.scene.objects.last.mesh, '/tmp/b.glb');
    });
  });

  group('moving by a diff gets to the same place as rebuilding', () {
    test('a field changing', () {
      final before = SceneDocument(entities: [crate('crate')]);
      final after = SceneDocument(
        entities: [crate('crate', at: Vector3(0, 4, 0))],
      );

      final view = OrblitDocumentView(before)..replace(after);
      expect(view.scene.objects.single.transform.getTranslation().y, 4);
      expectSameAsRebuilt(view, after);
    });

    test('a parent moving takes its children with it', () {
      final before = SceneDocument(
        entities: [
          thing('group'),
          crate('crate', parent: 'group', at: Vector3(0, 0, 5)),
        ],
      );
      final after = SceneDocument(
        entities: [
          thing(
            'group',
            components: {
              SceneComponents.transform: TransformComponent(
                position: Vector3(10, 0, 0),
              ),
            },
          ),
          crate('crate', parent: 'group', at: Vector3(0, 0, 5)),
        ],
      );

      final view = OrblitDocumentView(before)..replace(after);
      expect(
        view.scene.objects.single.transform.getTranslation(),
        Vector3(10, 0, 5),
      );
      expectSameAsRebuilt(view, after);
    });

    test('reparenting', () {
      final before = SceneDocument(
        entities: [
          thing(
            'a',
            components: {
              SceneComponents.transform: TransformComponent(
                position: Vector3(3, 0, 0),
              ),
            },
          ),
          thing(
            'b',
            components: {
              SceneComponents.transform: TransformComponent(
                position: Vector3(0, 7, 0),
              ),
            },
          ),
          crate('crate', parent: 'a'),
        ],
      );
      final after = before.withEntity(
        'crate',
        before['crate']!.copyWith(parent: 'b'),
      );

      final view = OrblitDocumentView(before)..replace(after);
      expect(
        view.scene.objects.single.transform.getTranslation(),
        Vector3(0, 7, 0),
      );
      expectSameAsRebuilt(view, after);
    });

    test('adding and removing', () {
      final before = SceneDocument(entities: [crate('a'), crate('b')]);
      final after = SceneDocument(entities: [crate('a'), crate('c')]);

      final view = OrblitDocumentView(before)..replace(after);
      expect(view.scene.objects, hasLength(2));
      expectSameAsRebuilt(view, after);
    });

    test('a component gained and lost', () {
      final before = SceneDocument(entities: [crate('lamp')]);
      final after = SceneDocument(
        entities: [
          thing(
            'lamp',
            components: {
              SceneComponents.transform: TransformComponent(),
              SceneComponents.light: const LightComponent(power: 60),
            },
          ),
        ],
      );

      final view = OrblitDocumentView(before);
      view.replace(after);
      expect(view.scene.objects, isEmpty);
      expect(view.scene.lights, hasLength(1));
      expectSameAsRebuilt(view, after);

      view.replace(before);
      expect(view.scene.lights, isEmpty);
      expectSameAsRebuilt(view, before);
    });

    test('the scene\'s own settings', () {
      final before = SceneDocument(entities: [crate('a')]);
      final after = before.copyWith(
        settings: const SceneSettings(sky: Tint.hex(0x884422), ambient: 900),
      );

      final view = OrblitDocumentView(before)..replace(after);
      expect(view.scene.sky.ambient, 900);
      expectSameAsRebuilt(view, after);
    });

    test('keys survive an edit, so the renderer keeps what it built', () {
      final before = SceneDocument(entities: [crate('crate')]);
      final view = OrblitDocumentView(before);
      final key = view.scene.objects.single.key;

      view.replace(
        SceneDocument(entities: [crate('crate', at: Vector3(1, 1, 1))]),
      );
      expect(view.scene.objects.single.key, key);
    });

    test('an empty diff changes nothing at all', () {
      final document = SceneDocument(entities: [crate('a')]);
      final view = OrblitDocumentView(document);
      final before = view.scene.objects.single.key;

      view.apply(SceneDiff.none);
      expect(view.scene.objects.single.key, before);
    });
  });
}
