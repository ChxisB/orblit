namespace Orblit.Asset.Tests;

// Tests with no Dart original beside the atlas ones: the roles Dart reads out of a model's materials.
public sealed class TextureRoleTests
{
    private static readonly AssetId Id = AssetId.Parse("models/thing.gltf");

    private static Task<GltfDocument> Of(string materials) =>
        GltfDocument.ReadAsync(
            Id,
            System.Text.Encoding.UTF8.GetBytes($$"""{"asset": {"version": "2.0"}, "materials": [{{materials}}]}"""),
            new MemoryAssetSource());

    [Theory]
    [InlineData(TextureRole.BaseColour, true)]
    [InlineData(TextureRole.MetallicRoughness, false)]
    [InlineData(TextureRole.Normal, false)]
    [InlineData(TextureRole.Occlusion, false)]
    [InlineData(TextureRole.Emissive, true)]
    public void OnlyColourPagesAreSrgb(TextureRole role, bool srgb) => Assert.Equal(srgb, role.Srgb());

    [Theory]
    [InlineData(TextureRole.BaseColour, "basecolour")]
    [InlineData(TextureRole.MetallicRoughness, "metallicroughness")]
    [InlineData(TextureRole.Normal, "normal")]
    [InlineData(TextureRole.Occlusion, "occlusion")]
    [InlineData(TextureRole.Emissive, "emissive")]
    public void EveryRoleNamesItsPage(TextureRole role, string suffix) => Assert.Equal(suffix, role.Suffix());

    [Fact]
    public void ABlankNormalIsFlatAndEverythingElseIsWhite()
    {
        Assert.Equal(new[] { 128, 128, 255, 255 }, TextureRole.Normal.Blank());
        foreach (var role in Enum.GetValues<TextureRole>().Where(role => role != TextureRole.Normal))
        {
            Assert.Equal(new[] { 255, 255, 255, 255 }, role.Blank());
        }
    }

    [Fact]
    public void ARoleThatDoesNotExistIsRefused()
    {
        var none = (TextureRole)99;

        Assert.Throws<InvalidOperationException>(() => none.Blank());
        Assert.Throws<InvalidOperationException>(() => none.Suffix());
    }

    [Fact]
    public async Task ListsEverySlotInAFixedOrder()
    {
        var document = await Of("""
            {"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 1, "texCoord": 1}},
             "normalTexture": {"index": 2}, "occlusionTexture": {"index": 3}, "emissiveTexture": {"index": 4}}
            """);

        var slots = TextureRoles.SlotsOf(document);

        Assert.Equal(
            [TextureRole.BaseColour, TextureRole.MetallicRoughness, TextureRole.Normal, TextureRole.Occlusion, TextureRole.Emissive],
            slots.Select(slot => slot.Role));
        Assert.Equal([0, 1, 2, 3, 4], slots.Select(slot => slot.Texture));
        Assert.Equal([0, 1, 0, 0, 0], slots.Select(slot => slot.TexCoord));
        Assert.Equal("metallicRoughnessTexture", slots[1].Key);
    }

    [Fact]
    public async Task SkipsASlotWithNoTextureIndex()
    {
        var document = await Of("""{"pbrMetallicRoughness": {"baseColorTexture": {}}, "normalTexture": {"index": 2.5}}""");

        Assert.Empty(TextureRoles.SlotsOf(document));
    }

    [Fact]
    public async Task ASlotIsTheMaterialsOwnMapSoWritingIntoItEditsTheDocument()
    {
        var document = await Of("""{"normalTexture": {"index": 2}}""");

        var slot = Assert.Single(TextureRoles.SlotsOf(document));
        slot.Info["index"] = 9;

        Assert.Equal(9, Assert.IsType<GltfMap>(document.List("materials")[0]["normalTexture"])["index"]);
        Assert.Same(document.List("materials")[0], slot.Owner);
    }

    [Fact]
    public async Task ATextureUsedAsTwoRolesIsConflictedAndKeepsItsFirst()
    {
        var document = await Of("""
            {"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 0}},
             "normalTexture": {"index": 1}},
            {"normalTexture": {"index": 1}, "emissiveTexture": {"index": 2}}
            """);

        var (roles, conflicted) = TextureRoles.RolesOf(document);

        Assert.Equal(TextureRole.BaseColour, roles[0]);
        Assert.Equal(TextureRole.Normal, roles[1]);
        Assert.Equal(TextureRole.Emissive, roles[2]);
        Assert.Equal([0], conflicted);
    }

    [Fact]
    public async Task AWholeNumberWrittenWithAPointIsNotAnIndex()
    {
        var document = await Of("""{"normalTexture": {"index": 2.0}}""");

        Assert.Empty(TextureRoles.SlotsOf(document));
    }
}
