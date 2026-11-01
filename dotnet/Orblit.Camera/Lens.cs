using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// The optics of a shot. Field of view is vertical and in degrees, because that
/// is what every tool an artist will have used states it in.
/// </summary>
/// <param name="FieldOfView">Vertical field of view, in degrees.</param>
/// <param name="Near">The near plane.</param>
/// <param name="Far">The far plane.</param>
/// <param name="Dutch">Roll about the view axis, in degrees. Named as cinema names it.</param>
/// <param name="Orthographic">
/// Whether parallel lines stay parallel. A flat lens is not a very long one: perspective at
/// a narrow angle still converges, so a sprite at the edge of the frame would be seen from
/// the side, and a game seen flat on has its art drawn face on.
/// </param>
/// <param name="Height">
/// How much of the world fits in the frame from top to bottom, in metres. The flat lens's
/// answer to a field of view. Ignored when the lens has perspective.
/// </param>
public sealed record Lens(
    double FieldOfView = 50,
    double Near = 0.1,
    double Far = 1000,
    double Dutch = 0,
    bool Orthographic = false,
    double Height = 10)
{
    /// <summary>A lens for a game seen flat on: no perspective, and a height in world units rather than an angle.</summary>
    public static Lens Flat(double height, double near = -100, double far = 100, double dutch = 0) =>
        new(50, near, far, dutch, true, height);

    /// <summary>
    /// Blends two lenses. Field of view and height go through their logarithms, because a zoom
    /// looks even when the angle halves in equal times rather than falling by equal amounts.
    /// The result takes the destination's kind from the start: crossing between a flat lens
    /// and one with perspective is a cut, not a blend.
    /// </summary>
    public static Lens Lerp(Lens a, Lens b, double t)
    {
        if (t <= 0)
        {
            return a;
        }

        if (t >= 1)
        {
            return b;
        }

        return new Lens(
            FieldOfView: Math.Exp(Mix(Math.Log(a.FieldOfView), Math.Log(b.FieldOfView), t)),
            Near: Mix(a.Near, b.Near, t),
            Far: Mix(a.Far, b.Far, t),
            Dutch: Mix(a.Dutch, b.Dutch, t),
            Orthographic: b.Orthographic,
            Height: Math.Exp(Mix(Math.Log(a.Height), Math.Log(b.Height), t)));
    }

    public override string ToString() => Orthographic
        ? $"Lens.flat({Doubles.ToStringAsFixed(Height, 1)} m)"
        : $"Lens({Doubles.ToStringAsFixed(FieldOfView, 1)}°)";

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);
}
