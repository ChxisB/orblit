namespace Orblit.Asset;

/// <summary>Where a line of the cooked material text came from.</summary>
/// <param name="File">The project path of the file the author wrote.</param>
/// <param name="Line">One-based.</param>
public sealed record SurfaceOrigin(string File, int Line)
{
    /// <summary>The file and line, as <c>materials/pulse.oshader:12</c>.</summary>
    public override string ToString() => $"{File}:{Line}";
}
