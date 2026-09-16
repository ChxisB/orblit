// Exercises the offline cooker's own PNG codec. It lives under bin/, not
// lib/, so this test reaches it by a relative file path rather than a
// package: import — the same way the codec itself is only ever meant to be
// reached by bin/atlas_cook.dart, never by a game that depends on
// orblit_sprite.
import 'dart:typed_data';

import 'package:test/test.dart';

import '../bin/src/png_codec.dart';

Uint8List _pattern(int w, int h, {int alphaAt = 255}) {
  final out = Uint8List(w * h * 4);
  for (var y = 0; y < h; y++) {
    for (var x = 0; x < w; x++) {
      final i = (y * w + x) * 4;
      out[i] = (x * 17 + 3) % 256;
      out[i + 1] = (y * 53 + 11) % 256;
      out[i + 2] = (x + y) % 256;
      out[i + 3] = alphaAt;
    }
  }
  return out;
}

void main() {
  group('the offline PNG codec', () {
    test('encoding then decoding gives back the same pixels', () {
      final pixels = _pattern(13, 9);
      final bytes = encodePng(13, 9, pixels);
      final decoded = decodePng(bytes);
      expect(decoded.width, 13);
      expect(decoded.height, 9);
      expect(decoded.pixels, equals(pixels));
    });

    test('partial transparency round-trips exactly', () {
      final pixels = _pattern(6, 6, alphaAt: 40);
      final decoded = decodePng(encodePng(6, 6, pixels));
      expect(decoded.pixels, equals(pixels));
    });

    test('a one-by-one image round-trips', () {
      final pixels = Uint8List.fromList([10, 20, 30, 40]);
      final decoded = decodePng(encodePng(1, 1, pixels));
      expect(decoded.pixels, equals(pixels));
    });

    test('something that is not a PNG is refused with a reason', () {
      expect(
        () => decodePng(Uint8List.fromList('not a png'.codeUnits)),
        throwsA(isA<PngFormatException>()),
      );
    });

    test('mismatched pixel data is refused rather than writing a bad file', () {
      expect(() => encodePng(4, 4, Uint8List(10)), throwsArgumentError);
    });
  });
}
