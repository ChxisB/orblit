import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';

MaterialDocument material(Map<String, Object?> json) =>
    MaterialDocument.fromJson(json).document;

/// Everything a material contributes to the message the renderer reads: its
/// flags and its floats. Two materials with the same one of these draw the
/// same, whatever else differs about them.
String drawnAs(OrblitMaterial material) {
  final floats = Float32List(OrblitMaterial.stride);
  material.pack(floats, 0);
  return '${material.flags}:${floats.join(',')}';
}

/// Two values for [field] that a material could hold, which are not each
/// other.
///
/// Two rather than one measured against the renderer's default, because some
/// parameters default to one end of their own range — a flag that defaults to
/// false cannot be shown to work by setting it to false.
(Object, Object) probes(MaterialField field) => switch (field.kind) {
  MaterialKind.number => (0.317, 0.591),
  MaterialKind.flag => (false, true),
  MaterialKind.rgba => (
    const [0.11, 0.22, 0.33, 0.44],
    const [0.55, 0.66, 0.77, 0.88],
  ),
  MaterialKind.rgb => (const [0.11, 0.22, 0.33], const [0.55, 0.66, 0.77]),
  MaterialKind.pair => (const [0.11, 0.22], const [0.55, 0.66]),
  MaterialKind.choice => (field.choices.first, field.choices.last),
};

void main() {
  group('every parameter reaches the renderer', () {
    // Wind is folded to nought unless the surface actually moves, so the base
    // every probe is measured against is a moving one — otherwise bearing and
    // strength would look like parameters that do nothing.
    final base = <String, Object?>{'windSpeed': 3.0, 'depthWrite': true};

    for (final field in MaterialFields.all) {
      test(field.name, () {
        final (one, other) = probes(field);
        final library = MaterialLibrary(
          materials: {
            'one.omat': material({
              'values': {...base, field.name: one},
            }),
            'other.omat': material({
              'values': {...base, field.name: other},
            }),
          },
        );

        expect(
          drawnAs(materialFrom(library.resolve('one.omat'), key: 1)),
          isNot(drawnAs(materialFrom(library.resolve('other.omat'), key: 1))),
          reason:
              '${field.name} is in MaterialFields but changes nothing the '
              'renderer draws with — materialFrom is not applying it.',
        );
      });
    }
  });

  group('maps', () {
    test('every slot in the table lands somewhere', () {
      for (final slot in MaterialFields.maps) {
        final library = MaterialLibrary(
          materials: {
            'a.omat': material({
              'maps': {slot: 'textures/$slot.png'},
            }),
          },
        );
        final built = materialFrom(library.resolve('a.omat'), key: 1);
        final paths = [
          built.baseColourMap,
          built.normalMap,
          built.metallicRoughnessMap,
          built.occlusionMap,
          built.emissiveMap,
          built.blendBaseColourMap,
          built.blendMaskMap,
        ].whereType<OrblitTexture>().map((texture) => texture.path);
        expect(paths, ['textures/$slot.png'], reason: slot);
      }
    });

    test('colours are decoded and measurements are not', () {
      final library = MaterialLibrary(
        materials: {
          'a.omat': material({
            'maps': {
              'baseColour': 'c.png',
              'emissive': 'e.png',
              'normal': 'n.png',
              'metallicRoughness': 'mr.png',
              'occlusion': 'o.png',
            },
          }),
        },
      );
      final built = materialFrom(library.resolve('a.omat'), key: 1);
      expect(built.baseColourMap!.srgb, isTrue);
      expect(built.emissiveMap!.srgb, isTrue);
      expect(built.normalMap!.srgb, isFalse);
      expect(built.metallicRoughnessMap!.srgb, isFalse);
      expect(built.occlusionMap!.srgb, isFalse);
    });

    test('a path nobody can place is left out rather than guessed at', () {
      final library = MaterialLibrary(
        materials: {
          'a.omat': material({
            'maps': {'baseColour': 'c.png', 'normal': 'n.png'},
          }),
        },
      );
      final built = materialFrom(
        library.resolve('a.omat'),
        key: 1,
        locate: (path) => path == 'c.png' ? '/assets/c.png' : null,
      );
      expect(built.baseColourMap!.path, '/assets/c.png');
      expect(built.normalMap, isNull);
    });
  });

  test('a material that says nothing draws as no material at all', () {
    final built = materialFrom(const ResolvedMaterial(), key: 9);
    expect(drawnAs(built), drawnAs(const OrblitMaterial(key: 9)));
  });

  test('a material that sets only a speed still complies fully', () {
    final library = MaterialLibrary(
      materials: {
        'a.omat': material({
          'values': {'windSpeed': 4},
        }),
      },
    );
    final built = materialFrom(library.resolve('a.omat'), key: 1);
    expect(built.wind.speed, 4.0);
    expect(built.wind.strength, 1.0);
  });
}
