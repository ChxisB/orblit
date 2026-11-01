namespace Orblit.Asset;

/// <summary>The factors worth folding into the cells, with the ones that cannot be folded left where they are.</summary>
internal static class AtlasFactors
{
    public static Dictionary<TextureRole, List<double>> Of(GltfMap material)
    {
        var factors = new Dictionary<TextureRole, List<double>>();
        if (GltfJson.Get(material, "pbrMetallicRoughness") is GltfMap pbr)
        {
            Pbr(pbr, factors);
        }

        // A factor above one is a brightness, not a tint, and it cannot be folded into eight bits, so a
        // material using one keeps its factor and merges only with materials that share it.
        var emissive = Numbers(GltfJson.Get(material, "emissiveFactor"));
        if (emissive is not null && emissive.Any(value => value != 1.0) && emissive.All(value => value <= 1.0))
        {
            factors[TextureRole.Emissive] = [.. emissive, 1.0];
        }

        return factors;
    }

    private static void Pbr(GltfMap pbr, Dictionary<TextureRole, List<double>> factors)
    {
        var colour = Numbers(GltfJson.Get(pbr, "baseColorFactor")) ?? [1.0, 1.0, 1.0, 1.0];
        if (colour.Any(value => value != 1.0))
        {
            factors[TextureRole.BaseColour] = colour;
        }

        var metallic = NumberOr(pbr, "metallicFactor");
        var roughness = NumberOr(pbr, "roughnessFactor");
        if (metallic != 1.0 || roughness != 1.0)
        {
            // glTF keeps roughness in green and metalness in blue.
            factors[TextureRole.MetallicRoughness] = [1.0, roughness, metallic, 1.0];
        }
    }

    // A list's numbers, leaving out whatever in it is not one. Null for a value that is not a list.
    private static List<double>? Numbers(object? value)
    {
        if (value is not List<object?> list)
        {
            return null;
        }

        var numbers = new List<double>();
        foreach (var one in list)
        {
            if (GltfJson.AsNumber(one) is { } number)
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    // A missing key is one, and a value that is not a number is an error.
    private static double NumberOr(GltfMap map, string key) => GltfJson.Get(map, key) switch
    {
        null => 1.0,
        var value => GltfJson.AsNumber(value) ?? throw new InvalidCastException($"\"{key}\" is not a number."),
    };
}
