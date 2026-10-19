import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

final _scene = OrblitScene(
  objects: [
    OrblitObject(
      key: 1,
      transform: Matrix4.identity(),
      colour: Vector3(1, 1, 1),
    ),
  ],
  camera: OrblitCamera(position: Vector3(0, 0, 5), target: Vector3.zero()),
);

DebugLine _line(Vector3 from, Vector3 to) =>
    DebugLine(from: from, to: to, colour: Vector3(1, 0, 0));

/// The cube is two metres a side, so its long axis runs from y = -1 to y = 1.
Vector3 _end(OrblitObject mark, double y) =>
    mark.transform.transformed3(Vector3(0, y, 0));

void _expectAt(Vector3 actual, Vector3 expected) {
  expect((actual - expected).length, closeTo(0, 1e-9), reason: '$actual');
}

void main() {
  test('adds nothing while it is off and never asks for the lines', () {
    var asked = false;
    final lines = OrblitDebugLines();
    final drawn = lines.draw(_scene, () {
      asked = true;
      return [_line(Vector3.zero(), Vector3(0, 1, 0))];
    });
    expect(identical(drawn, _scene), isTrue);
    expect(asked, isFalse);
  });

  test('adds one coloured cube after the scene objects for each line', () {
    final lines = OrblitDebugLines(enabled: true);
    final drawn = lines.draw(
      _scene,
      () => [
        _line(Vector3.zero(), Vector3(0, 1, 0)),
        _line(Vector3.zero(), Vector3(1, 0, 0)),
      ],
    );
    expect(drawn.objects.map((o) => o.key), [
      1,
      lines.keyBase,
      lines.keyBase + 1,
    ]);
    expect(drawn.objects.last.colour, Vector3(1, 0, 0));
    expect(drawn.objects.last.castShadows, isFalse);
  });

  test('runs each cube from one end of its line to the other', () {
    final lines = OrblitDebugLines(enabled: true);
    final from = Vector3(1, 2, 3);
    for (final to in [
      Vector3(1, 5, 3),
      Vector3(1, -1, 3),
      Vector3(4, 2, 3),
      Vector3(1, 2, -4),
    ]) {
      final mark = lines.draw(_scene, () => [_line(from, to)]).objects.last;
      _expectAt(_end(mark, -1), from);
      _expectAt(_end(mark, 1), to);
    }
  });

  test('is as thick as asked', () {
    final lines = OrblitDebugLines(enabled: true, width: 0.1);
    final mark = lines
        .draw(_scene, () => [_line(Vector3.zero(), Vector3(0, 2, 0))])
        .objects
        .last;
    final side = mark.transform.transformed3(Vector3(1, 0, 0));
    _expectAt(side, Vector3(0.05, 1, 0));
  });

  test('draws a point as a square of the width', () {
    final lines = OrblitDebugLines(enabled: true, width: 0.1);
    final at = Vector3(2, 3, 4);
    final mark = lines.draw(_scene, () => [_line(at, at)]).objects.last;
    _expectAt(mark.transform.transformed3(Vector3.zero()), at);
    _expectAt(_end(mark, 1) - _end(mark, -1), Vector3(0, 0.1, 0));
  });

  test('skips a line that has no place', () {
    final lines = OrblitDebugLines(enabled: true);
    final drawn = lines.draw(
      _scene,
      () => [
        _line(Vector3.zero(), Vector3(double.nan, 0, 0)),
        _line(Vector3.zero(), Vector3(0, double.infinity, 0)),
        _line(Vector3.zero(), Vector3(0, 1, 0)),
      ],
    );
    expect(drawn.objects.map((o) => o.key), [1, lines.keyBase]);
  });

  test('refuses a width that is not above zero', () {
    expect(() => OrblitDebugLines(width: 0), throwsArgumentError);
    expect(() => OrblitDebugLines(width: double.nan), throwsArgumentError);
  });
}
