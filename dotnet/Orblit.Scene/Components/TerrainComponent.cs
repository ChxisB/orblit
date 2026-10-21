namespace Orblit.Scene;

/// <summary>
/// Ground this entity puts in the scene: an <c>.oterrain</c> file, by its path in the project.
/// </summary>
/// <remarks>
/// The file is the terrain and the scene only says it is here. The heights are regions of their own
/// beside the settings file, far too large to write into a scene, and a terrain shared by two scenes
/// is one set of files rather than two copies that drift apart.
/// <para>
/// Laid in the world where its own texels say, whatever the entity's transform: a terrain is the
/// ground the world is measured from, and ground that could be turned or scaled would make every
/// height asked of it a question about which entity it belongs to.
/// </para>
/// </remarks>
public sealed class TerrainComponent(string? file = null, bool castShadows = true, bool receiveShadows = true)
    : SceneComponent
{
    /// <summary>Project-relative, or null until somebody chooses one.</summary>
    public string? File { get; } = file;

    public bool CastShadows { get; } = castShadows;

    public bool ReceiveShadows { get; } = receiveShadows;

    public override string Type => SceneComponents.Terrain;

    public static TerrainComponent FromJson(JsonMap json) => new(
        Values.Text(json, "file"),
        Values.Flag(json, "castShadows", true),
        Values.Flag(json, "receiveShadows", true));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["file"] = File,
        ["castShadows"] = CastShadows ? null : false,
        ["receiveShadows"] = ReceiveShadows ? null : false,
    });
}
