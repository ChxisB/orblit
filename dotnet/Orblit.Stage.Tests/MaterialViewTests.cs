using Orblit.Filament;
using Orblit.Json;
using Orblit.Scene;

namespace Orblit.Stage.Tests;

public class MaterialViewTests
{
    private static MaterialDocument Document(OrderedDictionary<string, object?> json) => MaterialDocument.FromJson(json).Document;

    private static OrderedDictionary<string, object?> Map(params (string Key, object? Value)[] pairs)
    {
        var map = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    private static MaterialLibrary Library(params (string Path, OrderedDictionary<string, object?> Json)[] materials) =>
        new(materials.ToDictionary(m => m.Path, m => Document(m.Json)));

    // Everything a material contributes to the message the renderer reads: its flags and its
    // floats. Two materials with the same one of these draw the same, whatever else differs.
    private static string DrawnAs(RenderMaterial material) => JsonText.Encode(material.ToJson());

    // Two values for the field that a material could hold, which are not each other. Two rather than
    // one measured against the renderer's default, because some parameters default to one end of their
    // own range, and a flag that defaults to false cannot be shown to work by setting it to false.
    private static (object One, object Other) Probes(MaterialField field) => field.Kind switch
    {
        MaterialKind.Number => (0.317, 0.591),
        MaterialKind.Flag => (false, true),
        MaterialKind.Rgba => (new object[] { 0.11, 0.22, 0.33, 0.44 }, new object[] { 0.55, 0.66, 0.77, 0.88 }),
        MaterialKind.Rgb => (new object[] { 0.11, 0.22, 0.33 }, new object[] { 0.55, 0.66, 0.77 }),
        MaterialKind.Pair => (new object[] { 0.11, 0.22 }, new object[] { 0.55, 0.66 }),
        _ => (field.Choices[0], field.Choices[^1]),
    };

    public static TheoryData<string> FieldNames() => [.. MaterialFields.All.Select(f => f.Name)];

    // Wind is folded to nought unless the surface actually moves, so the base every probe is measured
    // against is a moving one. Otherwise bearing and strength would look like parameters that do nothing.
    [Theory]
    [MemberData(nameof(FieldNames))]
    public void EveryParameterReachesTheRenderer(string name)
    {
        var (one, other) = Probes(MaterialFields.ByName[name]);
        var library = Library(
            ("one.omat", Map(("values", Map(("windSpeed", 3.0), ("depthWrite", true), (name, one))))),
            ("other.omat", Map(("values", Map(("windSpeed", 3.0), ("depthWrite", true), (name, other))))));

        Assert.NotEqual(
            DrawnAs(MaterialView.From(library.Resolve("one.omat"), 1)),
            DrawnAs(MaterialView.From(library.Resolve("other.omat"), 1)));
    }

    [Fact]
    public void EverySlotInTheTableLandsSomewhere()
    {
        foreach (var slot in MaterialFields.Maps)
        {
            var library = Library(("a.omat", Map(("maps", Map((slot, $"textures/{slot}.png"))))));

            var built = MaterialView.From(library.Resolve("a.omat"), 1);

            Assert.Equal([$"textures/{slot}.png"], built.Maps.OfType<Texture>().Select(texture => texture.Path));
        }
    }

    [Fact]
    public void ColoursAreDecodedAndMeasurementsAreNot()
    {
        var library = Library(("a.omat", Map(("maps", Map(
            ("baseColour", "c.png"),
            ("emissive", "e.png"),
            ("normal", "n.png"),
            ("metallicRoughness", "mr.png"),
            ("occlusion", "o.png"))))));

        var built = MaterialView.From(library.Resolve("a.omat"), 1);

        Assert.True(built.BaseColourMap!.Srgb);
        Assert.True(built.EmissiveMap!.Srgb);
        Assert.False(built.NormalMap!.Srgb);
        Assert.False(built.MetallicRoughnessMap!.Srgb);
        Assert.False(built.OcclusionMap!.Srgb);
    }

    [Fact]
    public void ThePathsTheBlendMapsNameAreDecodedAsTheyShouldBe()
    {
        var library = Library(("a.omat", Map(("maps", Map(("blendBaseColour", "b.png"), ("blendMask", "m.png"))))));

        var built = MaterialView.From(library.Resolve("a.omat"), 1);

        Assert.True(built.BlendBaseColourMap!.Srgb);
        Assert.False(built.BlendMaskMap!.Srgb);
    }

    [Fact]
    public void APathNobodyCanPlaceIsLeftOutRatherThanGuessedAt()
    {
        var library = Library(("a.omat", Map(("maps", Map(("baseColour", "c.png"), ("normal", "n.png"))))));

        var built = MaterialView.From(library.Resolve("a.omat"), 1, path => path == "c.png" ? "/assets/c.png" : null);

        Assert.Equal("/assets/c.png", built.BaseColourMap!.Path);
        Assert.Null(built.NormalMap);
    }

    [Fact]
    public void AMaterialThatSaysNothingDrawsAsNoMaterialAtAll()
    {
        var built = MaterialView.From(new ResolvedMaterial(), 9);

        Assert.Equal(DrawnAs(new RenderMaterial { Key = 9 }), DrawnAs(built));
    }

    [Fact]
    public void AMaterialThatSetsOnlyASpeedStillGetsTheDefaultStrength()
    {
        var library = Library(("a.omat", Map(("values", Map(("windSpeed", 4.0))))));

        var built = MaterialView.From(library.Resolve("a.omat"), 1);

        Assert.Equal(4.0, built.Wind.Speed);
        Assert.Equal(1.0, built.Wind.Strength);
    }

    [Fact]
    public void TheSecondSurfaceFollowsTheFirstUntilItIsToldOtherwise()
    {
        var follows = MaterialView.From(Library(("a.omat", Map(("values", Map(("tiling", new object[] { 3.0, 4.0 })))))).Resolve("a.omat"), 1);
        var apart = MaterialView.From(Library(("a.omat", Map(("values", Map(("tiling", new object[] { 3.0, 4.0 }), ("blendTiling", new object[] { 5.0, 6.0 })))))).Resolve("a.omat"), 1);

        Assert.Equal((3.0, 4.0), (follows.BlendTiling.X, follows.BlendTiling.Y));
        Assert.Equal((5.0, 6.0), (apart.BlendTiling.X, apart.BlendTiling.Y));
        Assert.Equal((3.0, 4.0), (apart.Tiling.X, apart.Tiling.Y));
    }

    [Fact]
    public void TheSecondOffsetFollowsTheFirstUntilItIsToldOtherwise()
    {
        var follows = MaterialView.From(Library(("a.omat", Map(("values", Map(("offset", new object[] { 0.25, 0.5 })))))).Resolve("a.omat"), 1);
        var apart = MaterialView.From(Library(("a.omat", Map(("values", Map(("offset", new object[] { 0.25, 0.5 }), ("blendOffset", new object[] { 0.75, 0.125 })))))).Resolve("a.omat"), 1);

        Assert.Equal((0.25, 0.5), (follows.BlendOffset.X, follows.BlendOffset.Y));
        Assert.Equal((0.75, 0.125), (apart.BlendOffset.X, apart.BlendOffset.Y));
    }

    [Fact]
    public void TheKeyIsTheOneGiven()
    {
        Assert.Equal(42, MaterialView.From(new ResolvedMaterial(), 42).Key);
    }
}
