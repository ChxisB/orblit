namespace Orblit.Filament;

/// <summary>
/// What the renderer already holds of the scene that is only sent when it changes: the revision of
/// each population and the shape of each terrain. The renderer owns it, and updates it once a
/// scene has landed.
/// </summary>
public sealed class Held
{
    internal Dictionary<int, int> Populations { get; } = [];

    internal Dictionary<int, TerrainHeld> Terrain { get; } = [];

    /// <summary>
    /// Whether the renderer holds any population. A call that names none clears every one it
    /// holds, so a scene whose last population went has to be sent even though it names nothing.
    /// </summary>
    internal bool HasPopulations => Populations.Count > 0;

    /// <summary>Records what <paramref name="scene"/> sent, once it has been accepted.</summary>
    public void Remember(RenderScene scene)
    {
        Populations.Clear();
        foreach (var population in scene.Populations)
        {
            Populations[population.Key] = population.Revision;
        }

        Terrain.Clear();
        foreach (var terrain in scene.Terrain)
        {
            Terrain[terrain.Key] = TerrainHeld.Of(terrain);
        }
    }
}
