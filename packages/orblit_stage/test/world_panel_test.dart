import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart';

/// A ray from [from] to the point [local] on a quad placed by [transform].
/// A quad's own space runs from -1 to 1, with its front face at z = 1.
PointerRay rayTo(Matrix4 transform, Vector3 from, Vector3 local) {
  final target = transform.transformed3(local);
  return PointerRay(from, (target - from)..normalize());
}

/// The name a call to the resource channel carries.
String nameOf(MethodCall call) => (call.arguments as Map)['name'] as String;

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  // A 200 by 100 panel on a wall: turned to face along +x, two metres out
  // sideways and twice as wide as it is tall.
  final transform = Matrix4.identity()
    ..setTranslation(Vector3(2, 1, 0))
    ..rotateY(pi / 2)
    ..scaleByVector3(Vector3(2, 1, 0.05));
  final eye = Vector3(10, 1, 0);

  late List<MethodCall> calls;
  late UiPanel panel;

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
  });

  WorldPanel build(Widget child) {
    panel = UiPanel(child: child, size: const Size(200, 100));
    addTearDown(panel.dispose);
    return WorldPanel(
      id: 'wall',
      panel: panel,
      transform: transform,
      objectKey: 7,
      materialKey: 8,
    );
  }

  group('a ray finds a pixel on the panel', () {
    test('the middle of the face is the middle of the picture', () {
      final wall = build(const SizedBox());

      final pixel = wall.pixelAt(rayTo(transform, eye, Vector3(0, 0, 1)));

      expect(pixel!.dx, closeTo(100, 1e-6));
      expect(pixel.dy, closeTo(50, 1e-6));
    });

    test('up on the face is up in the picture', () {
      final wall = build(const SizedBox());

      final pixel = wall.pixelAt(rayTo(transform, eye, Vector3(0.5, 0.5, 1)));

      expect(pixel!.dx, closeTo(150, 1e-6));
      expect(pixel.dy, closeTo(25, 1e-6));
    });

    test('a ray that lands off the face misses', () {
      final wall = build(const SizedBox());

      final ray = rayTo(transform, eye, Vector3(1.5, 0, 1));

      expect(wall.hits(ray), isFalse);
    });

    test('a ray from behind the face misses', () {
      final wall = build(const SizedBox());

      final ray = rayTo(transform, Vector3(-6, 1, 0), Vector3(0, 0, 1));

      expect(wall.hits(ray), isFalse);
    });

    test('a ray that starts behind the face and runs away misses', () {
      final wall = build(const SizedBox());

      // The face is at x = 2.05 looking out along +x. This ray is at x = -6
      // going further back, so the plane is behind it.
      final ray = PointerRay(Vector3(-6, 1, 0), Vector3(-1, 0, 0));

      expect(wall.hits(ray), isFalse);
    });

    test('a ray running along the face misses', () {
      final wall = build(const SizedBox());

      expect(
        wall.hits(PointerRay(Vector3(10, 1, 0), Vector3(0, 0, 1))),
        isFalse,
      );
    });

    test('moving the panel moves where it is hit', () {
      final wall = build(const SizedBox());
      final ray = rayTo(transform, eye, Vector3(0, 0, 1));

      wall.place(Matrix4.translation(Vector3(0, 50, 0)));

      expect(wall.hits(ray), isFalse);
    });
  });

  group('a panel takes a click and a drag', () {
    testWidgets('a press and release on a button is its event', (tester) async {
      final events = <String>[];
      final wall = build(
        UiSurface(
          width: 200,
          onEvent: (handler, payload) => events.add(handler),
          description: const UiNode(
            type: 'button',
            props: {'onPressed': 'open'},
            classes: 'w-full h-full',
            children: [UiNode(type: 'box', classes: 'w-full h-full')],
          ),
        ),
      );
      await tester.runAsync(wall.sync);
      final ray = rayTo(transform, eye, Vector3(0, 0, 1));

      wall
        ..down(ray)
        ..up(ray);

      expect(events, ['open']);
    });

    testWidgets('a press that misses the face does nothing', (tester) async {
      final events = <String>[];
      final wall = build(
        GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: () => events.add('tap'),
        ),
      );
      await tester.runAsync(wall.sync);
      final off = rayTo(transform, eye, Vector3(1.5, 0, 1));

      wall
        ..down(off)
        ..up(off);

      expect(events, isEmpty);
    });

    testWidgets('a drag keeps reporting after it leaves the face', (
      tester,
    ) async {
      final deltas = <double>[];
      final wall = build(
        GestureDetector(
          behavior: HitTestBehavior.opaque,
          onHorizontalDragUpdate: (details) => deltas.add(details.delta.dx),
        ),
      );
      await tester.runAsync(wall.sync);
      final start = rayTo(transform, eye, Vector3(-0.5, 0, 1));
      final inside = rayTo(transform, eye, Vector3(0.0, 0, 1));
      final outside = rayTo(transform, eye, Vector3(1.8, 0, 1));

      wall
        ..down(start)
        ..move(inside)
        ..move(outside)
        ..up(outside);

      expect(deltas.fold<double>(0, (a, b) => a + b), greaterThan(150));
    });
  });

  group('a changed picture goes to the renderer under a new name', () {
    testWidgets('the first picture is provided and drawn', (tester) async {
      final wall = build(const ColoredBox(color: Color(0xFFFF0000)));

      await tester.runAsync(wall.sync);

      final provided = calls.where((c) => c.method == 'provide').toList();
      expect(provided, hasLength(1));
      final name = nameOf(provided.single);
      expect(name, startsWith('orblit:resource/panel/wall/'));
      expect(name, endsWith('.png'));
      expect(wall.material.baseColourMap!.path, name);
      expect(wall.object.material, 8);
      expect(wall.object.key, 7);
    });

    testWidgets('a change is a new name and the old one is released', (
      tester,
    ) async {
      final wall = build(const ColoredBox(color: Color(0xFFFF0000)));
      await tester.runAsync(wall.sync);
      final first = wall.material.baseColourMap!.path;

      panel.update(const ColoredBox(color: Color(0xFF0000FF)));
      await tester.runAsync(wall.sync);

      final second = wall.material.baseColourMap!.path;
      expect(second, isNot(first));
      final released = calls.where((c) => c.method == 'release').single;
      expect(nameOf(released), first);
    });

    testWidgets('an unchanged panel sends nothing', (tester) async {
      final wall = build(const ColoredBox(color: Color(0xFFFF0000)));
      await tester.runAsync(wall.sync);
      calls.clear();

      await tester.runAsync(wall.sync);

      expect(calls, isEmpty);
    });

    testWidgets('letting go releases the last picture', (tester) async {
      final wall = build(const ColoredBox(color: Color(0xFFFF0000)));
      await tester.runAsync(wall.sync);
      final name = wall.material.baseColourMap!.path;

      await tester.runAsync(wall.dispose);

      final released = calls.where((c) => c.method == 'release').single;
      expect(nameOf(released), name);
    });

    test('before its first picture the quad is invisible', () {
      final wall = build(const SizedBox());

      expect(wall.material.baseColour.w, 0);
      expect(wall.material.baseColourMap, isNull);
    });
  });
}
