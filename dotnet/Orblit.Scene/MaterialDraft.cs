namespace Orblit.Scene;

/// <summary>
/// One glTF material being put together from a resolved <c>.omat</c>, and the parts of it that do
/// not depend on a texture.
/// </summary>
/// <remarks>
/// glTF states a material in several places at once: the core fields, the metallic-roughness block
/// and an extension for each thing the core has no room for. Each of those is one method here, so the
/// order they are called in is the order the keys land in the file.
/// </remarks>
internal sealed class MaterialDraft(ResolvedMaterial? resolved, ISet<string> used)
{
    public ResolvedMaterial? Resolved { get; } = resolved;

    public OrderedDictionary<string, object?> Out { get; } = new();

    public OrderedDictionary<string, object?> Pbr { get; } = new();

    /// <summary>What this material has that was not written, by slot.</summary>
    public OrderedDictionary<string, object?> Missed { get; } = new();

    private OrderedDictionary<string, object?> Extensions { get; } = new();

    public void Factors(Vector3 colour)
    {
        var base4 = Resolved?.Numbers("baseColour");
        Pbr["baseColorFactor"] = base4 is { Count: 4 }
            ? Values.NumbersToJson(base4)
            : Values.NumbersToJson([colour.X, colour.Y, colour.Z, 1.0]);

        if (Resolved?.Number("metallic") is { } metallic)
        {
            Pbr["metallicFactor"] = metallic;
        }

        if (Resolved?.Number("roughness") is { } roughness)
        {
            Pbr["roughnessFactor"] = roughness;
        }

        // A mesh with no metallic-roughness map and nothing to say about either is a plain painted
        // surface. glTF's defaults are fully metallic and fully rough, which is not what anybody means
        // by that, so they are stated rather than left out.
        Pbr.TryAdd("metallicFactor", 0.0);
        Pbr.TryAdd("roughnessFactor", 0.8);
    }

    public void Emissive()
    {
        if (Resolved?.Numbers("emissive") is not { Count: 3 } emissive)
        {
            return;
        }

        var strength = Resolved.Number("emissiveIntensity") ?? 1;
        var peak = emissive.Aggregate((a, b) => Math.Max(a, b));

        // glTF caps the factor at one and puts anything brighter in an extension, so a glow authored
        // at eight watts stays eight times as bright rather than being quietly clipped to white.
        var over = peak > 1 ? peak : 1.0;
        Out["emissiveFactor"] = Values.NumbersToJson(emissive.Select(c => c / over));
        var total = strength * over;
        if (Math.Abs(total - 1) > 1e-6)
        {
            Extend("KHR_materials_emissive_strength", new() { ["emissiveStrength"] = total });
        }
    }

    public void Blend()
    {
        var blend = Resolved?.Choice("blend");
        switch (blend)
        {
            case "masked":
                Out["alphaMode"] = "MASK";
                if (Resolved?.Number("maskThreshold") is { } threshold)
                {
                    Out["alphaCutoff"] = threshold;
                }

                break;
            case "transparent" or "fade" or "add":
                Out["alphaMode"] = "BLEND";
                if (blend == "add")
                {
                    Missed["blend"] = "add, which glTF has no word for";
                }

                break;
            default:
                break;
        }
    }

    public void Sides()
    {
        if ((Resolved?.Flag("doubleSided") ?? false) || Resolved?.Choice("culling") == "none")
        {
            Out["doubleSided"] = true;
        }
    }

    public void Unlit()
    {
        if (Resolved?.Choice("shading") == "unlit")
        {
            Extend("KHR_materials_unlit", new());
        }
    }

    public void ClearCoat()
    {
        if (Resolved?.Number("clearCoat") is not (> 0 and var factor))
        {
            return;
        }

        var coat = new OrderedDictionary<string, object?> { ["clearcoatFactor"] = factor };
        if (Resolved.Number("clearCoatRoughness") is { } roughness)
        {
            coat["clearcoatRoughnessFactor"] = roughness;
        }

        Extend("KHR_materials_clearcoat", coat);
    }

    public void Sheen()
    {
        if (Resolved?.Numbers("sheenColour") is not { Count: 3 } sheen)
        {
            return;
        }

        var block = new OrderedDictionary<string, object?> { ["sheenColorFactor"] = Values.NumbersToJson(sheen) };
        if (Resolved.Number("sheenRoughness") is { } roughness)
        {
            block["sheenRoughnessFactor"] = roughness;
        }

        Extend("KHR_materials_sheen", block);
    }

    public void Anisotropy()
    {
        if (Resolved?.Number("anisotropy") is not { } anisotropy || anisotropy == 0)
        {
            return;
        }

        // Orblit signs anisotropy to say which way the grain runs; glTF states the strength and the
        // angle apart, so a negative turns into a quarter turn of the same strength.
        var block = new OrderedDictionary<string, object?>
        {
            ["anisotropyStrength"] = Doubles.Clamp(Math.Abs(anisotropy), 0.0, 1.0),
        };
        if (anisotropy < 0)
        {
            block["anisotropyRotation"] = Math.PI / 2;
        }

        Extend("KHR_materials_anisotropy", block);
    }

    public void Reflectance()
    {
        if (Resolved?.Number("reflectance") is not { } reflectance || reflectance == 0.5)
        {
            return;
        }

        // Both describe how much a dielectric reflects head-on, and they agree at the default:
        // Orblit's f0 is 0.16r², glTF scales a 0.04 f0, so the scale is 4r² and r of a half is a
        // scale of one.
        var specular = Doubles.Clamp(4 * reflectance * reflectance, 0.0, 1.0);
        Extend("KHR_materials_specular", new() { ["specularColorFactor"] = Values.NumbersToJson([specular, specular, specular]) });
    }

    /// <summary>The material as the file holds it: the core fields, then the blocks that follow them.</summary>
    public OrderedDictionary<string, object?> Finish(string? path)
    {
        Out["pbrMetallicRoughness"] = Pbr;
        if (Extensions.Count > 0)
        {
            Out["extensions"] = Extensions;
        }

        if (path is not null || Missed.Count > 0)
        {
            var orblit = new OrderedDictionary<string, object?>();
            if (path is not null)
            {
                orblit["asset"] = path;
            }

            if (Missed.Count > 0)
            {
                orblit["unwritten"] = Missed;
            }

            Out["extras"] = new OrderedDictionary<string, object?> { ["orblit"] = orblit };
        }

        return Out;
    }

    private void Extend(string name, OrderedDictionary<string, object?> block)
    {
        Extensions[name] = block;
        used.Add(name);
    }
}
