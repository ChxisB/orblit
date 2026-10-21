using System.Globalization;
using Orblit.Sprite;

namespace Orblit.Asset;

/// <summary>Where a packed material landed: its page, and its rectangle on it in texels.</summary>
internal sealed record Placement(int Page, int X, int Y, int Width, int Height);

/// <summary>One run of <see cref="ModelAtlas.AtlasModelAsync"/>.</summary>
internal sealed class AtlasRun(GltfDocument document, ImageDecoder decode, ModelAtlasOptions options)
{
    private readonly List<string> _notes = [];
    private readonly List<GltfMap> _images = document.List("images");
    private readonly Dictionary<int, Rgba?> _decoded = [];

    public async Task<ModelAtlasResult> RunAsync()
    {
        var materials = document.List("materials");
        if (materials.Count == 0)
        {
            return Nothing();
        }

        var candidates = FindCandidates(materials);
        if (candidates.Count < options.MinMaterials)
        {
            if (candidates.Count > 0)
            {
                _notes.Add(
                    $"left unpacked: only {candidates.Count} of {materials.Count} materials could be packed, " +
                    $"and {options.MinMaterials} is the point at which it pays");
            }

            return Nothing();
        }

        var ready = await DecodeAsync(candidates).ConfigureAwait(false);
        if (ready.Count < options.MinMaterials)
        {
            return Nothing();
        }

        var padding = 2 * options.Padding;
        var ceiling = options.MaxCellSize > 0
            ? Math.Min(options.MaxCellSize, options.MaxPageSize - padding)
            : options.MaxPageSize - padding;
        foreach (var candidate in ready)
        {
            candidate.SizeCell(ceiling);
        }

        var extrude = Math.Min(options.Extrude, options.Padding);
        var packed = Pack(ready, extrude);
        if (packed.Problems.Count > 0)
        {
            _notes.Add($"left unpacked: {string.Join("; ", packed.Problems)}");
            return Nothing();
        }

        return Apply(ready, packed, extrude);
    }

    // Which primitives use each material.
    private Dictionary<int, List<GltfMap>> PrimitivesByMaterial()
    {
        var used = new Dictionary<int, List<GltfMap>>();
        foreach (var mesh in document.List("meshes"))
        {
            if (GltfJson.Get(mesh, "primitives") is not List<object?> primitives)
            {
                continue;
            }

            foreach (var primitive in primitives.OfType<GltfMap>())
            {
                if (GltfJson.Get(primitive, "material") is { } material && GltfJson.IsInt(material))
                {
                    var index = GltfJson.Saturated(material);
                    if (!used.TryGetValue(index, out var list))
                    {
                        used[index] = list = [];
                    }

                    list.Add(primitive);
                }
            }
        }

        return used;
    }

    private List<AtlasCandidate> FindCandidates(List<GltfMap> materials)
    {
        var finder = new AtlasFinder(document, _notes);
        var usedBy = PrimitivesByMaterial();
        var candidates = new List<AtlasCandidate>();
        for (var m = 0; m < materials.Count; m++)
        {
            var primitives = usedBy.TryGetValue(m, out var found) ? found : [];
            if (finder.Consider(materials[m], m, primitives) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    // Decode once per image, not once per use: two materials sharing a base colour map are common, and a 4096
    // JPEG is not cheap to turn into texels.
    private async Task<List<AtlasCandidate>> DecodeAsync(List<AtlasCandidate> candidates)
    {
        var ready = new List<AtlasCandidate>();
        foreach (var candidate in candidates)
        {
            if (await ReadPixelsAsync(candidate).ConfigureAwait(false))
            {
                ready.Add(candidate);
            }
        }

        return ready;
    }

    private async Task<bool> ReadPixelsAsync(AtlasCandidate candidate)
    {
        foreach (var (role, image) in candidate.Images)
        {
            if (!_decoded.TryGetValue(image, out var pixels))
            {
                _decoded[image] = pixels = await decode(image, _images[image]).ConfigureAwait(false);
            }

            if (pixels is null)
            {
                _notes.Add($"{candidate.Label}: left unpacked, because its {role.Name()} image could not be decoded here");
                return false;
            }

            candidate.Pixels[role] = pixels;
        }

        return true;
    }

    private AtlasPackResult Pack(List<AtlasCandidate> ready, int extrude) => AtlasPacker.PackAtlas(
        // The packer wants texels and the base colour page wants composing, so the composing happens here and
        // the packer gets the answer.
        [.. ready.Select(candidate => new AtlasSprite(
            candidate.Material.ToString(CultureInfo.InvariantCulture),
            candidate.CellWidth,
            candidate.CellHeight,
            candidate.Cell(TextureRole.BaseColour).Pixels))],
        // Every one of these is load-bearing, not a preference. Trimming would shrink a cell without
        // shrinking the UV rectangle that names it; rotating would turn the texture a quarter turn under UVs
        // that are axis-aligned; and merging duplicates would give two materials one region, which is right
        // for sprites and wrong here, because the other roles' maps are not duplicates just because the base
        // colour is.
        new AtlasPackOptions(
            maxPageSize: options.MaxPageSize,
            padding: options.Padding,
            extrude: extrude,
            trim: false,
            allowRotation: false,
            mergeDuplicates: false));

    private ModelAtlasResult Nothing() => new([], _notes);

    private ModelAtlasResult Apply(List<AtlasCandidate> ready, AtlasPackResult packed, int extrude)
    {
        var byMaterial = ready.ToDictionary(candidate => candidate.Material);
        // Every role any packed material uses becomes a page for all of them. A material given a role it never
        // had gets the value that makes that role do nothing, so it draws as it did, and it is that uniformity
        // which lets the materials collapse into one afterwards, which is the whole point.
        var used = ready.SelectMany(candidate => candidate.Images.Keys).ToHashSet();
        var order = Enum.GetValues<TextureRole>().Where(used.Contains).ToList();

        var pages = new List<AtlasPage>();
        var placed = new Dictionary<int, Placement>();
        for (var p = 0; p < packed.Pages.Count; p++)
        {
            var page = packed.Pages[p];
            foreach (var (name, region) in page.Regions)
            {
                placed[int.Parse(name, CultureInfo.InvariantCulture)] =
                    new Placement(p, (int)region.X, (int)region.Y, (int)region.Width, (int)region.Height);
            }

            foreach (var role in order)
            {
                pages.Add(new AtlasPage(p, role, Canvas(page, role, byMaterial, extrude)));
            }
        }

        new AtlasRewrite(document, new AtlasLayout(
            ready,
            placed,
            [.. packed.Pages.Select(page => ((int)page.Width, (int)page.Height))],
            order)).Apply(options, _notes);
        return new ModelAtlasResult(pages, _notes);
    }

    private static Rgba Canvas(AtlasPackPage page, TextureRole role, Dictionary<int, AtlasCandidate> byMaterial, int extrude)
    {
        var canvas = AtlasPixels.BlankPage(role, (int)page.Width, (int)page.Height);
        foreach (var (name, region) in page.Regions)
        {
            var candidate = byMaterial[int.Parse(name, CultureInfo.InvariantCulture)];
            AtlasPixels.Blit(canvas, candidate.Cell(role), (int)region.X, (int)region.Y, extrude);
        }

        return canvas;
    }
}
