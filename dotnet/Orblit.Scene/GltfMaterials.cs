using Orblit.Json;
using Orblit.Light;

namespace Orblit.Scene;

/// <summary>
/// The materials of a glTF being written: a <c>.omat</c> path worn over a colour, as the material
/// glTF has for it.
/// </summary>
internal sealed class GltfMaterials(GltfOutput output, GltfTextures textures, MaterialLibrary? library)
{
    private sealed record Wiring(string Slot, string Key, bool InPbr);

    // In the order the keys land in the file: the metallic-roughness block takes its two first.
    private static readonly Wiring[] Wirings =
    [
        new("baseColour", "baseColorTexture", true),
        new("metallicRoughness", "metallicRoughnessTexture", true),
        new("normal", "normalTexture", false),
        new("occlusion", "occlusionTexture", false),
        new("emissive", "emissiveTexture", false),
    ];

    // Slots glTF has no place for. They are kept in the material's extras so nothing is lost silently.
    private static readonly string[] Unwritten = ["blendBaseColour", "blendMask"];

    private readonly Dictionary<string, long> _materialAt = [];
    private readonly HashSet<string> _unresolved = [];

    /// <summary>
    /// The glTF material for a <c>.omat</c> path worn over a colour.
    /// </summary>
    /// <remarks>
    /// Both, because an entity with no material of its own still has a colour, and two entities
    /// wearing the same material in different colours are two materials in a file with nowhere else to
    /// put the difference.
    /// </remarks>
    public long Slot(string? path, Tint colour)
    {
        var resolved = path is null ? null : library?.Resolve(path);
        if (path is not null && library is null && _unresolved.Add(path))
        {
            output.Problems.Add(
                $"the material {path} was not resolved, so what wears it is written in the colour its mesh was drawn in.");
        }

        // Kept apart by what they say, not by where they came from. Two entities wearing the same
        // material differ in a colour that material overrides, and a file that wrote one entry each
        // would be four hundred identical materials: four hundred things to bind, for one appearance.
        var described = Describe(path, resolved, colour.Linear);
        var key = JsonText.Encode(described);
        return _materialAt.TryGetValue(key, out var cached)
            ? cached
            : _materialAt[key] = output.Add("materials", described);
    }

    /// <summary>
    /// Whether anything this entity wears, now or under a look, is read in the texture's own frame
    /// and so needs one written down.
    /// </summary>
    public bool NeedsTangents(MaterialComponent? wearing)
    {
        if (wearing is null || library is null)
        {
            return false;
        }

        var paths = wearing.Asset is null ? wearing.Looks.Values : wearing.Looks.Values.Prepend(wearing.Asset);
        return paths.Any(path => library.Resolve(path).Maps.ContainsKey("normal"));
    }

    private OrderedDictionary<string, object?> Describe(string? path, ResolvedMaterial? resolved, Vector3 colour)
    {
        var draft = new MaterialDraft(resolved, output.Used);
        if (path is not null)
        {
            draft.Out["name"] = path.Split('/')[^1].Split('.')[0];
        }

        draft.Factors(colour);
        var transform = TransformOf(resolved);
        foreach (var wiring in Wirings)
        {
            Wire(draft, wiring, transform);
        }

        foreach (var slot in Unwritten)
        {
            if (resolved?.Maps.GetValueOrDefault(slot) is { } source)
            {
                draft.Missed[slot] = source;
            }
        }

        draft.Emissive();
        draft.Blend();
        draft.Sides();
        draft.Unlit();
        draft.ClearCoat();
        draft.Sheen();
        draft.Anisotropy();
        draft.Reflectance();
        return draft.Finish(path);
    }

    private void Wire(MaterialDraft draft, Wiring wiring, OrderedDictionary<string, object?>? transform)
    {
        if (draft.Resolved?.Maps.GetValueOrDefault(wiring.Slot) is not { } source)
        {
            return;
        }

        // Asked for only once there is something to read with it, so a material with no maps does
        // not leave a sampler nothing uses.
        if (textures.Texture(source, textures.Sampler(draft.Resolved)) is not { } texture)
        {
            draft.Missed[wiring.Slot] = source;
            return;
        }

        var entry = new OrderedDictionary<string, object?> { ["index"] = texture };
        if (transform is not null)
        {
            entry["extensions"] = new OrderedDictionary<string, object?> { ["KHR_texture_transform"] = transform };
            output.Used.Add("KHR_texture_transform");
        }

        if (wiring.Key == "normalTexture" && draft.Resolved.Number("normalScale") is { } scale)
        {
            entry["scale"] = scale;
        }

        if (wiring.Key == "occlusionTexture" && draft.Resolved.Number("ambientOcclusion") is { } strength)
        {
            entry["strength"] = strength;
        }

        (wiring.InPbr ? draft.Pbr : draft.Out)[wiring.Key] = entry;
    }

    private static OrderedDictionary<string, object?>? TransformOf(ResolvedMaterial? resolved)
    {
        var tiling = resolved?.Numbers("tiling");
        var offset = resolved?.Numbers("offset");
        if (tiling is null && offset is null)
        {
            return null;
        }

        var transform = new OrderedDictionary<string, object?>();
        if (offset is { Count: 2 })
        {
            transform["offset"] = Values.NumbersToJson(offset);
        }

        if (tiling is { Count: 2 })
        {
            transform["scale"] = Values.NumbersToJson(tiling);
        }

        return transform;
    }
}
