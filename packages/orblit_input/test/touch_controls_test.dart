import 'package:orblit_input/orblit_input.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'action_fixture.dart';

// What an on-screen interface says reaches the action layer. The payloads are
// the ones the stick, touch-button and swipe-pad elements send.

void main() {
  late TouchControls touch;

  setUp(() => touch = TouchControls());

  group('a region', () {
    test('is held from a true payload until a false one', () {
      touch.hold('jump', true);
      expect(touch.held, {'jump'});

      touch.hold('jump', false);
      expect(touch.held, isEmpty);
    });

    test('ignores a payload that is not a bool', () {
      touch.hold('jump', true);
      touch
        ..hold('jump', null)
        ..hold('jump', 0)
        ..hold('jump', 'false');

      expect(touch.held, {'jump'});
    });
  });

  group('a stick', () {
    test('is where the payload puts it', () {
      touch.moveStick(PadStick.left, [0.5, -0.25]);

      expect(touch.sticks[PadStick.left], Vector2(0.5, -0.25));
    });

    test('is brought back to the edge of the circle', () {
      touch.moveStick(PadStick.left, [3.0, 4.0]);

      final stick = touch.sticks[PadStick.left]!;
      expect(stick.length, closeTo(1, 1e-12));
      expect(stick.x / stick.y, closeTo(0.75, 1e-12));
    });

    test('at rest has no entry', () {
      touch.moveStick(PadStick.left, [0.4, 0.4]);
      touch.moveStick(PadStick.left, [0.0, 0.0]);

      expect(touch.sticks, isEmpty);
    });

    test('keeps the last good value when a payload is wrong', () {
      touch.moveStick(PadStick.left, [0.4, 0.2]);
      for (final bad in <Object?>[
        null,
        'up',
        [1.0],
        [1.0, 2.0, 3.0],
        ['a', 'b'],
        [double.nan, 0.0],
        [double.infinity, 0.0],
      ]) {
        touch.moveStick(PadStick.left, bad);
      }

      expect(touch.sticks[PadStick.left], Vector2(0.4, 0.2));
    });

    test('takes whole numbers as well as fractions', () {
      touch.moveStick(PadStick.right, [1, 0]);

      expect(touch.sticks[PadStick.right], Vector2(1, 0));
    });
  });

  group('a swipe', () {
    test('adds up until the frame ends', () {
      touch
        ..swipe([3.0, 1.0])
        ..swipe([2.0, -4.0]);
      expect(touch.swiped, Vector2(5, -3));

      touch.endFrame();
      expect(touch.swiped, Vector2.zero());
    });

    test('ignores a payload that is not a pair', () {
      touch
        ..swipe(null)
        ..swipe([1.0])
        ..swipe([double.nan, 1.0]);

      expect(touch.swiped, Vector2.zero());
    });

    test('hands out a copy, so a game cannot change what is held', () {
      touch.swipe([1.0, 1.0]);
      touch.swiped.setZero();

      expect(touch.swiped, Vector2(1, 1));
    });
  });

  group('FrameControls with a screen', () {
    FrameControls controls({Map<PadAxis, double> axes = const {}}) =>
        FrameControls(
          touch: touch.held,
          sticks: touch.sticks,
          pad: PadState(
            slot: 0,
            identity: const PadIdentity(name: 'test'),
            down: const {},
            pressed: const {},
            released: const {},
            axes: axes,
            raw: axes,
          ),
        );

    test('reads a stick the screen pushes when the pad is at rest', () {
      touch.moveStick(PadStick.left, [0.0, 0.8]);

      final now = controls();
      expect(now.stick(PadStick.left), Vector2(0, 0.8));
      expect(
        now.read(const PadStickControl(PadStick.left)),
        closeTo(0.8, 1e-12),
      );
      expect(now.stick(PadStick.right), Vector2.zero());
    });

    test('reads the longer push when a pad and a screen share a stick', () {
      touch.moveStick(PadStick.left, [0.0, 0.3]);
      expect(
        controls(axes: {PadAxis.leftX: 0.9}).stick(PadStick.left),
        Vector2(0.9, 0),
      );

      touch.moveStick(PadStick.left, [0.0, 1.0]);
      expect(
        controls(axes: {PadAxis.leftX: 0.9}).stick(PadStick.left),
        Vector2(0, 1),
      );
    });

    test('lists a stick pushed past half as pressed', () {
      touch.moveStick(PadStick.left, [0.0, 0.2]);
      expect(controls().active, isEmpty);

      touch.moveStick(PadStick.left, [0.0, 0.6]);
      expect(controls().active, const [PadStickControl(PadStick.left)]);
    });

    test('sees later changes, since it holds views of the screen', () {
      final now = controls();
      touch
        ..hold('jump', true)
        ..moveStick(PadStick.left, [1.0, 0.0]);

      expect(now.read(const TouchControl('jump')), 1);
      expect(now.stick(PadStick.left), Vector2(1, 0));
    });
  });

  test('two fingers drive a move and a jump through the action layer', () {
    final input = ActionMap(
      actions: const [InputAction.axis2('move'), InputAction.button('jump')],
      contexts: [
        InputContext(
          'play',
          bindings: const {
            'move': [Binding(PadStickControl(PadStick.left))],
            'jump': [Binding(TouchControl('jump'))],
          },
        ),
      ],
      active: const ['play'],
    );
    FrameControls now() =>
        FrameControls(touch: touch.held, sticks: touch.sticks);

    input.update(now(), now: ms(0));
    expect(input.wasPressed('jump'), isFalse);

    touch
      ..moveStick(PadStick.left, [0.0, 1.0])
      ..hold('jump', true);
    input.update(now(), now: ms(16));
    expect(input.wasPressed('jump'), isTrue);
    expect(input.vector('move').y, greaterThan(0.9));

    touch.hold('jump', false);
    input.update(now(), now: ms(32));
    expect(input.isDown('jump'), isFalse);
    expect(input.vector('move').y, greaterThan(0.9), reason: 'still pushing');
  });
}
