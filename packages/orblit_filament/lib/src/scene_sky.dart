part of 'scene.dart';

// Everything that is not an object: the sky, and the fog, cloud and
// precipitation that stand in front of it.

enum SkyQuality {
  /// Eight steps through the cloud and two towards the light, with no
  /// erosion. For a phone, a browser, or anything sharing a frame with a lot
  /// else.
  lean(marchSteps: 8, lightSteps: 2, erosion: 2),

  /// Twelve and three, with erosion on what is near.
  fair(marchSteps: 12, lightSteps: 3, erosion: 0.6),

  /// Eighteen and three, with erosion wherever the detail would show.
  full(marchSteps: 18, lightSteps: 3, erosion: 0.35);

  const SkyQuality({
    required this.marchSteps,
    required this.lightSteps,
    required this.erosion,
  });

  /// How many samples are taken along a ray through the cloud. Nearly all of
  /// the cost is here.
  final int marchSteps;

  /// How many are taken towards the light at each of those, which is what
  /// puts a shadow on the underside of a cloud.
  final int lightSteps;

  /// How near a cloud has to be before it is bitten at the edges by finer
  /// noise. Above one is never.
  final double erosion;
}

class OrblitSky {
  OrblitSky({
    Vector3? colour,
    Vector3? zenith,
    Vector3? horizon,
    this.ambient = 28000,
    this.showBody = true,
    this.drawn = true,
    this.quality = SkyQuality.full,
    Vector3? bodyDirection,
    Vector3? bodyColour,
    this.bodySize = 0.0047,
    this.flash = 0,
    Vector3? flashDirection,
    this.flashSeed = 0,
    OrblitClouds? clouds,
  }) : colour = colour ?? Vector3(0.10, 0.12, 0.16),
       zenith = zenith ?? Vector3(0.05, 0.17, 0.48),
       horizon = horizon ?? Vector3(0.60, 0.74, 0.90),
       bodyDirection = bodyDirection ?? Vector3(0.35, 0.78, 0.52),
       bodyColour = bodyColour ?? Vector3(1.00, 0.96, 0.90),
       flashDirection = flashDirection ?? Vector3(0.0, 0.35, 1.0),
       clouds = clouds ?? OrblitClouds.none;

  /// Linear RGB. What the sky is worth as a light source, which is not the
  /// same question as what it looks like: this is the one colour the image
  /// based lighting is built from, and it is an average of a whole dome.
  final Vector3 colour;

  /// Linear RGB, straight up and along the ground.
  ///
  /// Two colours rather than one because the sky is not one colour. Overhead
  /// there is the least air to look through and it is deepest; at the horizon
  /// there is the most and it is nearly white. A single flat colour is the
  /// difference between a sky and a backdrop.
  final Vector3 zenith;
  final Vector3 horizon;

  /// How much light the sky casts, in lux. Roughly a tenth of the sun on a
  /// clear day, which is about the ratio outdoors.
  final double ambient;

  /// What the sky is allowed to cost.
  final SkyQuality quality;

  /// Whether there is a sky to draw at all.
  ///
  /// An interior has none, and a scene that does not draw one pays for none:
  /// the dome is the most expensive thing in a frame and it is skipped
  /// outright rather than drawn empty.
  final bool drawn;

  /// Whether whatever is lighting the scene is drawn in the sky as a disk.
  ///
  /// A sun nobody can see is a scene lit from a direction that has to be
  /// worked out from the shadows. The disk is drawn at the light's own colour
  /// and brightness, so a dim pale one reads as a moon without being a
  /// separate feature.
  final bool showBody;

  /// Which way the body is, what colour it is, and how wide it is in radians.
  ///
  /// The same direction the cloud is lit from, which is the point of having
  /// it here: a sun drawn in one place and a cloud lit from another is the
  /// single thing that gives a sky away.
  final Vector3 bodyDirection;
  final Vector3 bodyColour;
  final double bodySize;

  /// A strike, this instant: how bright, which way, and which strike.
  ///
  /// The seed is what the bolt is drawn from, so one strike is a different
  /// shape from the next and the same shape whenever that instant is played
  /// again.
  final double flash;
  final Vector3 flashDirection;
  final double flashSeed;

  /// The layer of cloud in it.
  ///
  /// Held by the sky rather than beside it, because a cloud is a thing the
  /// sky has: it is lit by the sky's own body, it covers the sky's own
  /// gradient, and drawing either without the other is what made the last
  /// two attempts read as wallpaper.
  final OrblitClouds clouds;

  /// Everything the sky is drawn from, in one array.
  ///
  /// The gradient, the body, the cloud and the strike travel together because
  /// they are drawn together: one pass along one view ray, so the cloud can
  /// cover the sun, the sun can light the cloud, and a strike can light both.
  Float32List get packed {
    final body = bodyDirection.length2 > 0
        ? (bodyDirection.clone()..normalize())
        : Vector3(0, 1, 0);
    final strike = flashDirection.length2 > 0
        ? (flashDirection.clone()..normalize())
        : Vector3(0, 0, 1);

    return Float32List.fromList([
      zenith.x,
      zenith.y,
      zenith.z,
      horizon.x,
      horizon.y,
      horizon.z,
      body.x,
      body.y,
      body.z,
      bodyColour.x,
      bodyColour.y,
      bodyColour.z,
      bodySize,
      showBody ? 1 : 0,
      clouds.colour.x,
      clouds.colour.y,
      clouds.colour.z,
      clouds.cover,
      clouds.altitude,
      clouds.thickness,
      clouds.featureSize,
      clouds.density,
      clouds.billow,
      clouds.extinction,
      quality.marchSteps.toDouble(),
      quality.lightSteps.toDouble(),
      quality.erosion,
      clouds.wind.x,
      clouds.wind.y,
      flash,
      strike.x,
      strike.y,
      strike.z,
      flashSeed,
    ]);
  }

  /// How many floats the sky occupies.
  static const int stride = 34;
}

/// Air with something in it.
///
/// Distance and height are one setting because they are one effect: fog thick
/// enough to see across a valley is fog that pools in the valley, and having
/// the first without the second reads as a filter over the lens rather than as
/// weather.
class OrblitFog {
  OrblitFog({
    Vector3? colour,
    this.density = 0.05,
    this.distance = 0,
    this.cutOffDistance = double.infinity,
    this.maximumOpacity = 1,
    this.height = 0,
    this.heightFalloff = 1,
    this.structure = 0,
    Vector2? wind,
    this.featureSize = 0.02,
    this.thickness = 6,
  }) : colour = colour ?? Vector3(0.5, 0.55, 0.6),
       wind = wind ?? Vector2(0.4, 0.15);

  /// Nothing in the air, and cheap: the pass is switched off rather than run
  /// with a density of zero.
  static final OrblitFog none = OrblitFog(density: 0);

  /// Linear RGB.
  final Vector3 colour;

  /// How thick the air is, per metre.
  final double density;

  /// Metres in front of the camera where fog begins, so the thing being looked
  /// at is not veiled by the air between it and the lens.
  final double distance;

  /// Metres past which fog stops thickening. What keeps a sky visible through
  /// heavy weather instead of turning it into a wall.
  final double cutOffDistance;

  /// The most it can obscure, from zero to one.
  final double maximumOpacity;

  /// The world height the fog's own layer sits at.
  final double height;

  /// How quickly it thins going up. Larger is a shallower layer hugging the
  /// ground; zero is uniform at every height.
  final double heightFalloff;

  /// How much shape the air has, from none to a great deal.
  ///
  /// Even fog is right for distance and cannot look like anything in
  /// particular: every cubic metre of it is the same as every other. Above
  /// zero, the same air is also drawn as a stack of noise sheets, which is
  /// what gives it the shape of cloud lying along a valley. Zero costs
  /// nothing — the sheets are not drawn at all.
  final double structure;

  /// Which way the air is moving across the ground, and how fast, in metres
  /// a second.
  ///
  /// Sent as a speed rather than as a rate the pattern scrolls at, because
  /// only the renderer knows how big the pattern is — and wind that changed
  /// speed when somebody resized the clouds would be a setting that lies.
  final Vector2 wind;

  /// How large its features are: how much of a metre one turn of the noise
  /// covers. Smaller is bigger cloud.
  final double featureSize;

  /// How deep the bank is, in metres, above and below its height.
  final double thickness;

  bool get isVisible => density > 0 && maximumOpacity > 0;

  Float32List get _packed => Float32List.fromList([
    colour.x,
    colour.y,
    colour.z,
    density,
    distance,
    // Infinity survives the channel, but arithmetic on the other side turns it
    // into NaN rather than "far away", and a NaN in the fog makes the whole
    // frame vanish. A large finite stand-in keeps that maths well-behaved —
    // and, since the sky is drawn far beyond it, leaves the sky its own
    // colour rather than turning it into a wall of fog.
    cutOffDistance.isFinite ? cutOffDistance : 1e9,
    maximumOpacity,
    height,
    heightFalloff,
    structure,
    wind.x,
    wind.y,
    featureSize,
    thickness,
    0,
    0,
  ]);

  /// How many floats the fog occupies.
  static const int stride = 16;
}

/// The cloud in the sky.
///
/// Not the same thing as the fog, and worth keeping apart: fog is the air
/// between here and the horizon, and cloud is a layer a long way overhead
/// that the light comes through. A scene can have either without the other,
/// and a setting that did both would be wrong for every scene that wants one.
class OrblitClouds {
  const OrblitClouds({
    required this.colour,
    this.cover = 0,
    required this.wind,
    this.featureSize = 1 / 700,
    this.altitude = 900,
    this.thickness = 600,
    this.density = 1,
    this.billow = 0.85,
    this.extinction = 0.012,
  });

  /// A clear sky, and cheap: the layer is not marched at all.
  static final OrblitClouds none = OrblitClouds(
    colour: Vector3(0.17, 0.22, 0.33),
    wind: Vector2.zero(),
  );

  /// Fair-weather cumulus: flat bases at the condensation level, cauliflower
  /// tops, and a lot of blue between them. The default sky, and the one
  /// everybody pictures when they picture a cloud.
  factory OrblitClouds.cumulus({double cover = 0.35, Vector2? wind}) =>
      OrblitClouds(
        colour: Vector3(0.17, 0.22, 0.33),
        cover: cover,
        wind: wind ?? Vector2(4, 1.5),
      );

  /// The flatter, wider version: lumps that have run together into a layer
  /// with breaks in it rather than shapes with sky around them.
  factory OrblitClouds.stratocumulus({double cover = 0.6, Vector2? wind}) =>
      OrblitClouds(
        colour: Vector3(0.15, 0.19, 0.28),
        cover: cover,
        wind: wind ?? Vector2(5, 2),
        featureSize: 1 / 950,
        altitude: 700,
        thickness: 320,
        density: 0.85,
        billow: 0.5,
        extinction: 0.010,
      );

  /// The grey lid. Low, shallow and nearly featureless, which is why an
  /// overcast day has no shape to its sky and no shadows under it.
  factory OrblitClouds.stratus({double cover = 0.95, Vector2? wind}) =>
      OrblitClouds(
        colour: Vector3(0.14, 0.16, 0.21),
        cover: cover,
        wind: wind ?? Vector2(3, 1),
        featureSize: 1 / 1700,
        altitude: 480,
        thickness: 260,
        density: 0.75,
        billow: 0.08,
        extinction: 0.009,
      );

  /// Ice, seven kilometres up. Thin enough that the sun comes straight
  /// through it, and drawn out into streaks by a wind nothing slows down.
  factory OrblitClouds.cirrus({double cover = 0.4, Vector2? wind}) =>
      OrblitClouds(
        colour: Vector3(0.26, 0.32, 0.44),
        cover: cover,
        wind: wind ?? Vector2(16, 6),
        featureSize: 1 / 2600,
        altitude: 7000,
        thickness: 900,
        density: 0.22,
        billow: 0.3,
        extinction: 0.004,
      );

  /// The anvil. Deep enough that its own base is in its own shadow, which is
  /// the whole reason a storm sky is dark while the day around it is not.
  factory OrblitClouds.cumulonimbus({double cover = 0.85, Vector2? wind}) =>
      OrblitClouds(
        colour: Vector3(0.08, 0.09, 0.12),
        cover: cover,
        wind: wind ?? Vector2(9, 4),
        featureSize: 1 / 1200,
        altitude: 600,
        thickness: 2600,
        density: 1.25,
        billow: 0.7,
        extinction: 0.016,
      );

  /// Linear RGB: what the sky puts back into the side the sun does not reach.
  ///
  /// Not the colour of the cloud — a cloud has no colour of its own, it is
  /// white water lit by whatever reaches it. This is the blue that fills in
  /// the shadowed side, and it is why an underside reads as grey-blue rather
  /// than as black.
  final Vector3 colour;

  /// How much of the sky is covered, from nothing to everything.
  final double cover;

  /// What carries it across the sky, in metres a second.
  final Vector2 wind;

  /// Turns of the noise per metre: the reciprocal of how big a lump is.
  final double featureSize;

  /// How high the base hangs and how deep the layer is, in metres.
  ///
  /// Depth is what separates cloud from a painted ceiling. A layer with none
  /// can only be lit from one side; a layer with six hundred metres of it has
  /// a lit top, a shadowed base and an edge the light comes through.
  final double altitude;
  final double thickness;

  /// How solid it is where it is solid at all.
  final double density;

  /// How far the noise is folded, from a smooth sheet to a cauliflower.
  final double billow;

  /// How much light a metre of it takes out of a ray.
  final double extinction;

  bool get isVisible => cover > 0.01 && density > 0;

  OrblitClouds copyWith({
    Vector3? colour,
    double? cover,
    Vector2? wind,
    double? featureSize,
    double? altitude,
    double? thickness,
    double? density,
    double? billow,
    double? extinction,
  }) => OrblitClouds(
    colour: colour ?? this.colour,
    cover: cover ?? this.cover,
    wind: wind ?? this.wind,
    featureSize: featureSize ?? this.featureSize,
    altitude: altitude ?? this.altitude,
    thickness: thickness ?? this.thickness,
    density: density ?? this.density,
    billow: billow ?? this.billow,
    extinction: extinction ?? this.extinction,
  );
}

/// Water or snow on its way down.
///
/// One description for both, because they are the same thing at different
/// speeds: a field of drops falling and being blown sideways. What separates
/// them is [stretch] — how far a drop travels while the shutter is open, which
/// is the difference between a streak and a flake.
class OrblitPrecipitation {
  OrblitPrecipitation({
    Vector3? colour,
    this.amount = 0,
    this.fall = 9,
    Vector2? wind,
    this.dropsPerMetre = 6,
    this.stretch = 26,
    this.threshold = 0.72,
  }) : colour = colour ?? Vector3(0.72, 0.78, 0.86),
       wind = wind ?? Vector2.zero();

  /// Dry weather, and cheap: the curtains are not drawn at all.
  static final OrblitPrecipitation none = OrblitPrecipitation(amount: 0);

  /// Linear RGB.
  final Vector3 colour;

  /// How much of it there is, from nothing to a downpour.
  final double amount;

  /// Metres a second, downwards. Rain falls at about nine; snow at under one.
  final double fall;

  /// What carries it sideways, in metres a second.
  final Vector2 wind;

  /// How many drops there are in a metre.
  final double dropsPerMetre;

  /// How far a drop is smeared along its fall. One is a flake; forty is rain
  /// caught in a headlight.
  final double stretch;

  /// How much of the field is drop rather than air. Higher is sparser.
  final double threshold;

  bool get isVisible => amount > 0;

  Float32List get _packed => Float32List.fromList([
    colour.x,
    colour.y,
    colour.z,
    amount,
    fall,
    wind.x,
    wind.y,
    dropsPerMetre,
    stretch,
    threshold,
    0,
    0,
  ]);

  /// How many floats the weather on its way down occupies.
  static const int stride = 12;
}

/// Everything the renderer needs for a frame.
///
/// Sent whole rather than as a diff. A message that describes the entire scene
/// cannot go stale: there is no state on the wire to fall out of step, and no
/// way for the renderer to believe nothing changed when something did — which
/// is a failure that looks exactly like a frozen viewport.
///
/// Whole on the wire and incremental in the renderer are not in tension. The
/// keys make the description addressable, so the far side can work out what
/// actually moved without being told, and pay only for that. Sending was never
/// the expensive half; tearing down every entity in the scene sixty times a
/// second was.
