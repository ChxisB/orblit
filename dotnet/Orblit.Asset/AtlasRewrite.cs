using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>What the packer decided: who was packed, where each landed, how big each page is and which roles it holds.</summary>
internal sealed record AtlasLayout(
    IReadOnlyList<AtlasCandidate> Packed,
    IReadOnlyDictionary<int, Placement> Placed,
    IReadOnlyList<(int Width, int Height)> Pages,
    IReadOnlyList<TextureRole> Roles);

/// <summary>
/// Points the packed materials at their pages, moves their texture coordinates onto the rectangles they were
/// given, and then collapses what has become identical.
/// </summary>
internal sealed class AtlasRewrite(GltfDocument document, AtlasLayout layout)
{
    private readonly Dictionary<(int Page, TextureRole Role), int> _texture = [];

    // An accessor shared by two materials has to be remapped once per material, because the two land on
    // different rectangles.
    private readonly Dictionary<(int Accessor, int Material), int> _remapped = [];

    public void Apply(ModelAtlasOptions options, List<string> notes)
    {
        AddPages();
        foreach (var candidate in layout.Packed)
        {
            Retarget(candidate);
            RemapUvs(candidate);
        }

        var pages = layout.Pages.Count;
        notes.Add(
            $"packed {layout.Packed.Count} materials onto {pages} {(pages == 1 ? "page" : "pages")} of " +
            string.Join(", ", layout.Roles.Select(role => role.Suffix())));
        Collapse(options, notes);
    }

    private static (GltfMap Holder, string Key) SlotOf(TextureRole role, GltfMap pbr, GltfMap material) => role switch
    {
        TextureRole.BaseColour => (pbr, "baseColorTexture"),
        TextureRole.MetallicRoughness => (pbr, "metallicRoughnessTexture"),
        TextureRole.Normal => (material, "normalTexture"),
        TextureRole.Occlusion => (material, "occlusionTexture"),
        TextureRole.Emissive => (material, "emissiveTexture"),
        _ => throw new InvalidOperationException($"{role} is not a texture role."),
    };

    // A texture slot pointing at index, keeping whatever the old slot said about itself. Scale on a normal map
    // and strength on an occlusion map are the material's, not the texture's: they survive the move to a
    // shared page, and dropping them would flatten or un-shade the surface.
    private static GltfMap SlotFor(object? existing, int index, int texCoord)
    {
        var slot = new GltfMap { ["index"] = index };
        if (texCoord != 0)
        {
            slot["texCoord"] = texCoord;
        }

        if (existing is GltfMap old)
        {
            foreach (var key in (string[])["scale", "strength", "extras"])
            {
                if (GltfJson.Get(old, key) is { } value)
                {
                    slot[key] = value;
                }
            }
        }

        return slot;
    }

    private void AddPages()
    {
        // One sampler for every page. Clamped, because a cell's neighbour is on the other side of its edge: a
        // page sampled with repeat wraps the far edge of the atlas into a cell, which draws a stripe of an
        // unrelated material along one side of every surface.
        var sampler = document.Add("samplers", new GltfMap
        {
            ["magFilter"] = 9729, // LINEAR
            ["minFilter"] = 9987, // LINEAR_MIPMAP_LINEAR
            ["wrapS"] = 33071, // CLAMP_TO_EDGE
            ["wrapT"] = 33071,
        });

        for (var p = 0; p < layout.Pages.Count; p++)
        {
            foreach (var role in layout.Roles)
            {
                var image = document.Add("images", new GltfMap
                {
                    ["uri"] = $"atlas{p}.{role.Suffix()}.ktx2",
                    ["mimeType"] = "image/ktx2",
                });
                // A KTX2 image is named through KHR_texture_basisu and not through source, which the spec
                // reserves for a PNG or JPEG fallback. The cook writes no fallback, so the extension is
                // required rather than merely used: a reader that cannot transcode it should say so instead
                // of drawing an untextured model.
                _texture[(p, role)] = document.Add("textures", new GltfMap
                {
                    ["sampler"] = sampler,
                    ["extensions"] = new GltfMap { ["KHR_texture_basisu"] = new GltfMap { ["source"] = image } },
                });
            }
        }

        Declare("extensionsUsed", "KHR_texture_basisu");
        Declare("extensionsRequired", "KHR_texture_basisu");
    }

    private void Retarget(AtlasCandidate candidate)
    {
        var at = layout.Placed[candidate.Material];
        var material = candidate.Definition;
        if (!material.TryGetValue("pbrMetallicRoughness", out var found))
        {
            material["pbrMetallicRoughness"] = found = new GltfMap();
        }

        var pbr = found as GltfMap ?? throw new InvalidCastException("\"pbrMetallicRoughness\" is not an object.");
        foreach (var role in layout.Roles)
        {
            var (holder, key) = SlotOf(role, pbr, material);
            holder[key] = SlotFor(GltfJson.Get(holder, key), _texture[(at.Page, role)], candidate.TexCoord);
        }

        // What was folded into the cell must stop being applied a second time on top of it. This is the step
        // that makes two materials which differed only by tint into one material, and skipping it would tint
        // everything twice.
        if (candidate.Factors.ContainsKey(TextureRole.BaseColour))
        {
            pbr.Remove("baseColorFactor");
        }

        if (candidate.Factors.ContainsKey(TextureRole.MetallicRoughness))
        {
            pbr.Remove("metallicFactor");
            pbr.Remove("roughnessFactor");
        }

        if (candidate.Factors.ContainsKey(TextureRole.Emissive))
        {
            material["emissiveFactor"] = new List<object?> { 1.0, 1.0, 1.0 };
        }
    }

    private void RemapUvs(AtlasCandidate candidate)
    {
        var at = layout.Placed[candidate.Material];
        var page = layout.Pages[at.Page];
        var scale = ((double)at.Width / page.Width, (double)at.Height / page.Height);
        var offset = ((double)at.X / page.Width, (double)at.Y / page.Height);
        var key = $"TEXCOORD_{candidate.TexCoord}";
        foreach (var primitive in candidate.Primitives)
        {
            var attributes = primitive["attributes"] as GltfMap ?? throw new InvalidCastException("\"attributes\" is not an object.");
            var accessor = GltfJson.Saturated(attributes[key]);
            if (!_remapped.TryGetValue((accessor, candidate.Material), out var index))
            {
                _remapped[(accessor, candidate.Material)] = index = document.AddVec2(Moved(document.ReadVec2(accessor), scale, offset));
            }

            attributes[key] = index;
        }
    }

    // glTF measures v from the top of the image and so does the packer, so there is no flip here. There is a
    // clamp: a UV a thousandth outside the unit square was harmless when it addressed its own image and reads a
    // neighbour's texels once it addresses a cell.
    private static float[] Moved(float[] uv, (double U, double V) scale, (double U, double V) offset)
    {
        var moved = new float[uv.Length];
        for (var i = 0; i < uv.Length; i += 2)
        {
            moved[i] = (float)((Doubles.Clamp(uv[i], 0.0, 1.0) * scale.U) + offset.U);
            moved[i + 1] = (float)((Doubles.Clamp(uv[i + 1], 0.0, 1.0) * scale.V) + offset.V);
        }

        return moved;
    }

    private void Collapse(ModelAtlasOptions options, List<string> notes)
    {
        var materials = GltfMerge.MergeMaterials(document);
        if (materials > 0)
        {
            notes.Add($"{materials} materials became copies of another and were pointed at it");
        }

        if (options.MergePrimitives)
        {
            var primitives = GltfMerge.MergePrimitives(document, options.MaxMergedVertices);
            if (primitives > 0)
            {
                notes.Add(
                    $"{primitives} primitives were joined into the ones they now share a material with, " +
                    $"which is {primitives} fewer draws");
            }
        }
    }

    private void Declare(string key, string extension)
    {
        if (!document.Json.TryGetValue(key, out var declared))
        {
            document.Json[key] = declared = new List<object?>();
        }

        if (declared is List<object?> list && !list.Contains(extension))
        {
            list.Add(extension);
        }
    }
}
