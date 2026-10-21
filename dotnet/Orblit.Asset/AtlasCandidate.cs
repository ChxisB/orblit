using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>A material that can be packed, and everything needed to pack it.</summary>
internal sealed class AtlasCandidate
{
    public required int Material { get; init; }

    public required GltfMap Definition { get; init; }

    public required int TexCoord { get; init; }

    /// <summary>Which image each of its roles reads.</summary>
    public required OrderedDictionary<TextureRole, int> Images { get; init; }

    /// <summary>
    /// The factor to fold into each role's cell, for the roles where folding it in is what makes two
    /// materials become one.
    /// </summary>
    public required Dictionary<TextureRole, List<double>> Factors { get; init; }

    public required IReadOnlyList<GltfMap> Primitives { get; init; }

    public Dictionary<TextureRole, Rgba> Pixels { get; } = [];

    public int CellWidth { get; private set; } = 1;

    public int CellHeight { get; private set; } = 1;

    public string Label => LabelOf(Definition, Material);

    public static string LabelOf(GltfMap material, int index) =>
        GltfJson.Get(material, "name") is string name ? $"\"{name}\"" : $"material {index}";

    /// <summary>
    /// A cell is as big as the material's largest map, so no map is thrown away and the smaller ones are
    /// stretched to meet it. Stretching a 512 roughness onto a 2048 cell costs page area and no detail, which
    /// is the right way round: the alternative is a second page size and a second UV remap.
    /// </summary>
    public void SizeCell(int ceiling)
    {
        var width = 1;
        var height = 1;
        foreach (var pixels in Pixels.Values)
        {
            width = Math.Max(width, pixels.Width);
            height = Math.Max(height, pixels.Height);
        }

        var scale = Math.Min(1.0, (double)ceiling / Math.Max(width, height));
        CellWidth = (int)Math.Max(1, Doubles.Round(width * scale));
        CellHeight = (int)Math.Max(1, Doubles.Round(height * scale));
    }

    /// <summary>One material's map for <paramref name="role"/>, at its cell size, with the factor folded in.</summary>
    public Rgba Cell(TextureRole role)
    {
        // A copy, because the same decoded image can be two materials' map and folding one material's tint
        // into it would tint the other as well.
        var cell = Pixels.TryGetValue(role, out var source)
            ? new Rgba(
                CellWidth,
                CellHeight,
                (byte[])AtlasPixels.Resample(source, CellWidth, CellHeight, role == TextureRole.Normal).Pixels.Clone())
            : Rgba.Filled(CellWidth, CellHeight, role.Blank());
        if (Factors.TryGetValue(role, out var factor))
        {
            AtlasPixels.MultiplyInto(cell, factor, role.Srgb());
        }

        return cell;
    }
}
