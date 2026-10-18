import 'dart:typed_data';
import 'dart:ui' as ui;

typedef Picture = ({int width, int height, Uint8List rgba});

/// The width, height and RGBA bytes of a PNG.
Future<Picture> decode(Uint8List png) async {
  final codec = await ui.instantiateImageCodec(png);
  final frame = await codec.getNextFrame();
  final data = await frame.image.toByteData();
  final out = (
    width: frame.image.width,
    height: frame.image.height,
    rgba: data!.buffer.asUint8List(),
  );
  frame.image.dispose();
  codec.dispose();
  return out;
}

List<int> pixel(Picture image, int x, int y) {
  final at = (y * image.width + x) * 4;
  return image.rgba.sublist(at, at + 4);
}
