import 'package:orblit_asset/src/asset_id.dart';
import 'package:orblit_asset/src/asset_source.dart';
import 'package:orblit_asset/src/gltf/accessor.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/gltf/merge.dart';
import 'package:test/test.dart';

import 'gltf_fixtures.dart';

Future<GltfDocument> open(QuadModel model) => GltfDocument.read(
  AssetId.parse('models/thing.glb'),
  model.toGlb(),
  MemoryAssetSource(const {}),
);

List<Map<String, Object?>> primitivesOf(GltfDocument document) => [
  for (final primitive in document.list('meshes').first['primitives'] as List)
    primitive as Map<String, Object?>,
];

List<int?> materialsOf(GltfDocument document) => [
  for (final p in primitivesOf(document)) p['material'] as int?,
];

void main() {
  group('mergeMaterials', () {
    test('folds materials that say the same thing into one', () async {
      final model = QuadModel();
      final a = model.material({'doubleSided': true, 'name': 'left'});
      final b = model.material({'doubleSided': true, 'name': 'right'});
      final c = model.material({'doubleSided': false});
      model.quad(a);
      model.quad(b, x: 2);
      model.quad(c, x: 4);

      final document = await open(model);
      expect(mergeMaterials(document), 1);
      // The name is not what a material draws like, so two materials that
      // differ only by it are one material.
      expect(materialsOf(document), [a, a, c]);
    });

    test('keeps two materials that differ anywhere apart', () async {
      final model = QuadModel();
      final a = model.material({
        'pbrMetallicRoughness': {
          'baseColorFactor': [1.0, 0.0, 0.0, 1.0],
        },
      });
      final b = model.material({
        'pbrMetallicRoughness': {
          'baseColorFactor': [1.0, 0.0, 0.0, 0.9],
        },
      });
      model.quad(a);
      model.quad(b, x: 2);

      final document = await open(model);
      expect(mergeMaterials(document), 0);
      expect(materialsOf(document), [a, b]);
    });

    test(
      'does not renumber, so nothing that points at a material breaks',
      () async {
        final model = QuadModel();
        final a = model.material({'doubleSided': true});
        final b = model.material({'doubleSided': true});
        model.quad(a);
        model.quad(b, x: 2);

        final document = await open(model);
        mergeMaterials(document);
        expect(
          document.list('materials'),
          hasLength(2),
          reason:
              'an unused material costs nothing; a shifted index costs a '
              'variant, an extension or an extras key pointing at the wrong '
              'one',
        );
      },
    );

    test('follows a variant mapping to the material it now names', () async {
      final model = QuadModel();
      final a = model.material({'doubleSided': true});
      final b = model.material({'doubleSided': true});
      model.quad(a);
      final primitive = model.primitives.first as Map<String, Object?>;
      primitive['extensions'] = {
        'KHR_materials_variants': {
          'mappings': [
            {
              'material': b,
              'variants': [0],
            },
          ],
        },
      };

      final document = await open(model);
      mergeMaterials(document);
      final mappings =
          ((primitivesOf(document).first['extensions']
                      as Map)['KHR_materials_variants']
                  as Map)['mappings']
              as List;
      expect((mappings.single as Map)['material'], a);
    });
  });

  group('mergePrimitives', () {
    test('joins two draws of one material into one', () async {
      final model = QuadModel();
      final one = model.material({'doubleSided': true});
      model.quad(one);
      model.quad(one, x: 2);

      final document = await open(model);
      expect(mergePrimitives(document), 1);

      final primitives = primitivesOf(document);
      expect(primitives, hasLength(1));
      final attributes =
          primitives.single['attributes'] as Map<String, Object?>;
      expect(document.readVec3(attributes['POSITION'] as int), [
        0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, //
        2, 0, 0, 3, 0, 0, 3, 1, 0, 2, 1, 0,
      ]);
      // The second quad's indices must have moved up by its four vertices, or
      // it draws the first quad twice.
      expect(document.readIndices(primitives.single['indices'] as int), [
        0,
        1,
        2,
        0,
        2,
        3,
        4,
        5,
        6,
        4,
        6,
        7,
      ]);

      final position = document.accessor(attributes['POSITION'] as int);
      expect(position['min'], [0, 0, 0]);
      expect(position['max'], [
        3,
        1,
        0,
      ], reason: 'a stale bound is a mesh that culls when it is on screen');
    });

    test('keeps the order a model draws in', () async {
      final model = QuadModel();
      final a = model.material({'name': 'a'});
      final b = model.material({'name': 'b'});
      model.quad(a);
      model.quad(b, x: 2);
      model.quad(a, x: 4);

      final document = await open(model);
      expect(mergePrimitives(document), 1);
      // The two `a` draws join where the first of them was, so `b` still draws
      // after them — the only draw order a glTF expresses.
      expect(materialsOf(document), [a, b]);
    });

    test('splits a batch before its indices need a third byte', () async {
      final model = QuadModel();
      final one = model.material({'doubleSided': true});
      model.quad(one);
      model.quad(one, x: 2);
      model.quad(one, x: 4);

      final document = await open(model);
      expect(mergePrimitives(document, maxVertices: 8), 1);
      final primitives = primitivesOf(document);
      expect(primitives, hasLength(2));
      expect(
        document.accessorCount(
          (primitives.first['attributes'] as Map)['POSITION'] as int,
        ),
        8,
      );
      expect(
        document.accessorCount(
          (primitives.last['attributes'] as Map)['POSITION'] as int,
        ),
        4,
      );
    });

    test('will not join primitives whose attributes differ', () async {
      final model = QuadModel();
      final one = model.material({'doubleSided': true});
      model.quad(one);
      model.quad(one, x: 2);
      // A second UV set on one of them: joining would leave half the vertices
      // with no value for it.
      final attributes =
          (model.primitives.last as Map)['attributes'] as Map<String, Object?>;
      attributes['TEXCOORD_1'] = attributes['TEXCOORD_0'];

      final document = await open(model);
      expect(mergePrimitives(document), 0);
      expect(primitivesOf(document), hasLength(2));
    });

    test('leaves skinned, morphed and variant draws alone', () async {
      for (final spoiler in <Map<String, Object?>>[
        {
          'targets': [
            {'POSITION': 0},
          ],
        },
        {
          'extensions': {
            'KHR_materials_variants': {'mappings': <Object?>[]},
          },
        },
        {
          'extensions': {
            'KHR_draco_mesh_compression': {'bufferView': 0},
          },
        },
        {'mode': 5},
      ]) {
        final model = QuadModel();
        final one = model.material({'doubleSided': true});
        model.quad(one);
        model.quad(one, x: 2);
        (model.primitives.last as Map<String, Object?>).addAll(spoiler);

        final document = await open(model);
        expect(
          mergePrimitives(document),
          0,
          reason: 'must not join a draw carrying $spoiler',
        );
        expect(primitivesOf(document), hasLength(2));
      }
    });

    test(
      'leaves a skinned draw alone even when both sides are skinned',
      () async {
        final model = QuadModel();
        final one = model.material({'doubleSided': true});
        model.quad(one);
        model.quad(one, x: 2);
        for (final primitive in model.primitives) {
          final attributes =
              (primitive as Map)['attributes'] as Map<String, Object?>;
          attributes['JOINTS_0'] = attributes['POSITION'];
          attributes['WEIGHTS_0'] = attributes['POSITION'];
        }

        final document = await open(model);
        // Joining would renumber vertices out from under the skin's joint
        // indices, which is a mesh that animates into a knot.
        expect(mergePrimitives(document), 0);
      },
    );
  });
}
