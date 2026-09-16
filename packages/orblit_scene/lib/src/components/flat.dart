import 'package:orblit_light/orblit_light.dart' show Tint;

import '../component.dart';
import '../values.dart';

/// A flat picture, drawn from a texture or a region of an atlas.
///
/// Placed by the entity's transform like everything else, so a sprite can be
/// parented to a 3D object and a 3D object to a sprite. [depth] is the only
/// thing that is 2D about it: flat layers have no meaningful distance from the
/// camera, so the order they draw in has to be stated rather than measured.
class SpriteComponent extends SceneComponent {
  const SpriteComponent({
    this.texture,
    this.atlas,
    this.region,
    this.animation,
    this.width = 1,
    this.height = 1,
    this.pivotX = 0.5,
    this.pivotY = 0.5,
    this.depth = 0,
    this.colour = const Tint.hex(0xFFFFFF),
    this.opacity = 1,
    this.additive = false,
  });

  static SpriteComponent fromJson(Map<String, Object?> json) => SpriteComponent(
    texture: Values.text(json, 'texture'),
    atlas: Values.text(json, 'atlas'),
    region: Values.text(json, 'region'),
    animation: Values.text(json, 'animation'),
    width: Values.number(json, 'width', 1),
    height: Values.number(json, 'height', 1),
    pivotX: Values.number(json, 'pivotX', 0.5),
    pivotY: Values.number(json, 'pivotY', 0.5),
    depth: Values.number(json, 'depth', 0),
    colour: Values.tint(json['colour'], fallback: const Tint.hex(0xFFFFFF)),
    opacity: Values.number(json, 'opacity', 1),
    additive: Values.flag(json, 'additive', fallback: false),
  );

  /// A picture of its own, as a project path.
  final String? texture;

  /// Or an atlas and the region in it, which is what a sprite sheet is. Both
  /// are kept rather than one being derived: an atlas can be rebuilt and the
  /// region names survive it, whereas coordinates would not.
  final String? atlas;
  final String? region;

  /// A named flipbook in [atlas] to play instead of holding one region.
  final String? animation;

  /// How big it is drawn, in the scene's own units rather than in pixels.
  ///
  /// Pixels would make a sprite change size when somebody changes the
  /// resolution, which is the one thing a flat picture must not do.
  final double width;
  final double height;

  /// Where on the picture the entity's position actually is, from 0 to 1.
  /// The middle by default, and the bottom middle is what a standing
  /// character wants.
  final double pivotX;
  final double pivotY;

  /// What draws in front of what. Higher is nearer the camera.
  final double depth;

  final Tint colour;
  final double opacity;

  /// Added to what is behind rather than covering it, for fire and glow.
  final bool additive;

  @override
  String get type => SceneComponents.sprite;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'texture': texture,
    'atlas': atlas,
    'region': region,
    'animation': animation,
    'width': width,
    'height': height,
    'pivotX': pivotX,
    'pivotY': pivotY,
    'depth': depth,
    'colour': Values.tintToJson(colour),
    'opacity': opacity,
    'additive': additive ? true : null,
  });
}

/// A grid of tiles, as a map file somebody authored elsewhere.
///
/// The map itself is not inlined. A Tiled map is tens of thousands of indices
/// and it is edited in Tiled — copying it into the scene file would make every
/// scene enormous, make merges impossible, and mean the copy goes stale the
/// first time anybody opens the real one.
class TilemapComponent extends SceneComponent {
  const TilemapComponent({this.asset, this.depth = 0, this.opacity = 1});

  static TilemapComponent fromJson(Map<String, Object?> json) =>
      TilemapComponent(
        asset: Values.text(json, 'asset'),
        depth: Values.number(json, 'depth', 0),
        opacity: Values.number(json, 'opacity', 1),
      );

  /// The map file, relative to the project.
  final String? asset;

  final double depth;
  final double opacity;

  @override
  String get type => SceneComponents.tilemap;

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'asset': asset, 'depth': depth, 'opacity': opacity});
}

/// One layer of a parallax backdrop.
class ParallaxLayer {
  const ParallaxLayer({
    required this.image,
    this.depth = 1,
    this.size = 1,
    this.offset = 0,
    this.drift = 0,
  });

  static ParallaxLayer fromJson(Map<String, Object?> json) => ParallaxLayer(
    image: Values.text(json, 'image') ?? '',
    depth: Values.number(json, 'depth', 1),
    size: Values.number(json, 'size', 1),
    offset: Values.number(json, 'offset', 0),
    drift: Values.number(json, 'drift', 0),
  );

  final String image;

  /// How far away it reads as. One is at the camera's own distance and moves
  /// with it exactly; larger is further off and moves less.
  final double depth;

  final double size;
  final double offset;

  /// How fast it slides on its own, for cloud that moves when nothing else
  /// does.
  final double drift;

  Map<String, Object?> toJson() => {
    'image': image,
    'depth': depth,
    'size': size,
    'offset': offset,
    'drift': drift,
  };
}

/// A backdrop of layers that move at different rates.
///
/// Stated inline rather than as a file, unlike a tilemap, because a parallax
/// is five or six lines and belongs to the scene it is behind. There is no
/// second tool that authors one.
class ParallaxComponent extends SceneComponent {
  const ParallaxComponent({this.layers = const []});

  static ParallaxComponent fromJson(Map<String, Object?> json) {
    final raw = json['layers'];
    return ParallaxComponent(
      layers: raw is! List
          ? const []
          : [
              for (final one in raw)
                if (one is Map<String, Object?>) ParallaxLayer.fromJson(one),
            ],
    );
  }

  /// Furthest first, which is the order they draw in.
  final List<ParallaxLayer> layers;

  @override
  String get type => SceneComponents.parallax;

  @override
  Map<String, Object?> toJson() => {
    'layers': [for (final layer in layers) layer.toJson()],
  };
}

/// An interface drawn over the scene.
class CanvasComponent extends SceneComponent {
  const CanvasComponent({this.asset, this.scale = 1});

  static CanvasComponent fromJson(Map<String, Object?> json) => CanvasComponent(
    asset: Values.text(json, 'asset'),
    scale: Values.number(json, 'scale', 1),
  );

  /// The interface file, relative to the project.
  final String? asset;

  final double scale;

  @override
  String get type => SceneComponents.canvas;

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'asset': asset, 'scale': scale});
}
