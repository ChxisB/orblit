using Orblit.Sprite;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

public sealed class GltfAtlasTests
{
    private static readonly int[] Red = [220, 40, 40, 255];
    private static readonly int[] Blue = [40, 40, 220, 255];
    private static readonly int[] Bump = [200, 120, 255, 255];
    private static readonly ModelAtlasOptions Separate = new(MergePrimitives: false);

    private static Task<GltfDocument> Open(QuadModel model) =>
        GltfDocument.ReadAsync(AssetId.Parse("models/thing.glb"), model.ToGlb(), new MemoryAssetSource());

    /// <summary>Decodes the PNGs a <see cref="QuadModel"/> stored in its buffer.</summary>
    internal static ImageDecoder DecoderOver(GltfDocument document) => (_, definition) =>
    {
        if (!definition.TryGetValue("bufferView", out var view) || view is null)
        {
            return Task.FromResult<Rgba?>(null);
        }

        var png = PngCodec.Decode(document.ViewBytes(Convert.ToInt32(view)).ToArray());
        return Task.FromResult<Rgba?>(new Rgba((int)png.Width, (int)png.Height, png.Pixels));
    };

    internal static int[] TexelAt(Rgba image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return [.. image.Pixels.Skip(at).Take(4).Select(b => (int)b)];
    }

    private static GltfMap Slot(int texture) => new() { ["index"] = texture };

    private static GltfMap Colour(int texture, GltfMap? extra = null)
    {
        var pbr = new GltfMap { ["baseColorTexture"] = Slot(texture) };
        foreach (var (key, value) in extra ?? [])
        {
            pbr[key] = value;
        }

        return new GltfMap { ["pbrMetallicRoughness"] = pbr };
    }

    /// <summary>The page texel the middle of a primitive's UVs lands on.</summary>
    private static int[] CentreOf(GltfDocument document, int primitive, Rgba page)
    {
        var list = (List<object?>)document.List("meshes")[0]["primitives"]!;
        var attributes = (GltfMap)((GltfMap)list[primitive]!)["attributes"]!;
        var uv = document.ReadVec2(Convert.ToInt32(attributes["TEXCOORD_0"]));
        double u = 0;
        double v = 0;
        for (var i = 0; i < uv.Length; i += 2)
        {
            u += uv[i];
            v += uv[i + 1];
        }

        var count = uv.Length / 2.0;
        return TexelAt(
            page,
            Math.Clamp((int)Math.Floor(u / count * page.Width), 0, page.Width - 1),
            Math.Clamp((int)Math.Floor(v / count * page.Height), 0, page.Height - 1));
    }

    [Fact]
    public async Task PacksTwoMaterialsOntoOnePageAndMovesTheirUvsIntoIt()
    {
        var model = new QuadModel();
        var a = model.Texture(Red);
        var b = model.Texture(Blue);
        model.Quad(model.Material(Colour(a)));
        model.Quad(model.Material(Colour(b)), x: 2);

        var document = await Open(model);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document), Separate);

        Assert.True(result.Changed, string.Join("; ", result.Notes));
        var only = Assert.Single(result.Pages);
        Assert.Equal(TextureRole.BaseColour, only.Role);
        Assert.Equal("atlas0.basecolour", only.Stem);

        // The point of the whole exercise: each quad still samples its own colour, from a different part of
        // one image.
        Assert.Equal(Red, CentreOf(document, 0, only.Image));
        Assert.Equal(Blue, CentreOf(document, 1, only.Image));

        // And both materials now name that one image, through the extension the spec reserves for KTX2 rather
        // than `source`, which must stay a PNG or JPEG fallback.
        var used = document.List("materials")
            .Select(m => Convert.ToInt32(((GltfMap)((GltfMap)m["pbrMetallicRoughness"]!)["baseColorTexture"]!)["index"]))
            .ToHashSet();
        var texture = document.List("textures")[Assert.Single(used)];
        Assert.False(texture.ContainsKey("source"));
        var basisu = (GltfMap)((GltfMap)texture["extensions"]!)["KHR_texture_basisu"]!;
        Assert.Contains("KHR_texture_basisu", (List<object?>)document.Json["extensionsRequired"]!);
        var image = document.List("images")[Convert.ToInt32(basisu["source"])];
        Assert.Equal("atlas0.basecolour.ktx2", image["uri"]);
        Assert.Equal("image/ktx2", image["mimeType"]);
    }

    [Fact]
    public async Task GivesAMaterialTheMapsItNeverHadBlankSoItCanShare()
    {
        var model = new QuadModel();
        var colour = model.Texture(Red);
        var bumps = model.Texture(Bump);
        var first = Colour(colour);
        first["normalTexture"] = Slot(bumps);
        model.Quad(model.Material(first));
        model.Quad(model.Material(Colour(colour)), x: 2);

        var document = await Open(model);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document), Separate);

        var roles = result.Pages.Select(p => p.Role).ToList();
        Assert.Contains(TextureRole.BaseColour, roles);
        Assert.Contains(TextureRole.Normal, roles);
        var normals = result.Pages.First(p => p.Role == TextureRole.Normal).Image;

        // The second material never had a normal map, so its cell must hold the value glTF samples when the
        // slot is absent. Otherwise handing it the slot would change how it draws.
        Assert.Equal([128, 128, 255, 255], CentreOf(document, 1, normals));
        Assert.Equal(Bump, CentreOf(document, 0, normals));
        Assert.True(document.List("materials")[1].ContainsKey("normalTexture"));
    }

    [Fact]
    public async Task FoldsAFactorIntoThePageSoTwoTintsBecomeOneMaterial()
    {
        var model = new QuadModel();
        var white = model.Texture([255, 255, 255, 255]);
        model.Quad(model.Material(Colour(white, new GltfMap { ["baseColorFactor"] = new List<object?> { 1.0, 0.0, 0.0, 1.0 } })));
        model.Quad(model.Material(Colour(white, new GltfMap { ["baseColorFactor"] = new List<object?> { 0.0, 0.0, 1.0, 1.0 } })), x: 2);

        var document = await Open(model);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document));

        var page = result.Pages.Single().Image;
        foreach (var material in document.List("materials"))
        {
            // A factor folded into the page must not be applied twice.
            var pbr = (GltfMap?)GltfJson.Get(material, "pbrMetallicRoughness");
            var factor = pbr is null ? null : GltfJson.Get(pbr, "baseColorFactor");
            Assert.True(
                factor is null || ((List<object?>)factor).All(v => GltfJson.AsNumber(v) == 1),
                "a factor folded into the page must not be applied twice");
        }

        // Two materials that differed only by tint are now one, and the tint lives in the texels.
        var primitives = (List<object?>)document.List("meshes")[0]["primitives"]!;
        var single = (GltfMap)Assert.Single(primitives)!;
        var uv = document.ReadVec2(Convert.ToInt32(((GltfMap)single["attributes"]!)["TEXCOORD_0"]));
        Assert.Equal(16, uv.Length);

        int At(int vertex)
        {
            var x = Math.Clamp((int)Math.Floor(uv[vertex * 2] * page.Width), 0, page.Width - 1);
            var y = Math.Clamp((int)Math.Floor(uv[(vertex * 2) + 1] * page.Height), 0, page.Height - 1);
            return TexelAt(page, x, y)[0];
        }

        // Vertex 0 belongs to the red quad, vertex 4 to the blue one.
        Assert.True(At(0) > 200);
        Assert.True(At(4) < 60);
    }

    [Fact]
    public async Task LeavesAModelWhoseMapsTileAcrossTheSurfaceAlone()
    {
        var model = new QuadModel();
        var a = model.Texture(Red);
        var b = model.Texture(Blue);
        model.Quad(model.Material(Colour(a)), uv: [0.0, 0.0, 4.0, 0.0, 4.0, 4.0, 0.0, 4.0]);
        model.Quad(model.Material(Colour(b)), x: 2);

        var document = await Open(model);
        var before = document.ReadVec2(1);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document));

        Assert.False(result.Changed);
        Assert.Contains("tiled", string.Join(' ', result.Notes));
        // A model left alone must come back byte for byte.
        Assert.Equal(before, document.ReadVec2(1));
    }

    [Fact]
    public async Task LeavesASlotCarryingATextureTransformAlone()
    {
        var model = new QuadModel();
        var a = model.Texture(Red);
        var b = model.Texture(Blue);
        var slot = Slot(a);
        slot["extensions"] = new GltfMap
        {
            ["KHR_texture_transform"] = new GltfMap { ["offset"] = new List<object?> { 0.5, 0.5 } },
        };
        model.Quad(model.Material(new GltfMap
        {
            ["pbrMetallicRoughness"] = new GltfMap { ["baseColorTexture"] = slot },
        }));
        model.Quad(model.Material(Colour(b)), x: 2);

        var document = await Open(model);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document));

        Assert.False(result.Changed);
        Assert.Contains("KHR_texture_transform", string.Join(' ', result.Notes));
    }

    [Fact]
    public async Task RefusesATextureAskedToBeTwoThingsAtOnce()
    {
        var model = new QuadModel();
        var both = model.Texture(Bump);
        var plain = model.Texture(Red);
        var first = Colour(both);
        first["normalTexture"] = Slot(both);
        model.Quad(model.Material(first));
        model.Quad(model.Material(Colour(plain)), x: 2);

        var document = await Open(model);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document));

        // One material is out. The other is alone, and one material is not an atlas worth making.
        Assert.False(result.Changed);
        Assert.Contains("two roles", string.Join(' ', result.Notes));
    }

    [Fact]
    public async Task LeavesAMaterialCarryingAnExtensionItDoesNotUnderstand()
    {
        var model = new QuadModel();
        var a = model.Texture(Red);
        var b = model.Texture(Blue);
        var first = Colour(a);
        first["extensions"] = new GltfMap
        {
            ["KHR_materials_clearcoat"] = new GltfMap { ["clearcoatFactor"] = 1.0 },
        };
        model.Quad(model.Material(first));
        model.Quad(model.Material(Colour(b)), x: 2);

        var document = await Open(model);
        var result = await ModelAtlas.AtlasModelAsync(document, DecoderOver(document));

        Assert.False(result.Changed);
        Assert.Contains("KHR_materials_clearcoat", string.Join(' ', result.Notes));
    }

    [Fact]
    public async Task CarriesThePartsOfTheDocumentItNeverLookedAtThrough()
    {
        var model = new QuadModel();
        var a = model.Texture(Red);
        var b = model.Texture(Blue);
        var first = Colour(a);
        first["name"] = "first";
        first["extras"] = new GltfMap { ["mine"] = 1 };
        first["doubleSided"] = true;
        model.Quad(model.Material(first));
        model.Quad(model.Material(Colour(b)), x: 2);
        model.Json["extras"] = new GltfMap { ["orblit"] = "hello" };
        ((GltfMap)((List<object?>)model.Json["nodes"]!)[0]!)["extras"] = new GltfMap { ["collider"] = "box" };

        var document = await Open(model);
        await ModelAtlas.AtlasModelAsync(document, DecoderOver(document), Separate);

        var again = await GltfDocument.ReadAsync(AssetId.Parse("models/thing.glb"), document.ToGlb(), new MemoryAssetSource());
        Assert.Equal("hello", ((GltfMap)again.Json["extras"]!)["orblit"]);
        Assert.Equal("box", ((GltfMap)again.List("nodes")[0]["extras"]!)["collider"]);
        var kept = again.List("materials").First(m => GltfJson.Get(m, "name") is "first");
        Assert.Equal(1, Convert.ToInt32(((GltfMap)kept["extras"]!)["mine"]));
        Assert.Equal(true, kept["doubleSided"]);
    }

    [Fact]
    public void ResamplesByAreaRatherThanByNearestNeighbour()
    {
        var source = new Rgba(2, 2, [0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 255]);
        var half = AtlasPixels.Resample(source, 1, 1);
        // Nearest neighbour would answer 0 or 255. An average answers between.
        Assert.InRange(half.Pixels[0], 100, 155);
        Assert.Equal(255, half.Pixels[3]);
    }

    [Fact]
    public void ExtrudesTheEdgeSoTheSamplerCannotReachANeighbour()
    {
        var page = AtlasPixels.BlankPage(TextureRole.BaseColour, 8, 8);
        AtlasPixels.Blit(page, Rgba.Filled(4, 4, Red), 2, 2, extrude: 2);

        Assert.Equal(Red, TexelAt(page, 2, 2));
        // Two texels of skirt on every side, so a bilinear tap at the seam of a mip still lands on this cell's
        // colour.
        Assert.Equal(Red, TexelAt(page, 0, 0));
        Assert.Equal(Red, TexelAt(page, 7, 7));
        Assert.Equal(Red, TexelAt(page, 1, 4));
    }

    [Fact]
    public void MultipliesAColourFactorInTheSpaceTheTexelsAreIn()
    {
        var image = Rgba.Filled(1, 1, [188, 188, 188, 255]);
        AtlasPixels.MultiplyInto(image, [0.5, 0.5, 0.5, 1.0], srgb: true);
        // Half the light, not half the number: 188 is about 0.5 linear, and half of that encodes to about 137,
        // not 94.
        Assert.InRange(image.Pixels[0], 130, 145);

        var linear = Rgba.Filled(1, 1, [188, 188, 188, 255]);
        AtlasPixels.MultiplyInto(linear, [0.5, 0.5, 0.5, 1.0], srgb: false);
        Assert.Equal(94, linear.Pixels[0]);
    }

    [Fact]
    public void RenormalisesAResampledNormalMap()
    {
        var image = Rgba.Filled(2, 1, [255, 128, 128, 255]);
        AtlasPixels.Renormalise(image);
        var x = (image.Pixels[0] / 255.0 * 2) - 1;
        var y = (image.Pixels[1] / 255.0 * 2) - 1;
        var z = (image.Pixels[2] / 255.0 * 2) - 1;
        Assert.InRange((x * x) + (y * y) + (z * z), 0.98, 1.02);
    }
}
