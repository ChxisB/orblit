namespace Orblit.Asset;

/// <summary>The result of reading one <c>.oshader</c>.</summary>
internal sealed class SurfaceTranslation
{
    /// <summary>The Filament material file text.</summary>
    public required string Material { get; init; }

    public required IReadOnlyList<SurfaceParam> Params { get; init; }

    /// <summary>
    /// Where each line of <see cref="Material"/> came from, in order. The lines the translator wrote itself point at
    /// the last line of the author's file.
    /// </summary>
    public required IReadOnlyList<SurfaceOrigin> Origins { get; init; }

    /// <summary>The files that were included, as project paths, in the order they were first named.</summary>
    public required IReadOnlySet<string> Includes { get; init; }

    /// <summary>Where matc's <paramref name="line"/> (one-based) came from.</summary>
    public SurfaceOrigin OriginOf(long line) => Origins[(int)Math.Clamp(line - 1, 0, Origins.Count - 1)];
}
