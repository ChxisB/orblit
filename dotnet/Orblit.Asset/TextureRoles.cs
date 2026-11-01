namespace Orblit.Asset;

/// <summary>Which texture is used as what, read from the materials rather than guessed from the image.</summary>
public static class TextureRoles
{
    /// <summary>
    /// Every texture slot a document's materials declare, in a fixed order. Walking the materials to learn a
    /// texture's role rather than guessing from the image is the one thing worth taking from how other
    /// engines import glTF: the file says what each texture is for, and reading it is both exact and cheap.
    /// </summary>
    public static List<TextureSlot> SlotsOf(GltfDocument document)
    {
        var slots = new List<TextureSlot>();
        foreach (var material in document.List("materials"))
        {
            if (GltfJson.Get(material, "pbrMetallicRoughness") is GltfMap pbr)
            {
                Slot(slots, TextureRole.BaseColour, material, pbr, "baseColorTexture");
                Slot(slots, TextureRole.MetallicRoughness, material, pbr, "metallicRoughnessTexture");
            }

            Slot(slots, TextureRole.Normal, material, material, "normalTexture");
            Slot(slots, TextureRole.Occlusion, material, material, "occlusionTexture");
            Slot(slots, TextureRole.Emissive, material, material, "emissiveTexture");
        }

        return slots;
    }

    /// <summary>
    /// Which role each texture is used as, and which textures are used as more than one. A texture used both as
    /// base colour and as roughness has no single correct colour space, so it is reported rather than packed:
    /// whichever page it went on, half its uses would be wrong, and wrong by an amount that looks like a
    /// lighting bug rather than an import bug.
    /// </summary>
    public static (OrderedDictionary<int, TextureRole> Roles, IReadOnlyCollection<int> Conflicted) RolesOf(GltfDocument document)
    {
        var roles = new OrderedDictionary<int, TextureRole>();
        // A set that keeps the order its members arrived in, which .NET has no type for. Only its keys are used.
        var conflicted = new OrderedDictionary<int, bool>();
        foreach (var slot in SlotsOf(document))
        {
            if (!roles.TryGetValue(slot.Texture, out var already))
            {
                roles[slot.Texture] = slot.Role;
            }
            else if (already != slot.Role)
            {
                conflicted[slot.Texture] = true;
            }
        }

        return (roles, conflicted.Keys);
    }

    private static void Slot(List<TextureSlot> into, TextureRole role, GltfMap material, GltfMap holder, string key)
    {
        if (GltfJson.Get(holder, key) is GltfMap info && GltfJson.IsInt(GltfJson.Get(info, "index")))
        {
            into.Add(new TextureSlot(role, material, key, info));
        }
    }
}
