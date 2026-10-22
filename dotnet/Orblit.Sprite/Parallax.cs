using Orblit.VectorMath;

namespace Orblit.Sprite;

/// <summary>
/// One plane of a scrolling background.
/// </summary>
/// <param name="Image">The picture to draw.</param>
/// <param name="Depth">
/// How much of the camera's movement this layer takes, as a fraction. One moves with the camera
/// exactly, the ground the player stands on. Nought does not move at all, the sky. Between them
/// is the illusion: distant hills at a tenth, mid trees at a half. Above one is a foreground
/// that rushes past, which is the same trick used the other way.
/// </param>
/// <param name="Size">How wide the image is in world units, for wrapping. Zero does not wrap.</param>
/// <param name="Offset">A fixed shift, for lining a layer up.</param>
/// <param name="Drift">
/// How fast it moves on its own, in world units a second. Clouds crossing a sky the camera is
/// not following. Without it every layer is still whenever the camera is, and a still sky reads
/// as a painted backdrop rather than as weather.
/// </param>
// F2: five parameters, and every one after the image is optional and called by name.
public sealed record Layer(string Image, double Depth = 1, double Size = 0, double Offset = 0, double Drift = 0);

/// <summary>
/// Several layers, and where each of them sits. The oldest trick in two dimensions and still the
/// most effective: things further away move less. What makes it read as depth rather than as
/// sliding wallpaper is that the ratios are consistent, a layer at a tenth stays at a tenth
/// however fast the camera goes.
/// </summary>
public sealed class Parallax(IReadOnlyList<Layer> layers)
{
    public IReadOnlyList<Layer> Layers { get; } = layers;

    public int Length => Layers.Count;

    /// <summary>
    /// How many copies of a layer are needed to cover <paramref name="across"/> world units. Two
    /// more than fit: one for the part scrolled off and one for the part not yet on. Getting this
    /// wrong by one is a gap at the edge of the screen that appears once per wrap.
    /// </summary>
    public static long CopiesFor(Layer layer, double across) =>
        layer.Size <= 0 ? 1 : Doubles.Ceil(across / layer.Size) + 2;

    /// <summary>
    /// Where each layer should be drawn, given the camera and the clock. A list in the same order
    /// as the layers, so a caller can zip them without looking anything up.
    /// </summary>
    public IReadOnlyList<double> At(double camera, double seconds) =>
        [.. Layers.Select(layer => Place(layer, camera, seconds))];

    /// <summary>Where one layer sits.</summary>
    public double PlaceOf(int index, double camera, double seconds) =>
        index < 0 || index >= Layers.Count ? 0 : Place(Layers[index], camera, seconds);

    private static double Place(Layer layer, double camera, double seconds)
    {
        var moved = (-camera * layer.Depth) + layer.Offset + (layer.Drift * seconds);
        if (layer.Size <= 0)
        {
            return moved;
        }

        // Wrapped into one width, so a layer scrolled for an hour is drawn at the same handful of
        // coordinates as one scrolled for a second, and the numbers never grow large enough to
        // lose precision, which is what makes a long-running background start to judder.
        var wrapped = Doubles.Mod(moved, layer.Size);
        return wrapped <= 0 ? wrapped : wrapped - layer.Size;
    }
}

/// <summary>Where a <see cref="View2"/> may go, as two opposite corners.</summary>
public readonly record struct ViewBounds(Vector2 Minimum, Vector2 Maximum);

/// <summary>
/// A camera in two dimensions, with the bounds it will not leave. There is no <c>at</c> argument,
/// because nothing would read it.
/// </summary>
public sealed class View2(double width = 16, double height = 9, ViewBounds? bounds = null)
{
    public Vector2 At { get; set; } = Vector2.Zero();

    public double Width { get; } = width;

    public double Height { get; } = height;

    /// <summary>Where it may go. Null lets it go anywhere.</summary>
    public ViewBounds? Bounds { get; } = bounds;

    /// <summary>
    /// Moves the camera towards <paramref name="target"/>, keeping it inside its bounds.
    /// <paramref name="ease"/> is how much of the remaining distance it covers each second: nought
    /// does not follow at all, and one is instant. A camera that snapped to its target would shake
    /// with the thing it is following.
    /// </summary>
    public void Follow(Vector2 target, double seconds, double ease = 6)
    {
        var blend = ease <= 0 ? 0.0 : 1 - Decay(ease, seconds);
        At += (target - At) * blend;
        Clamp();
    }

    public void JumpTo(Vector2 target)
    {
        At = target.Clone();
        Clamp();
    }

    private void Clamp()
    {
        if (Bounds is not { } limit)
        {
            return;
        }

        var halfWide = Width * 0.5;
        var halfTall = Height * 0.5;

        // The room the camera has is the world minus half a screen on each side. A world narrower
        // than the screen has none, and clamping to a negative range would put the camera outside
        // the world it was being kept inside.
        var lowX = limit.Minimum.X + halfWide;
        var highX = limit.Maximum.X - halfWide;
        var lowY = limit.Minimum.Y + halfTall;
        var highY = limit.Maximum.Y - halfTall;

        At = new Vector2(
            lowX > highX ? (limit.Minimum.X + limit.Maximum.X) * 0.5 : Doubles.Clamp(At.X, lowX, highX),
            lowY > highY ? (limit.Minimum.Y + limit.Maximum.Y) * 0.5 : Doubles.Clamp(At.Y, lowY, highY));
    }

    /// <summary>
    /// What is left after a second of decay, raised to the time. Framerate independence:
    /// following by a fixed fraction each frame follows twice as fast at twice the frame rate,
    /// which is the commonest reason a camera feels different on two machines.
    /// </summary>
    private static double Decay(double rate, double seconds)
    {
        var left = 1.0;
        var steps = (int)Math.Clamp(Doubles.Ceil(seconds * 240), 1, 4096);
        var step = seconds / steps;
        for (var i = 0; i < steps; i++)
        {
            left *= 1 - (rate * step);
            if (left <= 0)
            {
                return 0;
            }
        }

        return left;
    }
}
