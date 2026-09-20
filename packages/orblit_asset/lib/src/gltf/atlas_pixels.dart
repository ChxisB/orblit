import 'dart:math' as math;
import 'dart:typed_data';

import 'texture_roles.dart';

/// The pixel work a model atlas needs: resizing a role's map to its cell,
/// blitting it into a page, and extruding its edge into the padding.
///
/// Kept apart from the rewrite because it is the part with no glTF in it, and
/// therefore the part that can be tested by looking at texels.

/// sRGB to linear, for one channel, as a lookup table.
///
/// A table rather than `pow` per texel: baking a factor into a 2048-square
/// page is four million calls, and the input is a byte, so there are only 256
/// answers.
final Float32List _toLinear = () {
  final table = Float32List(256);
  for (var i = 0; i < 256; i++) {
    final c = i / 255.0;
    table[i] = c <= 0.04045 ? c / 12.92 : math.pow((c + 0.055) / 1.055, 2.4) as double;
  }
  return table;
}();

/// Linear to sRGB, as a byte.
int _toSrgb(double linear) {
  final c = linear <= 0.0
      ? 0.0
      : linear <= 0.0031308
          ? linear * 12.92
          : 1.055 * (math.pow(linear, 1 / 2.4) as double) - 0.055;
  return (c * 255.0 + 0.5).clamp(0.0, 255.0).toInt();
}

/// An image in the only layout anything here deals in: RGBA8, row 0 at the
/// top, which is both what the packer wants and where glTF's UV origin is.
class Rgba {
  Rgba(this.width, this.height, this.pixels)
      : assert(pixels.length == width * height * 4,
            'an RGBA8 image is width x height x 4 bytes');

  /// An image of one colour.
  factory Rgba.filled(int width, int height, List<int> rgba) {
    final pixels = Uint8List(width * height * 4);
    for (var i = 0; i < pixels.length; i += 4) {
      pixels[i] = rgba[0];
      pixels[i + 1] = rgba[1];
      pixels[i + 2] = rgba[2];
      pixels[i + 3] = rgba[3];
    }
    return Rgba(width, height, pixels);
  }

  final int width;
  final int height;
  final Uint8List pixels;
}

/// `source` resized to `width` by `height` by averaging the source texels each
/// destination texel covers.
///
/// Area averaging rather than a filter with a kernel: it is exact when the
/// ratio is a whole number, which is the case that actually happens (a 2048
/// map into a 1024 cell), it never rings, and it is the same answer on every
/// machine — which a cook has to be, or the cache is lying.
Rgba resample(Rgba source, int width, int height, {bool normal = false}) {
  if (source.width == width && source.height == height) return source;
  if (width <= 0 || height <= 0) return Rgba(0, 0, Uint8List(0));

  final out = Uint8List(width * height * 4);
  final xScale = source.width / width;
  final yScale = source.height / height;
  for (var y = 0; y < height; y++) {
    final y0 = (y * yScale).floor();
    final y1 = math.max(y0 + 1, ((y + 1) * yScale).ceil()).clamp(0, source.height);
    for (var x = 0; x < width; x++) {
      final x0 = (x * xScale).floor();
      final x1 =
          math.max(x0 + 1, ((x + 1) * xScale).ceil()).clamp(0, source.width);

      var r = 0.0, g = 0.0, b = 0.0, a = 0.0;
      var n = 0;
      for (var sy = y0; sy < y1; sy++) {
        var at = (sy * source.width + x0) * 4;
        for (var sx = x0; sx < x1; sx++) {
          // Premultiplied, so a transparent texel's colour does not bleed
          // into the average and leave a dark halo where the edge was.
          final alpha = source.pixels[at + 3] / 255.0;
          r += source.pixels[at] * alpha;
          g += source.pixels[at + 1] * alpha;
          b += source.pixels[at + 2] * alpha;
          a += source.pixels[at + 3];
          at += 4;
          n++;
        }
      }
      if (n == 0) n = 1;
      final alpha = a / n;
      final to = (y * width + x) * 4;
      final back = alpha <= 0.0 ? 0.0 : 255.0 / alpha;
      out[to] = ((r / n) * back).round().clamp(0, 255);
      out[to + 1] = ((g / n) * back).round().clamp(0, 255);
      out[to + 2] = ((b / n) * back).round().clamp(0, 255);
      out[to + 3] = alpha.round().clamp(0, 255);
    }
  }

  final result = Rgba(width, height, out);
  // An averaged normal is shorter than the ones it averaged, and a shorter
  // normal lights as though the surface were flatter than it is. Putting it
  // back on the unit sphere is the difference between a resized normal map
  // and a slightly deflated one.
  if (normal) renormalise(result);
  return result;
}

/// Puts every texel of a normal map back on the unit sphere.
void renormalise(Rgba image) {
  final pixels = image.pixels;
  for (var i = 0; i < pixels.length; i += 4) {
    final x = pixels[i] / 127.5 - 1.0;
    final y = pixels[i + 1] / 127.5 - 1.0;
    final z = pixels[i + 2] / 127.5 - 1.0;
    final length = math.sqrt(x * x + y * y + z * z);
    if (length < 1e-6) {
      pixels[i] = 128;
      pixels[i + 1] = 128;
      pixels[i + 2] = 255;
      continue;
    }
    pixels[i] = ((x / length + 1.0) * 127.5).round().clamp(0, 255);
    pixels[i + 1] = ((y / length + 1.0) * 127.5).round().clamp(0, 255);
    pixels[i + 2] = ((z / length + 1.0) * 127.5).round().clamp(0, 255);
  }
}

/// Multiplies a factor into an image, in the space the factor is defined in.
///
/// glTF's factors are linear and its colour maps are sRGB-encoded, so a
/// base colour factor cannot simply be multiplied into the bytes: doing that
/// darkens midtones by about the amount people then try to fix with a light.
/// Folding the factor in is what lets two materials that differ only by tint
/// become one material afterwards, which is where the draw calls go.
void multiplyInto(Rgba image, List<double> factor, {required bool srgb}) {
  final pixels = image.pixels;
  final channels = math.min(4, factor.length);
  if (channels == 0) return;
  for (var i = 0; i < pixels.length; i += 4) {
    for (var c = 0; c < channels; c++) {
      if (factor[c] == 1.0) continue;
      // Alpha is linear whatever the colour channels are.
      if (srgb && c < 3) {
        pixels[i + c] = _toSrgb(_toLinear[pixels[i + c]] * factor[c]);
      } else {
        pixels[i + c] =
            (pixels[i + c] * factor[c]).round().clamp(0, 255);
      }
    }
  }
}

/// Draws `source` into `page` at (`x`, `y`), then copies its edge texels
/// `extrude` texels outwards.
///
/// The extrusion is not optional decoration. A page is sampled with bilinear
/// filtering and read at every mip level, and both reach past a cell's edge —
/// without a border of its own colour to reach into, a cell picks up its
/// neighbour, which shows up as a fringe of the wrong texture along every
/// seam and gets worse the further away the object is.
void blit(Rgba page, Rgba source, int x, int y, {int extrude = 0}) {
  for (var row = 0; row < source.height; row++) {
    final to = ((y + row) * page.width + x) * 4;
    final from = row * source.width * 4;
    if (to < 0 || to + source.width * 4 > page.pixels.length) continue;
    page.pixels.setRange(to, to + source.width * 4, source.pixels, from);
  }
  if (extrude <= 0) return;

  for (var e = 1; e <= extrude; e++) {
    // Left and right of every row.
    for (var row = 0; row < source.height; row++) {
      final py = y + row;
      _copyTexel(page, x, py, x - e, py);
      _copyTexel(page, x + source.width - 1, py, x + source.width - 1 + e, py);
    }
    // Above and below every column, corners included, so the diagonal
    // neighbours a bilinear tap can reach are filled too.
    for (var column = -e; column < source.width + e; column++) {
      final px = x + column;
      final clamped = x + column.clamp(0, source.width - 1);
      _copyTexel(page, clamped, y, px, y - e);
      _copyTexel(page, clamped, y + source.height - 1, px,
          y + source.height - 1 + e);
    }
  }
}

void _copyTexel(Rgba page, int fromX, int fromY, int toX, int toY) {
  if (toX < 0 || toY < 0 || toX >= page.width || toY >= page.height) return;
  if (fromX < 0 || fromY < 0 || fromX >= page.width || fromY >= page.height) {
    return;
  }
  final from = (fromY * page.width + fromX) * 4;
  final to = (toY * page.width + toX) * 4;
  page.pixels.setRange(to, to + 4, page.pixels, from);
}

/// A page filled with the value that makes `role` do nothing.
Rgba blankPage(TextureRole role, int width, int height) =>
    Rgba.filled(width, height, role.blank);
