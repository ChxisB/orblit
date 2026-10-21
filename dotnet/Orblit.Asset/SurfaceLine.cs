namespace Orblit.Asset;

/// <summary>One line of a surface source, with where it was written.</summary>
internal sealed record SurfaceLine(SurfaceOrigin Origin, string Text);
