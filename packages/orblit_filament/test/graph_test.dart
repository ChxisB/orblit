import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';

void main() {
  group('the bounce pass', () {
    OrblitRenderGraph graphWith(List<double>? dials) => OrblitRenderGraph(
      targets: const [OrblitTarget(name: 'frame')],
      passes: [
        const OrblitPass(name: 'world', into: 'frame'),
        OrblitPass(
          name: 'bounce',
          kind: OrblitPassKind.effect,
          effect: OrblitEffect.bounce,
          reads: const ['frame'],
          plane: dials,
        ),
      ],
    );

    test('it reads one target and gets both the picture and the depth', () {
      // The colour and the depth of a target are two halves of one thing, so
      // a graph names it once. Making a host list it twice is how the two end
      // up naming different targets.
      final graph = graphWith(null);
      expect(graph.problems, isEmpty);
      final pass = graph.schedule.last;
      expect(pass.reads, ['frame']);
      expect(pass.effect, OrblitEffect.bounce);
    });

    test('a target it reads has to keep its depth', () {
      // Colour alone is a target the march cannot use: without depth there is
      // no telling what is in front of what, and the pass would gather light
      // from surfaces on the far side of a wall.
      final graph = OrblitRenderGraph(
        targets: const [OrblitTarget(name: 'flat', depth: false)],
        passes: [
          const OrblitPass(name: 'world', into: 'flat'),
          const OrblitPass(
            name: 'bounce',
            kind: OrblitPassKind.effect,
            effect: OrblitEffect.bounce,
            reads: ['flat'],
          ),
        ],
      );
      expect(
        graph.problems.map((p) => p.what).join(' '),
        contains('depth'),
        reason: 'a bounce over a target with no depth should be reported',
      );
    });

    test('its four dials cross in the order the renderer reads them', () {
      final packed = graphWith([3, 6, 0.4, 8]).packedPasses;
      final at = OrblitRenderGraph.passStride;
      expect(packed[at + 8], 3, reason: 'reach');
      expect(packed[at + 9], 6, reason: 'strength');
      expect(packed[at + 10], closeTo(0.4, 1e-6), reason: 'thickness');
      expect(packed[at + 11], 8, reason: 'directions');
      expect(packed[at + 12], OrblitEffect.bounce.index);
    });
  });

  _effectTests();
  group('the default graph', () {
    test('is the frame as it was before there was a graph', () {
      final graph = OrblitRenderGraph.standard();

      expect(graph.isRunnable, isTrue);
      expect(graph.schedule.map((pass) => pass.name), ['scene']);
      expect(graph.schedule.single.into, isNull);
    });

    test('an empty graph is not a broken one', () {
      // A host that never mentions a graph is not a host with a wrong graph.
      const graph = OrblitRenderGraph();
      expect(graph.problems, isEmpty);
      expect(graph.schedule, isEmpty);
    });
  });

  group('scheduling', () {
    final reflection = OrblitRenderGraph(
      targets: const [OrblitTarget(name: 'mirror', scale: 0.5)],
      passes: const [
        // Declared out of order on purpose: the frame first, then the thing
        // it needs. An order kept by hand would draw the mirror empty.
        OrblitPass(name: 'frame', reads: ['mirror']),
        OrblitPass(
          name: 'water',
          kind: OrblitPassKind.reflection,
          into: 'mirror',
          plane: [0, 1, 0, 0],
        ),
      ],
    );

    test('a target is written before it is read', () {
      expect(reflection.isRunnable, isTrue);
      expect(reflection.schedule.map((pass) => pass.name), ['water', 'frame']);
    });

    test('the frame is last whatever else there is', () {
      final graph = reflection
          .withTarget(const OrblitTarget(name: 'monitor'))
          .with_(const OrblitPass(name: 'prepass', into: 'monitor'));

      expect(graph.schedule.last.name, 'frame');
      expect(graph.schedule.map((pass) => pass.name), contains('prepass'));
    });

    test('passes that do not depend on each other keep their order', () {
      // Two independent passes have no correct order, so the one somebody
      // wrote down is the one a capture should show.
      final graph = OrblitRenderGraph(
        targets: const [
          OrblitTarget(name: 'a'),
          OrblitTarget(name: 'b'),
        ],
        passes: const [
          OrblitPass(name: 'second', into: 'b'),
          OrblitPass(name: 'first', into: 'a'),
          OrblitPass(name: 'frame', reads: ['a', 'b']),
        ],
      );

      expect(graph.schedule.map((pass) => pass.name), [
        'second',
        'first',
        'frame',
      ]);
    });

    test('a chain of three is ordered end to end', () {
      final graph = OrblitRenderGraph(
        targets: const [
          OrblitTarget(name: 'one'),
          OrblitTarget(name: 'two'),
        ],
        passes: const [
          OrblitPass(name: 'frame', reads: ['two']),
          OrblitPass(name: 'middle', into: 'two', reads: ['one']),
          OrblitPass(name: 'first', into: 'one'),
        ],
      );

      expect(graph.schedule.map((pass) => pass.name), [
        'first',
        'middle',
        'frame',
      ]);
    });

    test('a pass switched off is skipped, not scheduled around', () {
      final graph = OrblitRenderGraph(
        targets: const [OrblitTarget(name: 'mirror')],
        passes: const [
          OrblitPass(
            name: 'water',
            into: 'mirror',
            enabled: false,
            kind: OrblitPassKind.reflection,
            plane: [0, 1, 0, 0],
          ),
          OrblitPass(name: 'frame', reads: ['mirror']),
        ],
      );

      // The frame still draws. Whatever the mirror held last is what it
      // samples, which is the honest behaviour for a pass somebody turned off.
      expect(graph.schedule.map((pass) => pass.name), ['frame']);
      expect(graph.problems, isEmpty);
    });
  });

  group('what a graph gets wrong', () {
    test('two passes cannot both draw the frame', () {
      const graph = OrblitRenderGraph(
        passes: [
          OrblitPass(name: 'one'),
          OrblitPass(name: 'two'),
        ],
      );

      expect(
        graph.problems.map((problem) => problem.what).join(),
        contains('overwrite'),
      );
      // Nothing runs: a graph the renderer cannot make sense of should draw
      // what it drew last rather than half of a new idea.
      expect(graph.schedule, isEmpty);
    });

    test('a graph with no frame pass says so', () {
      const graph = OrblitRenderGraph(
        targets: [OrblitTarget(name: 'a')],
        passes: [OrblitPass(name: 'only', into: 'a')],
      );
      expect(
        graph.problems.map((problem) => problem.what).join(),
        contains('no pass draws the frame'),
      );
    });

    test('a target nobody declared is named, not guessed at', () {
      const graph = OrblitRenderGraph(
        passes: [
          OrblitPass(name: 'frame', reads: ['nowhere']),
        ],
      );

      expect(graph.problems.single.pass, 'frame');
      expect(graph.problems.single.what, contains('nowhere'));
    });

    test('two passes cannot write the same target', () {
      const graph = OrblitRenderGraph(
        targets: [OrblitTarget(name: 'a')],
        passes: [
          OrblitPass(name: 'one', into: 'a'),
          OrblitPass(name: 'two', into: 'a'),
          OrblitPass(name: 'frame', reads: ['a']),
        ],
      );
      expect(
        graph.problems.map((problem) => problem.what).join(),
        contains('already writes'),
      );
    });

    test('a pass cannot read what it writes', () {
      const graph = OrblitRenderGraph(
        targets: [OrblitTarget(name: 'a')],
        passes: [
          OrblitPass(name: 'itself', into: 'a', reads: ['a']),
          OrblitPass(name: 'frame'),
        ],
      );
      expect(
        graph.problems.map((problem) => problem.what).join(),
        contains('reads the target it writes'),
      );
    });

    test('two passes waiting on each other are named', () {
      const graph = OrblitRenderGraph(
        targets: [
          OrblitTarget(name: 'a'),
          OrblitTarget(name: 'b'),
        ],
        passes: [
          OrblitPass(name: 'one', into: 'a', reads: ['b']),
          OrblitPass(name: 'two', into: 'b', reads: ['a']),
          OrblitPass(name: 'frame'),
        ],
      );

      final waiting = graph.problems
          .where((problem) => problem.what.contains('waits on'))
          .map((problem) => problem.pass);
      expect(waiting, containsAll(['one', 'two']));
    });

    test('a reflection with no plane is not a reflection', () {
      const graph = OrblitRenderGraph(
        targets: [OrblitTarget(name: 'mirror')],
        passes: [
          OrblitPass(
            name: 'water',
            kind: OrblitPassKind.reflection,
            into: 'mirror',
          ),
          OrblitPass(name: 'frame', reads: ['mirror']),
        ],
      );
      expect(graph.problems.single.what, contains('no plane'));
    });

    test('a graph that has run away is refused rather than allocated', () {
      final graph = OrblitRenderGraph(
        passes: [
          for (var i = 0; i < OrblitRenderGraph.maxPasses + 1; i++)
            OrblitPass(name: 'pass$i'),
        ],
      );
      expect(graph.problems.first.what, contains('more than'));
    });

    test('a target nothing reads is worth saying, and is not an error', () {
      const graph = OrblitRenderGraph(
        targets: [OrblitTarget(name: 'thumbnail')],
        passes: [
          OrblitPass(name: 'shot', into: 'thumbnail'),
          OrblitPass(name: 'frame'),
        ],
      );

      expect(graph.problems, isEmpty);
      expect(graph.unreadTargets, ['thumbnail']);
    });
  });

  group('on the wire', () {
    final graph = OrblitRenderGraph(
      targets: const [
        OrblitTarget(name: 'mirror', scale: 0.5, colour: true),
        OrblitTarget(name: 'shadowless', depth: true, colour: false),
      ],
      passes: const [
        OrblitPass(name: 'frame', reads: ['mirror', 'shadowless'], layers: 0x0F),
        OrblitPass(name: 'prepass', into: 'shadowless'),
        OrblitPass(
          name: 'water',
          kind: OrblitPassKind.reflection,
          into: 'mirror',
          plane: [0, 1, 0, -2],
        ),
      ],
    );

    test('passes cross in the order they run', () {
      final packed = graph.packedPasses;
      expect(packed.length, 3 * OrblitRenderGraph.passStride);

      // Targets travel as indices. The names are for people; matching strings
      // sixty times a second is matching the same strings sixty times.
      const stride = OrblitRenderGraph.passStride;
      expect(packed[0 * stride + 1], 1); // prepass writes target 1
      expect(packed[1 * stride + 1], 0); // water writes target 0
      expect(packed[2 * stride + 1], -1); // the frame writes no target
    });

    test('a pass carries its reads, its layers and its plane', () {
      final packed = graph.packedPasses;
      const stride = OrblitRenderGraph.passStride;
      final frame = 2 * stride;

      expect(packed[frame + 2], 0x0F);
      expect(packed[frame + 4], 0); // reads mirror
      expect(packed[frame + 5], 1); // and depth
      expect(packed[frame + 6], -1); // and nothing else

      final water = 1 * stride;
      expect(packed[water + 8 + 1], 1); // the plane's normal
      expect(packed[water + 8 + 3], -2); // and its distance
    });

    test('targets carry their size and what they keep', () {
      final packed = graph.packedTargets;
      const stride = OrblitRenderGraph.targetStride;

      expect(packed.length, 2 * stride);
      expect(packed[0 * stride + 2], 0.5);
      expect(packed[1 * stride + 3], 1); // depth keeps depth
      expect(packed[1 * stride + 4], 0); // and no colour
    });
  });

  group('a capture', () {
    test('says what ran and what it cost', () {
      final capture = OrblitFrameCapture.from(
        Float32List.fromList([0.4, 120, 2.6, 4300, 0.2, 8]),
        ['prepass', 'scene', 'outline'],
      );

      expect(capture.passes.map((pass) => pass.name), [
        'prepass',
        'scene',
        'outline',
      ]);
      expect(capture.milliseconds, closeTo(3.2, 0.0001));
      expect(capture.draws, 4428);
      expect(capture.slowest!.name, 'scene');
    });

    test(
      'a frame the renderer has not reported on yet is empty, not wrong',
      () {
        final capture = OrblitFrameCapture.from(Float32List(0), ['scene']);
        expect(capture.passes, isEmpty);
        expect(capture.slowest, isNull);
        expect(capture.milliseconds, 0);
      },
    );
  });
}

void _effectTests() {
  group('effect passes on the wire', () {
    test('an effect names itself; every other kind says none', () {
      final graph = OrblitRenderGraph(
        targets: const [OrblitTarget(name: 'frame')],
        passes: const [
          OrblitPass(name: 'world', into: 'frame'),
          OrblitPass(
            name: 'sharpen',
            kind: OrblitPassKind.effect,
            effect: OrblitEffect.sharpen,
            reads: ['frame'],
          ),
        ],
      );

      final packed = graph.packedPasses;
      const stride = OrblitRenderGraph.passStride;

      // A scene pass has no effect, and must say so rather than leaving the
      // slot at zero — zero is a real effect.
      expect(packed[12], -1);
      expect(packed[stride + 12], OrblitEffect.sharpen.index);
      expect(packed[stride], OrblitPassKind.effect.index);
    });

    test('an effect reads the target the scene wrote', () {
      final graph = OrblitRenderGraph(
        targets: const [OrblitTarget(name: 'frame')],
        passes: const [
          OrblitPass(name: 'world', into: 'frame'),
          OrblitPass(
            name: 'sharpen',
            kind: OrblitPassKind.effect,
            effect: OrblitEffect.sharpen,
            reads: ['frame'],
          ),
        ],
      );
      const stride = OrblitRenderGraph.passStride;
      // First read is target 0; the rest are empty.
      expect(graph.packedPasses[stride + 4], 0);
      expect(graph.packedPasses[stride + 5], -1);
    });

    test('the effect survives a copyWith', () {
      const pass = OrblitPass(
        name: 'sharpen',
        kind: OrblitPassKind.effect,
        effect: OrblitEffect.sharpen,
      );
      expect(pass.copyWith(name: 'other').effect, OrblitEffect.sharpen);
    });
  });
}
