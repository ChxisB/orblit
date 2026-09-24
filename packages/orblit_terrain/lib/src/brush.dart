import 'patch.dart';
import 'stroke.dart';

/// What a brush does to the ground.
enum BrushTool {
  /// Lifts the ground under it; [TerrainStroke.invert] lowers it instead.
  raise(TerrainLayer.height),

  /// Sinks the ground under it; [TerrainStroke.invert] raises it instead.
  lower(TerrainLayer.height),

  /// Evens out the bumps, drawing each height towards its neighbours'.
  smooth(TerrainLayer.height),

  /// Draws the ground towards one height: the height where the stroke
  /// began, unless the stroke is given one.
  flatten(TerrainLayer.height),

  /// Draws the ground towards a ramp from where the stroke began to the
  /// farthest it has gone from there. Drag from one end to the other and
  /// back over the way, and the way back lays the ramp.
  slope(TerrainLayer.height),

  /// Lays one texture set over what is there. [TerrainStroke.invert] hands
  /// the ground back to automatic cover.
  cover(TerrainLayer.cover),

  /// Tints the ground towards one colour. [TerrainStroke.invert] washes the
  /// tint back out.
  colour(TerrainLayer.colour),

  /// Nudges the ground rougher or glossier than its textures.
  /// [TerrainStroke.invert] takes the nudge back off.
  roughness(TerrainLayer.colour),

  /// Cuts holes, for a cave mouth or a well. [TerrainStroke.invert] fills
  /// them.
  hole(TerrainLayer.cover);

  const BrushTool(this.layer);

  /// The map it writes, which is all its undo keeps.
  final TerrainLayer layer;
}

/// What every brush tool shares: how big it is, how hard it works, how soft
/// its edge is, how scattered and how close together its dabs are.
///
/// One set of settings for every tool, because a brush that behaves
/// differently for each of them is a brush nobody learns.
class Brush {
  const Brush({
    this.size = 16,
    this.strength = 0.5,
    this.falloff = 0.5,
    this.jitter = 0,
    this.spacing = 0.25,
  });

  /// Metres across.
  final double size;

  /// How hard it works, 0 to 1.
  ///
  /// Measured per pass rather than per dab, so closer [spacing] makes a
  /// smoother stroke and not a stronger one. At 1, one pass of a height tool
  /// moves the ground under the centre by a quarter of [size], and one pass
  /// of a blending tool all but reaches what it is blending towards.
  final double strength;

  /// How much of the radius eases off, 0 to 1: at 0 the brush is a hard
  /// disc, at 1 it fades from the very centre.
  final double falloff;

  /// How far each dab may stray from the path, as a share of the radius, 0
  /// to 1.
  final double jitter;

  /// How far apart the dabs are along a stroke, as a share of [size].
  final double spacing;

  /// The smallest [spacing] a stroke uses, so a brush set to nothing does not
  /// lay a dab for every pixel the pointer crosses.
  static const double minSpacing = 0.02;

  /// The spacing [strength] is measured at. A stroke at other spacing lays
  /// more or fewer dabs, each weaker or stronger, to the same total.
  static const double referenceSpacing = 0.25;

  double get radius => size / 2;

  /// How much of a dab reaches a point [distance] metres from its centre: 1
  /// across the hard middle, easing to 0 at the edge.
  double weightAt(double distance) {
    final radius = this.radius;
    if (!(radius > 0)) return 0;
    final along = distance / radius;
    if (along >= 1) return 0;
    final core = 1 - falloff.clamp(0.0, 1.0);
    if (along <= core) return 1;
    final left = 1 - (along - core) / (1 - core);
    return left * left * (3 - 2 * left);
  }

  Brush copyWith({
    double? size,
    double? strength,
    double? falloff,
    double? jitter,
    double? spacing,
  }) => Brush(
    size: size ?? this.size,
    strength: strength ?? this.strength,
    falloff: falloff ?? this.falloff,
    jitter: jitter ?? this.jitter,
    spacing: spacing ?? this.spacing,
  );

  @override
  bool operator ==(Object other) =>
      other is Brush &&
      other.size == size &&
      other.strength == strength &&
      other.falloff == falloff &&
      other.jitter == jitter &&
      other.spacing == spacing;

  @override
  int get hashCode => Object.hash(size, strength, falloff, jitter, spacing);
}
