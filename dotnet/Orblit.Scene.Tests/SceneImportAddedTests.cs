using System.Text;
using Orblit.Json;
using static Orblit.Scene.Tests.ImportDocs;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneImportAddedTests
{
    private static OrderedDictionary<string, object?> SceneAt(long version) => Map(
        ("nodes", List(0L)),
        ("extras", Map(("orblit", Map(("formatVersion", version))))));

    [Fact]
    public void ADocumentFromBeforeVersionFourKeepsTheEntityAndSaysWhyItsPartsAreGone()
    {
        var bytes = Document(("scenes", List(SceneAt(3))), ("nodes", List(Recorded("old"))));

        var read = SceneImport.ReadSceneFrom(bytes);

        var entity = Assert.Single(read.Document.Entities);
        Assert.Equal("old", entity.Id);
        Assert.Empty(entity.Components);
        Assert.Equal("Object \"old\" has no kind this editor can read. It was left out.", Assert.Single(read.Problems));
    }

    [Fact]
    public void HasProblemsFollowsTheList()
    {
        var clean = SceneImport.ReadSceneFrom(Document(("scenes", List(SceneAt(4))), ("nodes", List(Recorded("a")))));
        var lossy = SceneImport.ReadSceneFrom(Document(("nodes", List(Foreign("n", light: 0)))));

        Assert.False(clean.HasProblems);
        Assert.True(lossy.HasProblems);
    }

    [Fact]
    public void ADecodedDocumentIsReadWithoutTheBytesAroundIt()
    {
        var json = (JsonMap)JsonText.Decode(Encoding.UTF8.GetString(Document(("nodes", List(Foreign("n"))))))!;

        var read = SceneImport.GltfToScene(json, name: "Direct");

        Assert.Equal("Direct", read.Document.Name);
        Assert.Equal("n", Assert.Single(read.Document.Entities).Name);
    }
}
