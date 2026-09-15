import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';

/// The numbers three languages have to agree on, checked rather than trusted.
///
/// Every scene crosses the boundary as flat float arrays, and how wide a row
/// is exists in three places: the Dart that packs it, the Swift that checks
/// the message, and the C++ that reads it. They agree by hand, and the
/// comments on all three say so, which is not the same as anything noticing
/// when they stop.
///
/// What happens when they drift is worth spelling out, because it is not a
/// crash. Swift refuses the message, so the scene never reaches the renderer
/// and the view shows the error it got back — everything else still passes:
/// the packing tests, because Dart packs correctly; the frame smoke, because
/// the renderer still draws the last thing it was given. The failure is a
/// whole app that renders nothing, found by running it.
///
/// So this reads the native sources and compares the numbers. Coarse, but it
/// fails on the commit that causes it rather than on the first launch after.
void main() {
  final swift = _read(
    'darwin/orblit_filament/Sources/orblit_filament/OrblitFilamentPlugin.swift',
  );
  // The renderer, and the plain C++ beside it that it calls into: a number
  // that lives in either is the renderer's. The renderer's own constants are
  // in its C++ core's header now that the Objective-C class only forwards to
  // it — the same lines, moved, so this reads them there.
  final native =
      _read(
        'darwin/orblit_filament/Sources/orblit_filament_native/OrblitRendererCore.h',
      ) +
      _read(
        'darwin/orblit_filament/Sources/orblit_filament_native/OrblitDecals.h',
      );

  group('the strides the three sides share', () {
    // Dart's number, what Swift calls it, and what the renderer calls it —
    // null where that side has no say in it.
    const contract = <String, (int, String, String?)>{
      'a light': (OrblitLight.stride, 'lightStride', null),
      'a probe': (OrblitProbe.stride, 'probeStride', null),
      'a field': (OrblitField.stride, 'fieldStride', null),
      'an environment': (OrblitEnvironment.stride, 'environmentStride', null),
      'a graph pass': (OrblitRenderGraph.passStride, 'passStride', null),
      'a graph target': (OrblitRenderGraph.targetStride, 'targetStride', null),
      'a material': (
        OrblitMaterial.stride,
        'materialStride',
        'kMaterialParams',
      ),
      "a material's maps": (
        OrblitMaterial.mapCount,
        'materialMaps',
        'kMaterialMaps',
      ),
      'a video': (OrblitVideo.stride, 'videoStride', 'kVideoParams'),
      'fog': (OrblitFog.stride, 'fogStride', null),
      'precipitation': (
        OrblitPrecipitation.stride,
        'precipitationStride',
        null,
      ),
      'the sky': (OrblitSky.stride, 'skyStride', null),
      'a decal': (OrblitDecal.stride, 'decalStride', 'kDecalStride'),
      // The renderer's side of this one is in the outline's own header,
      // checked below, because the outline lives in plain C++ of its own.
      'an outline': (OrblitOutline.stride, 'outlineStride', null),
    };

    // The pipeline block is passed through by the plugin unchecked, and its
    // offsets are named in the shadows code rather than the renderer — so it
    // is compared there. A block one float short reads a zero where a dial
    // should be, which for the contact distance is a shadow traced nowhere.
    test('the pipeline is ${OrblitPipeline.stride} wide everywhere', () {
      final shadows = _read(
        'darwin/orblit_filament/Sources/orblit_filament_native/OrblitShadows.h',
      );
      expect(
        _nativeValue(shadows, 'kPipelineStride'),
        OrblitPipeline.stride,
        reason:
            'Dart packs ${OrblitPipeline.stride} floats for the pipeline and '
            'the renderer names a different number of offsets',
      );
    });

    contract.forEach((what, agreed) {
      final (dart, swiftName, nativeName) = agreed;

      test('$what is $dart wide everywhere', () {
        expect(
          _swiftValue(swift, swiftName),
          dart,
          reason:
              'Dart packs $dart floats for $what and the plugin checks '
              'for a different number, so it will refuse every scene',
        );
        if (nativeName != null) {
          expect(
            _nativeValue(native, nativeName),
            dart,
            reason:
                'Dart packs $dart floats for $what and the renderer reads '
                'a different number, so it will read the wrong offsets',
          );
        }
      });
    });
  });

  group('what a device can do', () {
    final abi = _read(
      'darwin/orblit_filament/Sources/orblit_filament_native/include/orblit_renderer.h',
    );

    test('is asked in the order the C ABI numbers its questions', () {
      final body = RegExp(
        r'typedef enum orblit_capability \{(.*?)\} orblit_capability;',
        dotAll: true,
      ).firstMatch(abi);
      expect(body, isNotNull, reason: 'orblit_renderer.h no longer says');
      final native = [
        for (final found in RegExp(
          r'^\s*ORBLIT_CAPABILITY_([A-Z_]+)',
          multiLine: true,
        ).allMatches(body!.group(1)!))
          if (found.group(1) != 'COUNT') found.group(1)!,
      ];
      final dart = [
        for (final question in OrblitCapability.values)
          question.name
              .replaceAllMapped(RegExp('[A-Z]'), (m) => '_${m.group(0)}')
              .toUpperCase(),
      ];
      expect(
        dart,
        native,
        reason:
            'Dart reads each answer by its position, so a question added or '
            'moved on one side reads another question\'s answer on the other',
      );
    });

    test('spells the texture families with the same bits', () {
      final bits = [
        for (final found in RegExp(
          r'ORBLIT_FORMAT_\w+ = 1 << (\d+)',
        ).allMatches(abi))
          1 << int.parse(found.group(1)!),
      ];
      expect([
        for (final family in OrblitTextureFamily.values) family.bit,
      ], bits);
    });
  });

  test('the decal budget Dart reports against is the one the renderer '
      'paints', () {
    final found = RegExp(
      r'constexpr uint32_t kDecalBudget\s*=\s*(\d+)',
    ).firstMatch(native);
    expect(found, isNotNull, reason: 'OrblitDecals.h no longer says');
    expect(int.parse(found!.group(1)!), OrblitDecal.budget);
  });

  group('the outline', () {
    final header = _read(
      'darwin/orblit_filament/Sources/orblit_filament_native/OrblitOutline.h',
    );

    test('reads as many settings as Dart packs', () {
      expect(
        _nativeValue(header, 'kOutlineParams'),
        OrblitOutline.stride,
        reason:
            'Dart packs ${OrblitOutline.stride} floats for an outline and the '
            'renderer reads a different number',
      );
    });

    test('numbers the hidden styles as Dart does', () {
      for (final style in OrblitOccluded.values) {
        expect(
          RegExp(
            r'\b' + style.name + r'\s*=\s*' + '${style.index}' + r'\b',
          ).hasMatch(header),
          isTrue,
          reason:
              '${style.name} is ${style.index} in Dart and not in the '
              "renderer's Occluded",
        );
      }
    });

    test('is compiled into the CocoaPods build as well as SwiftPM', () {
      // SwiftPM compiles every file in the target's directory; CocoaPods only
      // the extensions it is told. A .cpp it is not told about is a link
      // error in one build system and a working app in the other.
      final podspec = _read('darwin/orblit_filament.podspec');
      expect(podspec, contains('cpp'));
    });
  });

  // The splat numbers live in their own files on both native sides, because
  // the feature does: its Swift decoding in OrblitSplatMessage.swift and its
  // C++ in OrblitSplats.h.
  group('the numbers Gaussian splats share', () {
    final splatSwift = _read(
      'darwin/orblit_filament/Sources/orblit_filament/OrblitSplatMessage.swift',
    );
    final splatNative = _read(
      'darwin/orblit_filament/Sources/orblit_filament_native/OrblitSplats.h',
    );
    final material = _read('darwin/materials/splat.mat');

    test('a cloud is ${OrblitSplats.stride} floats wide everywhere', () {
      expect(_swiftValue(splatSwift, 'splatStride'), OrblitSplats.stride);
      expect(_nativeValue(splatNative, 'kSplatParams'), OrblitSplats.stride);
    });

    test('a splat record is ${OrblitSplats.recordBytes} bytes everywhere', () {
      expect(_swiftValue(splatSwift, 'recordBytes'), OrblitSplats.recordBytes);
      expect(
        _nativeValue(splatNative, 'kSplatRecordBytes'),
        OrblitSplats.recordBytes,
      );
    });

    test('the shader and the loader quantise harmonics the same way', () {
      // A coefficient is stored as 128 + round(127 * value / scale) and read
      // back as (byte - 128) / 127 * scale. Two numbers, written out in two
      // languages, and a drift between them is not a crash or a refused
      // message: it is a capture whose colours are quietly wrong from every
      // direction but straight on.
      final steps = RegExp(
        r'constexpr float kSplatHarmonicSteps\s*=\s*([0-9.]+)f',
      ).firstMatch(splatNative);
      final decode = RegExp(
        r'-\s*128\.0\)\s*/\s*([0-9.]+)',
      ).firstMatch(material);
      expect(steps, isNotNull);
      expect(decode, isNotNull);
      expect(double.parse(decode!.group(1)!), double.parse(steps!.group(1)!));
    });

    test('the shader and the uploader agree on the texture width', () {
      final width = RegExp(
        r'constexpr uint32_t kSplatTextureWidth\s*=\s*(\d+)',
      ).firstMatch(splatNative);
      final shader = RegExp(r'#define kWidth (\d+)').firstMatch(material);
      expect(width, isNotNull);
      expect(shader, isNotNull);
      expect(shader!.group(1), width!.group(1));
    });
  });
  _screenEffects(swift);
}

/// God rays and distortion keep their numbers in their own plain C++ header
/// rather than in the renderer, so they are checked against that.
void _screenEffects(String swift) {
  final screen = _read(
    'darwin/orblit_filament/Sources/orblit_filament_native/ScreenEffects.h',
  );

  group('god rays and distortion', () {
    test('agree on how wide a row is', () {
      expect(_swiftValue(swift, 'godRayStride'), OrblitGodRays.stride);
      expect(_swiftValue(swift, 'distortionStride'), OrblitDistortion.stride);
      expect(_nativeValue(screen, 'kGodRayStride'), OrblitGodRays.stride);
      expect(
        _nativeValue(screen, 'kDistortionStride'),
        OrblitDistortion.stride,
      );
      expect(
        _nativeValue(screen, 'kDistortionCapacity'),
        OrblitDistortion.capacity,
      );
    });

    test('agree on what the numbers mean', () {
      // An effect index out of step runs the wrong shader over the frame;
      // a kind out of step bends it the wrong way.
      expect(_nativeInt(screen, 'kEffectGodRays'), OrblitEffect.godRays.index);
      expect(
        _nativeInt(screen, 'kEffectDistortion'),
        OrblitEffect.distortion.index,
      );
      expect(
        _nativeInt(screen, 'kDistortionShockwave'),
        OrblitDistortionKind.shockwave.index,
      );
      expect(
        _nativeInt(screen, 'kDistortionHaze'),
        OrblitDistortionKind.haze.index,
      );
      expect(
        _nativeInt(screen, 'kDistortionLens'),
        OrblitDistortionKind.lens.index,
      );
    });
  });
}

/// `constexpr int kName = 6;`
int _nativeInt(String source, String name) {
  final found = RegExp(
    r'constexpr int ' + name + r'\s*=\s*(\d+)',
  ).firstMatch(source);
  expect(found, isNotNull, reason: 'the renderer no longer declares $name');
  return int.parse(found!.group(1)!);
}

/// `private static let name = 12`, whatever the access level.
int _swiftValue(String source, String name) {
  final found = RegExp(
    r'static let ' + name + r'\s*=\s*(\d+)',
  ).firstMatch(source);
  expect(found, isNotNull, reason: 'the plugin no longer declares $name');
  return int.parse(found!.group(1)!);
}

/// `constexpr size_t kName = 12;`
int _nativeValue(String source, String name) {
  final found = RegExp(
    r'constexpr size_t ' + name + r'\s*=\s*(\d+)',
  ).firstMatch(source);
  expect(found, isNotNull, reason: 'the renderer no longer declares $name');
  return int.parse(found!.group(1)!);
}

/// The native source, wherever the test was run from.
String _read(String withinPackage) {
  for (final root in ['.', 'packages/orblit_filament']) {
    final file = File('$root/$withinPackage');
    if (file.existsSync()) return file.readAsStringSync();
  }
  // Deliberately not a skip. A guard that quietly stands down when it cannot
  // find what it guards is worse than no guard, because it reports green.
  fail(
    'cannot find $withinPackage from ${Directory.current.path} — this test '
    'reads the native sources and has to be run where it can see them',
  );
}
