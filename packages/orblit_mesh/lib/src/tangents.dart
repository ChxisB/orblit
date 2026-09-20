import 'dart:math' as math;
import 'dart:typed_data';

import 'triangles.dart';

/// Per-vertex tangents for [triangles], four floats each.
///
/// A normal map states which way a surface leans in the texture's own frame,
/// so reading one takes a direction across the surface that matches the way
/// the texture was laid on. That is the tangent. Without it every renderer
/// guesses, and they guess differently — which is why the same model lit the
/// same way looks subtly wrong in the next tool along, with the bumps leaning
/// the other way.
///
/// The fourth number is a sign, not a length: it says whether the third axis
/// of the frame points one way or the other, which is how a mirrored UV
/// island gets its bumps the right way up instead of inside out.
///
/// Computed per triangle and averaged per vertex — the usual construction,
/// and the one every exporter that is not carrying MikkTSpace around uses.
/// It agrees with MikkTSpace wherever the triangles sharing a vertex agree,
/// which is everywhere except a hard seam, and a hard seam has split the
/// vertex already.
Float32List tangentsOf(Triangles triangles) {
  final count = triangles.positions.length ~/ 3;
  final out = Float32List(count * 4);
  if (count == 0 || triangles.uvs.length != count * 2) return out;

  final along = Float32List(count * 3);
  final across = Float32List(count * 3);

  final indices = triangles.indices;
  for (var i = 0; i + 2 < indices.length; i += 3) {
    final a = indices[i];
    final b = indices[i + 1];
    final c = indices[i + 2];

    final x1 = triangles.positions[b * 3] - triangles.positions[a * 3];
    final y1 = triangles.positions[b * 3 + 1] - triangles.positions[a * 3 + 1];
    final z1 = triangles.positions[b * 3 + 2] - triangles.positions[a * 3 + 2];
    final x2 = triangles.positions[c * 3] - triangles.positions[a * 3];
    final y2 = triangles.positions[c * 3 + 1] - triangles.positions[a * 3 + 1];
    final z2 = triangles.positions[c * 3 + 2] - triangles.positions[a * 3 + 2];

    final u1 = triangles.uvs[b * 2] - triangles.uvs[a * 2];
    final v1 = triangles.uvs[b * 2 + 1] - triangles.uvs[a * 2 + 1];
    final u2 = triangles.uvs[c * 2] - triangles.uvs[a * 2];
    final v2 = triangles.uvs[c * 2 + 1] - triangles.uvs[a * 2 + 1];

    // The area this triangle covers in the texture. A triangle that covers
    // none of it — every corner on the same spot, or on one line — has no
    // direction to give, and dividing by it would hand every vertex it
    // touches an infinity.
    final area = u1 * v2 - u2 * v1;
    if (area == 0 || !area.isFinite) continue;
    final scale = 1 / area;

    final tx = (v2 * x1 - v1 * x2) * scale;
    final ty = (v2 * y1 - v1 * y2) * scale;
    final tz = (v2 * z1 - v1 * z2) * scale;
    final bx = (u1 * x2 - u2 * x1) * scale;
    final by = (u1 * y2 - u2 * y1) * scale;
    final bz = (u1 * z2 - u2 * z1) * scale;

    for (final at in [a, b, c]) {
      along[at * 3] += tx;
      along[at * 3 + 1] += ty;
      along[at * 3 + 2] += tz;
      across[at * 3] += bx;
      across[at * 3 + 1] += by;
      across[at * 3 + 2] += bz;
    }
  }

  final hasNormals = triangles.normals.length == count * 3;
  for (var i = 0; i < count; i++) {
    final nx = hasNormals ? triangles.normals[i * 3] : 0.0;
    final ny = hasNormals ? triangles.normals[i * 3 + 1] : 1.0;
    final nz = hasNormals ? triangles.normals[i * 3 + 2] : 0.0;

    var tx = along[i * 3];
    var ty = along[i * 3 + 1];
    var tz = along[i * 3 + 2];

    // Square the frame up: take off however much of the tangent points along
    // the normal, which leaves the part that lies in the surface.
    final leaning = nx * tx + ny * ty + nz * tz;
    tx -= nx * leaning;
    ty -= ny * leaning;
    tz -= nz * leaning;

    var length = math.sqrt(tx * tx + ty * ty + tz * tz);
    if (length < 1e-12 || !length.isFinite) {
      // No usable direction — an unwrapped vertex, or one every triangle
      // round it gave nothing. Any direction lying in the surface will do,
      // so take the normal crossed with whichever axis it leans on least,
      // which is the one choice that cannot collapse again.
      final sideways = nx.abs() < 0.9;
      final ex = sideways ? 1.0 : 0.0;
      final ey = sideways ? 0.0 : 1.0;
      tx = ny * 0.0 - nz * ey;
      ty = nz * ex - nx * 0.0;
      tz = nx * ey - ny * ex;
      length = math.sqrt(tx * tx + ty * ty + tz * tz);
      if (length < 1e-12) {
        out[i * 4] = 1;
        out[i * 4 + 3] = 1;
        continue;
      }
    }

    out[i * 4] = tx / length;
    out[i * 4 + 1] = ty / length;
    out[i * 4 + 2] = tz / length;

    // Which way round the third axis goes. The cross of the normal and the
    // tangent either agrees with the direction the texture runs across the
    // surface or it is exactly opposed, and that is the whole of the sign.
    final cx = ny * tz - nz * ty;
    final cy = nz * tx - nx * tz;
    final cz = nx * ty - ny * tx;
    final agrees =
        cx * across[i * 3] + cy * across[i * 3 + 1] + cz * across[i * 3 + 2];
    out[i * 4 + 3] = agrees < 0 ? -1 : 1;
  }

  return out;
}
