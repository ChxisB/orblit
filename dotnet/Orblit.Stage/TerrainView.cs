using Orblit.Filament;
using Ground = Orblit.Terrain.Terrain;

namespace Orblit.Stage;

/// <summary>How a terrain is drawn, and where its pictures come from.</summary>
public sealed record TerrainViewOptions
{
    /// <summary>
    /// Turns a path into the image itself, decoded: RGBA bytes a row at a time, square, and every
    /// image of every set the same size. An image it cannot supply draws as the plain one.
    /// </summary>
    public Func<string, byte[]?>? Pixels { get; init; }

    /// <summary>
    /// Must move whenever an image's pixels change and the number of sets and their size do not,
    /// since nothing else says they did.
    /// </summary>
    public int PicturesRevision { get; init; }

    public int MeshSize { get; init; } = 64;

    public int Levels { get; init; } = 6;

    public bool CastShadows { get; init; } = true;

    public bool ReceiveShadows { get; init; } = true;
}

/// <summary>
/// Turns ground kept as data into the terrain the renderer draws.
/// The regions' maps are handed over as they are, not copied: a region the renderer already holds
/// costs nothing to pass again, and one that was edited is sent because its revision moved, which
/// every edit to a region does.
/// A terrain the renderer cannot draw, with regions smaller than it takes, too many of them, or
/// images of different sizes, throws <see cref="ArgumentException"/> saying why, rather than drawing
/// nothing.
/// </summary>
public static class TerrainView
{
    public static RenderTerrain From(Ground terrain, int key, TerrainViewOptions? options = null)
    {
        options ??= new TerrainViewOptions();

        // A coordinate past an int is not one the renderer could hold, so a checked cast refuses it
        // where a wrapped one would name another region. A revision only has to differ from the last,
        // so it may wrap.
        return new RenderTerrain(
            key,
            [
                .. terrain.Regions.Select(region => new TerrainRegion(
                    checked((int)region.Key.X),
                    checked((int)region.Key.Z),
                    region.Heights,
                    region.Cover,
                    region.Colour)
                {
                    Revision = unchecked((int)region.Revision),
                }),
            ],
            [
                .. terrain.Sets.Select(set => new TerrainSet
                {
                    Albedo = Image(options, set.Albedo),
                    Normal = Image(options, set.Normal),
                    TileSize = set.TileSize,
                    Triplanar = set.Triplanar,
                }),
            ],
            new TerrainOptions
            {
                RegionSize = terrain.RegionSize,
                Spacing = terrain.Spacing,
                PicturesRevision = options.PicturesRevision,
                BlendSharpness = terrain.BlendSharpness,
                AutoSteep = checked((int)terrain.AutoCover.Steep),
                AutoFlat = checked((int)terrain.AutoCover.Flat),
                AutoSlope = terrain.AutoCover.Slope,
                AutoHeightFalloff = terrain.AutoCover.HeightFalloff,
                MeshSize = options.MeshSize,
                Levels = options.Levels,
                CastShadows = options.CastShadows,
                ReceiveShadows = options.ReceiveShadows,
            });
    }

    private static byte[]? Image(TerrainViewOptions options, string? path) =>
        path is null || options.Pixels is null ? null : options.Pixels(path);
}
