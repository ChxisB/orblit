import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_ui/orblit_ui.dart';

import 'png.dart';

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

  testWidgets('taking a panel down tells nobody to draw it', (tester) async {
    final panel = UiPanel(
      size: const Size(8, 8),
      child: const ColoredBox(color: Color(0xFFFF0000)),
    );
    await tester.runAsync(panel.paint);
    var told = 0;
    panel.addListener(() => told++);

    panel.dispose();

    expect(told, 0);
  });

  testWidgets('a pointer after taking a panel down does nothing', (
    tester,
  ) async {
    var taps = 0;
    final panel = UiPanel(
      size: const Size(8, 8),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: () => taps++,
      ),
    );
    await tester.runAsync(panel.paint);
    panel.send(const PointerDownEvent(position: Offset(4, 4)));
    panel.dispose();

    expect(
      () => panel
        ..send(const PointerUpEvent(position: Offset(4, 4)))
        ..send(const PointerDownEvent(position: Offset(4, 4))),
      returnsNormally,
    );
    expect(taps, 0);
  });

  group('a panel takes pointers in its own pixels', () {
    testWidgets('a slider has an overlay and follows a drag', (tester) async {
      var value = 0.5;
      final panel = UiPanel(
        size: const Size(200, 40),
        child: Slider(value: value, onChanged: (v) => value = v),
      );
      addTearDown(panel.dispose);
      await tester.runAsync(panel.paint);

      panel
        ..send(const PointerDownEvent(position: Offset(100, 20)))
        ..send(const PointerMoveEvent(position: Offset(180, 20)))
        ..send(const PointerUpEvent(position: Offset(180, 20)));

      expect(value, greaterThan(0.8));
    });

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

    testWidgets('a press leaves the real pointer with the same number alone', (
      tester,
    ) async {
      final panel = UiPanel(
        size: const Size(100, 50),
        child: GestureDetector(behavior: HitTestBehavior.opaque, onTap: () {}),
      );
      addTearDown(panel.dispose);
      await tester.runAsync(panel.paint);
      final arena = GestureBinding.instance.gestureArena;
      arena.add(1, _Member());

      panel.send(const PointerDownEvent(position: Offset(5, 5), pointer: 1));

      // The arena of a real pointer is still open, so it takes a member. A
      // panel press that shared its number would have closed it.
      expect(() => arena.add(1, _Member()), returnsNormally);
      panel.send(const PointerUpEvent(position: Offset(5, 5), pointer: 1));
      arena
        ..close(1)
        ..sweep(1);
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

final class _Member extends GestureArenaMember {
  @override
  void acceptGesture(int pointer) {}

  @override
  void rejectGesture(int pointer) {}
}
