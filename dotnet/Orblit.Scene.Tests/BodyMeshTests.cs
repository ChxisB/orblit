using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class BodyMeshTests
{
    [Fact]
    public void MeshColliderGeometrySurvivesJsonAndCopiesWithoutAliasingInputs()
    {
        var vertices = new List<double> { -1, 0, -1, 1, 0, -1, 0, 0, 1 };
        var indices = new List<long> { 0, 2, 1 };
        var body = new BodyComponent(shape: BodyShape.Mesh, meshVertices: vertices, meshIndices: indices);
        vertices[0] = 99;
        indices[0] = 99;
        var loaded = BodyComponent.FromJson(body.ToJson()).CopyWith(mass: 5);

        Assert.Equal(BodyShape.Mesh, loaded.Shape);
        Assert.Equal(-1, loaded.MeshVertices[0]);
        Assert.Equal([0L, 2L, 1L], loaded.MeshIndices);
        Assert.Throws<NotSupportedException>(() => ((ICollection<long>)loaded.MeshIndices).Add(1));
    }

    [Fact]
    public void MalformedIndicesStayInvalidInsteadOfBecomingADifferentTriangle()
    {
        var body = BodyComponent.FromJson(Map(("shape", "mesh"), ("meshIndices", List(0L, "x", 2L))));

        Assert.Equal([0L, -1L, 2L], body.MeshIndices);

        var bad = BodyComponent.FromJson(Map(("meshVertices", List(0L, "x", 0L, 1L, 0L, 0L, 0L, 0L, 1L))));

        Assert.Empty(bad.MeshVertices);
    }
}
