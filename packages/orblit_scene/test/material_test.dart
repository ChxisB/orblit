import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

MaterialDocument read(Map<String, Object?> json) =>
    MaterialDocument.fromJson(json).document;

List<String> problems(Map<String, Object?> json) =>
    MaterialDocument.fromJson(json).problems;

void main() {
  group('reading a material', () {
    test('keeps every parameter it understands, as the kind it is', () {
      final material = read({
        'values': {
          'metallic': 1,
          'doubleSided': true,
          'baseColour': [1, 0.5, 0, 1],
          'emissive': [0.1, 0.2, 0.3],
          'tiling': [2, 3],
          'blend': 'fade',
        },
      });

      expect(material.values['metallic'], 1.0);
      expect(material.values['doubleSided'], true);
      expect(material.values['baseColour'], [1.0, 0.5, 0.0, 1.0]);
      expect(material.values['emissive'], [0.1, 0.2, 0.3]);
      expect(material.values['tiling'], [2.0, 3.0]);
      expect(material.values['blend'], 'fade');
    });

    test('an integer and a double are the same number', () {
      expect(
        read({
          'values': {'roughness': 1},
        }).values['roughness'],
        1.0,
      );
      expect(
        read({
          'values': {'roughness': 1.0},
        }).values['roughness'],
        1.0,
      );
    });

    test('names a parameter it does not know rather than keeping it', () {
      final json = {
        'values': {'shinyness': 1},
      };
      expect(read(json).values, isEmpty);
      expect(problems(json).single, contains('shinyness'));
    });

    test('drops a value of the wrong shape and says what was wanted', () {
      final json = {
        'values': {
          'roughness': 'half',
          'baseColour': [1, 1, 1],
          'blend': 'shiny',
          'depthWrite': 1,
        },
      };
      expect(read(json).values, isEmpty);
      expect(problems(json), hasLength(4));
      expect(problems(json)[0], contains('a number'));
      expect(problems(json)[1], contains('four numbers'));
      expect(problems(json)[2], contains('opaque'));
      expect(problems(json)[3], contains('true or false'));
    });

    test('one bad parameter does not lose the good ones', () {
      final material = read({
        'values': {'roughness': 'half', 'metallic': 1},
      });
      expect(material.values, {'metallic': 1.0});
    });

    test('keeps maps by slot and refuses a slot it has no use for', () {
      final json = {
        'maps': {
          'baseColour': 'textures/brass.png',
          'shinyness': 'textures/x.png',
          'normal': '',
        },
      };
      expect(read(json).maps, {'baseColour': 'textures/brass.png'});
      expect(problems(json), hasLength(2));
    });

    test('writes back what it read, and nothing it did not', () {
      const json = {
        'parent': 'materials/metal.omat',
        'values': {'metallic': 1.0},
        'maps': {'normal': 'textures/n.png'},
      };
      expect(read(json).toJson(), json);
      // A material that states nothing states nothing, rather than a file full
      // of empty objects.
      expect(read(const {}).toJson(), isEmpty);
    });
  });

  group('resolving', () {
    test('a child takes what it does not state from its parent', () {
      final library = MaterialLibrary(
        materials: {
          'base.omat': read({
            'values': {'metallic': 1, 'roughness': 0.2},
          }),
          'child.omat': read({
            'parent': 'base.omat',
            'values': {'roughness': 0.8},
          }),
        },
      );

      final resolved = library.resolve('child.omat');
      expect(resolved.number('metallic'), 1.0);
      expect(resolved.number('roughness'), 0.8);
      expect(resolved.problems, isEmpty);
    });

    test('the nearest ancestor wins, down a long chain', () {
      final library = MaterialLibrary(
        materials: {
          'a.omat': read({
            'values': {'roughness': 0.1},
          }),
          'b.omat': read({
            'parent': 'a.omat',
            'values': {'roughness': 0.2},
          }),
          'c.omat': read({
            'parent': 'b.omat',
            'values': {'roughness': 0.3},
          }),
          'd.omat': read({'parent': 'c.omat'}),
        },
      );
      expect(library.resolve('d.omat').number('roughness'), 0.3);
    });

    test('maps are inherited and replaced whole', () {
      final library = MaterialLibrary(
        materials: {
          'base.omat': read({
            'maps': {'baseColour': 'a.png', 'normal': 'n.png'},
          }),
          'child.omat': read({
            'parent': 'base.omat',
            'maps': {'baseColour': 'b.png'},
          }),
        },
      );
      expect(library.resolve('child.omat').maps, {
        'baseColour': 'b.png',
        'normal': 'n.png',
      });
    });

    test('a group overrides what the material itself states', () {
      final library = MaterialLibrary(
        materials: {
          'crate.omat': read({
            'group': 'props',
            'values': {'roughness': 0.1, 'metallic': 1},
          }),
        },
        groups: {
          'props': read({
            'values': {'roughness': 0.9},
          }),
        },
      );

      final resolved = library.resolve('crate.omat');
      expect(resolved.number('roughness'), 0.9);
      expect(resolved.number('metallic'), 1.0);
    });

    test('a material chooses its own group over an ancestor\'s', () {
      final library = MaterialLibrary(
        materials: {
          'base.omat': read({'group': 'far'}),
          'child.omat': read({'parent': 'base.omat', 'group': 'near'}),
        },
        groups: {
          'far': read({
            'values': {'roughness': 0.1},
          }),
          'near': read({
            'values': {'roughness': 0.9},
          }),
        },
      );
      expect(library.resolve('child.omat').number('roughness'), 0.9);
    });

    test('a missing parent is reported and what is left still resolves', () {
      final library = MaterialLibrary(
        materials: {
          'child.omat': read({
            'parent': 'gone.omat',
            'values': {'roughness': 0.4},
          }),
        },
      );
      final resolved = library.resolve('child.omat');
      expect(resolved.number('roughness'), 0.4);
      expect(resolved.problems.single, contains('gone.omat'));
    });

    test('a chain that loops is reported rather than hung on', () {
      final library = MaterialLibrary(
        materials: {
          'a.omat': read({
            'parent': 'b.omat',
            'values': {'metallic': 1},
          }),
          'b.omat': read({'parent': 'a.omat'}),
        },
      );
      final resolved = library.resolve('a.omat');
      expect(resolved.problems.single, contains('inherits from itself'));
      expect(resolved.number('metallic'), 1.0);
    });

    test('a missing group is reported, not guessed at', () {
      final library = MaterialLibrary(
        materials: {
          'a.omat': read({'group': 'nowhere'}),
        },
      );
      expect(library.resolve('a.omat').problems.single, contains('nowhere'));
    });

    test('a material nobody put there resolves to nothing, with a problem', () {
      final resolved = MaterialLibrary().resolve('missing.omat');
      expect(resolved.values, isEmpty);
      expect(resolved.problems.single, contains('missing.omat'));
    });

    test('putting a material again is seen by everything below it', () {
      final library = MaterialLibrary(
        materials: {
          'base.omat': read({
            'values': {'roughness': 0.1},
          }),
          'child.omat': read({'parent': 'base.omat'}),
        },
      );
      expect(library.resolve('child.omat').number('roughness'), 0.1);

      library.put(
        'base.omat',
        read({
          'values': {'roughness': 0.9},
        }),
      );
      expect(library.resolve('child.omat').number('roughness'), 0.9);

      library.putGroup(
        'props',
        read({
          'values': {'roughness': 0.5},
        }),
      );
      library.put(
        'child.omat',
        read({'parent': 'base.omat', 'group': 'props'}),
      );
      expect(library.resolve('child.omat').number('roughness'), 0.5);
    });

    test('resolving twice gives the same answer without walking again', () {
      final library = MaterialLibrary(
        materials: {
          'a.omat': read({
            'values': {'roughness': 0.3},
          }),
        },
      );
      expect(library.resolve('a.omat'), same(library.resolve('a.omat')));
    });
  });

  group('the parameter table', () {
    test('has no name twice', () {
      expect(MaterialFields.byName, hasLength(MaterialFields.all.length));
    });

    test('gives every choice at least two to choose between', () {
      for (final field in MaterialFields.all) {
        if (field.kind != MaterialKind.choice) {
          expect(field.choices, isEmpty, reason: field.name);
        } else {
          expect(field.choices.length, greaterThan(1), reason: field.name);
        }
      }
    });
  });

  group('looks', () {
    test('an object wears its own material when a look says nothing', () {
      const worn = MaterialComponent(asset: 'a.omat');
      expect(worn.under(null), 'a.omat');
      expect(worn.under('winter'), 'a.omat');
    });

    test('and the look\'s when it has one', () {
      const worn = MaterialComponent(
        asset: 'a.omat',
        looks: {'winter': 'snow.omat'},
      );
      expect(worn.under('winter'), 'snow.omat');
      expect(worn.under('summer'), 'a.omat');
      expect(worn.lookNames, ['winter']);
    });

    test('survives being written out and read back', () {
      const worn = MaterialComponent(
        asset: 'a.omat',
        looks: {'winter': 'snow.omat'},
      );
      final again = MaterialComponent.fromJson(worn.toJson());
      expect(again.asset, 'a.omat');
      expect(again.looks, {'winter': 'snow.omat'});
    });

    test('a component with no looks writes no looks', () {
      expect(const MaterialComponent(asset: 'a.omat').toJson(), {
        'asset': 'a.omat',
      });
    });
  });
}
