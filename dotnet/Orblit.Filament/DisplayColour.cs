namespace Orblit.Filament;

/// <summary>
/// A colour as a screen shows it, each part from nought to one. Lit things use linear colour,
/// and this is for what is never lit or graded, such as an outline.
/// </summary>
public sealed record DisplayColour(double R, double G, double B, double A)
{
    /// <summary>The colour in <paramref name="argb"/>, alpha in the top byte.</summary>
    public static DisplayColour FromArgb(uint argb) => new(
        ((argb >> 16) & 0xFF) / 255.0,
        ((argb >> 8) & 0xFF) / 255.0,
        (argb & 0xFF) / 255.0,
        (argb >> 24) / 255.0);
}
