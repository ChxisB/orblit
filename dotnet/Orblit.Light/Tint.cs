using System.Globalization;
using Orblit.VectorMath;

namespace Orblit.Light;

/// <summary>
/// A colour as somebody writes one down, and as a renderer needs it.
/// <c>0xFF8A3D</c> is sRGB, spaced so that equal steps look equally different.
/// A renderer adds and multiplies light, and that only works in linear. The
/// conversion lives here so no colour goes through it twice.
/// </summary>
public readonly record struct Tint(double Red, double Green, double Blue)
{
    public static readonly Tint White = new(1, 1, 1);

    public static readonly Tint Black = new(0, 0, 0);

    /// <summary>The same colour in the space light behaves in.</summary>
    public Vector3 Linear => new(ToLinear(Red), ToLinear(Green), ToLinear(Blue));

    /// <summary>The way a colour is written: <c>0xFF8A3D</c>, no alpha, because a sky is not partly transparent.</summary>
    public static Tint Hex(int value) =>
        new(((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);

    /// <summary>
    /// The inverse of <see cref="Linear"/>. A glTF states colour linearly, so it
    /// has to be converted on the way in as well as out, with one transfer
    /// function and not two.
    /// </summary>
    public static Tint FromLinear(Vector3 colour) => new(ToSrgb(colour.X), ToSrgb(colour.Y), ToSrgb(colour.Z));

    /// <summary>
    /// Part of the way from one to another, mixed in sRGB. Mixed in linear, a
    /// dusk to night ramp spends most of its length nearly black.
    /// </summary>
    public static Tint Lerp(Tint from, Tint to, double t)
    {
        var at = Doubles.Clamp(t, 0.0, 1.0);
        return new Tint(
            from.Red + ((to.Red - from.Red) * at),
            from.Green + ((to.Green - from.Green) * at),
            from.Blue + ((to.Blue - from.Blue) * at));
    }

    public Tint Scaled(double by) => new(Red * by, Green * by, Blue * by);

    /// <summary>Compared with <c>==</c> on each channel, so a NaN channel is not equal to itself.</summary>
    public bool Equals(Tint other) => Red == other.Red && Green == other.Green && Blue == other.Blue;

    public override int GetHashCode() => HashCode.Combine(Red, Green, Blue);

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"Tint({Red:F3}, {Green:F3}, {Blue:F3})");

    // The transfer function sRGB is defined by. A gamma of 2.2 is the
    // approximation, and it is wrong by enough to see in the dark end.
    private static double ToLinear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static double ToSrgb(double channel)
    {
        var value = double.IsFinite(channel) ? Doubles.Clamp(channel, 0.0, 1.0) : 0.0;
        return value <= 0.0031308 ? value * 12.92 : (1.055 * Math.Pow(value, 1 / 2.4)) - 0.055;
    }
}
