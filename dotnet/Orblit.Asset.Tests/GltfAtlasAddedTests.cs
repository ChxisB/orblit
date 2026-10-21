using Orblit.Sprite;

namespace Orblit.Asset.Tests;

// Added in C#. The Dart tests and the fixture always pass every option, and a fixture cannot count decodes.
public sealed class GltfAtlasAddedTests
{
    private static GltfMap Colour(int texture) => new()
    {
        ["pbrMetallicRoughness"] = new GltfMap { ["baseColorTexture"] = new GltfMap { ["index"] = texture } },
    };

    [Fact]
    public void DefaultOptionsMatchDart() =>
        Assert.Equal(new ModelAtlasOptions(2048, 4, 2, 0, 2, true, 65536), new ModelAtlasOptions());

    [Fact]
    public async Task DecodesAnImageOnceHoweverManyMaterialsUseIt()
    {
        var model = new QuadModel();
        var shared = model.Texture([220, 40, 40, 255]);
        var other = model.Texture([40, 40, 220, 255]);
        model.Quad(model.Material(Colour(shared)));
        model.Quad(model.Material(Colour(shared)), x: 2);
        model.Quad(model.Material(Colour(other)), x: 4);
        var document = await GltfDocument.ReadAsync(
            AssetId.Parse("models/thing.glb"), model.ToGlb(), new MemoryAssetSource());

        var decoder = GltfAtlasTests.DecoderOver(document);
        var calls = 0;
        Task<Rgba?> Counted(int image, GltfMap definition)
        {
            calls++;
            return decoder(image, definition);
        }

        await ModelAtlas.AtlasModelAsync(document, Counted, new ModelAtlasOptions(MergePrimitives: false));

        Assert.Equal(2, calls);
    }
}
