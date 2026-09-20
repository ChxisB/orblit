import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/importers/native_tool.dart' as native;
import 'package:test/test.dart';

import 'gltf_fixtures.dart';

const red = [220, 40, 40, 255];
const blue = [40, 40, 220, 255];

/// A model the atlas has something to do with: two quads, two materials, two
/// base-colour maps.
QuadModel twoMaterials() {
  final model = QuadModel();
  final a = model.texture(red);
  final b = model.texture(blue);
  model.quad(model.material({
    'pbrMetallicRoughness': {
      'baseColorTexture': {'index': a},
    },
  }));
  model.quad(model.material({
    'pbrMetallicRoughness': {
      'baseColorTexture': {'index': b},
    },
  }), x: 2);
  return model;
}

ImportRequest requestFor(
  Uint8List bytes,
  Map<String, Object?> settings, {
  CookTarget target = CookTargets.macos,
}) =>
    ImportRequest(
      id: AssetId.parse('models/thing.glb'),
      bytes: bytes,
      settings: settings,
      target: target,
      source: MemoryAssetSource(const {}),
    );

void main() {
  final importer = GltfImporter();

  group('GltfImporter settings', () {
    test('is off unless the asset asks for it', () {
      expect(importer.resolveSettings(const ImportSettings()),
          {'atlas': false});
      expect(importer.resolveSettings(const ImportSettings(values: {'atlas': false})),
          {'atlas': false});
    });

    test('fills in every key once it is on', () {
      final settings =
          importer.resolveSettings(const ImportSettings(values: {'atlas': true}));
      expect(settings['atlas'], isTrue);
      expect(settings['maxPageSize'], 2048);
      expect(settings['padding'], 4);
      expect(settings['extrude'], 2);
      expect(settings['minMaterials'], 2);
      expect(settings['mergePrimitives'], isTrue);
      expect(settings['maxMergedVertices'], 65536);
    });

    test('says a value is wrong rather than quietly using another', () {
      // A number outside the range is a mistake in the project file, and the
      // one thing worse than a cook that stops is a cook that pretends.
      for (final wrong in const <Map<String, Object?>>[
        {'maxPageSize': 1 << 20},
        {'padding': -3},
        {'maxMergedVertices': 0},
        {'minMaterials': 0},
        {'extrude': 'lots'},
      ]) {
        expect(
          () => importer
              .resolveSettings(ImportSettings(values: {'atlas': true, ...wrong})),
          throwsA(isA<ArgumentError>()),
          reason: '$wrong',
        );
      }
    });

    test('changed version, because a cache keyed on the old one is wrong', () {
      // The settings gained `atlas`; a bundle cooked before that must not be
      // served for an asset that now asks to be packed.
      expect(importer.version, greaterThanOrEqualTo(2));
    });
  });

  group('GltfImporter', () {
    test('hands the bytes straight back when atlasing is off', () async {
      final bytes = twoMaterials().toGlb();
      final result =
          await importer.import(requestFor(bytes, const {'atlas': false}));
      expect(result.outputs.keys, ['glb']);
      expect(result.outputs['glb'], same(bytes));
    });

    test('hands the bytes back unchanged when there is nothing to pack',
        () async {
      // One material: packing it would cost a recook and save nothing.
      final model = QuadModel();
      final only = model.texture(red);
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': only},
        },
      }));
      final bytes = model.toGlb();

      final result = await importer.import(requestFor(bytes, const {
        'atlas': true,
        'maxPageSize': 512,
        'padding': 4,
        'extrude': 2,
        'maxCellSize': 0,
        'minMaterials': 2,
        'mergePrimitives': true,
        'maxMergedVertices': 65536,
      }));
      expect(result.outputs['glb'], same(bytes),
          reason: 'a model that comes back byte for byte is a cache hit next '
              'time, not a needless miss');
      expect(result.notes.join(' '), contains('1 of'));
    });
  });

  group('ModelAtlasCook', () {
    test('cooks the pages and rewrites the model to name them', () async {
      final tool = native.NativeTool('orblit_texture_cook',
          environmentVariable: 'ORBLIT_TEXTURE_COOK');
      if (tool.locate() == null) {
        markTestSkipped('orblit_texture_cook is not built');
        return;
      }

      final result = await GltfImporter().import(requestFor(
        twoMaterials().toGlb(),
        const {
          'atlas': true,
          'maxPageSize': 512,
          'padding': 4,
          'extrude': 2,
          'maxCellSize': 0,
          'minMaterials': 2,
          'mergePrimitives': true,
          'maxMergedVertices': 65536,
        },
      ));

      // The GLB, the page in the portable format, and a sibling per family the
      // target can read.
      expect(result.outputs.keys, contains('glb'));
      expect(result.outputs.keys, contains('atlas0.basecolour.ktx2'));
      expect(result.outputs.keys, contains('atlas0.basecolour.bc.ktx2'));

      final document = await GltfDocument.read(
        AssetId.parse('models/thing.glb'),
        Uint8List.fromList(result.outputs['glb']!),
        MemoryAssetSource(const {}),
      );

      // Every image the model still names must be one of the outputs beside
      // it, or the loader is being sent to a file that was never written.
      final images = document.list('images');
      expect(images, isNotEmpty);
      for (final image in images) {
        expect(image['bufferView'], isNull,
            reason: 'a source PNG left embedded is one the GPU cannot read');
        expect(result.outputs.keys, contains(image['uri']));
      }
      // The two source PNGs are gone: nothing points at them, so nothing
      // should be decoding and uploading them.
      expect(images, hasLength(1));
    }, timeout: const Timeout(Duration(minutes: 2)));
  });
}
