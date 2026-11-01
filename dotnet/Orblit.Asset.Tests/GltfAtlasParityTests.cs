using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.Sprite;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.GltfFixtures;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class GltfAtlasParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "gltf_atlas");

    [Fact]
    public Task ResampleMatchesTheFixture() => CheckAsync(Golden, "resample", c => Task.FromResult<JsonNode?>(
        Out(AtlasPixels.Resample(Image(c, 0, 1, 2), In(c, 3).GetInt32(), In(c, 4).GetInt32(), In(c, 5).GetBoolean()))));

    [Fact]
    public Task RenormaliseMatchesTheFixture() => CheckAsync(Golden, "renormalise", c =>
    {
        var image = Image(c, 0, 1, 2);
        AtlasPixels.Renormalise(image);
        return Task.FromResult<JsonNode?>(Out(image));
    });

    [Fact]
    public Task MultiplyIntoMatchesTheFixture() => CheckAsync(Golden, "multiply", c =>
    {
        var image = Image(c, 0, 1, 2);
        var factor = In(c, 3).EnumerateArray().Select(Fixture.Number).ToList();
        AtlasPixels.MultiplyInto(image, factor, In(c, 4).GetBoolean());
        return Task.FromResult<JsonNode?>(Out(image));
    });

    [Fact]
    public Task BlitMatchesTheFixture() => CheckAsync(Golden, "blit", c =>
    {
        var page = Image(c, 0, 1, 2);
        foreach (var blit in In(c, 3).EnumerateArray())
        {
            var source = new Rgba(blit[1].GetInt32(), blit[2].GetInt32(), Convert.FromBase64String(blit[0].GetString()!));
            AtlasPixels.Blit(page, source, blit[3].GetInt32(), blit[4].GetInt32(), blit[5].GetInt32());
        }

        return Task.FromResult<JsonNode?>(Out(page));
    });

    [Fact]
    public Task BlankPageMatchesTheFixture() => CheckAsync(Golden, "blank", c => Task.FromResult<JsonNode?>(
        Out(AtlasPixels.BlankPage((TextureRole)In(c, 0).GetInt32(), In(c, 1).GetInt32(), In(c, 2).GetInt32()))));

    [Fact]
    public Task AtlasModelMatchesTheFixture() => CheckAsync(Golden, "atlas", async c =>
    {
        var document = await GltfDocument.ReadAsync(
            AssetId.Parse("models/thing.glb"), Convert.FromBase64String(Text(c, 0)), new MemoryAssetSource());
        var o = In(c, 1);
        var options = new ModelAtlasOptions(
            o[0].GetInt32(), o[1].GetInt32(), o[2].GetInt32(), o[3].GetInt32(), o[4].GetInt32(), o[5].GetBoolean(), o[6].GetInt32());
        var blind = In(c, 2).EnumerateArray().Select(i => i.GetInt32()).ToHashSet();
        var result = await ModelAtlas.AtlasModelAsync(document, Decoder(document, blind), options);

        var value = GlbParts(document.ToGlb());
        value["changed"] = result.Changed;
        value["notes"] = Dump.Arr(result.Notes, Dump.Text);
        value["pages"] = Dump.Arr(result.Pages, page =>
        {
            var json = Out(page.Image);
            json["stem"] = page.Stem;
            json["role"] = page.Role.Name();
            return json;
        });
        return value;
    });

    private static Rgba Image(JsonElement c, int pixels, int width, int height) =>
        new(In(c, width).GetInt32(), In(c, height).GetInt32(), Convert.FromBase64String(Text(c, pixels)));

    private static JsonObject Out(Rgba image) => new()
    {
        ["width"] = image.Width,
        ["height"] = image.Height,
        ["pixels"] = Convert.ToBase64String(image.Pixels),
    };

    // The exporter's decoder: a PNG in a buffer view, and nothing for an image that is blind or has no view.
    private static ImageDecoder Decoder(GltfDocument document, HashSet<int> blind) => (index, definition) =>
    {
        if (blind.Contains(index) || GltfJson.Get(definition, "bufferView") is not { } view || !GltfJson.IsInt(view))
        {
            return Task.FromResult<Rgba?>(null);
        }

        var png = PngCodec.Decode(document.ViewBytes(GltfJson.Saturated(view)).ToArray());
        return Task.FromResult<Rgba?>(new Rgba((int)png.Width, (int)png.Height, png.Pixels));
    };
}
