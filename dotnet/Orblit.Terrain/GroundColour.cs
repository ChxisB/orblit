using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// What the colour map holds for one texel: a colour the ground is multiplied by, and a nudge to
/// its roughness. Four bytes, in the order the colour map stores them and the shader reads them:
/// red, green, blue, then roughness.
/// The colour is sRGB, like a texture's. White leaves the ground as its textures paint it. The
/// roughness byte is a shift of -1 to +1 centred on 128, so a fresh map changes nothing.
/// A value, not an object: a <c>uint</c> holding <c>0xRRGGBBNN</c>.
/// </summary>
public readonly record struct GroundColour(uint Rgba)
{
    /// <summary>White, and roughness as the textures have it. What a new region holds.</summary>
    public static readonly GroundColour None = new(0xFFFFFF80);

    public int Red => (int)((Rgba >> 24) & 0xFF);

    public int Green => (int)((Rgba >> 16) & 0xFF);

    public int Blue => (int)((Rgba >> 8) & 0xFF);

    /// <summary>The roughness shift as it is stored, 0-255, 128 changing nothing.</summary>
    public int RoughnessByte => (int)(Rgba & 0xFF);

    /// <summary>
    /// How much rougher the ground is made, -1 to +1. Added to the texture's roughness and
    /// clamped, so -1 is a mirror and +1 is chalk whatever the texture says.
    /// </summary>
    public double Roughness => (((double)RoughnessByte / 255) * 2) - 1;

    /// <summary>
    /// A colour from its parts. Channels are 0-255 and wrap into their byte;
    /// <paramref name="roughness"/> is clamped to -1...1.
    /// </summary>
    public static GroundColour Of(int red = 255, int green = 255, int blue = 255, double roughness = 0) =>
        Bytes(red, green, blue, RoughnessByteOf(roughness));

    /// <summary>A colour from the four bytes the colour map stores.</summary>
    public static GroundColour Bytes(int red, int green, int blue, int roughness) => new(
        ((uint)(red & 0xFF) << 24) |
        ((uint)(green & 0xFF) << 16) |
        ((uint)(blue & 0xFF) << 8) |
        (uint)(roughness & 0xFF));

    public GroundColour WithColour(int red, int green, int blue) => Bytes(red, green, blue, RoughnessByte);

    public GroundColour WithRoughness(double roughness) =>
        Bytes(Red, Green, Blue, RoughnessByteOf(roughness));

    private static int RoughnessByteOf(double roughness) =>
        (int)Doubles.Round((Doubles.Clamp(roughness, -1.0, 1.0) + 1) / 2 * 255);
}
