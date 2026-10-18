import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';

const params = [
  {'name': 'speed', 'type': 'float', 'instance': true, 'default': 1.5},
  {
    'name': 'tint',
    'type': 'vec4',
    'instance': true,
    'hints': {'source_color': <String>[]},
    'default': [1, 0.5, 0.25, 1],
  },
  {'name': 'glow', 'type': 'bool', 'instance': false},
  {
    'name': 'noise',
    'type': 'sampler2D',
    'instance': false,
    'hints': {'filter_nearest': <String>[]},
  },
];

final cooked = CookedSurface.fromParams('cook/pulse.filamat', params);

SceneEntity wearing(String id, [Map<String, Object> values = const {}]) =>
    SceneEntity(
      id: id,
      name: id,
      components: {
        SceneComponents.mesh: const MeshComponent(),
        SceneComponents.material: MaterialComponent(
          asset: 'shaders/pulse.oshader',
          values: values,
        ),
      },
    );

OrblitDocumentView viewOf(List<SceneEntity> entities) =>
    OrblitDocumentView(SceneDocument(entities: entities), projectRoot: '/p')
      ..surfaces = {'shaders/pulse.oshader': cooked};

OrblitSurface surfaceOf(OrblitDocumentView view, int? key) =>
    view.scene.materials.singleWhere((m) => m.key == key).surface!;

void main() {
  group('reading what the cook declared', () {
    test('widths, defaults and pictures come from the list', () {
      expect(cooked.widths, {'speed': 1, 'tint': 4, 'glow': 1});
      expect(cooked.defaults, {
        'speed': [1.5],
        'tint': [1.0, 0.5, 0.25, 1.0],
      });
      expect(cooked.pictures, {'noise': false});
    });

    test('a picture that holds a colour is read as sRGB', () {
      final surface = CookedSurface.fromParams('p', [
        {
          'name': 'albedo',
          'type': 'sampler2D',
          'hints': {'source_color': <String>[]},
        },
      ]);
      expect(surface.pictures, {'albedo': true});
    });

    test('an entry it cannot read is left out', () {
      final surface = CookedSurface.fromParams('p', [
        {'name': 'a', 'type': 'mat4'},
        {'type': 'float'},
        'nonsense',
        {
          'name': 'b',
          'type': 'vec2',
          'default': [1, 2, 3],
        },
      ]);
      expect(surface.widths, {'b': 2});
      expect(surface.defaults, isEmpty);
      expect(CookedSurface.fromParams('p', 'nonsense').widths, isEmpty);
    });
  });

  group('wearing a shader', () {
    test('objects that state nothing share one material with the defaults', () {
      final view = viewOf([wearing('a'), wearing('b')]);
      final keys = view.scene.objects.map((o) => o.material).toSet();

      expect(keys, hasLength(1));
      expect(view.scene.materials, hasLength(1));
      final surface = surfaceOf(view, keys.single);
      expect(surface.package, '/p/cook/pulse.filamat');
      expect(surface.values['speed'], [1.5]);
      expect(surface.values['tint'], [1.0, 0.5, 0.25, 1.0]);
    });

    test('two objects differ by what each states', () {
      final view = viewOf([
        wearing('a', {
          'tint': [0, 1, 0, 1],
        }),
        wearing('b', {'speed': 4}),
      ]);
      final keys = view.scene.objects.map((o) => o.material).toList();

      expect(keys.toSet(), hasLength(2));
      final first = surfaceOf(view, keys[0]);
      final second = surfaceOf(view, keys[1]);
      expect(first.values['tint'], [0, 1, 0, 1]);
      expect(first.values['speed'], [1.5]);
      expect(second.values['tint'], [1.0, 0.5, 0.25, 1.0]);
      expect(second.values['speed'], [4]);
    });

    test(
      'a value for a name nothing declares, or the wrong width, is dropped',
      () {
        final view = viewOf([
          wearing('a', {
            'speed': [1, 2],
            'missing': 3,
          }),
        ]);
        final surface = surfaceOf(view, view.scene.objects.single.material);
        expect(surface.values.keys, unorderedEquals(['speed', 'tint']));
        expect(surface.values['speed'], [1.5]);
      },
    );

    test('a flag and a picture are sent as what the shader declares', () {
      final view = viewOf([
        wearing('a', {'glow': true, 'noise': 'art/noise.png'}),
      ]);
      final surface = surfaceOf(view, view.scene.objects.single.material);
      expect(surface.values['glow'], [1.0]);
      expect(surface.textures['noise']!.path, '/p/art/noise.png');
      expect(surface.textures['noise']!.srgb, isFalse);
    });

    test('an object keeps its key while a value changes', () {
      final view = viewOf([
        wearing('a', {'speed': 2}),
      ]);
      final before = view.scene.objects.single.material;

      view.replace(
        SceneDocument(
          entities: [
            wearing('a', {'speed': 3}),
          ],
        ),
      );

      expect(view.scene.objects.single.material, before);
      expect(surfaceOf(view, before).values['speed'], [3]);
      expect(view.scene.materials, hasLength(1));
    });

    test('a shader nobody has cooked leaves the object as its mesh was', () {
      final view = OrblitDocumentView(
        SceneDocument(entities: [wearing('a')]),
        projectRoot: '/p',
      );
      expect(view.scene.objects.single.material, isNull);
      expect(view.scene.materials, isEmpty);
    });

    test('cooking it later puts the surface on the object', () {
      final view = OrblitDocumentView(
        SceneDocument(entities: [wearing('a')]),
        projectRoot: '/p',
      )..surfaces = {'shaders/pulse.oshader': cooked};
      expect(view.scene.objects.single.material, isNotNull);
    });
  });
}
