namespace Orblit.Sprite;

/// <summary>A rectangle of pixels inside a larger image.</summary>
/// <param name="Name">What the packer called it.</param>
/// <param name="X">Where it sits in the packed image, in pixels.</param>
/// <param name="Y">Where it sits in the packed image, in pixels.</param>
/// <param name="Width">Its width in the packed image.</param>
/// <param name="Height">Its height in the packed image.</param>
/// <param name="Rotated">
/// Whether the packer turned it a quarter turn to fit. Ignoring this is the commonest way an
/// atlas comes out wrong: the frames that packed better sideways are drawn on their side.
/// </param>
/// <param name="Trimmed">Whether the packer cut the empty space off it.</param>
/// <param name="OffsetX">
/// Where the trimmed part sits inside the original frame. Without it every frame is drawn
/// snug against its own corner and the sprite jumps about between frames.
/// </param>
/// <param name="OffsetY">Where the trimmed part sits inside the original frame.</param>
/// <param name="SourceWidth">How wide it was before trimming. Zero when it was never trimmed.</param>
/// <param name="SourceHeight">How tall it was before trimming. Zero when it was never trimmed.</param>
// F2: eleven parameters, and every one after the size is optional and called by name.
public sealed record Region(
    string Name,
    long X,
    long Y,
    long Width,
    long Height,
    bool Rotated = false,
    bool Trimmed = false,
    long OffsetX = 0,
    long OffsetY = 0,
    long SourceWidth = 0,
    long SourceHeight = 0)
{
    /// <summary>Its size as the game should treat it: the original, if it was trimmed.</summary>
    public (long Width, long Height) PlacedSize =>
        Trimmed && SourceWidth > 0 ? (SourceWidth, SourceHeight) : (Width, Height);

    /// <summary>
    /// Texture coordinates, given the size of the image it came from. Fractions rather than
    /// pixels, because that is what a sampler takes.
    /// </summary>
    public (double U0, double V0, double U1, double V1) Uv(long imageWidth, long imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return (0, 0, 1, 1);
        }

        return ((double)X / imageWidth, (double)Y / imageHeight, (double)(X + Width) / imageWidth, (double)(Y + Height) / imageHeight);
    }
}
