import 'dart:math' as math;
import 'dart:ui';

import 'package:orblit_filament/orblit_filament.dart' show OrblitCamera;
import 'package:vector_math/vector_math_64.dart';

/// A line from the eye through a point on the screen.
///
/// What a click on a view means in the world: the first thing along this line
/// is what was clicked. It holds no scene, so it does not know what is on it.
final class PointerRay {
  const PointerRay(this.origin, this.direction);

  /// The ray through [point], in pixels from the view's top left, as
  /// [camera] sees a view of [view] pixels.
  ///
  /// A perspective ray starts at the camera and fans out. An orthographic ray
  /// starts at a place on the lens and every one runs the same way.
  factory PointerRay.fromCamera(OrblitCamera camera, Offset point, Size view) {
    final forward = (camera.target - camera.position)..normalize();
    // shortcut: up is world Y because the camera has no roll, so a camera
    // looking straight up or down has no right-hand side. Upgrade when the
    // camera gains an up vector.
    final right = forward.cross(Vector3(0, 1, 0))..normalize();
    final up = right.cross(forward);
    final across = (2 * point.dx / view.width - 1) * view.width / view.height;
    final over = 1 - 2 * point.dy / view.height;
    if (camera.orthographic) {
      final half = camera.viewHeight / 2;
      return PointerRay(
        camera.position + right * (across * half) + up * (over * half),
        forward,
      );
    }
    final half = math.tan(camera.fieldOfView * math.pi / 360);
    return PointerRay(
      camera.position,
      (forward + right * (across * half) + up * (over * half))..normalize(),
    );
  }

  final Vector3 origin;

  /// Which way it runs. Need not be unit length.
  final Vector3 direction;
}
