// A small, offline-only PNG codec for the atlas cooker.
//
// `orblit_sprite` is a pure Dart package every game depends on, so it must
// not gain `package:image` or any other heavy dependency just so its offline
// command can read and write pictures. This file lives under `bin/`, not
// `lib/`, specifically so it is never compiled into a game: `dart:io`'s own
// zlib codec is the deflate PNG's IDAT chunks already use (RFC 1950 wraps
// RFC 1951, which is exactly what `ZLibCodec` speaks), so nothing beyond the
// Dart SDK a command-line tool already has is needed to decode or encode one.
//
// What this does not do: Adam7 interlacing, or writing anything smaller than
// 8-bit truecolour-with-alpha. Real art tools export straightforward PNGs
// almost always, and a cooker that refuses the rare interlaced file with a
// clear reason is a better trade than several hundred more lines nothing
// packs by is likely to exercise.

import 'dart:io';
import 'dart:typed_data';

/// A decoded picture: RGBA8, straight alpha, row nought at the top — the
/// same shape [AtlasSprite] and the rest of the packer expect.
class DecodedPng {
  const DecodedPng({required this.width, required this.height, required this.pixels});
  final int width;
  final int height;
  final Uint8List pixels;
}

/// A PNG this codec could not read, with why rather than a stack trace
/// through somebody else's parser.
class PngFormatException implements Exception {
  const PngFormatException(this.message);
  final String message;
  @override
  String toString() => 'PngFormatException: $message';
}

const List<int> _signature = [137, 80, 78, 71, 13, 10, 26, 10];

DecodedPng decodePng(Uint8List bytes) {
  if (bytes.length < 8 || !_startsWith(bytes, _signature)) {
    throw const PngFormatException('not a PNG: missing signature');
  }

  var offset = 8;
  int? width;
  int? height;
  int? bitDepth;
  int? colorType;
  final idat = BytesBuilder(copy: false);
  Uint8List? palette;
  Uint8List? transparency;

  while (offset + 8 <= bytes.length) {
    final length = _readUint32(bytes, offset);
    final type = String.fromCharCodes(bytes, offset + 4, offset + 8);
    final dataStart = offset + 8;
    final dataEnd = dataStart + length;
    if (dataEnd + 4 > bytes.length) {
      throw const PngFormatException('a chunk runs past the end of the file');
    }
    final data = bytes.sublist(dataStart, dataEnd);

    switch (type) {
      case 'IHDR':
        if (data.length != 13) throw const PngFormatException('IHDR is the wrong size');
        width = _readUint32(data, 0);
        height = _readUint32(data, 4);
        bitDepth = data[8];
        colorType = data[9];
        final interlace = data[12];
        if (interlace != 0) {
          throw const PngFormatException('interlaced PNGs are not supported by this cooker');
        }
      case 'PLTE':
        palette = data;
      case 'tRNS':
        transparency = data;
      case 'IDAT':
        idat.add(data);
      case 'IEND':
        offset = bytes.length;
        continue;
    }
    offset = dataEnd + 4;
  }

  if (width == null || height == null || bitDepth == null || colorType == null) {
    throw const PngFormatException('no IHDR chunk');
  }
  if (![1, 2, 4, 8, 16].contains(bitDepth)) {
    throw PngFormatException('unsupported bit depth $bitDepth');
  }
  if (![0, 2, 3, 4, 6].contains(colorType)) {
    throw PngFormatException('unsupported colour type $colorType');
  }
  if (colorType == 3 && palette == null) {
    throw const PngFormatException('a palette image has no PLTE chunk');
  }

  final channels = switch (colorType) {
    0 => 1,
    2 => 3,
    3 => 1,
    4 => 2,
    6 => 4,
    _ => throw PngFormatException('unsupported colour type $colorType'),
  };

  final raw = ZLibDecoder().convert(idat.takeBytes());
  final bitsPerPixel = channels * bitDepth;
  final bytesPerScanline = (width * bitsPerPixel + 7) ~/ 8;
  final bpp = (bitsPerPixel + 7) ~/ 8;

  final unfiltered = Uint8List(bytesPerScanline * height);
  var previous = Uint8List(bytesPerScanline);
  var read = 0;
  for (var y = 0; y < height; y++) {
    if (read >= raw.length) throw const PngFormatException('ran out of pixel data early');
    final filterType = raw[read];
    read++;
    final row = Uint8List(bytesPerScanline);
    for (var x = 0; x < bytesPerScanline; x++) {
      final raw0 = raw[read + x];
      final a = x >= bpp ? row[x - bpp] : 0;
      final b = previous[x];
      final c = x >= bpp ? previous[x - bpp] : 0;
      final value = switch (filterType) {
        0 => raw0,
        1 => raw0 + a,
        2 => raw0 + b,
        3 => raw0 + ((a + b) >> 1),
        4 => raw0 + _paeth(a, b, c),
        _ => throw PngFormatException('unsupported scanline filter $filterType'),
      };
      row[x] = value & 0xff;
    }
    read += bytesPerScanline;
    unfiltered.setRange(y * bytesPerScanline, (y + 1) * bytesPerScanline, row);
    previous = row;
  }

  final pixels = Uint8List(width * height * 4);
  for (var y = 0; y < height; y++) {
    final rowStart = y * bytesPerScanline;
    for (var x = 0; x < width; x++) {
      final pixelBit = x * bitsPerPixel;
      final di = (y * width + x) * 4;
      switch (colorType) {
        case 0:
          final raw0 = _sample(unfiltered, rowStart, pixelBit, bitDepth);
          final v = _to8(raw0, bitDepth);
          pixels[di] = v;
          pixels[di + 1] = v;
          pixels[di + 2] = v;
          pixels[di + 3] = _keyedAlpha1(transparency, raw0);
        case 2:
          final r = _sample(unfiltered, rowStart, pixelBit, bitDepth);
          final g = _sample(unfiltered, rowStart, pixelBit + bitDepth, bitDepth);
          final b = _sample(unfiltered, rowStart, pixelBit + bitDepth * 2, bitDepth);
          pixels[di] = _to8(r, bitDepth);
          pixels[di + 1] = _to8(g, bitDepth);
          pixels[di + 2] = _to8(b, bitDepth);
          pixels[di + 3] = _keyedAlpha3(transparency, r, g, b);
        case 3:
          final index = _sample(unfiltered, rowStart, pixelBit, bitDepth);
          final p = palette!;
          if (index * 3 + 2 < p.length) {
            pixels[di] = p[index * 3];
            pixels[di + 1] = p[index * 3 + 1];
            pixels[di + 2] = p[index * 3 + 2];
          }
          pixels[di + 3] = transparency != null && index < transparency.length ? transparency[index] : 255;
        case 4:
          final v = _sample(unfiltered, rowStart, pixelBit, bitDepth);
          final a = _sample(unfiltered, rowStart, pixelBit + bitDepth, bitDepth);
          final v8 = _to8(v, bitDepth);
          pixels[di] = v8;
          pixels[di + 1] = v8;
          pixels[di + 2] = v8;
          pixels[di + 3] = _to8(a, bitDepth);
        case 6:
          final r = _sample(unfiltered, rowStart, pixelBit, bitDepth);
          final g = _sample(unfiltered, rowStart, pixelBit + bitDepth, bitDepth);
          final b = _sample(unfiltered, rowStart, pixelBit + bitDepth * 2, bitDepth);
          final a = _sample(unfiltered, rowStart, pixelBit + bitDepth * 3, bitDepth);
          pixels[di] = _to8(r, bitDepth);
          pixels[di + 1] = _to8(g, bitDepth);
          pixels[di + 2] = _to8(b, bitDepth);
          pixels[di + 3] = _to8(a, bitDepth);
      }
    }
  }

  return DecodedPng(width: width, height: height, pixels: pixels);
}

/// [rgba] as an 8-bit truecolour-with-alpha PNG: one IDAT, filter type
/// `None` throughout. Not the smallest a page could be — a real filter
/// heuristic and multiple IDAT chunks would compress better — but simple
/// enough to be sure it round-trips, which for a page an atlas depends on
/// matters more than a few percent of file size.
Uint8List encodePng(int width, int height, Uint8List rgba) {
  if (rgba.length != width * height * 4) {
    throw ArgumentError('rgba must hold $width x $height RGBA8 texels');
  }

  final raw = BytesBuilder(copy: false);
  for (var y = 0; y < height; y++) {
    raw.addByte(0); // filter type None
    raw.add(rgba.sublist(y * width * 4, (y + 1) * width * 4));
  }
  final compressed = Uint8List.fromList(ZLibEncoder().convert(raw.takeBytes()));

  final out = BytesBuilder(copy: false);
  out.add(_signature);
  final ihdr = Uint8List(13);
  _writeUint32(ihdr, 0, width);
  _writeUint32(ihdr, 4, height);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // colour type: truecolour with alpha
  ihdr[10] = 0; // compression
  ihdr[11] = 0; // filter
  ihdr[12] = 0; // interlace
  _writeChunk(out, 'IHDR', ihdr);
  _writeChunk(out, 'IDAT', compressed);
  _writeChunk(out, 'IEND', Uint8List(0));
  return out.takeBytes();
}

void _writeChunk(BytesBuilder out, String type, Uint8List data) {
  final length = Uint8List(4);
  _writeUint32(length, 0, data.length);
  out.add(length);
  final typeAndData = Uint8List(4 + data.length)
    ..setRange(0, 4, type.codeUnits)
    ..setRange(4, 4 + data.length, data);
  out.add(typeAndData);
  final crc = Uint8List(4);
  _writeUint32(crc, 0, _crc32(typeAndData));
  out.add(crc);
}

bool _startsWith(Uint8List bytes, List<int> prefix) {
  if (bytes.length < prefix.length) return false;
  for (var i = 0; i < prefix.length; i++) {
    if (bytes[i] != prefix[i]) return false;
  }
  return true;
}

int _readUint32(Uint8List bytes, int offset) =>
    (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

void _writeUint32(Uint8List bytes, int offset, int value) {
  bytes[offset] = (value >> 24) & 0xff;
  bytes[offset + 1] = (value >> 16) & 0xff;
  bytes[offset + 2] = (value >> 8) & 0xff;
  bytes[offset + 3] = value & 0xff;
}

int _sample(Uint8List rows, int rowStart, int bitOffset, int bitDepth) {
  if (bitDepth == 16) {
    final byteIndex = rowStart + bitOffset ~/ 8;
    return (rows[byteIndex] << 8) | rows[byteIndex + 1];
  }
  if (bitDepth == 8) {
    return rows[rowStart + bitOffset ~/ 8];
  }
  final byteIndex = rowStart + bitOffset ~/ 8;
  final bitInByte = bitOffset % 8;
  final shift = 8 - bitInByte - bitDepth;
  final mask = (1 << bitDepth) - 1;
  return (rows[byteIndex] >> shift) & mask;
}

int _to8(int value, int bitDepth) {
  if (bitDepth == 8) return value;
  if (bitDepth == 16) return value >> 8;
  final maxValue = (1 << bitDepth) - 1;
  return (value * 255) ~/ maxValue;
}

int _keyedAlpha1(Uint8List? trns, int rawGray) {
  if (trns == null || trns.length < 2) return 255;
  final key = (trns[0] << 8) | trns[1];
  return rawGray == key ? 0 : 255;
}

int _keyedAlpha3(Uint8List? trns, int r, int g, int b) {
  if (trns == null || trns.length < 6) return 255;
  final kr = (trns[0] << 8) | trns[1];
  final kg = (trns[2] << 8) | trns[3];
  final kb = (trns[4] << 8) | trns[5];
  return (r == kr && g == kg && b == kb) ? 0 : 255;
}

int _paeth(int a, int b, int c) {
  final p = a + b - c;
  final pa = (p - a).abs();
  final pb = (p - b).abs();
  final pc = (p - c).abs();
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

// The CRC-32 used by every PNG chunk (ISO/IEC 15948 / RFC 2083 Annex D),
// whose reference implementation the PNG specification itself places in the
// public domain. Table-built once per isolate rather than per chunk.
final List<int> _crcTable = _buildCrcTable();

List<int> _buildCrcTable() {
  final table = List<int>.filled(256, 0);
  for (var n = 0; n < 256; n++) {
    var c = n;
    for (var k = 0; k < 8; k++) {
      c = (c & 1) != 0 ? (0xedb88320 ^ (c >> 1)) : (c >> 1);
    }
    table[n] = c;
  }
  return table;
}

int _crc32(Uint8List bytes) {
  var crc = 0xffffffff;
  for (final byte in bytes) {
    crc = _crcTable[(crc ^ byte) & 0xff] ^ (crc >> 8);
  }
  return crc ^ 0xffffffff;
}
