using System.Text;
using Orblit.Json;
using Orblit.Mesh;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

/// <summary>Export behaviour the fixture cannot pin: wording, and what the port refuses by design.</summary>
public class SceneExportAddedTests
{
    private static readonly byte[] JpegStart = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];

    private static SceneEntity Cube(string id, string? asset = null) => new(id, id, null, true, new Dictionary<string, SceneComponent>
    {
        [SceneComponents.Mesh] = new MeshComponent(shape: new Shape { Kind = ShapeKind.Cube }),
        [SceneComponents.Material] = new MaterialComponent(asset: asset),
    });

    private static MaterialLibrary Library(params (string Path, JsonMap Json)[] documents) =>
        new(documents.ToDictionary(one => one.Path, one => MaterialDocument.FromJson(one.Json).Document));

    private static SceneWritten WriteTextured(byte[] picture)
    {
        var scene = new SceneDocument(entities: [Cube("e", "wall.omat")]);
        var library = Library(("wall.omat", Map(("maps", Map(("baseColour", "tex/wall.png"))))));
        return scene.WriteAs(SceneFormat.Glb, materials: library, files: new Dictionary<string, byte[]> { ["tex/wall.png"] = picture });
    }

    private static JsonMap HeaderOf(SceneWritten written) => GltfFiles.GlbChunks(written.First.Bytes)!.Value.Json;

    private static SceneWritten PlaceLoose(JsonMap model)
    {
        var scene = new SceneDocument(entities:
        [
            new SceneEntity("e", "e", null, true, new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Mesh] = new MeshComponent(asset: "block.glb"),
            }),
        ]);
        return scene.WriteAs(SceneFormat.Glb, files: new Dictionary<string, byte[]> { ["block.glb"] = GltfFiles.GlbBytes(model, []) });
    }

    [Fact]
    public void ATextureThatIsNotAPictureIsReportedByNameAndLeftOut()
    {
        var written = WriteTextured(Encoding.UTF8.GetBytes("hello"));

        Assert.Equal(
            "tex/wall.png is not a picture this can embed. glTF carries PNG, JPEG, WebP and KTX2, and this is none of them.",
            Assert.Single(written.Problems));
        Assert.False(HeaderOf(written).ContainsKey("images"));
    }

    [Fact]
    public void APictureIsKnownByItsBytesNotItsName()
    {
        var images = (IReadOnlyList<object?>)HeaderOf(WriteTextured(JpegStart))["images"]!;

        Assert.Equal("image/jpeg", ((IReadOnlyDictionary<string, object?>)images[0]!)["mimeType"]);
    }

    [Fact]
    public void AModelWithAnImageBesideItIsRefusedWithAReason()
    {
        var written = PlaceLoose(Map(
            ("asset", Map(("version", "2.0"))),
            ("images", List(Map(("uri", "wall.png"))))));

        Assert.Contains("reads a texture from wall.png, beside it rather than inside it", Assert.Single(written.Problems));
    }

    [Fact]
    public void AModelWithTwoBuffersIsRefusedWithTheCount()
    {
        var written = PlaceLoose(Map(
            ("asset", Map(("version", "2.0"))),
            ("buffers", List(Map(("byteLength", 4L)), Map(("byteLength", 4L))))));

        Assert.Contains("keeps its data in 2 buffers", Assert.Single(written.Problems));
    }

    [Fact]
    public void TwoMaterialsThatShareAFileNameStayTwoMaterialsInAnObj()
    {
        var scene = new SceneDocument(entities: [Cube("a", "x/paint.omat"), Cube("b", "y/paint.omat")]);
        var library = Library(
            ("x/paint.omat", Map(("values", Map(("roughness", 0.2))))),
            ("y/paint.omat", Map(("values", Map(("roughness", 0.8))))));

        var mtl = scene.WriteAs(SceneFormat.Obj, materials: library).Files[1].Text;

        Assert.Contains("newmtl paint\n", mtl, StringComparison.Ordinal);
        Assert.Contains("newmtl paint.2\n", mtl, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoEntitiesThatSayTheSameThingShareOneMaterialInAnObj()
    {
        var scene = new SceneDocument(entities: [Cube("a", "x/paint.omat"), Cube("b", "x/paint.omat")]);
        var library = Library(("x/paint.omat", Map(("values", Map(("roughness", 0.2))))));

        var mtl = scene.WriteAs(SceneFormat.Obj, materials: library).Files[1].Text;

        Assert.Equal(1, mtl.Split("newmtl ", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void OnlyAnObjGivesUpTheScene()
    {
        Assert.True(SceneFormat.Gltf.KeepsScene);
        Assert.True(SceneFormat.Glb.KeepsScene);
        Assert.False(SceneFormat.Obj.KeepsScene);
    }

    [Fact]
    public void AWebpNeedsNothingPastItsTwelveByteHeader()
    {
        var images = (IReadOnlyList<object?>)HeaderOf(WriteTextured(Encoding.ASCII.GetBytes("RIFFxxxxWEBP")))["images"]!;

        Assert.Equal("image/webp", ((IReadOnlyDictionary<string, object?>)images[0]!)["mimeType"]);
    }

    [Theory]
    [InlineData("RIFF")]
    [InlineData("XXXXxxxxWEBP")]
    public void AWebpNeedsBothTheRiffStartAndTheWebpMark(string bytes)
    {
        var written = WriteTextured(Encoding.ASCII.GetBytes(bytes));

        Assert.Contains("is not a picture this can embed", Assert.Single(written.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ACameraWithNoDepthToItHasNoFarPlane()
    {
        var scene = new SceneDocument(entities:
        [
            new SceneEntity("c", "c", null, true, new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Camera] = new CameraComponent(near: 2, far: 2),
            }),
        ]);

        var cameras = (IReadOnlyList<object?>)HeaderOf(scene.WriteAs(SceneFormat.Glb))["cameras"]!;
        var perspective = (IReadOnlyDictionary<string, object?>)((IReadOnlyDictionary<string, object?>)cameras[0]!)["perspective"]!;

        Assert.False(perspective.ContainsKey("zfar"));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(2L)]
    public void AModelWhoseSceneIsOutOfRangeHangsItsFirstScene(long which)
    {
        var written = PlaceLoose(Map(
            ("asset", Map(("version", "2.0"))),
            ("scene", which),
            ("scenes", List(Map(("nodes", List(0L))), Map(("nodes", List(1L))))),
            ("nodes", List(Map(("name", "a")), Map(("name", "b"))))));

        var nodes = (IReadOnlyList<object?>)HeaderOf(written)["nodes"]!;

        Same(List(1L), ((IReadOnlyDictionary<string, object?>)nodes[0]!)["children"]);
    }

    [Theory]
    [InlineData("skins")]
    [InlineData("animations")]
    public void AModelWithASkinOrAnAnimationIsCopiedForEachPlacing(string kind)
    {
        var (json, binary) = GltfFiles.GlbChunks(Shape.Of(ShapeKind.Cube).Build().ToGlb("Block", [new GlbMaterial { Name = "Paint" }]))!.Value;
        var model = new Dictionary<string, object?>(json)
        {
            [kind] = kind == "skins" ? List(Map(("joints", List(0L)))) : List(Map(("samplers", List()), ("channels", List()))),
        };
        var scene = new SceneDocument(entities:
        [
            .. new[] { "a", "b" }.Select(id => new SceneEntity(id, id, null, true, new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Mesh] = new MeshComponent(asset: "block.glb"),
            })),
        ]);

        var written = scene.WriteAs(SceneFormat.Glb, files: new Dictionary<string, byte[]> { ["block.glb"] = GltfFiles.GlbBytes(model, binary) });

        Assert.Empty(written.Problems);
        Assert.Equal(2, ((IReadOnlyList<object?>)HeaderOf(written)[kind]!).Count);
    }

    [Fact]
    public void AnEmptyMeshIsNotAnObjectInAnObj()
    {
        var scene = new SceneDocument(entities:
        [
            new SceneEntity("e", "e", null, true, new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Mesh] = new MeshComponent(geometry: new Orblit.Mesh.Mesh()),
            }),
        ]);

        var obj = scene.WriteAs(SceneFormat.Obj).First.Text;

        Assert.DoesNotContain("\no ", obj, StringComparison.Ordinal);
    }
}
