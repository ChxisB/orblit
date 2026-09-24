/// What the colour map holds for one texel: a colour the ground is multiplied
/// by, and a nudge to its roughness. Four bytes, in the order the colour map
/// stores them and the shader reads them: red, green, blue, then roughness.
///
/// The colour is sRGB, like a texture's. White leaves the ground as its
/// textures paint it. The roughness byte is a shift of −1 to +1 centred on
/// 128, so a fresh map changes nothing.
///
/// A value, not an object: an `int` holding `0xRRGGBBNN`.
extension type const GroundColour(int rgba) {
  /// White, and roughness as the textures have it. What a new region holds.
  static const GroundColour none = GroundColour(0xFFFFFF80);

  /// A colour from its parts. Channels are 0–255 and wrap into their byte;
  /// [roughness] is clamped to −1…1.
  factory GroundColour.of({
    int red = 255,
    int green = 255,
    int blue = 255,
    double roughness = 0,
  }) => GroundColour.bytes(red, green, blue, _roughnessByte(roughness));

  /// A colour from the four bytes the colour map stores.
  factory GroundColour.bytes(int red, int green, int blue, int roughness) =>
      GroundColour(
        (red & 0xFF) << 24 |
            (green & 0xFF) << 16 |
            (blue & 0xFF) << 8 |
            (roughness & 0xFF),
      );

  int get red => (rgba >> 24) & 0xFF;

  int get green => (rgba >> 16) & 0xFF;

  int get blue => (rgba >> 8) & 0xFF;

  /// The roughness shift as it is stored, 0–255, 128 changing nothing.
  int get roughnessByte => rgba & 0xFF;

  /// How much rougher the ground is made, −1 to +1. Added to the texture's
  /// roughness and clamped, so −1 is a mirror and +1 is chalk whatever the
  /// texture says.
  double get roughness => roughnessByte / 255 * 2 - 1;

  GroundColour withColour(int red, int green, int blue) =>
      GroundColour.bytes(red, green, blue, roughnessByte);

  GroundColour withRoughness(double roughness) =>
      GroundColour.bytes(red, green, blue, _roughnessByte(roughness));

  static int _roughnessByte(double roughness) =>
      ((roughness.clamp(-1.0, 1.0) + 1) / 2 * 255).round();
}
