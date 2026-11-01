using Orblit.Terrain;
using TerrainData = Orblit.Terrain.Terrain;

namespace Orblit.Asset;

/// <summary>
/// A terrain read back out of a cooked bundle or a pack.
/// <see cref="Terrain"/> holds the settings and no regions. Give it and <see cref="Regions"/> to a
/// <see cref="TerrainStreamer"/>, which reads the ones near the focus as it moves.
/// </summary>
public sealed class CookedTerrain
{
    private CookedTerrain(TerrainData terrain, IRegionSource regions, IReadOnlyList<string> problems)
    {
        Terrain = terrain;
        Regions = regions;
        Problems = problems;
    }

    public TerrainData Terrain { get; }

    /// <summary>The regions the bundle holds, as the terrain's file lists them.</summary>
    public IRegionSource Regions { get; }

    /// <summary>What the terrain's file left out and which listed regions are missing.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>
    /// Opens the terrain cooked from the source file <paramref name="file"/>, such as
    /// <c>terrain/world.oterrain</c>, reading its settings from <paramref name="source"/> and finding its
    /// regions there.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="AssetNotFoundException"/> when the terrain itself is not there and a
    /// <see cref="TerrainFormatException"/> when it cannot be read. A region it lists that the source does not
    /// hold is left out and named in <see cref="Problems"/>, once, and is never asked for.
    /// </remarks>
    public static async Task<CookedTerrain> OpenAsync(IAssetSource source, AssetId file)
    {
        var bytes = await source.ReadAsync(Cooked(file, TerrainImporter.TerrainOutput)).ConfigureAwait(false);
        var load = TerrainData.Decode(TextRules.Decode(bytes));
        var present = new List<RegionKey>();
        var problems = new List<string>(load.Problems);
        foreach (var key in load.Regions)
        {
            var region = Cooked(TerrainImporter.RegionFile(file, key), TerrainImporter.RegionOutput);
            if (await source.ExistsAsync(region).ConfigureAwait(false))
            {
                present.Add(key);
            }
            else
            {
                problems.Add($"{key.FileName} is listed and is not in the bundle.");
            }
        }

        return new CookedTerrain(load.Terrain, new CookedRegions(source, file, present), problems.AsReadOnly());
    }

    /// <summary>Where the cook filed <paramref name="output"/> of <paramref name="source"/>: <c>&lt;asset id&gt;.&lt;output&gt;</c>.</summary>
    internal static AssetId Cooked(AssetId source, string output) => AssetId.Parse($"{source}.{output}");
}

internal sealed class CookedRegions(IAssetSource source, AssetId terrain, List<RegionKey> keys) : IRegionSource
{
    public IEnumerable<RegionKey> Keys { get; } = keys.AsReadOnly();

    public Task<byte[]> ReadAsync(RegionKey key) =>
        source.ReadAsync(CookedTerrain.Cooked(TerrainImporter.RegionFile(terrain, key), TerrainImporter.RegionOutput));
}
