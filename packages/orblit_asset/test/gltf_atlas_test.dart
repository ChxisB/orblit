import 'dart:typed_data';

import 'package:orblit_asset/src/asset_id.dart';
import 'package:orblit_asset/src/asset_source.dart';
import 'package:orblit_asset/src/gltf/accessor.dart';
import 'package:orblit_asset/src/gltf/atlas_pixels.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/gltf/model_atlas.dart';
import 'package:orblit_asset/src/gltf/texture_roles.dart';
import 'package:test/test.dart';

import 'gltf_fixtures.dart';

const red = [220, 40, 40, 255];
const blue = [40, 40, 220, 255];

Future<GltfDocument> open(QuadModel model) => GltfDocument.read(
      AssetId.parse('models/thing.glb'),
      model.toGlb(),
      MemoryAssetSource(const {}),
    );

/// The page texel the middle of [primitive]'s UVs lands on.
List<int> centreOf(GltfDocument document, int primitive, Rgba page) {
  final list = document.list('meshes').first['primitives'] as List;
  final attributes =
      (list[primitive] as Map)['attributes'] as Map<String, Object?>;
  final uv = document.readVec2(attributes['TEXCOORD_0'] as int);
  var u = 0.0;
  var v = 0.0;
  for (var i = 0; i < uv.length; i += 2) {
    u += uv[i];
    v += uv[i + 1];
  }
  final count = uv.length / 2;
  return texelAt(
    page,
    (u / count * page.width).floor().clamp(0, page.width - 1),
    (v / count * page.height).floor().clamp(0, page.height - 1),
  );
}

void main() {
  group('atlasModel', () {
    test('packs two materials onto one page and moves their UVs into it',
        () async {
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

      final document = await open(model);
      final result = await atlasModel(
        document,
        options: const ModelAtlasOptions(mergePrimitives: false),
        decode: decoderOver(document),
      );

      expect(result.changed, isTrue, reason: result.notes.join('; '));
      expect(result.pages, hasLength(1));
      expect(result.pages.single.role, TextureRole.baseColour);
      expect(result.pages.single.stem, 'atlas0.basecolour');

      // The point of the whole exercise: each quad still samples its own
      // colour, from a different part of one image.
      final page = result.pages.single.image;
      expect(centreOf(document, 0, page), red);
      expect(centreOf(document, 1, page), blue);

      // And both materials now name that one image, through the extension the
      // spec reserves for KTX2 rather than `source`, which must stay a
      // PNG/JPEG fallback.
      final textures = document.list('textures');
      final used = {
        for (final material in document.list('materials'))
          ((material['pbrMetallicRoughness'] as Map)['baseColorTexture']
              as Map)['index'] as int,
      };
      expect(used, hasLength(1));
      final texture = textures[used.single];
      expect(texture['source'], isNull);
      expect(
        ((texture['extensions'] as Map)['KHR_texture_basisu']
            as Map)['source'],
        isNotNull,
      );
      expect(document.json['extensionsRequired'],
          contains('KHR_texture_basisu'));
      final image = document.list('images')[
          ((texture['extensions'] as Map)['KHR_texture_basisu'] as Map)['source']
              as int];
      expect(image['uri'], 'atlas0.basecolour.ktx2');
      expect(image['mimeType'], 'image/ktx2');
    });

    test('gives a material the maps it never had, blank, so it can share',
        () async {
      final model = QuadModel();
      final colour = model.texture(red);
      final bumps = model.texture(const [200, 120, 255, 255]);
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': colour},
        },
        'normalTexture': {'index': bumps},
      }));
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': colour},
        },
      }), x: 2);

      final document = await open(model);
      final result = await atlasModel(
        document,
        options: const ModelAtlasOptions(mergePrimitives: false),
        decode: decoderOver(document),
      );

      expect(result.pages.map((p) => p.role),
          containsAll([TextureRole.baseColour, TextureRole.normal]));
      final normals =
          result.pages.firstWhere((p) => p.role == TextureRole.normal).image;

      // The second material never had a normal map, so its cell must hold the
      // value glTF samples when the slot is absent — otherwise handing it the
      // slot would change how it draws.
      expect(centreOf(document, 1, normals), [128, 128, 255, 255]);
      expect(centreOf(document, 0, normals), const [200, 120, 255, 255]);
      expect(document.list('materials')[1]['normalTexture'], isNotNull);
    });

    test('folds a factor into the page so two tints become one material',
        () async {
      final model = QuadModel();
      final white = model.texture(const [255, 255, 255, 255]);
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': white},
          'baseColorFactor': [1.0, 0.0, 0.0, 1.0],
        },
      }));
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': white},
          'baseColorFactor': [0.0, 0.0, 1.0, 1.0],
        },
      }), x: 2);

      final document = await open(model);
      final result = await atlasModel(
        document,
        decode: decoderOver(document),
      );

      final page = result.pages.single.image;
      final materials = document.list('materials');
      for (final material in materials) {
        final pbr = material['pbrMetallicRoughness'] as Map?;
        expect(pbr?['baseColorFactor'], anyOf(isNull, [1.0, 1.0, 1.0, 1.0]),
            reason: 'a factor folded into the page must not be applied twice');
      }

      // Two materials that differed only by tint are now one, and the tint
      // lives in the texels.
      final primitives = document.list('meshes').first['primitives'] as List;
      expect(primitives, hasLength(1),
          reason: 'one material means one draw call');
      final uv = document.readVec2(
          ((primitives.single as Map)['attributes'] as Map)['TEXCOORD_0']
              as int);
      expect(uv.length, 16, reason: 'both quads, joined');

      int at(int vertex) {
        final x = (uv[vertex * 2] * page.width).floor().clamp(0, page.width - 1);
        final y =
            (uv[vertex * 2 + 1] * page.height).floor().clamp(0, page.height - 1);
        return texelAt(page, x, y)[0];
      }

      // Vertex 0 belongs to the red quad, vertex 4 to the blue one.
      expect(at(0), greaterThan(200));
      expect(at(4), lessThan(60));
    });

    test('leaves a model whose maps tile across the surface alone', () async {
      final model = QuadModel();
      final a = model.texture(red);
      final b = model.texture(blue);
      model.quad(
        model.material({
          'pbrMetallicRoughness': {
            'baseColorTexture': {'index': a},
          },
        }),
        uv: const [0.0, 0.0, 4.0, 0.0, 4.0, 4.0, 0.0, 4.0],
      );
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': b},
        },
      }), x: 2);

      final document = await open(model);
      final before = document.readVec2(1);
      final result =
          await atlasModel(document, decode: decoderOver(document));

      expect(result.changed, isFalse);
      expect(result.notes.join(' '), contains('tiled'));
      expect(document.readVec2(1), before,
          reason: 'a model left alone must come back byte for byte');
    });

    test('leaves a slot carrying a texture transform alone', () async {
      final model = QuadModel();
      final a = model.texture(red);
      final b = model.texture(blue);
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {
            'index': a,
            'extensions': {
              'KHR_texture_transform': {
                'offset': [0.5, 0.5],
              },
            },
          },
        },
      }));
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': b},
        },
      }), x: 2);

      final document = await open(model);
      final result =
          await atlasModel(document, decode: decoderOver(document));

      expect(result.changed, isFalse);
      expect(result.notes.join(' '), contains('KHR_texture_transform'));
    });

    test('refuses a texture asked to be two things at once', () async {
      final model = QuadModel();
      final both = model.texture(const [200, 120, 255, 255]);
      final plain = model.texture(red);
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': both},
        },
        'normalTexture': {'index': both},
      }));
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': plain},
        },
      }), x: 2);

      final document = await open(model);
      final result =
          await atlasModel(document, decode: decoderOver(document));

      // One material is out; the other is alone, and one material is not an
      // atlas worth making.
      expect(result.changed, isFalse);
      expect(result.notes.join(' '), contains('two roles'));
    });

    test('leaves a material carrying an extension it does not understand',
        () async {
      final model = QuadModel();
      final a = model.texture(red);
      final b = model.texture(blue);
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': a},
        },
        'extensions': {
          'KHR_materials_clearcoat': {'clearcoatFactor': 1.0},
        },
      }));
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': b},
        },
      }), x: 2);

      final document = await open(model);
      final result =
          await atlasModel(document, decode: decoderOver(document));

      expect(result.changed, isFalse);
      expect(result.notes.join(' '), contains('KHR_materials_clearcoat'));
    });

    test('carries the parts of the document it never looked at through',
        () async {
      final model = QuadModel();
      final a = model.texture(red);
      final b = model.texture(blue);
      model.quad(model.material({
        'name': 'first',
        'extras': {'mine': 1},
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': a},
        },
        'doubleSided': true,
      }));
      model.quad(model.material({
        'pbrMetallicRoughness': {
          'baseColorTexture': {'index': b},
        },
      }), x: 2);
      model.json['extras'] = {'orblit': 'hello'};
      ((model.json['nodes'] as List).first as Map<String, Object?>)['extras'] =
          {'collider': 'box'};

      final document = await open(model);
      await atlasModel(
        document,
        options: const ModelAtlasOptions(mergePrimitives: false),
        decode: decoderOver(document),
      );

      final again = await GltfDocument.read(
        AssetId.parse('models/thing.glb'),
        document.toGlb(),
        MemoryAssetSource(const {}),
      );
      expect(again.json['extras'], {'orblit': 'hello'});
      expect(again.list('nodes').first['extras'], {'collider': 'box'});
      final first = again
          .list('materials')
          .firstWhere((m) => m['name'] == 'first');
      expect(first['extras'], {'mine': 1});
      expect(first['doubleSided'], true);
    });
  });

  group('pixels', () {
    test('resamples by area rather than by nearest neighbour', () {
      final source = Rgba(2, 2, Uint8List.fromList(const [
        0, 0, 0, 255, //
        255, 255, 255, 255,
        255, 255, 255, 255,
        0, 0, 0, 255,
      ]));
      final half = resample(source, 1, 1);
      // Nearest neighbour would answer 0 or 255; an average answers between.
      expect(half.pixels[0], inInclusiveRange(100, 155));
      expect(half.pixels[3], 255);
    });

    test('extrudes the edge so the sampler cannot reach a neighbour', () {
      final page = blankPage(TextureRole.baseColour, 8, 8);
      final cell = Rgba.filled(4, 4, red);
      blit(page, cell, 2, 2, extrude: 2);

      expect(texelAt(page, 2, 2), red);
      // Two texels of skirt on every side, so a bilinear tap at the seam of a
      // mip still lands on this cell's colour.
      expect(texelAt(page, 0, 0), red);
      expect(texelAt(page, 7, 7), red);
      expect(texelAt(page, 1, 4), red);
    });

    test('multiplies a colour factor in the space the texels are in', () {
      final image = Rgba.filled(1, 1, const [188, 188, 188, 255]);
      multiplyInto(image, const [0.5, 0.5, 0.5, 1.0], srgb: true);
      // Half the light, not half the number: 188 is about 0.5 linear, and half
      // of that encodes to about 137, not 94.
      expect(image.pixels[0], inInclusiveRange(130, 145));

      final linear = Rgba.filled(1, 1, const [188, 188, 188, 255]);
      multiplyInto(linear, const [0.5, 0.5, 0.5, 1.0], srgb: false);
      expect(linear.pixels[0], 94);
    });

    test('renormalises a resampled normal map', () {
      final image = Rgba.filled(2, 1, const [255, 128, 128, 255]);
      renormalise(image);
      final x = image.pixels[0] / 255 * 2 - 1;
      final y = image.pixels[1] / 255 * 2 - 1;
      final z = image.pixels[2] / 255 * 2 - 1;
      expect(x * x + y * y + z * z, closeTo(1.0, 0.02));
    });
  });
}
