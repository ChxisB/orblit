namespace Orblit.Filament;

/// <summary>
/// What the renderer already holds of the scene that is only sent when it changes: the revision of
/// each population, splat cloud and sprite layer, and the shape of each terrain. The renderer
/// owns it, and updates it once a scene has landed.
/// </summary>
public sealed class Held
{
    internal Dictionary<int, int> Populations { get; } = [];

    /// <summary>The revision of each cloud held in memory. A cloud read from a file has no records to hold.</summary>
    internal Dictionary<int, int> Splats { get; } = [];

    internal Dictionary<int, int> Sprites { get; } = [];

    internal Dictionary<int, TerrainHeld> Terrain { get; } = [];

    /// <summary>
    /// Whether the renderer holds any population. A call that names none clears every one it
    /// holds, so a scene whose last population went has to be sent even though it names nothing.
    /// </summary>
    internal bool HasPopulations => Populations.Count > 0;

    /// <summary>Whether the renderer holds any cloud, from memory or from a file. The same rule as for populations.</summary>
    internal bool HasSplats { get; private set; }

    /// <summary>Whether the renderer holds any sprite layer. The same rule as for populations.</summary>
    internal bool HasSprites { get; private set; }

    /// <summary>Records what <paramref name="scene"/> sent, once it has been accepted.</summary>
    public void Remember(RenderScene scene)
    {
        Populations.Clear();
        foreach (var population in scene.Populations)
        {
            Populations[population.Key] = population.Revision;
        }

        Splats.Clear();
        foreach (var cloud in scene.Splats.Where(cloud => cloud.Data is not null))
        {
            Splats[cloud.Key] = cloud.Revision;
        }

        Sprites.Clear();
        foreach (var layer in scene.Sprites)
        {
            Sprites[layer.Key] = layer.Revision;
        }

        HasSplats = scene.Splats.Count > 0;
        HasSprites = scene.Sprites.Count > 0;

        Terrain.Clear();
        foreach (var terrain in scene.Terrain)
        {
            Terrain[terrain.Key] = TerrainHeld.Of(terrain);
        }
    }
}
