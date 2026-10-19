import 'dart:ui';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

/// Where [ray] meets the plane z = 0.
Vector3 onFloor(PointerRay ray) =>
    ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);

void main() {
  const view = Size(100, 100);
  final camera = OrblitCamera(
    position: Vector3(0, 0, 5),
    target: Vector3.zero(),
    fieldOfView: 90,
  );

  group('a perspective camera', () {
    test('the middle of the view looks straight ahead', () {
      final ray = PointerRay.fromCamera(camera, const Offset(50, 50), view);

      expect(ray.origin, Vector3(0, 0, 5));
      expect(ray.direction.x, closeTo(0, 1e-9));
      expect(ray.direction.y, closeTo(0, 1e-9));
      expect(ray.direction.z, closeTo(-1, 1e-9));
    });

    test('the top right corner is a ray through where the maths says', () {
      final ray = PointerRay.fromCamera(camera, const Offset(100, 0), view);

      // A right angle of view at five metres sees five metres each way.
      final hit = onFloor(ray);
      expect(hit.x, closeTo(5, 1e-9));
      expect(hit.y, closeTo(5, 1e-9));
    });

    test('a wide view stretches sideways and not upwards', () {
      final ray = PointerRay.fromCamera(
        camera,
        const Offset(200, 0),
        const Size(200, 100),
      );

      final hit = onFloor(ray);
      expect(hit.x, closeTo(10, 1e-9));
      expect(hit.y, closeTo(5, 1e-9));
    });
  });

  test('a tall view measures the angle across its width', () {
    // The renderer measures the angle across the shorter side.
    const tall = Size(100, 200);

    final side = onFloor(
      PointerRay.fromCamera(camera, const Offset(100, 100), tall),
    );
    final top = onFloor(
      PointerRay.fromCamera(camera, const Offset(50, 0), tall),
    );

    expect(side.x, closeTo(5, 1e-9));
    expect(top.y, closeTo(10, 1e-9));
  });

  group('an orthographic camera', () {
    test('every ray runs the same way from its own place', () {
      final flat = camera.copyWith(orthographic: true, viewHeight: 10);

      final ray = PointerRay.fromCamera(flat, const Offset(100, 0), view);

      expect(ray.direction, Vector3(0, 0, -1));
      expect(ray.origin.x, closeTo(5, 1e-9));
      expect(ray.origin.y, closeTo(5, 1e-9));
      expect(ray.origin.z, closeTo(5, 1e-9));
    });
  });
}
