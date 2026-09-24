import 'dart:math' as math;

/// What covers one texel of ground: which texture sets, how they mix, how
/// they lie, and whether the ground is there at all. Thirty-two bits, the
/// same thirty-two the cover map stores and the shader reads.
///
/// ```text
/// 31 ... 27  base set        0–31
/// 26 ... 22  overlay set     0–31
/// 21 ... 14  blend           0–255, how much of the overlay shows
/// 13 ... 10  angle           0–15, sixteenths of a turn
///  9 ...  7  scale           a code, see [scale]
///  6 ...  3  (unused, kept zero)
///         2  hole
///         1  navigation
///         0  automatic
/// ```
///
/// A value, not an object: it is an `int` wearing names, so a map of a
/// million of them is a `Uint32List` and nothing more. Every change hands
/// back a new one.
extension type const Cover(int word) {
  /// Plain ground under set 0, placed by hand.
  static const Cover plain = Cover(0);

  /// Ground whose sets are chosen by slope and height, see `AutoCover`. What
  /// a new region is covered with, so untouched terrain is not one texture.
  static const Cover auto = Cover(1);

  /// The most sets a terrain can have. Ids run 0 to 31.
  static const int setCount = 32;

  /// How many steps the blend has between all base and all overlay.
  static const int blendSteps = 255;

  /// How many steps a whole turn of the angle has.
  static const int angleSteps = 16;

  /// The scales a cover can take, in percent: how much bigger than its set's
  /// own size a texture lies. Negative is smaller.
  static const List<int> scales = [0, 20, 40, 60, 80, -60, -40, -20];

  /// A cover from its parts. Out-of-range ids and steps wrap into their bits
  /// rather than spilling into the next field; a [blend] is clamped to 0–1
  /// and a [scale] snaps to the nearest of [scales].
  factory Cover.of({
    int base = 0,
    int overlay = 0,
    double blend = 0,
    int angle = 0,
    int scale = 0,
    bool hole = false,
    bool navigation = false,
    bool automatic = false,
  }) => Cover(
    (base & 0x1F) << 27 |
        (overlay & 0x1F) << 22 |
        _blendStep(blend) << 14 |
        (angle & 0xF) << 10 |
        _scaleCode(scale) << 7 |
        (hole ? 1 : 0) << 2 |
        (navigation ? 1 : 0) << 1 |
        (automatic ? 1 : 0),
  );

  /// The set underneath.
  int get base => (word >> 27) & 0x1F;

  /// The set laid over [base], showing by [blend].
  int get overlay => (word >> 22) & 0x1F;

  /// How much of [overlay] shows, 0 to [blendSteps].
  int get blendStep => (word >> 14) & 0xFF;

  /// How much of [overlay] shows, 0 to 1.
  double get blend => blendStep / blendSteps;

  /// How far the textures are turned, in sixteenths of a turn.
  int get angle => (word >> 10) & 0xF;

  /// How far the textures are turned, anticlockwise seen from above.
  double get radians => angle * (2 * math.pi / angleSteps);

  /// The scale as it is stored: an index into [scales].
  int get scaleCode => (word >> 7) & 0x7;

  /// How much bigger than its set's own size the texture lies, in percent.
  int get scale => scales[scaleCode];

  /// What a texture coordinate is multiplied by for [scale]: a texture lying
  /// twenty percent bigger is read at 0.8.
  double get uvMultiplier => 1 - scale / 100;

  /// No ground here. The triangles that touch this texel are not drawn, and
  /// nothing stands on them.
  bool get hole => (word >> 2) & 1 == 1;

  /// Whether a navigation build should treat this texel as walkable ground.
  bool get navigation => (word >> 1) & 1 == 1;

  /// Whether the sets here come from slope and height rather than from
  /// [base], [overlay] and [blend].
  bool get automatic => word & 1 == 1;

  Cover withBase(int base) => _withField(27, 0x1F, base);

  Cover withOverlay(int overlay) => _withField(22, 0x1F, overlay);

  Cover withBlend(double blend) => _withField(14, 0xFF, _blendStep(blend));

  Cover withAngle(int angle) => _withField(10, 0xF, angle);

  Cover withScale(int scale) => _withField(7, 0x7, _scaleCode(scale));

  Cover withHole(bool hole) => _withField(2, 1, hole ? 1 : 0);

  Cover withNavigation(bool navigation) => _withField(1, 1, navigation ? 1 : 0);

  Cover withAutomatic(bool automatic) => _withField(0, 1, automatic ? 1 : 0);

  /// The word with one field replaced. Masked back to thirty-two bits, since
  /// `~` on a Dart `int` sets the bits above them.
  Cover _withField(int shift, int mask, int value) =>
      Cover((word & ~(mask << shift) | (value & mask) << shift) & 0xFFFFFFFF);

  static int _blendStep(double blend) =>
      (blend.clamp(0.0, 1.0) * blendSteps).round();

  /// The code for the nearest of [scales]. Steps of twenty, so the code is
  /// the step count taken round eight: 20 is 1, −20 is 7.
  static int _scaleCode(int percent) =>
      (percent.clamp(-60, 80) / 20).round() % 8;
}
