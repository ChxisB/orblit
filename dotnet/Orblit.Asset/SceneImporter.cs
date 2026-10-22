using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Reads a scene file for the assets it names, so that editing a model recooks the scene that places it. A
/// scene holds no bytes worth cooking, since it is already the small file it wants to be. What this produces
/// is the scene itself and, much more importantly, the list of everything under it. That list is what a build
/// watches, and what decides whether a bundle carries a model at all.
/// </summary>
public sealed class SceneImporter : Importer
{
    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "oscene" };

    // A suffix rather than a fixed list, so that baseColorTexture and emissiveTexture are both found without
    // either being named here. The cost of a wrong guess is a dependency too many, which recooks once too
    // often, far cheaper than the other mistake.
    private static readonly string[] AssetSuffixes =
    [
        "asset", "Asset", "model", "Model", "texture", "Texture", "mesh", "Mesh", "splats", "Splats",
        "environment", "Environment", "material", "Material", "sprite", "Sprite", "atlas", "Atlas",
    ];

    public override string Name => "scene";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    // On the pool so that a scene that cannot be read faults the task, as an async method would, rather than
    // throwing at the call.
    public override Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings) =>
        Task.Run<IReadOnlySet<AssetId>>(() => Named(id, bytes));

    private static HashSet<AssetId> Named(AssetId id, byte[] bytes)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(TextRules.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new ImportFailureException(id, "is not readable as a scene", error.Message);
        }

        var found = new HashSet<AssetId>();
        Collect(id, parsed, found);
        return found;
    }

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        Task.FromResult(new ImportResult { Outputs = new Dictionary<string, byte[]> { ["oscene"] = request.Bytes } });

    // Walks the whole document looking for asset references, rather than reading the fields a scene is known
    // to have today. A scene's schema grows, and a new component with a new texture field is a normal thing to
    // add. A collector that knew the schema would keep working while quietly missing the new field. Since
    // every reference in a scene is a string under a key that says what it is, looking at every such string
    // finds them all, including the ones added after this was written.
    private static void Collect(AssetId scene, object? node, HashSet<AssetId> into)
    {
        if (node is List<object?> children)
        {
            foreach (var child in children)
            {
                Collect(scene, child, into);
            }

            return;
        }

        if (node is not GltfMap map)
        {
            return;
        }

        foreach (var (key, value) in map)
        {
            if (value is string name && NamesAnAsset(key))
            {
                if (Reference(scene, name) is { } id)
                {
                    into.Add(id);
                }
            }
            else
            {
                Collect(scene, value, into);
            }
        }
    }

    // A scene holds asset ids, not paths: models/floor.glb is that asset wherever the scene lives, so moving a
    // scene between folders does not break it. This is the opposite of glTF, which resolves its URIs against
    // the document, and the difference is deliberate. glTF is a format the engine reads rather than one it
    // defines, and its rule is the spec's.
    //
    // A reference that starts ./ or ../ is the exception, and is read against the scene's own folder. It is
    // spelled that way precisely to say "near me", and a scene generated beside its assets uses it.
    //
    // A string without a file extension is not a reference. A material field naming a material in the scene's
    // own list, or "matte white", is a normal thing for a scene to hold, and it is not a file. Treating it as
    // one would make the cook fail on a scene that is perfectly valid, and the failure would read "no such
    // asset: matte white", which explains nothing. Every asset this pipeline cooks has an extension, so
    // requiring one costs nothing and removes the whole class of false reference.
    private static AssetId? Reference(AssetId scene, string value)
    {
        var id = value.StartsWith("./", StringComparison.Ordinal) || value.StartsWith("../", StringComparison.Ordinal)
            ? scene.Resolve(value)
            : AssetId.TryParse(value);
        return id is not null && id.Extension.Length > 0 ? id : null;
    }

    private static bool NamesAnAsset(string key) =>
        AssetSuffixes.Any(suffix => key.EndsWith(suffix, StringComparison.Ordinal));
}
