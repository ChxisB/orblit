import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_ui/orblit_ui.dart';

// The stick, touch-button and swipe-pad elements. Each sends what it sees
// through onEvent, and a game turns that into controls. The payload shapes
// here are the ones orblit_input's TouchControls reads.

const _size = 'width: 160px; height: 160px';

const _controls = UiNode(
  type: 'row',
  children: [
    UiNode(
      type: 'stick',
      css: '$_size; background: #333333; border-radius: 80px',
      props: {'onMove': 'move'},
    ),
    UiNode(
      type: 'touch-button',
      css: 'padding: 24px; background: #aa3300',
      props: {'onHold': 'jump'},
      children: [UiNode(type: 'text', text: 'Jump')],
    ),
    UiNode(type: 'swipe-pad', css: _size, props: {'onSwipe': 'look'}),
  ],
);

// The widget each element type builds, by name, because the classes are not
// part of the package's public face.
const _widgets = {
  'stick': 'TouchStick',
  'touch-button': 'TouchHold',
  'swipe-pad': 'TouchSwipe',
};

void main() {
  late List<List<Object?>> events;

  Future<void> show(WidgetTester tester, UiNode description) async {
    events = [];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Align(
            alignment: Alignment.topLeft,
            child: UiSurface(
              description: description,
              onEvent: (handler, payload) => events.add([handler, payload]),
            ),
          ),
        ),
      ),
    );
  }

  Offset centre(WidgetTester tester, String type) => tester.getCenter(
    find.byWidgetPredicate((w) => w.runtimeType.toString() == _widgets[type]),
  );

  group('a stick', () {
    testWidgets('sends where the thumb is, up positive, and zero on release', (
      tester,
    ) async {
      await show(tester, _controls);
      final middle = centre(tester, 'stick');

      final thumb = await tester.startGesture(middle + const Offset(40, -20));
      expect(events.last[0], 'move');
      final first = events.last[1]! as List<double>;
      expect(first[0], closeTo(0.5, 1e-9));
      expect(first[1], closeTo(0.25, 1e-9));

      await thumb.moveTo(middle + const Offset(0, 40));
      final second = events.last[1]! as List<double>;
      expect(second[0], closeTo(0, 1e-9));
      expect(second[1], closeTo(-0.5, 1e-9));

      await thumb.up();
      expect(events.last, [
        'move',
        const <double>[0, 0],
      ]);
    });

    testWidgets('holds a push past the edge to the circle', (tester) async {
      await show(tester, _controls);

      await tester.startGesture(centre(tester, 'stick') + const Offset(70, 70));

      final push = events.last[1]! as List<double>;
      expect(push[0], closeTo(0.7071, 1e-4));
      expect(push[1], closeTo(-0.7071, 1e-4));
    });

    testWidgets('lets go when the system takes the finger', (tester) async {
      await show(tester, _controls);
      final thumb = await tester.startGesture(
        centre(tester, 'stick') + const Offset(30, 0),
      );

      await thumb.cancel();

      expect(events.last, [
        'move',
        const <double>[0, 0],
      ]);
    });

    testWidgets('lets go when it is rebuilt away', (tester) async {
      await show(tester, _controls);
      await tester.startGesture(centre(tester, 'stick') + const Offset(30, 0));
      events.clear();

      await show(tester, const UiNode(type: 'text', text: 'Paused'));
      await tester.pump();

      expect(events, [
        [
          'move',
          const <double>[0, 0],
        ],
      ]);
    });

    testWidgets('sends a plain zero for y level with the middle', (
      tester,
    ) async {
      await show(tester, _controls);

      await tester.startGesture(centre(tester, 'stick') + const Offset(40, 0));

      final push = events.last[1]! as List<double>;
      expect(push[1], 0);
      expect(push[1].isNegative, isFalse, reason: 'not -0.0');
    });

    testWidgets('says nothing when the thumb has not moved', (tester) async {
      await show(tester, _controls);
      final thumb = await tester.startGesture(
        centre(tester, 'stick') + const Offset(40, 0),
      );
      final count = events.length;

      await thumb.moveBy(Offset.zero);

      expect(events, hasLength(count));
    });

    testWidgets('draws the knob under the thumb', (tester) async {
      await show(tester, _controls);
      final middle = centre(tester, 'stick');
      final knob = find.byWidgetPredicate(
        (w) =>
            w is DecoratedBox &&
            w.decoration is BoxDecoration &&
            (w.decoration as BoxDecoration).shape == BoxShape.circle,
      );

      await tester.startGesture(middle + const Offset(40, 0));
      await tester.pump();

      expect(tester.getCenter(knob).dx, closeTo(middle.dx + 40, 1e-6));
      expect(tester.getCenter(knob).dy, closeTo(middle.dy, 1e-6));
    });

    testWidgets('follows only its first finger', (tester) async {
      await show(tester, _controls);
      final middle = centre(tester, 'stick');
      await tester.startGesture(middle + const Offset(40, 0), pointer: 1);
      final count = events.length;

      final second = await tester.startGesture(
        middle + const Offset(-40, 0),
        pointer: 2,
      );
      await second.moveTo(middle + const Offset(0, 40));
      await second.up();

      expect(events, hasLength(count));
    });
  });

  group('a touch button', () {
    testWidgets('is held from touch to lift', (tester) async {
      await show(tester, _controls);

      final finger = await tester.startGesture(centre(tester, 'touch-button'));
      expect(events.last, ['jump', true]);

      await finger.up();
      expect(events.last, ['jump', false]);
      expect(find.text('Jump'), findsOneWidget);
    });

    testWidgets('takes a touch on its padding and dims its whole face', (
      tester,
    ) async {
      await show(tester, _controls);
      final face = tester.getTopLeft(
        find.byWidgetPredicate((w) => w.runtimeType.toString() == 'TouchHold'),
      );

      final finger = await tester.startGesture(face + const Offset(2, 2));
      await tester.pump();

      expect(events.last, ['jump', true]);
      final dim = tester.widget<Opacity>(find.byType(Opacity));
      expect(dim.opacity, 0.6);
      expect(
        find.descendant(
          of: find.byType(Opacity),
          matching: find.byType(DecoratedBox),
        ),
        findsOneWidget,
        reason: 'the background dims with the label',
      );

      await finger.up();
    });

    testWidgets('lets go when the system takes the finger', (tester) async {
      await show(tester, _controls);
      final finger = await tester.startGesture(centre(tester, 'touch-button'));

      await finger.cancel();

      expect(events.last, ['jump', false]);
    });

    testWidgets('lets go when it is rebuilt away', (tester) async {
      await show(tester, _controls);
      await tester.startGesture(centre(tester, 'touch-button'));
      events.clear();

      await show(tester, const UiNode(type: 'text', text: 'Paused'));
      await tester.pump();

      expect(events, [
        ['jump', false],
      ]);
    });
  });

  group('a swipe pad', () {
    testWidgets('sends each move, up positive', (tester) async {
      await show(tester, _controls);
      final start = centre(tester, 'swipe-pad');

      final finger = await tester.startGesture(start);
      expect(events, isEmpty, reason: 'a touch alone is not a swipe');

      await finger.moveBy(const Offset(6, -4));
      await finger.moveBy(const Offset(-2, 3));
      await finger.up();

      expect(events, [
        [
          'look',
          const <double>[6, 4],
        ],
        [
          'look',
          const <double>[-2, -3],
        ],
      ]);
    });

    testWidgets('is a square of its own when no size is given', (tester) async {
      await show(
        tester,
        const UiNode(
          type: 'row',
          children: [
            UiNode(type: 'swipe-pad'),
            UiNode(type: 'stick'),
          ],
        ),
      );

      expect(
        tester.getSize(
          find.byWidgetPredicate(
            (w) => w.runtimeType.toString() == 'TouchSwipe',
          ),
        ),
        const Size(120, 120),
      );
      expect(
        tester.getSize(
          find.byWidgetPredicate(
            (w) => w.runtimeType.toString() == 'TouchStick',
          ),
        ),
        const Size(120, 120),
      );
    });

    testWidgets('says nothing when the finger lands elsewhere', (tester) async {
      await show(tester, _controls);
      final finger = await tester.startGesture(centre(tester, 'stick'));

      await finger.moveBy(const Offset(5, 5));

      expect(events.where((e) => e[0] == 'look'), isEmpty);
    });
  });

  testWidgets('three fingers drive a stick, a button and a swipe at once', (
    tester,
  ) async {
    await show(tester, _controls);

    final thumb = await tester.startGesture(
      centre(tester, 'stick') + const Offset(40, 0),
      pointer: 1,
    );
    final jump = await tester.startGesture(
      centre(tester, 'touch-button'),
      pointer: 2,
    );
    final look = await tester.startGesture(
      centre(tester, 'swipe-pad'),
      pointer: 3,
    );
    await look.moveBy(const Offset(10, 0));
    await thumb.moveBy(const Offset(0, -20));

    expect(events.map((e) => e[0]), containsAll(['move', 'jump', 'look']));
    expect(events.where((e) => e[0] == 'jump'), [
      ['jump', true],
    ]);

    await jump.up();
    expect(events.last, ['jump', false]);

    final stick = events.lastWhere((e) => e[0] == 'move')[1]! as List<double>;
    expect(stick[0], greaterThan(0.2), reason: 'the thumb is still down');
  });

  testWidgets('an element with no handler named still draws and listens', (
    tester,
  ) async {
    await show(
      tester,
      const UiNode(
        type: 'column',
        children: [
          UiNode(type: 'stick', css: _size),
          UiNode(type: 'swipe-pad', css: _size),
        ],
      ),
    );

    final finger = await tester.startGesture(centre(tester, 'stick'));
    await finger.moveBy(const Offset(10, 0));
    await finger.up();

    expect(events, isEmpty);
  });
}
