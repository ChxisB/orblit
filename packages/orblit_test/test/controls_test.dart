import 'package:orblit_input/orblit_input.dart';
import 'package:orblit_test/orblit_test.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  late ScriptedControls controls;

  setUp(() => controls = ScriptedControls());

  test('nothing is held to begin with', () {
    expect(controls.read(const KeyControl('Space')), 0);
    expect(controls.stick(PadStick.left), Vector2.zero());
  });

  test('a control is held from press to release', () {
    controls.press(const KeyControl('Space'));
    expect(controls.read(const KeyControl('Space')), 1);

    controls.release(const KeyControl('Space'));
    expect(controls.read(const KeyControl('Space')), 0);
  });

  test('an axis reads the value it was pressed at', () {
    controls.press(const PadAxisControl(PadAxis.leftTrigger), 0.7);

    expect(controls.read(const PadAxisControl(PadAxis.leftTrigger)), 0.7);
  });

  test('a stick is read as a vector and as how far it is pushed', () {
    controls.pushStick(PadStick.left, Vector2(0.6, 0.8));

    expect(controls.stick(PadStick.left), Vector2(0.6, 0.8));
    expect(
      controls.read(const PadStickControl(PadStick.left)),
      closeTo(1, 1e-9),
    );
    expect(controls.stick(PadStick.right), Vector2.zero());
  });

  test('a stick pushed to zero is let go', () {
    controls
      ..pushStick(PadStick.left, Vector2(0.6, 0))
      ..pushStick(PadStick.left, Vector2.zero());

    expect(controls.read(const PadStickControl(PadStick.left)), 0);
  });

  test('a stick hands out a copy, so a game can scale what it reads', () {
    controls.pushStick(PadStick.left, Vector2(0, 0.5));

    controls.stick(PadStick.left).scale(4);

    expect(controls.stick(PadStick.left), Vector2(0, 0.5));
  });
}
