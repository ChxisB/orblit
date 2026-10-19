import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// A straight segment to draw over a scene, in world space.
final class DebugLine {
  const DebugLine({required this.from, required this.to, required this.colour});

  final Vector3 from;
  final Vector3 to;

  /// Linear red, green and blue.
  final Vector3 colour;
}

/// Draws segments over a scene while [enabled], for finding out what a
/// simulation is doing: the contact points and normals of a physics world, a
/// ray, a path.
///
/// The renderer has no line primitive, so each segment is the built-in cube
/// stretched along it. It is lit like anything else, so a segment in shadow
/// is dark. A segment shorter than [width] is drawn as a square of that size,
/// which is how a point shows.
///
/// Stateless: [draw] answers with a scene and keeps nothing, so call it where
/// the scene is made each frame. The stage depends on no simulation. The
/// caller turns its own data into [DebugLine]s.
final class OrblitDebugLines {
  OrblitDebugLines({
    this.enabled = false,
    this.width = 0.02,
    this.keyBase = 0x44420000,
  }) {
    if (!(width > 0 && width.isFinite)) {
      throw ArgumentError.value(width, 'width', 'must be above zero');
    }
  }

  /// Whether [draw] adds anything. Off, it costs a branch.
  bool enabled;

  /// The thickness of a segment, in the scene's units.
  final double width;

  /// The key of the first segment, clear of the scene's own keys.
  final int keyBase;

  /// [scene] with a segment for each of [lines], or [scene] itself while
  /// disabled. [lines] is not called then, so a world that lists its contacts
  /// by copying them pays nothing for a switch that is off.
  OrblitScene draw(OrblitScene scene, Iterable<DebugLine> Function() lines) {
    if (!enabled) return scene;
    final marks = <OrblitObject>[];
    for (final line in lines()) {
      final mark = _markOf(keyBase + marks.length, line);
      if (mark != null) marks.add(mark);
    }
    if (marks.isEmpty) return scene;
    return scene.copyWith(objects: [...scene.objects, ...marks]);
  }

  /// The segment as a cube, or null when an end is not a finite point.
  OrblitObject? _markOf(int key, DebugLine line) {
    final along = line.to - line.from;
    final length = along.length;
    // An end that is not a number makes a matrix the renderer cannot place.
    if (!length.isFinite) return null;
    final heading = length > 1e-9 ? along / length : Vector3(0, 1, 0);
    final half = Vector3(width, length < width ? width : length, width) / 2;
    return OrblitObject(
      key: key,
      transform: Matrix4.compose(
        (line.from + line.to) / 2,
        Quaternion.fromTwoVectors(Vector3(0, 1, 0), heading),
        half,
      ),
      colour: line.colour,
      castShadows: false,
      receiveShadows: false,
    );
  }
}
