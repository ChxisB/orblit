import 'dart:typed_data';

/// How big a picture is, read from its header without decoding it.
class PictureSize {
  const PictureSize({
    required this.width,
    required this.height,
    this.depth = 1,
    this.layers = 1,
    this.faces = 1,
    this.levels = 1,
  });

  final int width;
  final int height;

  /// Slices, for a 3D texture. One for everything else.
  final int depth;

  /// Array slices, for a texture array. One for everything else.
  final int layers;

  /// Six for a cube map, one otherwise.
  final int faces;

  /// Mip levels stored in the file. One when the file holds only the
  /// full-size picture.
  final int levels;

  /// Every pixel the file will decode to, mips, layers and faces included.
  ///
  /// The number that matters for memory. A 4096 cube map with mips is fifty
  /// times the pixels its width and height suggest, and a limit that looked at
  /// width and height alone would wave it through.
  int get pixels {
    var total = 0;
    for (var level = 0; level < levels; level++) {
      final w = width >> level;
      final h = height >> level;
      final d = depth >> level;
      total += (w < 1 ? 1 : w) * (h < 1 ? 1 : h) * (d < 1 ? 1 : d);
    }
    return total * layers * faces;
  }

  @override
  String toString() {
    final size = depth > 1 ? '$width×$height×$depth' : '$width×$height';
    final extra = [
      if (levels > 1) '$levels levels',
      if (layers > 1) '$layers layers',
      if (faces > 1) '$faces faces',
    ];
    return extra.isEmpty ? size : '$size, ${extra.join(', ')}';
  }
}

/// How big the picture in [bytes] is, or null when this is not a picture in a
/// format that can be read this way.
///
/// **Header only.** Nothing here allocates in proportion to the picture, walks
/// the pixel data or hands anything to a decoder, which is the whole point: it
/// is the check that runs *before* a downloaded file is decoded, on bytes
/// nobody has vouched for yet. A picture whose dimensions would fill memory is
/// a few dozen bytes on the wire, and by the time a decoder has been asked how
/// big it is, it has usually already tried to allocate it.
///
/// Null means "this cannot be checked", not "this is safe". A caller enforcing
/// a limit has to decide what to do about a format it cannot read, and saying
/// so with null rather than a reassuring zero is how it gets that choice.
PictureSize? pictureSizeOf(Uint8List bytes) {
  if (_startsWith(bytes, _png)) return _pngSize(bytes);
  if (_startsWith(bytes, _ktx2)) return _ktx2Size(bytes);
  if (bytes.length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) {
    return _jpegSize(bytes);
  }
  if (_startsWith(bytes, _riff) &&
      bytes.length >= 12 &&
      _startsWith(Uint8List.sublistView(bytes, 8), _webp)) {
    return _webpSize(bytes);
  }
  return null;
}

const List<int> _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
const List<int> _riff = [0x52, 0x49, 0x46, 0x46]; // RIFF
const List<int> _webp = [0x57, 0x45, 0x42, 0x50]; // WEBP
const List<int> _ktx2 = [
  0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A, //
];

/// IHDR is required to be the first chunk, so its place is fixed: eight bytes
/// of signature, four of length, four of type, then the two dimensions.
PictureSize? _pngSize(Uint8List bytes) {
  if (bytes.length < 24) return null;
  if (bytes[12] != 0x49 || // I
      bytes[13] != 0x48 || // H
      bytes[14] != 0x44 || // D
      bytes[15] != 0x52) {
    return null;
  }
  final width = _big32(bytes, 16);
  final height = _big32(bytes, 20);
  if (width <= 0 || height <= 0) return null;
  return PictureSize(width: width, height: height);
}

/// KTX2 puts everything needed in a fixed header, little-endian, and is the
/// only format here that can hold mips, layers and faces — so it is the one
/// where reading past width and height matters.
PictureSize? _ktx2Size(Uint8List bytes) {
  if (bytes.length < 48) return null;
  final width = _little32(bytes, 20);
  final height = _little32(bytes, 24);
  if (width <= 0) return null;
  return PictureSize(
    width: width,
    // A 1D texture writes nought for height and depth, meaning "not that many,
    // none at all" — one row, and one slice.
    height: height < 1 ? 1 : height,
    depth: _orOne(_little32(bytes, 28)),
    layers: _orOne(_little32(bytes, 32)),
    faces: _orOne(_little32(bytes, 36)),
    levels: _orOne(_little32(bytes, 40)),
  );
}

/// Walks JPEG's segments to the frame header, which is the only segment that
/// states the size and can sit behind any amount of metadata — a phone's EXIF
/// thumbnail alone is tens of kilobytes.
PictureSize? _jpegSize(Uint8List bytes) {
  var at = 2;
  while (at + 4 <= bytes.length) {
    if (bytes[at] != 0xFF) return null;

    // Fill bytes: any number of 0xFF may pad the gap before a marker.
    var marker = bytes[at + 1];
    var next = at + 2;
    while (marker == 0xFF && next < bytes.length) {
      marker = bytes[next];
      next++;
    }

    // Standalone markers carry no length: restarts, and start-of-image.
    if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9)) {
      at = next;
      continue;
    }
    if (next + 2 > bytes.length) return null;
    final length = _big16(bytes, next);
    if (length < 2) return null;

    // Every frame marker states the size the same way. C4, C8 and CC are the
    // three in this range that are not frames — a Huffman table, an extension
    // and an arithmetic-coding table — and reading a size out of one would
    // give a number that looks plausible.
    final frame =
        (marker >= 0xC0 && marker <= 0xCF) &&
        marker != 0xC4 &&
        marker != 0xC8 &&
        marker != 0xCC;
    if (frame) {
      if (next + 7 > bytes.length) return null;
      final height = _big16(bytes, next + 3);
      final width = _big16(bytes, next + 5);
      if (width <= 0 || height <= 0) return null;
      return PictureSize(width: width, height: height);
    }

    // Start of scan: the pixel data begins, and there is no frame header
    // behind it. Stop rather than walk into the entropy-coded bytes, where
    // anything can look like a marker.
    if (marker == 0xDA) return null;
    at = next + length;
  }
  return null;
}

/// WebP is three formats behind one signature, and each states its size
/// differently and in bit fields rather than bytes.
PictureSize? _webpSize(Uint8List bytes) {
  if (bytes.length < 16) return null;
  final tag = String.fromCharCodes(bytes, 12, 16);
  switch (tag) {
    case 'VP8 ':
      // A lossy frame: three bytes of frame tag, a three-byte sync code, then
      // fourteen bits each of width and height with a two-bit scale above.
      if (bytes.length < 30) return null;
      if (bytes[23] != 0x9D || bytes[24] != 0x01 || bytes[25] != 0x2A) {
        return null;
      }
      return PictureSize(
        width: _little16(bytes, 26) & 0x3FFF,
        height: _little16(bytes, 28) & 0x3FFF,
      );
    case 'VP8L':
      // Lossless: a one-byte signature, then fourteen bits of width minus one
      // and fourteen of height minus one, packed across four bytes.
      if (bytes.length < 25 || bytes[20] != 0x2F) return null;
      final packed =
          bytes[21] | (bytes[22] << 8) | (bytes[23] << 16) | (bytes[24] << 24);
      return PictureSize(
        width: (packed & 0x3FFF) + 1,
        height: ((packed >> 14) & 0x3FFF) + 1,
      );
    case 'VP8X':
      // Extended: the canvas size, three bytes each, minus one. This is the
      // one that matters for a limit — an animation or an alpha-plus-colour
      // file states its real size only here.
      if (bytes.length < 30) return null;
      return PictureSize(
        width: _little24(bytes, 24) + 1,
        height: _little24(bytes, 27) + 1,
      );
    default:
      return null;
  }
}

int _orOne(int value) => value < 1 ? 1 : value;

bool _startsWith(Uint8List bytes, List<int> prefix) {
  if (bytes.length < prefix.length) return false;
  for (var i = 0; i < prefix.length; i++) {
    if (bytes[i] != prefix[i]) return false;
  }
  return true;
}

int _big16(Uint8List b, int at) => (b[at] << 8) | b[at + 1];

int _big32(Uint8List b, int at) =>
    (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

int _little16(Uint8List b, int at) => b[at] | (b[at + 1] << 8);

int _little24(Uint8List b, int at) =>
    b[at] | (b[at + 1] << 8) | (b[at + 2] << 16);

int _little32(Uint8List b, int at) =>
    b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
