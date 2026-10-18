import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/orblit_examples.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

const Size _view = Size(800, 450);

/// Looking straight at the middle of the screen on the wall.
final OrblitCamera _front = OrblitCamera(
  position: Vector3(0, 2.25, 8),
  target: Vector3(0, 2.25, -2.49),
  fieldOfView: 50,
);

/// Where a point of the 800 by 450 screen shows in the view.
///
/// The camera is square to the wall 10.49 metres off, so a metre on the wall
/// is a fixed number of view pixels.
Offset onScreen(double x, double y) {
  const pixelsPerMetre = 225 / (10.49 * 0.4663077);
  return Offset(
    400 + (x - 400) / 800 * 6 * pixelsPerMetre,
    225 - (225 - y) / 450 * 3.375 * pixelsPerMetre,
  );
}

String nameOf(MethodCall call) => (call.arguments as Map)['name'] as String;

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late List<MethodCall> calls;
  late WorldUiExample example;

  setUp(() {
    calls = [];
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          const MethodChannel('orblit_filament/resources'),
          (call) async {
            calls.add(call);
            return call.method == 'release' ? true : null;
          },
        );
    example = WorldUiExample();
  });

  // The example's queue is made from whatever zone calls it. In a test body
  // that is the fake one, where a picture never lands until the test ends, so
  // everything that asks for a picture runs here.
  Future<void> real(WidgetTester tester, void Function() body) async {
    await tester.runAsync(() async {
      body();
      await example.idle;
    });
  }

  Future<void> open(WidgetTester tester) =>
      real(tester, () => example.scene(_front, 0));

  // A press leaves the button's ink running, and the framework fails a test
  // that ends with a ticker left behind. It checks before any tear-down runs.
  void exampleTest(String name, Future<void> Function(WidgetTester) body) {
    testWidgets(name, (tester) async {
      try {
        await body(tester);
      } finally {
        await tester.runAsync(example.close);
      }
    });
  }

  group('the screen on the wall', () {
    exampleTest('a click on it presses the button and draws a new picture', (
      tester,
    ) async {
      await open(tester);
      final first = calls.where((c) => c.method == 'provide').length;

      final middle = onScreen(400, 225);
      expect(example.hits(middle, _view), isTrue);
      await real(tester, () {
        example
          ..down(middle, _view)
          ..up(middle, _view);
      });

      expect(example.presses, 1);
      expect(calls.where((c) => c.method == 'provide').length, first + 1);
    });

    exampleTest('a click beside it does nothing', (tester) async {
      await open(tester);

      await real(tester, () {
        example
          ..down(const Offset(5, 5), _view)
          ..up(const Offset(5, 5), _view);
      });

      expect(example.presses, 0);
    });

    exampleTest('a drag along the slider moves it and a drag back undoes it', (
      tester,
    ) async {
      await open(tester);

      await real(tester, () {
        example.down(onScreen(400, 386), _view);
        for (var x = 440.0; x <= 700; x += 40) {
          example.move(onScreen(x, 386), _view);
        }
        example.up(onScreen(700, 386), _view);
      });
      final right = example.level;
      await real(tester, () {
        example
          ..down(onScreen(700, 386), _view)
          ..move(onScreen(360, 386), _view)
          ..up(onScreen(360, 386), _view);
      });

      expect(right, greaterThan(0.7));
      expect(example.level, lessThan(0.2));
    });
  });

  group('the overlay', () {
    Future<List<Offset>> dragFrom(WidgetTester tester, Offset start) async {
      final orbit = <Offset>[];
      await open(tester);
      await tester.pumpWidget(
        Directionality(
          textDirection: TextDirection.ltr,
          child: Align(
            alignment: Alignment.topLeft,
            child: SizedBox.fromSize(
              size: _view,
              child: Stack(
                children: [
                  GestureDetector(
                    behavior: HitTestBehavior.opaque,
                    onPanUpdate: (details) => orbit.add(details.delta),
                  ),
                  Positioned.fill(
                    child: Builder(
                      builder: (context) => example.overlay(context, () {})!,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
      await tester.dragFrom(start, const Offset(120, 0));
      return orbit;
    }

    exampleTest('a drag that starts on the screen does not orbit', (
      tester,
    ) async {
      // On the title, which does not change, because a redraw asked for from
      // the test body would wait for a zone the test is not in.
      expect(await dragFrom(tester, onScreen(200, 70)), isEmpty);
    });

    exampleTest('a drag that starts beside it orbits', (tester) async {
      expect(await dragFrom(tester, const Offset(10, 10)), isNotEmpty);
    });
  });

  group('the names', () {
    exampleTest('two hundred are in the scene once their picture is', (
      tester,
    ) async {
      var before = 0;
      await real(
        tester,
        () => before = example.scene(_front, 0).objects.length,
      );
      final after = example.scene(_front, 0).objects.length;

      expect(after - before, greaterThan(100));
    });

    exampleTest('none when there are none', (tester) async {
      example.labels = 0;
      await open(tester);

      final scene = example.scene(_front, 0);

      // The floor, the wall, the screen and the crowd.
      expect(scene.objects.length, 3 + 200);
    });
  });

  group('the cost, headless and on the test machine', () {
    exampleTest('a frame of names, a press and a picture of two hundred', (
      tester,
    ) async {
      await open(tester);

      final frame = Stopwatch()..start();
      const frames = 200;
      for (var i = 0; i < frames; i++) {
        example.scene(_front, i / 60);
      }
      frame.stop();

      final press = Stopwatch();
      final middle = onScreen(400, 225);
      await tester.runAsync(() async {
        press.start();
        example
          ..down(middle, _view)
          ..up(middle, _view);
        await example.idle;
        press.stop();
      });

      final atlas = LabelAtlas();
      final plates = Nameplates(atlas: atlas, fadeStart: 6, fadeEnd: 14);
      final drawing = Stopwatch();
      await tester.runAsync(() async {
        drawing.start();
        await plates.sync([for (var i = 0; i < 200; i++) 'Name $i']);
        drawing.stop();
      });
      final build = Stopwatch()..start();
      for (var i = 0; i < frames; i++) {
        plates.build(_front, [
          for (var j = 0; j < 200; j++)
            Nameplate(id: j, text: 'Name $j', position: Vector3(j * 0.1, 1, 0)),
        ]);
      }
      build.stop();

      // ignore: avoid_print, the numbers are what this test is for
      print(
        'cost: scene ${(frame.elapsedMicroseconds / frames).round()} us, '
        'names build ${(build.elapsedMicroseconds / frames).round()} us, '
        'press to picture ${press.elapsedMilliseconds} ms, '
        '200 strings to picture ${drawing.elapsedMilliseconds} ms '
        '(${atlas.size.width.round()}x${atlas.size.height.round()})',
      );

      // Loose, so only a change of an order of magnitude fails it.
      expect(frame.elapsedMilliseconds / frames, lessThan(25));
      expect(build.elapsedMilliseconds / frames, lessThan(25));
      expect(press.elapsedMilliseconds, lessThan(1000));
      expect(drawing.elapsedMilliseconds, lessThan(2000));
    });
  });
}
