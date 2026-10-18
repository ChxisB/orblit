import 'package:flutter/gestures.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart';

import 'pointer_ray.dart';

/// A [UiPanel] standing in the scene on a flat face, with a click and a drag
/// that reach it.
///
/// The face is the front of the built-in cube, squashed thin: a quad is a cube
/// with a small z scale in [transform], so a screen on a wall is the cube's
/// +z side. The cube spans -1 to 1 on each axis, so a transform of
/// `scale(2, 1, 0.05)` is a face four metres wide and two high. Give the panel
/// the same shape in pixels or the picture is stretched.
///
/// Two halves. [object] and [material] are what to put in the scene, and
/// [sync] draws the panel when it changed and gives the renderer the new
/// picture. [down], [move] and [up] take a [PointerRay] and turn it into a
/// pointer on the panel.
///
/// The renderer loads a name once, so each picture goes under a name of its
/// own and the one before is released. That is a copy of the whole picture per
/// change, which is fine for a screen that changes when somebody presses
/// something and not for one that animates every frame.
final class WorldPanel {
  WorldPanel({
    required this.id,
    required this.panel,
    required Matrix4 transform,
    required this.objectKey,
    required this.materialKey,
  }) {
    place(transform);
  }

  /// Names this panel's pictures, so two panels never share one.
  final String id;

  /// The panel it shows. The caller owns it and disposes it.
  final UiPanel panel;

  final int objectKey;
  final int materialKey;

  late Matrix4 _transform;
  late Matrix4 _inverse;
  String? _name;
  int _picture = 0;

  // Where the pointer last was on the face's plane, so a drag that ends with
  // the ray off the plane still finishes where it left.
  Offset? _last;

  /// The one pointer a panel takes.
  static const int _pointer = 1;

  /// Where the face is. Takes effect for the next ray.
  void place(Matrix4 transform) {
    _transform = Matrix4.copy(transform);
    _inverse = Matrix4.copy(transform)..invert();
  }

  /// What to put in the scene's objects.
  OrblitObject get object => OrblitObject(
    key: objectKey,
    material: materialKey,
    transform: _transform,
    colour: Vector3(1, 1, 1),
    castShadows: false,
    receiveShadows: false,
  );

  /// What to put in the scene's materials. Invisible until the first [sync].
  OrblitMaterial get material {
    final name = _name;
    return OrblitMaterial(
      key: materialKey,
      shading: OrblitShading.unlit,
      blend: OrblitBlend.fade,
      baseColour: Vector4(1, 1, 1, name == null ? 0 : 1),
      // A picture's rows run top down and the cube's v runs bottom up.
      tiling: Vector2(1, -1),
      offset: Vector2(0, 1),
      wrap: OrblitWrap.clamp,
      baseColourMap: name == null ? null : OrblitTexture(name),
    );
  }

  /// Draws the panel if it changed and hands the renderer the new picture.
  /// Publish the scene again afterwards, because [material] names it.
  ///
  /// Await one call before making the next.
  Future<void> sync() async {
    if (!panel.isDirty) return;
    final bytes = await panel.paint();
    final next = OrblitResources.nameFor('panel/$id/${++_picture}.png');
    await OrblitResources.provide(next, bytes);
    final old = _name;
    _name = next;
    if (old != null) await OrblitResources.release(old);
  }

  /// Lets go of the picture the renderer holds. The panel stays the caller's.
  Future<void> dispose() async {
    final name = _name;
    _name = null;
    if (name != null) await OrblitResources.release(name);
  }

  /// Where [ray] lands on the panel, in its pixels, or null if it misses the
  /// face. A ray from behind the face misses.
  Offset? pixelAt(PointerRay ray) {
    final pixel = _onPlane(ray);
    if (pixel == null || !(Offset.zero & panel.size).contains(pixel)) {
      return null;
    }
    return pixel;
  }

  bool hits(PointerRay ray) => pixelAt(ray) != null;

  /// Presses on the panel where [ray] lands. Does nothing if it misses.
  void down(PointerRay ray) {
    final pixel = pixelAt(ray);
    if (pixel == null) return;
    _last = pixel;
    panel.send(PointerDownEvent(position: pixel, pointer: _pointer));
  }

  /// Moves the press. A drag that leaves the face keeps going, as a drag on a
  /// screen does when the pointer strays off it.
  void move(PointerRay ray) => _follow(
    ray,
    (pixel) => PointerMoveEvent(position: pixel, pointer: _pointer),
  );

  /// Lets go.
  void up(PointerRay ray) {
    _follow(ray, (pixel) => PointerUpEvent(position: pixel, pointer: _pointer));
    _last = null;
  }

  void _follow(PointerRay ray, PointerEvent Function(Offset pixel) make) {
    final pixel = _onPlane(ray) ?? _last;
    if (pixel == null) return;
    _last = pixel;
    panel.send(make(pixel));
  }

  /// Where [ray] crosses the face's plane in panel pixels, however far from
  /// the face. Worked in the face's own space, where it is the plane z = 1
  /// and the cube's scale and turn are already undone.
  Offset? _onPlane(PointerRay ray) {
    final from = _inverse.transformed3(ray.origin);
    final along = _inverse.rotate3(ray.direction.clone());
    if (along.z >= 0 || from.z < 1) return null;
    final t = (1 - from.z) / along.z;
    final x = from.x + along.x * t;
    final y = from.y + along.y * t;
    return Offset(
      (x + 1) / 2 * panel.size.width,
      (1 - y) / 2 * panel.size.height,
    );
  }
}
