import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_ui/orblit_ui.dart';

/// The width, height and RGBA bytes of a PNG.
Future<({int width, int height, Uint8List rgba})> decode(Uint8List png) async {
  final codec = await ui.instantiateImageCodec(png);
  final frame = await codec.getNextFrame();
  final data = await frame.image.toByteData();
  final out = (
    width: frame.image.width,
    height: frame.image.height,
    rgba: data!.buffer.asUint8List(),
  );
  frame.image.dispose();
  codec.dispose();
  return out;
}

List<int> pixel(({int width, int height, Uint8List rgba}) image, int x, int y) {
  final at = (y * image.width + x) * 4;
  return image.rgba.sublist(at, at + 4);
}

void main() {
  group('a panel draws without a window', () {
    testWidgets('the picture is as big as the size times the ratio', (
      tester,
    ) async {
      final panel = UiPanel(
        size: const Size(64, 32),
        pixelRatio: 2,
        child: const ColoredBox(color: Color(0xFFFF0000)),
      );
      addTearDown(panel.dispose);

      final image = await tester.runAsync(() async {
        return decode(await panel.paint());
      });

      expect(image!.width, 128);
      expect(image.height, 64);
      expect(pixel(image, 10, 10), [255, 0, 0, 255]);
      expect(pixel(image, 127, 63), [255, 0, 0, 255]);
    });

    testWidgets('a description from the interface layer draws', (tester) async {
      final panel = UiPanel(
        size: const Size(40, 40),
        child: const UiSurface(
          width: 40,
          description: UiNode(type: 'box', css: 'background-color: #00ff00'),
        ),
      );
      addTearDown(panel.dispose);

      final image = await tester.runAsync(() async {
        return decode(await panel.paint());
      });

      expect(pixel(image!, 20, 20), [0, 255, 0, 255]);
    });

    testWidgets('a change marks it out of date until it is drawn again', (
      tester,
    ) async {
      final panel = UiPanel(
        size: const Size(8, 8),
        child: const ColoredBox(color: Color(0xFFFF0000)),
      );
      addTearDown(panel.dispose);
      var told = 0;
      panel.addListener(() => told++);

      await tester.runAsync(panel.paint);
      expect(panel.isDirty, isFalse);

      panel.update(const ColoredBox(color: Color(0xFF0000FF)));
      expect(panel.isDirty, isTrue);
      expect(told, greaterThan(0));

      final image = await tester.runAsync(() async {
        return decode(await panel.paint());
      });
      expect(pixel(image!, 4, 4), [0, 0, 255, 255]);
      expect(panel.isDirty, isFalse);
    });
  });

  group('a panel takes pointers in its own pixels', () {
    testWidgets('a press and release on a button is its event', (tester) async {
      final events = <String>[];
      final panel = UiPanel(
        size: const Size(100, 50),
        child: UiSurface(
          width: 100,
          onEvent: (handler, payload) => events.add(handler),
          description: const UiNode(
            type: 'button',
            props: {'onPressed': 'start'},
            classes: 'w-full h-full',
            children: [UiNode(type: 'box', classes: 'w-full h-full')],
          ),
        ),
      );
      addTearDown(panel.dispose);
      await tester.runAsync(panel.paint);

      panel.send(const PointerDownEvent(position: Offset(50, 25)));
      expect(events, isEmpty, reason: 'a tap completes on release');
      panel.send(const PointerUpEvent(position: Offset(50, 25)));

      expect(events, ['start']);
    });

    testWidgets('a press that lands on nothing does nothing', (tester) async {
      final events = <String>[];
      final panel = UiPanel(
        size: const Size(100, 50),
        child: Align(
          alignment: Alignment.topLeft,
          child: SizedBox(
            width: 20,
            height: 20,
            child: GestureDetector(onTap: () => events.add('tap')),
          ),
        ),
      );
      addTearDown(panel.dispose);
      await tester.runAsync(panel.paint);

      panel
        ..send(const PointerDownEvent(position: Offset(90, 40)))
        ..send(const PointerUpEvent(position: Offset(90, 40)));

      expect(events, isEmpty);
    });

    testWidgets('a drag keeps reporting after it leaves the panel', (
      tester,
    ) async {
      final deltas = <double>[];
      final panel = UiPanel(
        size: const Size(100, 50),
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onHorizontalDragUpdate: (details) => deltas.add(details.delta.dx),
        ),
      );
      addTearDown(panel.dispose);
      await tester.runAsync(panel.paint);

      panel
        ..send(const PointerDownEvent(position: Offset(10, 25)))
        ..send(const PointerMoveEvent(position: Offset(40, 25)))
        ..send(const PointerMoveEvent(position: Offset(70, 25)))
        // Past the right edge, as a drag on a wall goes when the pointer
        // strays off it.
        ..send(const PointerMoveEvent(position: Offset(160, 25)))
        ..send(const PointerUpEvent(position: Offset(160, 25)));

      expect(deltas.fold<double>(0, (a, b) => a + b), greaterThan(100));
      expect(deltas.last, 90);
    });
  });
}
