import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'material.dart' show OrblitFilter, OrblitTexture;

/// How a sprite layer's pixels combine with what is behind them.
enum OrblitSpriteBlend {
  /// Over what is behind, by the picture's own alpha. Premultiplied, so a soft
  /// edge fades to what is behind it rather than to a dark fringe.
  alpha,

  /// Added to what is behind, so it only ever brightens: sparks, glows,
  /// a muzzle flash, a laser.
  add,
}

/// One flat picture: a rectangle of a layer's image, placed in the layer.
///
/// Plain numbers rather than vectors, so a sprite can be `const` and a list of
/// ten thousand of them is ten thousand small objects rather than forty
/// thousand.
class OrblitSprite {
  const OrblitSprite({
    required this.x,
    required this.y,
    this.width = 1,
    this.height = 1,
    this.depth = 0,
    this.rotation = 0,
    this.pivotX = 0.5,
    this.pivotY = 0.5,
    this.u0 = 0,
    this.v0 = 0,
    this.u1 = 1,
    this.v1 = 1,
    this.red = 1,
    this.green = 1,
    this.blue = 1,
    this.alpha = 1,
  });

  /// Where the pivot is, in the layer's own units.
  final double x;
  final double y;

  /// How big it is. Negative flips it: a character facing left is the same
  /// sprite as one facing right, with a width of minus itself.
  final double width;
  final double height;

  /// Carried through to the renderer, so sprites share a depth with anything
  /// solid in the scene. Within a layer, order decides what is on top.
  final double depth;

  /// A turn about the pivot, in radians, anticlockwise.
  final double rotation;

  /// The point it is placed and turned by, as a fraction of its size: nought
  /// is the left or bottom edge and one the right or top. The middle, by
  /// default; the bottom middle is what a character standing on something
  /// wants.
  final double pivotX;
  final double pivotY;

  /// The rectangle of the layer's image it shows, as fractions of the image,
  /// with [v0] at the top — which is how an atlas measures a region.
  final double u0;
  final double v0;
  final double u1;
  final double v1;

  /// Its colour, in linear light, multiplied over the image.
  final double red;
  final double green;
  final double blue;
  final double alpha;

  /// The rectangle of an image this is cut from, given in pixels.
  OrblitSprite cut(
    int left,
    int top,
    int width,
    int height,
    int imageWidth,
    int imageHeight,
  ) {
    return OrblitSprite(
      x: x,
      y: y,
      width: this.width,
      height: this.height,
      depth: depth,
      rotation: rotation,
      pivotX: pivotX,
      pivotY: pivotY,
      u0: left / imageWidth,
      v0: top / imageHeight,
      u1: (left + width) / imageWidth,
      v1: (top + height) / imageHeight,
      red: red,
      green: green,
      blue: blue,
      alpha: alpha,
    );
  }

  /// Writes this sprite's [OrblitSprites.stride] floats at [at].
  void writeInto(Float32List into, int at) {
    into[at] = x;
    into[at + 1] = y;
    into[at + 2] = depth;
    into[at + 3] = rotation;
    into[at + 4] = width;
    into[at + 5] = height;
    into[at + 6] = pivotX;
    into[at + 7] = pivotY;
    into[at + 8] = u0;
    into[at + 9] = v0;
    into[at + 10] = u1;
    into[at + 11] = v1;
    into[at + 12] = red;
    into[at + 13] = green;
    into[at + 14] = blue;
    into[at + 15] = alpha;
  }
}

/// A layer of sprites: one image, and any number of rectangles cut from it.
///
/// A layer is one draw however many sprites it holds, and draws them in the
/// order [sprites] gives them. Layers draw by [order], lowest first. Every
/// part of the renderer a 2D scene needs is here: an orthographic
/// [OrblitCamera], and — because a sprite's colours are the artist's —
/// `ToneMapping.linear`, so the picture is not graded as though it were
/// photographed.
///
/// The sprites cross to the renderer only when [revision] is not the one it
/// last took, the bargain a population makes. Everything else about a layer —
/// its [transform], its [tint] — is sent every frame and costs nothing to
/// change, which is how a backdrop scrolls or a whole layer fades.
class OrblitSprites {
  OrblitSprites({
    required this.key,
    required this.sprites,
    this.image,
    Matrix4? transform,
    Vector4? tint,
    this.order = 0,
    this.filter = OrblitFilter.sharp,
    bool? snap,
    this.blend = OrblitSpriteBlend.alpha,
    this.revision = 0,
  }) : transform = transform ?? Matrix4.identity(),
       tint = tint ?? Vector4.all(1),
       snap = snap ?? filter == OrblitFilter.sharp,
       assert(
         sprites.length % stride == 0,
         'sprites are whole records of $stride floats',
       );

  /// What this layer is, across frames.
  final int key;

  /// The sprites, [stride] floats each — see [OrblitSprite.writeInto] and
  /// [pack].
  final Float32List sprites;

  /// The picture the sprites are cut from. Null draws each as a rectangle of
  /// its own colour. A resource name works as a path does — see
  /// `OrblitResources`.
  final OrblitTexture? image;

  /// Where the layer is: every sprite in it, moved at once.
  final Matrix4 transform;

  /// A colour over every sprite in the layer, in linear light.
  final Vector4 tint;

  /// Which layers draw first: lowest first, and signed, so a backdrop at -10
  /// is behind a default layer at nought.
  final int order;

  /// [OrblitFilter.sharp] for pixel art, which is the default because a 2D
  /// layer is more often pixel art than not; [OrblitFilter.smooth] for
  /// anything painted.
  final OrblitFilter filter;

  /// Whether corners land on whole pixels. On by default for sharp layers,
  /// where a sprite moving by a fraction of a pixel would otherwise shimmer.
  final bool snap;

  final OrblitSpriteBlend blend;

  /// Bumped by whoever changes [sprites].
  final int revision;

  /// Floats a sprite. Must match kSpriteRecordFloats and spriteStride.
  static const int stride = 16;

  /// Floats a layer's settings take: the transform, then the tint. Must match
  /// kSpriteLayerParams and spriteLayerStride.
  static const int layerStride = 20;

  int get count => sprites.length ~/ stride;

  /// The bits the renderer reads, in the order OrblitSprites.h names them.
  int get flags =>
      (filter == OrblitFilter.sharp ? 1 : 0) |
      (snap ? 2 : 0) |
      (image != null && !image!.srgb ? 4 : 0) |
      (blend == OrblitSpriteBlend.add ? 8 : 0);

  /// Writes this layer's [layerStride] floats at [at].
  void packParams(Float32List into, int at) {
    into.setRange(at, at + 16, transform.storage);
    into[at + 16] = tint.x;
    into[at + 17] = tint.y;
    into[at + 18] = tint.z;
    into[at + 19] = tint.w;
  }

  /// [list] as the floats [sprites] takes.
  static Float32List pack(List<OrblitSprite> list) {
    final out = Float32List(list.length * stride);
    for (var i = 0; i < list.length; i++) {
      list[i].writeInto(out, i * stride);
    }
    return out;
  }
}
