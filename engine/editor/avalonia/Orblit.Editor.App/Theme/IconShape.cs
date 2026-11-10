using Avalonia;
using Avalonia.Media;

namespace Orblit.Editor.App.Theme;

/// <summary>One path of an icon with its paint. A paint is 0 for none, <see cref="Themed"/> or 0xAARRGGBB.</summary>
internal sealed class IconShape(string path)
{
    /// <summary>Stands for the icon colour of the theme.</summary>
    public const uint Themed = 1;

    Geometry? _geometry;
    Geometry? _clipGeometry;

    public uint Fill { get; init; }

    public uint Stroke { get; init; }

    public double StrokeWidth { get; init; } = 1;

    public PenLineCap Cap { get; init; } = PenLineCap.Flat;

    public PenLineJoin Join { get; init; } = PenLineJoin.Miter;

    public Matrix Transform { get; init; } = Matrix.Identity;

    /// <summary>Path data that cuts the shape, in the same space as the shape, or null for no cut.</summary>
    public string? Clip { get; init; }

    public string Path { get; } = path;

    public Geometry Geometry => _geometry ??= Geometry.Parse(Path);

    public Geometry? ClipGeometry => Clip is null ? null : _clipGeometry ??= Geometry.Parse(Clip);
}

/// <summary>An icon's shapes, drawn in a box of this size.</summary>
internal sealed record IconDrawing(double Width, double Height, IconShape[] Shapes);
