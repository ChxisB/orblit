namespace Orblit.Editor.Bridge;

/// <summary>Writes an entity tree to a scene file, the way a save does.</summary>
internal static class PackedFile
{
    /// <summary>
    /// Packs the entity and every entity under it that it owns, and saves the result to the path. Throws with
    /// the engine's error code when either step fails.
    /// </summary>
    public static void Write(EngineHost host, nint root, string path)
    {
        var prefab = host.Construct("Prefab");
        try
        {
            var error = host.PrefabPack(prefab, root);
            if (error == 0)
            {
                error = host.ResourceSaverSave(host.Singleton("ResourceSaver"), prefab, path, 0);
            }
            if (error != 0)
            {
                throw new IOException($"The engine could not save {path}. Error code {error}.");
            }
        }
        finally
        {
            host.Release(prefab);
        }
    }
}
