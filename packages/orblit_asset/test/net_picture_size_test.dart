import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  group('what a picture says it is', () {
    test('a PNG, from its IHDR', () {
      final size = pictureSizeOf(_png(1920, 1080));
      expect(size!.width, 1920);
      expect(size.height, 1080);
      expect(size.pixels, 1920 * 1080);
    });

    test('a JPEG, from the frame header after the segments before it', () {
      // Two segments to walk past first, because a reader that assumes the
      // frame comes straight after the marker works on the file it was
      // written against and nothing else.
      final size = pictureSizeOf(_jpeg(800, 600, before: 2));
      expect(size!.width, 800);
      expect(size.height, 600);
    });

    test('a progressive JPEG, whose frame marker is not C0', () {
      final size = pictureSizeOf(_jpeg(640, 480, frame: 0xc2));
      expect(size!.width, 640);
      expect(size.height, 480);
    });

    test('a lossy WebP', () {
      final size = pictureSizeOf(_webpLossy(1254, 1254));
      expect(size!.width, 1254);
      expect(size.height, 1254);
    });

    test('a lossless WebP, whose size is packed across bytes', () {
      final size = pictureSizeOf(_webpLossless(1254, 1254));
      expect(size!.width, 1254);
      expect(size.height, 1254);
    });

    test('an extended WebP, from the canvas', () {
      final size = pictureSizeOf(_webpExtended(1254, 1254));
      expect(size!.width, 1254);
      expect(size.height, 1254);
    });

    test('a KTX2', () {
      final size = pictureSizeOf(_ktx2(2048, 2048));
      expect(size!.width, 2048);
      expect(size.height, 2048);
      expect(size.levels, 1);
      expect(size.pixels, 2048 * 2048);
    });
  });

  group('what it would cost to decode', () {
    test('counts every mip level, not just the biggest', () {
      // The reason this is not width × height: a mipped texture is about a
      // third larger again, and a limit that ignores the chain is a limit
      // that lets a third more through than it says.
      final size = pictureSizeOf(_ktx2(256, 256, levels: 9));
      expect(size!.levels, 9);
      var expected = 0;
      for (var at = 0; at < 9; at++) {
        expected += (256 >> at) * (256 >> at);
      }
      expect(size.pixels, expected);
    });

    test('counts all six faces of a cube map', () {
      final size = pictureSizeOf(_ktx2(512, 512, faces: 6));
      expect(size!.faces, 6);
      expect(size.pixels, 512 * 512 * 6);
    });

    test('counts every slice of an array', () {
      final size = pictureSizeOf(_ktx2(64, 64, layers: 4));
      expect(size!.layers, 4);
      expect(size.pixels, 64 * 64 * 4);
    });

    test('a zero in the header means one', () {
      // KTX2 writes nought for "not an array" and "not a cube map", which is
      // a fine way to say it and a terrible number to multiply by.
      final size = pictureSizeOf(_ktx2(32, 32, layers: 0, faces: 0, levels: 0));
      expect(size!.pixels, 32 * 32);
    });
  });

  group('what it cannot tell', () {
    test('says nothing rather than guessing at an unknown format', () {
      expect(
        pictureSizeOf(Uint8List.fromList(utf8.encode('glTF not a picture'))),
        isNull,
      );
    });

    test('says nothing for a header that stops partway', () {
      final half = _png(1024, 1024).sublist(0, 18);
      expect(pictureSizeOf(half), isNull);
    });

    test('says nothing for an empty file', () {
      expect(pictureSizeOf(Uint8List(0)), isNull);
    });

    test('says nothing for a KTX1, which is a different format', () {
      final ktx1 = Uint8List(64)
        ..setAll(0, [0xab, 0x4b, 0x54, 0x58, 0x20, 0x31, 0x31, 0xbb]);
      expect(pictureSizeOf(ktx1), isNull);
    });
  });
}

Uint8List _png(int width, int height) {
  final bytes = Uint8List(64);
  bytes.setAll(0, const [137, 80, 78, 71, 13, 10, 26, 10]);
  final view = ByteData.view(bytes.buffer);
  view.setUint32(8, 13);
  bytes.setAll(12, utf8.encode('IHDR'));
  view.setUint32(16, width);
  view.setUint32(20, height);
  return bytes;
}

/// A JPEG with [before] segments ahead of the frame, which uses [frame] as its
/// marker.
Uint8List _jpeg(int width, int height, {int before = 0, int frame = 0xc0}) {
  final out = BytesBuilder()..add([0xff, 0xd8]);
  for (var at = 0; at < before; at++) {
    out.add([0xff, 0xe0, 0x00, 0x10]);
    out.add(List.filled(14, 0));
  }
  out.add([
    0xff,
    frame,
    0x00,
    0x11,
    0x08,
    (height >> 8) & 0xff,
    height & 0xff,
    (width >> 8) & 0xff,
    width & 0xff,
  ]);
  out.add([0xff, 0xda]);
  return out.toBytes();
}

Uint8List _riff(String fourcc, Uint8List payload) {
  final bytes = Uint8List(12 + payload.length);
  bytes.setAll(0, utf8.encode('RIFF'));
  ByteData.view(bytes.buffer).setUint32(4, 4 + payload.length, Endian.little);
  bytes.setAll(8, utf8.encode('WEBP'));
  bytes.setAll(12, payload);
  return bytes;
}

Uint8List _webpLossy(int width, int height) {
  final chunk = Uint8List(32);
  chunk.setAll(0, utf8.encode('VP8 '));
  final view = ByteData.view(chunk.buffer);
  view.setUint32(4, 20, Endian.little);
  // Three bytes of frame tag, then the sync code, then the sizes.
  chunk.setAll(11, const [0x9d, 0x01, 0x2a]);
  view.setUint16(14, width, Endian.little);
  view.setUint16(16, height, Endian.little);
  return _riff('VP8 ', chunk);
}

Uint8List _webpLossless(int width, int height) {
  final chunk = Uint8List(24);
  chunk.setAll(0, utf8.encode('VP8L'));
  final view = ByteData.view(chunk.buffer);
  view.setUint32(4, 16, Endian.little);
  chunk[8] = 0x2f;
  // Fourteen bits each, one less than the size, packed little-endian.
  view.setUint32(9, (width - 1) | ((height - 1) << 14), Endian.little);
  return _riff('VP8L', chunk);
}

Uint8List _webpExtended(int width, int height) {
  final chunk = Uint8List(24);
  chunk.setAll(0, utf8.encode('VP8X'));
  ByteData.view(chunk.buffer).setUint32(4, 10, Endian.little);
  // Twenty-four bits each, one less than the size.
  for (var at = 0; at < 3; at++) {
    chunk[12 + at] = ((width - 1) >> (8 * at)) & 0xff;
    chunk[15 + at] = ((height - 1) >> (8 * at)) & 0xff;
  }
  return _riff('VP8X', chunk);
}

Uint8List _ktx2(
  int width,
  int height, {
  int levels = 1,
  int faces = 1,
  int layers = 1,
}) {
  final entries = levels == 0 ? 1 : levels;
  final bytes = Uint8List(80 + entries * 24);
  bytes.setAll(0, const [
    0xab,
    0x4b,
    0x54,
    0x58,
    0x20,
    0x32,
    0x30,
    0xbb,
    0x0d,
    0x0a,
    0x1a,
    0x0a,
  ]);
  final view = ByteData.view(bytes.buffer);
  view.setUint32(12, 1, Endian.little); // vkFormat
  view.setUint32(16, 1, Endian.little); // typeSize
  view.setUint32(20, width, Endian.little);
  view.setUint32(24, height, Endian.little);
  view.setUint32(28, 0, Endian.little); // pixelDepth
  view.setUint32(32, layers, Endian.little);
  view.setUint32(36, faces, Endian.little);
  view.setUint32(40, levels, Endian.little);
  return bytes;
}
