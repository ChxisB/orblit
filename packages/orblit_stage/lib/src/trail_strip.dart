import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_particles/orblit_particles.dart';
import 'package:vector_math/vector_math_64.dart';

import 'particle_sheet.dart';

/// A trail drawn as a ribbon that turns its face to the eye.
///
/// The renderer takes no new vertices each frame, so this is a sheet two
/// particles wide: one for each edge of the ribbon, one row for each point of
/// the trail. The sheet's joints are set to where the edges are, as for cloth.
/// Row `i` is point `i` of the [TrailStrip], the newest first.
final class OrblitTrailStrip {
  OrblitTrailStrip({
    required this.points,
    List<double> colour = const [1, 1, 1],
  }) : _sheet = OrblitParticleSheet(
         columns: 2,
         rows: points,
         spacing: 1,
         colour: colour,
       );

  final int points;
  final OrblitParticleSheet _sheet;

  /// The ribbon as a binary glb, to hand to `OrblitResources.provide`. Both
  /// faces are drawn.
  Uint8List toGlb() => _sheet.toGlb();

  /// Every joint set for [strip], for `OrblitObject.joints`. [eye] is where
  /// the camera is, in the same space as the strip.
  ///
  /// The poses are relative to `strip.head`, so draw the object with
  /// `Matrix4.translation(strip.head)`. The renderer culls from the sheet at
  /// rest, a box 1 m wide that hangs `points - 1` m below the head, and a
  /// trail outside it can be culled while in view.
  ///
  /// Rows past the end of the strip are folded onto its last point, so they
  /// draw nothing. Where a point has no direction, no width, or lies in line
  /// with the eye, its two edges meet and the ribbon pinches there.
  List<OrblitJointPose> poses(TrailStrip strip, Vector3 eye) {
    final count = math.min(strip.count, points);
    final head = strip.head;
    final centres = [
      for (var i = 0; i < count; i++)
        Vector3(
              strip.positions[i * 3],
              strip.positions[i * 3 + 1],
              strip.positions[i * 3 + 2],
            ) -
            head,
    ];
    final seen = eye - head;
    final tail = centres.isEmpty ? Vector3.zero() : centres.last;
    final edges = <Vector3>[];
    for (var i = 0; i < points; i++) {
      edges.addAll(
        i < count ? _edges(centres, i, strip.widths[i], seen) : [tail, tail],
      );
    }
    return _sheet.poses((particle) => edges[particle]);
  }

  List<Vector3> _edges(
    List<Vector3> centres,
    int i,
    double width,
    Vector3 eye,
  ) {
    final here = centres[i];
    final tangent =
        centres[math.min(i + 1, centres.length - 1)] -
        centres[math.max(i - 1, 0)];
    final side = tangent.cross(eye - here);
    // Short of a direction, a band has no side to lie on, so it pinches.
    if (tangent.length2 < 1e-18 ||
        (eye - here).length2 < 1e-18 ||
        side.length2 < 1e-12 * tangent.length2 * (eye - here).length2) {
      return [here, here];
    }
    side.scale(0.5 * width / side.length);
    return [here - side, here + side];
  }
}
