import 'package:orblit_input/orblit_input.dart';
import 'package:test/test.dart';

import 'action_fixture.dart';

void main() {
  test('waits for the next control pressed', () {
    final next = NextInput(ActionKind.button);

    next.update(frame());
    expect(next.captured, isNull);
    next.update(frame(keys: {'KeyQ'}));

    expect(next.captured, const KeyControl('KeyQ'));
  });

  test('ignores what was already down until it comes up', () {
    final next = NextInput(ActionKind.button);

    next.update(frame(keys: {'Enter'}));
    next.update(frame(keys: {'Enter'}));
    expect(next.captured, isNull, reason: 'the key that opened the screen');
    next.update(frame());
    expect(next.captured, isNull);
    next.update(frame(keys: {'Enter'}));

    expect(next.captured, const KeyControl('Enter'));
  });

  test('ignores a held control but takes a new one beside it', () {
    final next = NextInput(ActionKind.button);

    next.update(frame(keys: {'Enter'}));
    next.update(frame(keys: {'Enter', 'KeyQ'}));

    expect(next.captured, const KeyControl('KeyQ'));
  });

  test('keeps its first answer', () {
    final next = NextInput(ActionKind.button);
    next.update(frame());
    next.update(frame(keys: {'KeyQ'}));

    next.update(frame(keys: {'KeyW'}));
    next.update(frame());

    expect(next.captured, const KeyControl('KeyQ'));
  });

  test('gives a button the half of a stick that was pushed', () {
    final next = NextInput(ActionKind.button);
    next.update(frame());

    next.update(frame(axes: {PadAxis.leftX: 0.9}));

    expect(
      next.captured,
      const PadAxisControl(PadAxis.leftX, side: AxisSide.positive),
    );
  });

  test('gives the other half for a push the other way', () {
    final next = NextInput(ActionKind.axis);
    next.update(frame());

    next.update(frame(axes: {PadAxis.leftX: -0.9}));

    expect(
      next.captured,
      const PadAxisControl(PadAxis.leftX, side: AxisSide.negative),
    );
  });

  test('takes the other way of a stick that was held one way', () {
    final next = NextInput(ActionKind.axis);
    next.update(frame(axes: {PadAxis.leftX: 0.9}));
    next.update(frame(axes: {PadAxis.leftX: 0.9}));
    expect(next.captured, isNull);

    next.update(frame(axes: {PadAxis.leftX: -0.9}));

    expect(
      next.captured,
      const PadAxisControl(PadAxis.leftX, side: AxisSide.negative),
    );
  });

  test('gives an axis action a trigger as its axis', () {
    final next = NextInput(ActionKind.axis);
    next.update(frame());

    next.update(
      frame(down: {PadButton.rightTrigger}, axes: {PadAxis.rightTrigger: 0.9}),
    );

    expect(
      next.captured,
      const PadAxisControl(PadAxis.rightTrigger, side: AxisSide.positive),
    );
  });

  test('gives a two-axis action the whole stick', () {
    final next = NextInput(ActionKind.axis2);
    next.update(frame());

    next.update(frame(axes: {PadAxis.leftX: 0.9}));

    expect(next.captured, const PadStickControl(PadStick.left));
  });

  test('does not take a pad that is barely touched', () {
    final next = NextInput(ActionKind.axis);
    next.update(frame());

    next.update(frame(axes: {PadAxis.leftX: 0.3}));

    expect(next.captured, isNull);
  });
}
