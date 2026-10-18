import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/painting.dart';

/// Many short strings drawn once into one picture.
///
/// A name over a head is the same few words every frame. Drawing each as its
/// own image costs a texture per label, while one picture with every distinct
/// string packed into it costs one texture however many labels use it. Each
/// string keeps a rectangle in that picture, and whatever draws the label
/// samples that rectangle.
///
/// The picture has a fixed [width] and grows in height. Growing never moves a
/// string, so a rectangle read earlier stays right in pixels. Only the
/// picture's height changes, so a caller that turns a rectangle into texture
/// coordinates divides by the current [size].
final class LabelAtlas {
  LabelAtlas({
    this.style = const TextStyle(color: Color(0xFFFFFFFF), fontSize: 32),
    this.width = 1024,
    int initialHeight = 128,
    this.maxHeight = 4096,
    this.padding = 2,
  }) : assert(initialHeight <= maxHeight),
       _height = initialHeight;

  /// How the strings are drawn. White, so the surface that samples the atlas
  /// can tint it.
  final TextStyle style;

  /// The picture's width in pixels. A string wider than this, with its
  /// [padding], is refused.
  final int width;

  /// The tallest the picture may grow. Past it [add] throws.
  final int maxHeight;

  /// Empty pixels left around each string, so a filtered sample at its edge
  /// does not pick up its neighbour.
  final int padding;

  final Map<String, ui.Rect> _rects = {};
  int _height;
  int _revision = 0;
  bool _dirty = false;

  // The shelf being filled: where the next string goes and how tall the
  // tallest string on the shelf is.
  late double _x = padding.toDouble();
  late double _y = padding.toDouble();
  double _shelf = 0;

  /// The picture's size in pixels.
  ui.Size get size => ui.Size(width.toDouble(), _height.toDouble());

  /// Changes each time [paint] draws a picture that differs from the last.
  int get revision => _revision;

  /// Whether a string has been added since [paint] last drew.
  bool get isDirty => _dirty;

  /// Where [text] sits in the picture, in pixels, or null if it was never added.
  ui.Rect? rectOf(String text) => _rects[text];

  /// Gives [text] a place. A string already in the atlas keeps its place.
  void add(String text) {
    if (_rects.containsKey(text)) return;
    _rects[text] = _place(_measure(text));
    _dirty = true;
  }

  /// Draws every string and returns the picture as PNG bytes.
  // shortcut: redraws every string on each change, upgrade when an atlas with
  // thousands of strings changes every frame.
  Future<Uint8List> paint() async {
    // Cleared before the await so a string added while the picture encodes
    // marks the atlas out of date again.
    if (_dirty) _revision++;
    _dirty = false;
    final recorder = ui.PictureRecorder();
    final canvas = ui.Canvas(recorder);
    _rects.forEach((text, rect) {
      _painter(text)
        ..paint(canvas, rect.topLeft)
        ..dispose();
    });
    final picture = recorder.endRecording();
    final image = await picture.toImage(width, _height);
    picture.dispose();
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    image.dispose();
    return data!.buffer.asUint8List();
  }

  ui.Size _measure(String text) {
    final painter = _painter(text);
    final size = painter.size;
    painter.dispose();
    return size;
  }

  TextPainter _painter(String text) => TextPainter(
    text: TextSpan(text: text, style: style),
    textDirection: TextDirection.ltr,
  )..layout();

  /// Finds a spot on the current shelf, or on a new one, and grows the picture
  /// if the spot is below its bottom. Nothing changes if it cannot fit.
  ui.Rect _place(ui.Size label) {
    final w = label.width.ceilToDouble();
    final h = label.height.ceilToDouble();
    if (w + 2 * padding > width) {
      throw ArgumentError.value(w, 'text', 'wider than the atlas');
    }
    final gap = padding.toDouble();
    final wraps = _x + w + gap > width;
    final x = wraps ? gap : _x;
    final y = wraps ? _y + _shelf + gap : _y;
    var height = _height;
    while (y + h + gap > height) {
      height *= 2;
    }
    if (height > maxHeight) {
      throw StateError('the label atlas is full at ${width}x$maxHeight');
    }
    _height = height;
    _x = x + w + gap;
    _y = y;
    _shelf = wraps ? h : math.max(_shelf, h);
    return ui.Rect.fromLTWH(x, y, w, h);
  }
}
