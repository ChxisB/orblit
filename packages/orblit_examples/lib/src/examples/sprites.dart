import 'dart:async';
import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';

/// A 2D scene, drawn by the same renderer as every 3D one.
///
/// Three layers, each one draw however many sprites it holds: a brick
/// backdrop, a crowd of coins turning over, and sparks added on top. The
/// sprite sheet is painted here, in code, and handed to the renderer as bytes
/// — no file is written anywhere — which is how a browser, an APK or a
/// download hands a picture over too.
///
/// The backdrop is the part worth watching. Its tiles are sent once; what
/// scrolls it is the layer's transform, twenty floats a frame. The coins are
/// the other extreme: every one of them moves, so all of them are sent every
/// frame, and even at twenty thousand that is one buffer and one draw.
class SpritesExample extends Example {
  SpritesExample();

  @override
  String get name => 'Sprites';

  @override
  ExampleSection get section => ExampleSection.content;

  @override
  String get blurb =>
      'Pixel art in layers: one draw a layer, drawn in order, and a backdrop '
      'that scrolls without resending a tile.';

  /// How many coins there are.
  int count = 2000;

  /// Nearest sampling, for pixel art; off smooths the pixels into mush, which
  /// is the point of being able to see it.
  bool sharp = true;

  /// Corners on whole pixels, so a sprite moving by a fraction of one does not
  /// shimmer.
  bool snap = true;

  bool scrolling = true;
  bool sparks = true;

  static const _counts = {'200': 200, '2k': 2000, '20k': 20000};

  /// The sheet: four columns and four rows of sixteen-pixel cells.
  static const _sheet = 'orblit:resource/examples/sprites/sheet.png';
  static const _side = 64;
  static const _cell = 16;

  /// How tall the view is, in world units. A cell is shown at one unit, so a
  /// sixteen-pixel sprite is an exact multiple of a pixel at most window
  /// sizes.
  static const _viewHeight = 18.0;

  Atlas? _atlas;
  Future<void>? _painting;

  Float32List _coins = Float32List(0);
  List<_Coin> _crowd = const [];
  int _coinRevision = 0;

  Float32List? _tiles;
  Float32List _sparks = Float32List(0);
  int _sparkRevision = 0;

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    _painting ??= _paint();
    final atlas = _atlas;

    if (_crowd.length != count) _crowd = _Coin.crowd(count);
    if (_coins.length != count * OrblitSprites.stride) {
      _coins = Float32List(count * OrblitSprites.stride);
    }

    final layers = <OrblitSprites>[];
    if (atlas != null) {
      final tile = atlas['frame_4']!.uv(_side, _side);
      final tiles = _tiles ??= _backdrop(tile);
      // Scrolled by exactly one tile's width and back, so the wrap is seamless.
      final scroll = scrolling ? -(seconds * 1.5) % 2.0 : 0.0;
      layers.add(
        OrblitSprites(
          key: 1,
          sprites: tiles,
          image: const OrblitTexture(_sheet),
          order: -10,
          filter: sharp ? OrblitFilter.sharp : OrblitFilter.smooth,
          snap: snap,
          tint: Vector4(0.35, 0.35, 0.42, 1),
          transform: Matrix4.translationValues(scroll, 0, 0),
        ),
      );

      _placeCoins(atlas, seconds);
      layers.add(
        OrblitSprites(
          key: 2,
          sprites: _coins,
          image: const OrblitTexture(_sheet),
          filter: sharp ? OrblitFilter.sharp : OrblitFilter.smooth,
          snap: snap,
          revision: _coinRevision,
        ),
      );

      if (sparks) {
        _placeSparks(atlas, seconds);
        layers.add(
          OrblitSprites(
            key: 3,
            sprites: _sparks,
            image: const OrblitTexture(_sheet),
            order: 5,
            filter: OrblitFilter.smooth,
            blend: OrblitSpriteBlend.add,
            revision: _sparkRevision,
          ),
        );
      }
    }

    return OrblitScene(
      objects: const [],
      sprites: layers,
      camera: OrblitCamera(
        position: Vector3(0, 0, 20),
        target: Vector3.zero(),
        orthographic: true,
        viewHeight: _viewHeight,
      ),
      sky: OrblitSky(colour: Vector3(0.01, 0.01, 0.02), ambient: 0),
      // Straight through, with nothing smoothing edges: a sprite's colours are
      // the artist's, and its edges are meant to be exactly as sharp as drawn.
      post: OrblitPostProcess(
        antiAliasing: AntiAliasing.off,
        dithering: false,
        grading: OrblitGrading(toneMapping: ToneMapping.linear),
      ),
    );
  }

  /// A wall of bricks two tiles wider than the view, sent once.
  Float32List _backdrop(({double u0, double v0, double u1, double v1}) tile) {
    const across = 44;
    const down = 20;
    final sprites = <OrblitSprite>[
      for (var row = 0; row < down; row++)
        for (var column = 0; column < across; column++)
          OrblitSprite(
            x: column * 2.0 - across + 1,
            y: row * 2.0 - down + 1,
            width: 2,
            height: 2,
            u0: tile.u0,
            v0: tile.v0,
            u1: tile.u1,
            v1: tile.v1,
          ),
    ];
    return OrblitSprites.pack(sprites);
  }

  void _placeCoins(Atlas atlas, double seconds) {
    final frames = atlas.sequence('frame_').sublist(0, 4);
    for (var i = 0; i < _crowd.length; i++) {
      final coin = _crowd[i];
      final hop = (math.sin(seconds * coin.speed + coin.phase)).abs() * 1.2;
      final frame = frames[((seconds * 8 + coin.phase * 4).floor()) % 4];
      final uv = frame.uv(_side, _side);
      OrblitSprite(
        x: coin.x,
        y: coin.y + hop,
        u0: uv.u0,
        v0: uv.v0,
        u1: uv.u1,
        v1: uv.v1,
      ).writeInto(_coins, i * OrblitSprites.stride);
    }
    _coinRevision++;
  }

  void _placeSparks(Atlas atlas, double seconds) {
    const many = 80;
    if (_sparks.length != many * OrblitSprites.stride) {
      _sparks = Float32List(many * OrblitSprites.stride);
    }
    final glow = atlas['frame_8']!.uv(_side, _side);
    for (var i = 0; i < many; i++) {
      final turn = seconds * 0.6 + i * 2 * math.pi / many;
      final reach = 5 + 2 * math.sin(seconds * 1.3 + i);
      final size = 1.5 + math.sin(seconds * 5 + i * 1.7);
      OrblitSprite(
        x: math.cos(turn) * reach,
        y: math.sin(turn) * reach * 0.6,
        width: size,
        height: size,
        u0: glow.u0,
        v0: glow.v0,
        u1: glow.u1,
        v1: glow.v1,
        red: 1,
        green: 0.55,
        blue: 0.2,
        alpha: 0.9,
      ).writeInto(_sparks, i * OrblitSprites.stride);
    }
    _sparkRevision++;
  }

  /// Paints the sheet, and hands it to the renderer as bytes.
  Future<void> _paint() async {
    final pixels = Uint8List(_side * _side * 4);
    void put(int x, int y, int r, int g, int b, int a) {
      final at = (y * _side + x) * 4;
      pixels[at] = r;
      pixels[at + 1] = g;
      pixels[at + 2] = b;
      pixels[at + 3] = a;
    }

    // Four frames of a coin turning over, along the top row: the same disc,
    // narrower each frame until it is edge on, then wider again.
    const halfWidths = [7.0, 5.0, 1.5, 5.0];
    for (var frame = 0; frame < 4; frame++) {
      for (var y = 0; y < _cell; y++) {
        for (var x = 0; x < _cell; x++) {
          final dx = (x + 0.5 - 8) / halfWidths[frame];
          final dy = (y + 0.5 - 8) / 7;
          final d = dx * dx + dy * dy;
          if (d > 1) continue;
          final rim = d > 0.55;
          final shine = x < 7 && y < 6 && !rim;
          put(
            frame * _cell + x,
            y,
            shine ? 255 : (rim ? 196 : 244),
            shine ? 244 : (rim ? 132 : 190),
            shine ? 170 : (rim ? 28 : 52),
            255,
          );
        }
      }
    }

    // A brick, first cell of the second row: mortar every eight pixels down,
    // and every sixteen across, offset by half on alternate courses.
    for (var y = 0; y < _cell; y++) {
      for (var x = 0; x < _cell; x++) {
        final course = y ~/ 8;
        final across = (x + (course.isOdd ? 8 : 0)) % _cell;
        final mortar = y % 8 == 0 || across == 0;
        final grain = ((x * 7 + y * 13) % 5) * 6;
        put(
          x,
          _cell + y,
          mortar ? 120 : 150 + grain,
          mortar ? 116 : 62 + grain ~/ 2,
          mortar ? 110 : 48,
          255,
        );
      }
    }

    // A spark, first cell of the third row: a soft round glow whose alpha
    // falls off with distance, which is what an additive layer wants.
    for (var y = 0; y < _cell; y++) {
      for (var x = 0; x < _cell; x++) {
        final dx = (x + 0.5 - 8) / 8;
        final dy = (y + 0.5 - 8) / 8;
        final fall = (1 - math.sqrt(dx * dx + dy * dy)).clamp(0.0, 1.0);
        final level = (fall * fall * 255).round();
        put(x, 2 * _cell + y, 255, 255, 255, level);
      }
    }

    final image = await _decode(pixels);
    final png = await image.toByteData(format: ui.ImageByteFormat.png);
    image.dispose();
    if (png == null) return;
    await OrblitResources.provide(_sheet, png.buffer.asUint8List());
    _atlas = Atlas.grid(
      image: _sheet,
      imageWidth: _side,
      imageHeight: _side,
      cellWidth: _cell,
      cellHeight: _cell,
    );
  }

  Future<ui.Image> _decode(Uint8List pixels) {
    final done = Completer<ui.Image>();
    ui.decodeImageFromPixels(
      pixels,
      _side,
      _side,
      ui.PixelFormat.rgba8888,
      done.complete,
    );
    return done.future;
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Choice(
        label: 'Coins',
        options: _counts.keys.toList(),
        selected: _counts.entries
            .firstWhere(
              (entry) => entry.value == count,
              orElse: () => _counts.entries.elementAt(1),
            )
            .key,
        onSelect: (option) {
          count = _counts[option]!;
          changed();
        },
      ),
      Toggle(
        label: 'Sharp pixels',
        value: sharp,
        onChanged: (value) {
          sharp = value;
          changed();
        },
      ),
      Toggle(
        label: 'Snap to pixels',
        value: snap,
        note: 'Corners land on whole pixels, so nothing shimmers as it moves.',
        onChanged: (value) {
          snap = value;
          changed();
        },
      ),
      Toggle(
        label: 'Scroll the backdrop',
        value: scrolling,
        note: 'By moving its layer: not one tile is sent again.',
        onChanged: (value) {
          scrolling = value;
          changed();
        },
      ),
      Toggle(
        label: 'Sparks',
        value: sparks,
        note: 'An additive layer, drawn on top by its order.',
        onChanged: (value) {
          sparks = value;
          changed();
        },
      ),
    ],
  );

  @override
  String get code => '''
// The sheet, as bytes: from an asset, a download or, here, painted in code.
await OrblitResources.provide('orblit:resource/sheet.png', png);
final atlas = Atlas.grid(image: 'sheet', imageWidth: 64, imageHeight: 64,
    cellWidth: 16, cellHeight: 16);

// A layer is one image and any number of rectangles of it: one draw.
final uv = atlas['frame_0']!.uv(64, 64);
OrblitSprites(
  key: 2,
  image: const OrblitTexture('orblit:resource/sheet.png'),
  sprites: OrblitSprites.pack([
    OrblitSprite(x: 0, y: 0, u0: uv.u0, v0: uv.v0, u1: uv.u1, v1: uv.v1),
  ]),
  revision: revision, // bump when the sprites change
)

// A backdrop scrolls by its layer's transform, and sends no tiles to do it.
OrblitSprites(key: 1, sprites: tiles, order: -10,
    transform: Matrix4.translationValues(scroll, 0, 0))

// Seen through an orthographic camera, graded straight through.
OrblitCamera(position: Vector3(0, 0, 20), target: Vector3.zero(),
    orthographic: true, viewHeight: 18)
''';
}

/// Where a coin sits and how it hops, the same every run.
class _Coin {
  const _Coin(this.x, this.y, this.speed, this.phase);

  final double x;
  final double y;
  final double speed;
  final double phase;

  static List<_Coin> crowd(int count) {
    final random = math.Random(11);
    return [
      for (var i = 0; i < count; i++)
        _Coin(
          random.nextDouble() * 30 - 15,
          random.nextDouble() * 14 - 8,
          2 + random.nextDouble() * 3,
          random.nextDouble() * math.pi * 2,
        ),
    ];
  }
}
