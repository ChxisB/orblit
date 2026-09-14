import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';

void main() {
  group('motion blur', () {
    test('is off unless a graph asks for it', () {
      // Nothing about a scene with no graph mentions it, so the frame a host
      // has always drawn is the frame it still draws.
      expect(OrblitRenderGraph.standard().passes.map((p) => p.effect), [null]);
    });

    test(
      'its graph is the world into a target and the blur onto the screen',
      () {
        final graph = const OrblitMotionBlur().graph();
        expect(graph.problems, isEmpty);
        final order = graph.schedule;
        expect(order.map((p) => p.name), ['world', 'motion blur']);
        expect(order.last.effect, OrblitEffect.motionBlur);
        expect(order.last.into, isNull, reason: 'the blur draws the frame');
        expect(order.last.reads, ['frame']);
      },
    );

    test('its four dials cross in the order the renderer reads them', () {
      final packed = const OrblitMotionBlur(
        shutter: 1 / 30,
        maxPixels: 32,
        objects: false,
        samples: 9,
      ).graph().packedPasses;
      const at = OrblitRenderGraph.passStride;
      expect(packed[at + 8], closeTo(1 / 30, 1e-6), reason: 'shutter');
      expect(packed[at + 9], 32, reason: 'the clamp in pixels');
      expect(packed[at + 10], -1, reason: 'camera only');
      expect(packed[at + 11], 9, reason: 'taps');
      expect(packed[at + 12], OrblitEffect.motionBlur.index);
    });

    test(
      'a shutter left unset follows the camera, and says so with nought',
      () {
        const blur = OrblitMotionBlur();
        expect(blur.dials.first, 0);
        expect(blur.dials[2], 1, reason: 'objects blur by default');
      },
    );

    test('a streak is speed times the time the shutter is open', () {
      // Something crossing at 600 pixels a second: a thousandth of a second
      // barely moves it, a thirtieth smears it twenty pixels, and the clamp
      // stops a fast pan from turning the picture into its own average.
      const blur = OrblitMotionBlur();
      expect(blur.streak(600, cameraShutter: 1 / 1000), closeTo(0.6, 1e-9));
      expect(blur.streak(600, cameraShutter: 1 / 30), closeTo(20, 1e-9));
      expect(blur.streak(6000, cameraShutter: 1 / 30), 40);
      expect(
        const OrblitMotionBlur(shutter: 1 / 60).streak(600, cameraShutter: 1),
        closeTo(10, 1e-9),
        reason: 'a shutter of its own overrides the camera',
      );
    });

    test('a target it reads has to keep its depth', () {
      final graph = OrblitRenderGraph(
        targets: const [OrblitTarget(name: 'flat', depth: false)],
        passes: [
          const OrblitPass(name: 'world', into: 'flat'),
          const OrblitMotionBlur().pass(reads: 'flat'),
        ],
      );
      expect(graph.problems.map((p) => p.what).join(' '), contains('depth'));
    });

    test('the renderer numbers the effect the way Dart does', () {
      // The effect crosses as its index, and the renderer switches on a
      // constant of its own. Appended rather than inserted, so every effect
      // before it keeps its number — and checked, because an index that
      // drifts runs a different shader rather than failing.
      final native = _read(
        'darwin/orblit_filament/Sources/orblit_filament_native/OrblitRendererCore.h',
      );
      final found = RegExp(
        r'constexpr int kEffectMotionBlur\s*=\s*(\d+);',
      ).firstMatch(native);
      expect(found, isNotNull);
      expect(int.parse(found!.group(1)!), OrblitEffect.motionBlur.index);
    });

    test('the blur binds its samplers before it can give up on a frame', () {
      // The resolve and tile passes keep a material instance each for as long
      // as the effect is wanted, and those instances name textures neither of
      // them owns for long: the graph's depth, which the renderer destroys
      // the moment the graph changes, and the blur's own targets, which go
      // whenever the picture changes size. A frame with nothing moving leaves
      // prepare() early — which is exactly the state just after a host
      // switches scenes — so samplers bound only beside their draws are left
      // naming a texture Filament has already destroyed, and the next frame
      // that does draw them binds a freed handle. Filament ends the process
      // for that, and the gallery went down on it.
      final native = _read(
        'darwin/orblit_filament/Sources/orblit_filament_native/OrblitMotionBlur.cpp',
      );
      final at = native.indexOf('void MotionBlur::prepare(');
      expect(at, greaterThan(0), reason: 'prepare is still where it was');
      final prepare = native.substring(at);

      // Where it decides there is nothing to blur and returns.
      final givesUp = prepare.indexOf(
        'if (cameraScale <= 0.0 && !drewObjects)',
      );
      expect(givesUp, greaterThan(0), reason: 'prepare still returns early');

      for (final binding in const [
        'resolve.setParameter("objects"',
        'tiles.setParameter("velocity"',
      ]) {
        final bound = prepare.indexOf(binding);
        expect(
          bound,
          greaterThan(0),
          reason: '$binding) has to be there at all',
        );
        expect(
          bound,
          lessThan(givesUp),
          reason:
              '$binding) has to be bound before prepare can return, or the '
              'instance is left holding a destroyed texture',
        );
      }
    });
  });
}

String _read(String withinPackage) {
  for (final root in ['.', 'packages/orblit_filament']) {
    final file = File('$root/$withinPackage');
    if (file.existsSync()) return file.readAsStringSync();
  }
  fail('cannot find $withinPackage from ${Directory.current.path}');
}
