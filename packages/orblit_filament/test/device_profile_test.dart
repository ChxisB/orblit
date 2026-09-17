import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';

/// What a device can do, read from the answers the renderer sends, and the
/// tier and budgets worked out from them.
void main() {
  // In OrblitCapability's order: backend, feature level, texture size, array
  // layers, formats, half float, threads, megabytes.
  const desktop = [1, 3, 16384, 2048, 0x3C, 1, 12, 36864];
  const simulator = [1, 2, 8192, 2048, 0x3, 1, 12, 36864];
  const phone = [2, 3, 4096, 2048, 0x3, 1, 8, 4096];
  const browser = [3, 1, 4096, 256, 0x1, 0, 1, 0];

  group('reading the answers', () {
    test('every answer lands in its own field', () {
      final profile = OrblitDeviceProfile.fromCapabilities(desktop);
      expect(profile.api, OrblitGraphicsApi.metal);
      expect(profile.featureLevel, 3);
      expect(profile.maxTextureSize, 16384);
      expect(profile.maxArrayTextureLayers, 2048);
      expect(profile.halfFloatTextures, isTrue);
      expect(profile.workerThreads, 12);
      expect(profile.systemMemoryMegabytes, 36864);
    });

    test('the format bits become families', () {
      final profile = OrblitDeviceProfile.fromCapabilities(desktop);
      expect(profile.textureFamilies, {
        OrblitTextureFamily.bc1to3,
        OrblitTextureFamily.bc4and5,
        OrblitTextureFamily.bc6h,
        OrblitTextureFamily.bc7,
      });
      expect(profile.supports(OrblitTextureFamily.astc), isFalse);
    });

    test('a device that will not say how much memory it has is unknown, '
        'not empty', () {
      expect(
        OrblitDeviceProfile.fromCapabilities(browser).systemMemoryMegabytes,
        isNull,
      );
    });

    test('missing and unknown answers are read as the least a device has', () {
      final profile = OrblitDeviceProfile.fromCapabilities(const [-1, -1]);
      expect(profile.featureLevel, 1);
      expect(profile.maxTextureSize, 2048);
      expect(profile.textureFamilies, isEmpty);
      expect(profile.workerThreads, 1);
      expect(profile.tier, OrblitDeviceTier.low);
    });
  });

  group('the tier', () {
    test('a desktop that proves everything is high', () {
      expect(
        OrblitDeviceProfile.fromCapabilities(desktop).tier,
        OrblitDeviceTier.high,
      );
    });

    test('anything that cannot draw the standard surface is low, however '
        'many cores it has', () {
      final profile = OrblitDeviceProfile.fromCapabilities(simulator);
      expect(profile.slimSurface, isTrue);
      expect(profile.tier, OrblitDeviceTier.low);
    });

    test('a capable phone is medium', () {
      expect(
        OrblitDeviceProfile.fromCapabilities(phone).tier,
        OrblitDeviceTier.medium,
      );
    });

    test('a browser is low', () {
      expect(
        OrblitDeviceProfile.fromCapabilities(browser).tier,
        OrblitDeviceTier.low,
      );
    });

    test('high needs memory it can prove', () {
      final unsaid = [...desktop]..[7] = 0;
      expect(
        OrblitDeviceProfile.fromCapabilities(unsaid).tier,
        OrblitDeviceTier.medium,
      );
    });
  });

  group('the budgets', () {
    test('grow with the tier', () {
      final low = OrblitDeviceProfile.fromCapabilities(browser);
      final high = OrblitDeviceProfile.fromCapabilities(desktop);
      expect(low.splatBudget, lessThan(high.splatBudget));
      expect(low.harmonicDegree, 0);
      expect(high.harmonicDegree, 3);
    });

    test('sort splats coarsely only where the tier is low', () {
      expect(
        OrblitDeviceProfile.fromCapabilities(browser).coarseSplatOrder,
        isTrue,
      );
      expect(
        OrblitDeviceProfile.fromCapabilities(phone).coarseSplatOrder,
        isFalse,
      );
      expect(
        OrblitDeviceProfile.fromCapabilities(desktop).coarseSplatOrder,
        isFalse,
      );
    });

    test('never ask for a texture larger than the device can hold', () {
      const tiny = OrblitDeviceProfile(
        featureLevel: 3,
        maxTextureSize: 512,
        workerThreads: 4,
      );
      expect(tiny.textureSizeBudget, 512);
    });

    test('upload more texture a frame the higher the tier', () {
      final low = OrblitDeviceProfile.fromCapabilities(browser);
      final medium = OrblitDeviceProfile.fromCapabilities(phone);
      final high = OrblitDeviceProfile.fromCapabilities(desktop);
      expect(
        low.textureUploadKilobytes,
        lessThan(medium.textureUploadKilobytes),
      );
      expect(
        medium.textureUploadKilobytes,
        lessThan(high.textureUploadKilobytes),
      );
    });
  });

  group('the files to fetch for a cooked texture', () {
    test('are the siblings of the families the device has, best first, and '
        'the universal file last', () {
      const everything = OrblitDeviceProfile(
        textureFamilies: {
          OrblitTextureFamily.astc,
          OrblitTextureFamily.bc7,
          OrblitTextureFamily.bc4and5,
          OrblitTextureFamily.etc2,
        },
      );
      expect(everything.textureCandidates('textures/wood.ktx2'), [
        'textures/wood.astc.ktx2',
        'textures/wood.bc.ktx2',
        'textures/wood.etc2.ktx2',
        'textures/wood.ktx2',
      ]);
    });

    test('leave out a family the device cannot sample', () {
      expect(
        OrblitDeviceProfile.fromCapabilities(
          desktop,
        ).textureCandidates('a/b.ktx2'),
        ['a/b.bc.ktx2', 'a/b.ktx2'],
      );
      expect(
        OrblitDeviceProfile.fromCapabilities(phone).textureCandidates('b.ktx2'),
        ['b.astc.ktx2', 'b.etc2.ktx2', 'b.ktx2'],
      );
      expect(
        OrblitDeviceProfile.fromCapabilities(const [
          3,
          1,
          4096,
          256,
          0,
          0,
          1,
          0,
        ]).textureCandidates('b.ktx2'),
        ['b.ktx2'],
        reason: 'a browser with no compressed formats gets Basis',
      );
    });

    test('are only BC when BC7 or BC5 is there, which is what a BC sibling '
        'holds', () {
      const s3tcOnly = OrblitDeviceProfile(
        textureFamilies: {OrblitTextureFamily.bc1to3},
      );
      expect(s3tcOnly.textureCandidates('b.ktx2'), ['b.ktx2']);
    });

    test('keep the extension as it was written', () {
      const astc = OrblitDeviceProfile(
        textureFamilies: {OrblitTextureFamily.astc},
      );
      expect(astc.textureCandidates('B.KTX2'), ['B.astc.KTX2', 'B.KTX2']);
    });

    test('are just the name for anything but a cooked set', () {
      final profile = OrblitDeviceProfile.fromCapabilities(phone);
      for (final path in [
        'wood.png',
        'wood.astc.ktx2',
        'wood.BC.ktx2',
        'wood.etc2.ktx2',
        'environment.ktx',
      ]) {
        expect(profile.textureCandidates(path), [path]);
      }
    });
  });
}
