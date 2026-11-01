using Orblit.Mesh;
using static Orblit.Scene.Tests.ImportDocs;

namespace Orblit.Scene.Tests;

public class SceneImportObjTests
{
    private const string Two =
        "mtllib two.mtl\n"
        + "v 0 0 0\n"
        + "v 1 0 0\n"
        + "v 1 1 0\n"
        + "v 0 1 0\n"
        + "o floor\n"
        + "usemtl red\n"
        + "f 1 2 3 4\n"
        + "o wall\n"
        + "usemtl blue\n"
        + "f 1 2 3\n"
        + "f 1 3 4\n";

    private const string Mtl = "newmtl red\nKd 1 0 0\nnewmtl blue\nKd 0 0 1\n";

    private static Dictionary<string, byte[]> TwoMtl() => new() { ["two.mtl"] = Bytes(Mtl) };

    // an OBJ

    [Fact]
    public void OpensAsOneEntityForEachGroupWithItsFacesAndColour()
    {
        var read = SceneImport.ReadSceneFrom(Bytes(Two), "Level", TwoMtl());
        var doc = read.Document;

        Assert.Equal("Level", doc.Name);
        Assert.Equal(["floor", "wall"], doc.Entities.Select(e => e.Name));
        Assert.Equal([1, 2], doc.Entities.Select(e => MeshOf(e).Geometry!.FaceCount));
        Assert.True(MeshOf(doc.Entities[0]).Authored);
        var red = MeshOf(doc.Entities[0]).Colour;
        Assert.Equal(1, red.Red, 1e-9);
        Assert.Equal(0, red.Blue);
        var blue = MeshOf(doc.Entities[1]).Colour;
        Assert.Equal(1, blue.Blue, 1e-9);
        Assert.Equal(0, blue.Red);
        Assert.False(read.WasWrittenHere);
        Assert.Empty(read.Problems);
    }

    [Fact]
    public void SaysSoWhenOneGroupWearsTwoMaterials()
    {
        var read = SceneImport.ReadSceneFrom(Bytes(Two.Replace("o wall", "o floor", StringComparison.Ordinal)), files: TwoMtl());

        Assert.Single(read.Document.Entities);
        Assert.Contains("2 materials", Assert.Single(read.Problems));
    }

    [Fact]
    public void CarriesWhatTheReaderCouldNotReadIntoTheProblems()
    {
        var read = SceneImport.ReadSceneFrom(Bytes(Two));

        Assert.Contains("two.mtl", Assert.Single(read.Problems));
        Assert.NotNull(MeshOf(read.Document.Entities[0]).Geometry);
    }

    [Fact]
    public void AMeshWrittenByThisPackageComesBackAsOneEntity()
    {
        var files = Shape.Of(ShapeKind.Cube).Build().ToObj("box");
        var read = SceneImport.ReadSceneFrom(files[0].Bytes, files: files.ToDictionary(file => file.Name, file => file.Bytes));

        Assert.Equal("box", Assert.Single(read.Document.Entities).Name);
        Assert.Equal(6, MeshOf(read.Document.Entities[0]).Geometry!.FaceCount);
    }

    [Fact]
    public void WithNoFacesIsRefusedAsAScene() =>
        Assert.Throws<SceneFormatException>(() => SceneImport.ReadSceneFrom(Bytes("v 0 0 0\n")));

    // the sniff

    [Fact]
    public void AGltfWithAByteOrderMarkStillGoesToTheGltfReader()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Bytes("[1]")];

        var error = Assert.Throws<SceneFormatException>(() => SceneImport.ReadSceneFrom(bytes));
        Assert.Contains("glTF", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonEvenAnArrayStillGoesToTheGltfReader()
    {
        var error = Assert.Throws<SceneFormatException>(() => SceneImport.ReadSceneFrom(Bytes("  [1, 2]")));
        Assert.Contains("glTF", error.Message, StringComparison.Ordinal);
    }
}
