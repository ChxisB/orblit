namespace Orblit.Asset;

/// <summary>Which materials can be packed, and why the rest cannot.</summary>
internal sealed class AtlasFinder
{
    // An allow list rather than a deny list, and deliberately short: every other KHR_materials_* extension
    // adds texture slots this rewrite does not know to pack, and packing a material while leaving its
    // clearcoat map pointing at the old UVs would put the clearcoat somewhere else on the model. Adding one
    // here means teaching TextureRoles about its slots first.
    private static readonly HashSet<string> PackableExtensions =
    [
        "KHR_materials_emissive_strength",
        "KHR_materials_unlit",
        "KHR_materials_ior",
    ];

    private readonly GltfDocument _document;
    private readonly List<string> _notes;
    private readonly List<GltfMap> _textures;
    private readonly List<TextureSlot> _slots;
    private readonly IReadOnlyCollection<int> _conflicted;

    public AtlasFinder(GltfDocument document, List<string> notes)
    {
        _document = document;
        _notes = notes;
        _textures = document.List("textures");
        _slots = TextureRoles.SlotsOf(document);
        _conflicted = TextureRoles.RolesOf(document).Conflicted;
    }

    public AtlasCandidate? Consider(GltfMap material, int index, IReadOnlyList<GltfMap> primitives)
    {
        var label = AtlasCandidate.LabelOf(material, index);
        if (UnknownExtensions(material) is { Count: > 0 } unknown)
        {
            Note(label, $"it uses {string.Join(", ", unknown)} and this pack does not know where those keep their textures");
            return null;
        }

        var slots = _slots.Where(slot => ReferenceEquals(slot.Owner, material)).ToList();
        if (slots.Count == 0)
        {
            return null;
        }

        var texCoords = slots.Select(slot => slot.TexCoord).Distinct().ToList();
        if (texCoords.Count > 1)
        {
            Note(label, $"its maps read different UV sets ({string.Join(", ", texCoords)}) and one atlas rectangle can only serve one");
            return null;
        }

        var images = ImagesOf(slots, label);
        if (images is null || primitives.Count == 0 || !UvsInside(primitives, texCoords[0], label))
        {
            return null;
        }

        return new AtlasCandidate
        {
            Material = index,
            Definition = material,
            TexCoord = texCoords[0],
            Images = images,
            Factors = AtlasFactors.Of(material),
            Primitives = primitives,
        };
    }

    private static List<string>? UnknownExtensions(GltfMap material) =>
        GltfJson.Get(material, "extensions") is GltfMap extensions
            ? [.. extensions.Keys.Where(key => !PackableExtensions.Contains(key))]
            : null;

    // The image each role reads, or null with the reason noted when a slot cannot be packed.
    private OrderedDictionary<TextureRole, int>? ImagesOf(List<TextureSlot> slots, string label)
    {
        var images = new OrderedDictionary<TextureRole, int>();
        foreach (var slot in slots)
        {
            var (image, reason) = Examine(slot);
            if (reason is not null)
            {
                Note(label, reason);
                return null;
            }

            images[slot.Role] = image;
        }

        return images;
    }

    private (int Image, string? Reason) Examine(TextureSlot slot)
    {
        if (_conflicted.Contains(slot.Texture))
        {
            return (0, $"texture {slot.Texture} is used in two roles, one of them {slot.Role.Name()}, " +
                "so there is no colour space and no cook that is right for it");
        }

        if (GltfJson.Get(slot.Info, "extensions") is GltfMap extensions && extensions.ContainsKey("KHR_texture_transform"))
        {
            return (0, $"its {slot.Key} carries a KHR_texture_transform, " +
                "which would be applied to the atlas rectangle rather than to the map");
        }

        if (slot.Texture < 0 || slot.Texture >= _textures.Count)
        {
            return (0, $"its {slot.Key} names texture {slot.Texture}, which the document does not have");
        }

        return ImageOf(_textures[slot.Texture]) is { } image
            ? (image, null)
            : (0, $"its {slot.Key} names a texture with no readable image");
    }

    // The test that actually decides it: a map is tileable only if something tiles it, and what tiles it is
    // UVs outside the unit square. Reading the wrap mode would be guessing, because plenty of models say
    // REPEAT and never leave 0..1, and packing a genuinely tiled map turns a brick wall into one brick
    // stretched over a building.
    private bool UvsInside(IReadOnlyList<GltfMap> primitives, int texCoord, string label)
    {
        foreach (var primitive in primitives)
        {
            if (GltfJson.Get(primitive, "attributes") is not GltfMap attributes)
            {
                return false;
            }

            var accessor = GltfJson.Get(attributes, $"TEXCOORD_{texCoord}");
            if (!GltfJson.IsInt(accessor))
            {
                Note(label, $"a primitive that uses it has no TEXCOORD_{texCoord} to remap");
                return false;
            }

            float[] uv;
            try
            {
                uv = _document.ReadVec2(GltfJson.Saturated(accessor));
            }
            catch (FormatException error)
            {
                // The dash is part of the note text that fixtures pin.
                _notes.Add($"{label}: left unpacked — {error.Message}");
                return false;
            }

            if (uv.Any(value => value < -0.001 || value > 1.001))
            {
                Note(label, "its UVs run outside 0 to 1, which means the map is tiled across the surface");
                return false;
            }
        }

        return true;
    }

    private static int? ImageOf(GltfMap texture)
    {
        if (GltfJson.Get(texture, "extensions") is GltfMap extensions
            && GltfJson.Get(extensions, "KHR_texture_basisu") is GltfMap basis
            && GltfJson.IsInt(GltfJson.Get(basis, "source")))
        {
            return GltfJson.Saturated(GltfJson.Get(basis, "source"));
        }

        return GltfJson.Get(texture, "source") is { } source ? GltfJson.Saturated(source) : null;
    }

    private void Note(string label, string reason) => _notes.Add($"{label}: left unpacked, because {reason}");
}
