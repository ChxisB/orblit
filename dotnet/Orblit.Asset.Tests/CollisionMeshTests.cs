using System.Text;
using static Orblit.Asset.Tests.CollisionModels;

namespace Orblit.Asset.Tests;

public class CollisionMeshTests
{
    [Fact]
    public async Task GeometryRoundTripsWithAVersionAndRejectsInvalidIndices()
    {
        var mesh = await ReadAsync(Model());
        Assert.Equal(Positions, mesh.Vertices);
        Assert.Equal([0, 2, 1, 0, 3, 2], CollisionMesh.Decode(mesh.Encode()).Indices);
        Assert.Throws<ArgumentException>(() => new CollisionMesh(Positions, [0, 1, 4]));
        Assert.Throws<FormatException>(() => CollisionMesh.Decode("""{"version":2}"""u8));
    }

    [Fact]
    public async Task ActiveSceneNodeTransformsAndRepeatedInstancesAreBaked()
    {
        var mesh = await ReadAsync(Model("""
            [
              {"translation": [3, 0, 0], "children": [1, 2]},
              {"mesh": 0, "translation": [0, 2, 0], "scale": [2, 1, 1]},
              {"mesh": 0, "translation": [0, 4, 0]},
              {"mesh": 0, "translation": [999, 0, 0]}
            ]
            """));
        Assert.Equal([1, 2, -1], mesh.Vertices.Take(3));
        Assert.Equal([2, 4, -1], mesh.Vertices.Skip(12).Take(3));
        Assert.Equal(24, mesh.Vertices.Count);
        Assert.Equal([4, 6, 5, 4, 7, 6], mesh.Indices.Skip(6));
    }

    [Fact]
    public async Task TriangleStripsFansAndMirroredTransformsPreserveWinding()
    {
        var strip = await ReadAsync(Model(mode: 5, indices: [0, 1, 3, 2]));
        Assert.Equal([0, 1, 3, 3, 1, 2], strip.Indices);
        var fan = await ReadAsync(Model(mode: 6, indices: [0, 1, 2, 3]));
        Assert.Equal([0, 1, 2, 0, 2, 3], fan.Indices);
        var mirrored = await ReadAsync(Model("""[{"mesh": 0, "scale": [-1, 1, 1]}]"""));
        Assert.Equal([0, 1, 2, 0, 2, 3], mirrored.Indices);
    }

    [Fact]
    public async Task MalformedIndexedArraysAndCompressedGeometryAreRefused()
    {
        var json = Model();
        var nodes = (List<object?>)json["nodes"]!;
        nodes.Insert(0, null);
        await Assert.ThrowsAsync<FormatException>(() => ReadAsync(json));
        nodes.RemoveAt(0);
        json["scene"] = "zero";
        await Assert.ThrowsAsync<FormatException>(() => ReadAsync(json));
        json["scene"] = 0;
        var primitive = (GltfMap)((List<object?>)((GltfMap)((List<object?>)json["meshes"]!)[0]!)["primitives"]!)[0]!;
        primitive["extensions"] = GltfFixtures.Map("""{"KHR_draco_mesh_compression": {}}""");
        await Assert.ThrowsAsync<FormatException>(() => ReadAsync(json));
    }

    [Fact]
    public void AnObjCutsQuadsAndGroupsIntoTrianglesOfOneMesh()
    {
        var mesh = FromObj("v 0 0 0\nv 2 0 0\nv 2 0 2\nv 0 0 2\nv 0 1 0\no floor\nf 1 2 3 4\no wall\nf 1 2 5\n");
        Assert.Equal(9, mesh.Indices.Count);
        Assert.Equal(2, mesh.Vertices.Max());
    }

    [Fact]
    public void AnObjPointTooFarOutForAFloatIsAFormatErrorNotACrash() =>
        Assert.Throws<FormatException>(() => FromObj("v 1e39 0 0\nv 0 1e39 0\nv 0 0 1e39\nf 1 2 3\n"));

    [Fact]
    public void AnObjThatIsNotAModelIsRefusedAsAFormatError()
    {
        Assert.Throws<FormatException>(() => FromObj("v 0 0 0\n"));
        Assert.Throws<FormatException>(() => FromObj(""));
    }

    [Fact]
    public async Task SkinsAndCyclicHierarchiesFailBeforeAColliderIsMade()
    {
        await Assert.ThrowsAsync<FormatException>(() => ReadAsync(Model(skin: true)));
        await Assert.ThrowsAsync<FormatException>(() => ReadAsync(Model("""[{"mesh": 0, "children": [0]}]""")));
    }

    private static CollisionMesh FromObj(string text) => CollisionMesh.FromObj(Encoding.UTF8.GetBytes(text));
}
