namespace Orblit.Filament;

/// <summary>
/// One flat picture: a rectangle of a layer's image, placed in the layer. Plain numbers rather
/// than vectors, so a list of ten thousand of them is ten thousand small objects and not forty
/// thousand.
/// </summary>
public sealed record LayerSprite
{
    /// <summary>Floats a sprite takes in the layer's records. Must match kSpriteRecordFloats.</summary>
    public const int Stride = 16;

    /// <summary>Where the pivot is, in the layer's own units.</summary>
    public required double X { get; init; }

    public required double Y { get; init; }

    /// <summary>
    /// How big it is. Negative flips it: a character facing left is the same sprite as one facing
    /// right, with a width of minus itself.
    /// </summary>
    public double Width { get; init; } = 1;

    public double Height { get; init; } = 1;

    /// <summary>
    /// Carried through to the renderer, so sprites share a depth with anything solid in the scene.
    /// Within a layer, order decides what is on top.
    /// </summary>
    public double Depth { get; init; }

    /// <summary>A turn about the pivot, in radians, anticlockwise.</summary>
    public double Rotation { get; init; }

    /// <summary>
    /// The point it is placed and turned by, as a fraction of its size: nought is the left or
    /// bottom edge and one the right or top. The middle by default; the bottom middle is what a
    /// character standing on something wants.
    /// </summary>
    public double PivotX { get; init; } = 0.5;

    public double PivotY { get; init; } = 0.5;

    /// <summary>
    /// The rectangle of the layer's image it shows, as fractions of the image, with
    /// <see cref="V0"/> at the top, which is how an atlas measures a region.
    /// </summary>
    public double U0 { get; init; }

    public double V0 { get; init; }

    public double U1 { get; init; } = 1;

    public double V1 { get; init; } = 1;

    /// <summary>Its colour, in linear light, multiplied over the image.</summary>
    public double Red { get; init; } = 1;

    public double Green { get; init; } = 1;

    public double Blue { get; init; } = 1;

    public double Alpha { get; init; } = 1;

    /// <summary>This sprite showing the rectangle of an image given in pixels.</summary>
    // F2: a rectangle and the image it is cut from, six numbers in the order an atlas tool prints them.
    public LayerSprite Cut(int left, int top, int width, int height, int imageWidth, int imageHeight) =>
        this with
        {
            U0 = (double)left / imageWidth,
            V0 = (double)top / imageHeight,
            U1 = (double)(left + width) / imageWidth,
            V1 = (double)(top + height) / imageHeight,
        };

    /// <summary>Writes this sprite's <see cref="Stride"/> floats at <paramref name="at"/>.</summary>
    internal void WriteInto(float[] into, int at) => Packing.Floats(
        X, Y, Depth, Rotation,
        Width, Height, PivotX, PivotY,
        U0, V0, U1, V1,
        Red, Green, Blue, Alpha).CopyTo(into, at);
}
