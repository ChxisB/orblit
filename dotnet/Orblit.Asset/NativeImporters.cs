namespace Orblit.Asset;

/// <summary>
/// Every importer that drives a native program, ready to register. A list rather than a registry, so a
/// caller can put its own importers ahead of these. The registry tries them in order, and a project that
/// wants its own <c>.png</c> handling should get it.
/// </summary>
public static class NativeImporters
{
    public static IReadOnlyList<Importer> Create() =>
    [
        new TextureImporter(),
        new ModelImporter(),
        new SplatImporter(),
        new EnvironmentImporter(),
        new MaterialImporter(),
    ];
}
