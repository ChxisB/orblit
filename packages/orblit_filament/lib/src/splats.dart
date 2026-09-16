import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

/// A cloud of 3D Gaussians: a captured place, or anything generated to look
/// like one.
///
/// 3D Gaussian splatting (Kerbl, Kopanas, Leimkühler and Drettakis,
/// SIGGRAPH 2023) represents a scene as millions of small, soft, coloured
/// ellipsoids rather than as surfaces. Each is drawn as an ellipse on screen,
/// sized from how its ellipsoid projects, faded by a Gaussian and blended over
/// what is behind it — so they have to be drawn back to front, and the
/// renderer re-sorts them whenever the camera moves, off the render thread (on
/// a Web Worker in a browser), leaving out the ones the camera cannot see.
///
/// Splats are drawn after the solid scene, test against its depth and do not
/// write any: a wall in front of a cloud hides it, and a cloud never hides a
/// wall. They are not lit — a capture has its lighting baked into its
/// colours — and they neither cast nor receive shadows.
///
/// Two ways to say what the cloud is: a [path] to a `.ply` as the reference
/// trainer writes it or a compact `.splat`, read on the native side; or
/// [data] in the compact layout, for a cloud made in Dart. [pack] builds that
/// layout from plain arrays.
class OrblitSplats {
  OrblitSplats({
    required this.key,
    this.path,
    this.data,
    Matrix4? transform,
    this.opacity = 1,
    this.brightness = 1,
    this.sorted = true,
    this.harmonics = 2,
    this.limit,
    this.coarseOrder = false,
    this.revision = 0,
  }) : transform = transform ?? Matrix4.identity(),
       assert(
         (path == null) != (data == null),
         'a cloud comes from a file or from memory, not both or neither',
       ),
       assert(
         harmonics >= 0 && harmonics <= 3,
         'a capture carries spherical harmonics of degree 0, 1, 2 or 3',
       ),
       assert(
         limit == null || (limit > 0 && limit <= maxLimit),
         'a limit is at least one splat and at most $maxLimit',
       ),
       assert(
         data == null || data.length % recordBytes == 0,
         'in-memory splats are whole $recordBytes-byte records',
       );

  /// What this cloud is, across frames. The renderer keeps its textures and
  /// its sorter against this key.
  final int key;

  /// A capture to read: a `.ply` as the reference trainer writes one, a
  /// compact `.splat`, Niantic's `.spz`, or an `.osplat` cooked from any of
  /// those.
  ///
  /// Read once, and again only when the path, [harmonics] or [limit] changes.
  /// An `.osplat` is the cloud exactly as the renderer holds it, so opening
  /// one is a read and four copies rather than a parse, an exponential and a
  /// quaternion a splat — which is what a launch that must not stall wants.
  final String? path;

  /// Splats in the compact 32-byte layout — see [pack].
  ///
  /// Sent only when [revision] has moved since the renderer last took it,
  /// the same bargain a population makes: a few hundred thousand splats is
  /// megabytes, and a cloud that is only being looked at sends none of it.
  final Uint8List? data;

  /// Where the cloud is, as the capture's own coordinates to the world's.
  ///
  /// Captures come out of structure-from-motion in whatever orientation the
  /// first photograph had, which is very often upside down; this is where
  /// that is put right. Scale reaches every splat's size as well as its
  /// position, so a cloud scaled up is the same picture, larger.
  final Matrix4 transform;

  /// Multiplies every splat's own opacity.
  final double opacity;

  /// Multiplies every splat's colour, in linear light.
  final double brightness;

  /// Whether the splats are sorted back to front. On, always, for a picture:
  /// this exists so that what the sort is worth can be measured, and a cloud
  /// that is not sorted is drawn whole, every splat, in the order given.
  final bool sorted;

  /// How much of a capture's view-dependent colour to read: the degree of the
  /// spherical harmonics, 0, 1, 2 or 3.
  ///
  /// A trainer fits each splat's colour as spherical harmonics rather than as
  /// one colour, so that a surface can be one colour seen from here and
  /// another seen from there. That is what a polished floor, a window and wet
  /// tarmac all are, and with only the flat degree-zero term they are painted
  /// the average of every direction at once. Each degree above nought is
  /// another band of it.
  ///
  /// The bands are not free. They are quantised to a byte a coefficient and
  /// laid out sixteen to a texel, so each degree costs sixteen bytes a splat:
  /// at a million splats, 16 MB for degree 1, 32 MB for degree 2 and 48 MB
  /// for degree 3, on top of the 48 MB the splats themselves take. Two by
  /// default, which is what most captures are trained to and where nearly all
  /// of the effect is; 0 leaves them out and draws exactly what this drew
  /// before it could read them at all. [OrblitDeviceProfile.harmonicDegree]
  /// is where a device's own answer starts.
  ///
  /// Only a `.ply` carries any. The compact 32-byte record has no room for
  /// them, so a cloud from [data] or from a `.splat` is flat whatever this
  /// says. A file trained to a lower degree than this asks for is read as far
  /// as it goes, and one trained higher is read to here and says so.
  final int harmonics;

  /// The most splats to draw, or null for every one.
  ///
  /// A cloud is ranked as it is read, by how opaque each splat is and how much
  /// of the screen it can cover, and a limit keeps the ones that add most. So
  /// what a smaller budget takes away is the faint, small splats a capture is
  /// thickest with, not a random share of everything — and the rest never
  /// reach the GPU, which is the point on a phone. The renderer says, in the
  /// scene's notes, when a limit dropped any.
  ///
  /// [OrblitDeviceProfile.splatBudget] is where a device's own answer starts.
  ///
  /// Applied as the cloud is read, so a new limit reads a file again, and a
  /// cloud from [data] takes it with its next [revision]. At most [maxLimit].
  final int? limit;

  /// Whether to sort on sixteen bits of depth rather than thirty-two.
  ///
  /// Half the passes, for splats within a 65 536th of the visible splats'
  /// depth of one another coming out in either order — which nobody can see in
  /// a capture of any size, and which lets the order keep up with a turning
  /// camera on a device where a full sort would lag behind it.
  /// [OrblitDeviceProfile.coarseSplatOrder] says which devices those are.
  final bool coarseOrder;

  /// Bumped by whoever writes into [data].
  final int revision;

  /// How many there are, for a cloud held in memory. Zero for a file, whose
  /// size the renderer finds out when it reads it.
  int get count => data == null ? 0 : data!.length ~/ recordBytes;

  /// Bit flags in the order the renderer reads them: whether to sort; the
  /// spherical-harmonic degree in the two bits above that; whether the sort is
  /// coarse in bit three; and the limit from bit eight up, nought for none.
  /// Must match kSplatFlag* in OrblitSplats.h.
  ///
  /// A limit near [maxLimit] sets the top bit, so the flags of such a cloud
  /// are a negative 32-bit integer on the wire. The renderer reads them
  /// unsigned.
  int get flags =>
      (sorted ? 1 : 0) |
      (harmonics << 1) |
      (coarseOrder ? 8 : 0) |
      ((limit ?? 0) << limitShift);

  /// Where the limit starts in [flags]. Must match kSplatFlagLimitShift.
  static const int limitShift = 8;

  /// The largest limit the flags have room for: twenty-four bits.
  static const int maxLimit = 0xFFFFFF;

  /// Floats per cloud in the scene message: the transform, column-major, then
  /// the opacity and the brightness. Must match splatStride in the plugin and
  /// kSplatParams in OrblitSplats.h.
  static const int stride = 18;

  /// Bytes per splat in the compact layout. Must match kSplatRecordBytes.
  static const int recordBytes = 32;

  /// Writes this cloud's [stride] floats at [at].
  void packParams(Float32List into, int at) {
    into.setRange(at, at + 16, transform.storage);
    into[at + 16] = opacity;
    into[at + 17] = brightness;
  }

  /// The compact layout, from plain arrays of [count] splats.
  ///
  /// [positions] and [scales] are three floats each — scales are standard
  /// deviations in metres, not the logs a `.ply` stores. [colours] is four
  /// floats each, red, green, blue as display values from nought to one and
  /// alpha as the splat's peak opacity. [rotations] is four each, a
  /// quaternion as (w, x, y, z); null means none.
  ///
  /// The layout keeps colour and rotation to a byte a channel, which is what
  /// a `.splat` file does and is below what anybody can see in a soft blob.
  static Uint8List pack({
    required Float32List positions,
    required Float32List scales,
    required Float32List colours,
    Float32List? rotations,
  }) {
    final count = positions.length ~/ 3;
    assert(scales.length == count * 3, 'three scales a splat');
    assert(colours.length == count * 4, 'four colour channels a splat');
    assert(
      rotations == null || rotations.length == count * 4,
      'four quaternion components a splat',
    );

    final bytes = Uint8List(count * recordBytes);
    final floats = Float32List.view(bytes.buffer);
    for (var i = 0; i < count; i++) {
      final f = i * (recordBytes ~/ 4);
      floats[f] = positions[i * 3];
      floats[f + 1] = positions[i * 3 + 1];
      floats[f + 2] = positions[i * 3 + 2];
      floats[f + 3] = scales[i * 3];
      floats[f + 4] = scales[i * 3 + 1];
      floats[f + 5] = scales[i * 3 + 2];

      final b = i * recordBytes + 24;
      for (var c = 0; c < 4; c++) {
        bytes[b + c] = (colours[i * 4 + c].clamp(0.0, 1.0) * 255).round();
      }

      var w = 1.0, x = 0.0, y = 0.0, z = 0.0;
      if (rotations != null) {
        w = rotations[i * 4];
        x = rotations[i * 4 + 1];
        y = rotations[i * 4 + 2];
        z = rotations[i * 4 + 3];
        final length = math.sqrt(w * w + x * x + y * y + z * z);
        if (length > 0) {
          w /= length;
          x /= length;
          y /= length;
          z /= length;
        } else {
          w = 1;
        }
      }
      bytes[b + 4] = _quantise(w);
      bytes[b + 5] = _quantise(x);
      bytes[b + 6] = _quantise(y);
      bytes[b + 7] = _quantise(z);
    }
    return bytes;
  }

  /// A unit component to a byte, as (v * 128 + 128), which is the `.splat`
  /// convention. One is 256 and so saturates at 255 — a hair under one,
  /// which the renderer's normalisation takes back out.
  static int _quantise(double value) =>
      (value * 128 + 128).round().clamp(0, 255);
}
