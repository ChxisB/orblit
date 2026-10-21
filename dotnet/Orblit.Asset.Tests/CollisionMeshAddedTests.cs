using System.Text;
using Orblit.Mesh;
using static Orblit.Asset.Tests.CollisionModels;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

public class CollisionMeshAddedTests
{
    private static readonly double[] Triangle = [0, 0, 0, 1, 0, 0, 0, 1, 0];

    [Fact]
    public void AShapeIsAtLeastOneTriangleOfFinitePointsWithIndicesInRange()
    {
        Assert.Equal([0, 1, 2], new CollisionMesh(Triangle, [0, 1, 2]).Indices);
        var refused = new (double[] Vertices, int[] Indices)[]
        {
            (Triangle[..6], [0, 1, 2]),
            ([.. Triangle, 1], [0, 1, 2]),
            ([.. Triangle[..8], double.NaN], [0, 1, 2]),
            ([.. Triangle[..8], double.PositiveInfinity], [0, 1, 2]),
            (Triangle, []),
            (Triangle, [0, 1, 2, 0]),
            (Triangle, [0, 1, -1]),
            (Triangle, [0, 1, 3]),
        };
        foreach (var (vertices, indices) in refused)
        {
            var error = Assert.Throws<ArgumentException>(() => new CollisionMesh(vertices, indices));
            Assert.Equal("Expected finite positions and indexed triangles.", error.Message);
        }
    }

    [Fact]
    public void TheMeshKeepsACopyOfWhatItWasGiven()
    {
        var vertices = Triangle.ToArray();
        var indices = new[] { 0, 1, 2 };
        var mesh = new CollisionMesh(vertices, indices);
        vertices[0] = 9;
        indices[0] = 2;
        Assert.Equal(Triangle, mesh.Vertices);
        Assert.Equal([0, 1, 2], mesh.Indices);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)mesh.Vertices)[0] = 9);
    }

    [Fact]
    public void DecodeRefusesBytesThatAreNotUtf8()
    {
        Assert.Throws<FormatException>(() => CollisionMesh.Decode([0xFF, 0xFE, 0x7B]));
    }

    [Fact]
    public void DecodeRefusesAPointOrAnIndexThatIsNotANumber()
    {
        foreach (var body in new[] { """[0,0,0,1,0,0,0,1,0,"a"],"indices":[0,1,2]""", """[0,0,0,1,0,0,0,1,0],"indices":[0,1,2.5]""", """[0,0,0,1,0,0,0,1,0],"indices":[0,1,"a"]""" })
        {
            var error = Assert.Throws<FormatException>(
                () => CollisionMesh.Decode(Encoding.UTF8.GetBytes($$"""{"version":1,"vertices":{{body}}}""")));
            Assert.Equal("Invalid collision mesh geometry.", error.Message);
        }
    }

    [Fact]
    public void AnIndexPastAnIntIsAShapeErrorNotAnOverflow()
    {
        foreach (var index in new[] { "9223372036854775807", "-9223372036854775808", "2147483648" })
        {
            var text = $$"""{"version":1,"vertices":[0,0,0,1,0,0,0,1,0],"indices":[0,1,{{index}}]}""";
            Assert.Throws<ArgumentException>(() => CollisionMesh.Decode(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void AnObjPassesItsLimitsOn()
    {
        var bytes = "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"u8.ToArray();
        Assert.Equal([0, 1, 2], CollisionMesh.FromObj(bytes).Indices);
        Assert.Throws<FormatException>(() => CollisionMesh.FromObj(bytes, new ReadLimits { Vertices = 2 }));
    }

    [Fact]
    public async Task EachRefusalSaysWhatWasWrong()
    {
        var cases = new (GltfMap Model, string Message)[]
        {
            (Model("""[{"mesh": 0, "skin": 0}]"""), "Skinned collision geometry is unsupported."),
            (Model("""[{"mesh": 0, "children": [0]}]"""), "Invalid or cyclic model hierarchy."),
            (Model("""[{"mesh": 0, "children": [4]}]"""), "Invalid or cyclic model hierarchy."),
            (Model("""[{"mesh": 0, "children": [-1]}]"""), "Invalid or cyclic model hierarchy."),
            (Model("""[{"mesh": 5}]"""), "Invalid mesh."),
            (Model("""[{"mesh": -1}]"""), "Invalid mesh."),
            (Model("""[{"mesh": 0.0}]"""), "Expected an integer model index."),
            (Model("""[{"mesh": 0, "children": ["a"]}]"""), "Expected node indices."),
            (Model("""[{"mesh": 0, "translation": [1, 2]}]"""), "Invalid model transform."),
            (Model("""[{"mesh": 0, "translation": [1, 2, 3, "a"]}]"""), "Invalid model transform."),
            (Model("""[{"mesh": 0, "matrix": [1, 2]}]"""), "Invalid model transform."),
            (Model("""[{"mesh": 0, "rotation": [0, 0, 0, "a"]}]"""), "Invalid model transform."),
            (Model("""[{"mesh": 0, "rotation": [0, 0, 0, 0]}]"""), "Zero model rotation."),
            (Model(indices: [0, 1]), "Incomplete triangles."),
            (Model(indices: [0, 1, 4]), "Collision index is outside its primitive."),
            (Model(indices: [0, 1, 9]), "Collision index is outside its primitive."),
            (Edited(m => m["scene"] = -1), "Invalid scene."),
            (Edited(m => m["scene"] = 1), "Invalid scene."),
            (Edited(m => m["scene"] = 7), "Invalid scene."),
            (Edited(m => m["meshes"] = Map("{}")), "Invalid model meshes."),
            (Edited(m => m["nodes"] = new List<object?> { 1 }), "Invalid model nodes."),
            (Edited(m => Meshes(m)[0] = Map("{}")), "Missing mesh primitives."),
            (Edited(m => Primitives(m)[0] = 1), "Invalid primitive."),
            (Edited(m => Primitive(m)["targets"] = new List<object?>()), "Morph collision geometry is unsupported."),
            (Edited(m => Primitive(m)["attributes"] = Map("{}")), "Missing collision positions."),
            (Edited(m => Primitive(m)["extensions"] = Map("""{"KHR_draco_mesh_compression": {}}""")),
                "Compressed collision geometry is unsupported."),
            (Edited(m => Views(m)[0]["extensions"] = Map("""{"EXT_meshopt_compression": {}}""")),
                "Compressed collision geometry is unsupported."),
            (Edited(m => Views(m)[0]["extensions"] = Map("""{"KHR_meshopt_compression": {}}""")),
                "Compressed collision geometry is unsupported."),
        };
        foreach (var (model, message) in cases)
        {
            var error = await Assert.ThrowsAsync<FormatException>(() => ReadAsync(model));
            Assert.Equal(message, error.Message);
        }
    }

    // The encoder cannot write an infinity, so one is swapped into the text after it.
    [Fact]
    public async Task ATransformWithAnInfinityIsRefused()
    {
        var text = Encoding.UTF8.GetString(Write(Model("""[{"mesh": 0, "translation": ["inf", 0, 0]}]"""))).Replace("\"inf\"", "1e999", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<FormatException>(() => ReadAsync(Encoding.UTF8.GetBytes(text)));
        Assert.Equal("Invalid model transform.", error.Message);
    }

    // An unsigned index past 31 bits wraps below nought in an int, and is still outside the primitive.
    [Fact]
    public async Task AFourByteIndexPastTheEndIsRefusedLikeAnyOther()
    {
        var model = Model();
        var binary = Bytes([.. Positions.Select(p => (float)p)]).Concat(BitConverter.GetBytes(0xFFFFFFFF)).Concat(new byte[8]).ToArray();
        var buffer = (GltfMap)((List<object?>)model["buffers"]!)[0]!;
        buffer["byteLength"] = binary.Length;
        buffer["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(binary);
        Views(model)[1]["byteLength"] = 12;
        var accessor = (GltfMap)((List<object?>)model["accessors"]!)[1]!;
        accessor["componentType"] = 5125;
        accessor["count"] = 3;
        var error = await Assert.ThrowsAsync<FormatException>(() => ReadAsync(model));
        Assert.Equal("Collision index is outside its primitive.", error.Message);
    }

    // One node more than the guard allows is a model nobody wrote, and a stack that runs out would end the process.
    [Fact]
    public async Task ANodeChainIsReadToTheDepthGuardAndNoFurther()
    {
        var deepest = await ReadAsync(Model(Chain(512)));
        Assert.Equal(6, deepest.Indices.Count);
        var error = await Assert.ThrowsAsync<FormatException>(() => ReadAsync(Model(Chain(513))));
        Assert.Equal("Invalid or cyclic model hierarchy.", error.Message);
    }

    [Fact]
    public async Task ASharedChildIsReadOncePerParentAndNotTakenForACycle()
    {
        var mesh = await ReadAsync(Model("""
            [
              {"children": [1, 2]},
              {"children": [3]},
              {"children": [3]},
              {"mesh": 0}
            ]
            """));
        Assert.Equal(12, mesh.Indices.Count);
    }

    [Fact]
    public async Task AModelWithoutScenesReadsEveryNodeNoOtherNodeHolds()
    {
        var model = Model("""[{"children": [1]}, {"mesh": 0, "translation": [0, 5, 0]}, {"mesh": 0}]""");
        model.Remove("scenes");
        model.Remove("scene");
        var mesh = await ReadAsync(model);
        Assert.Equal(24, mesh.Vertices.Count);
        Assert.Equal([5, 0], new[] { mesh.Vertices[1], mesh.Vertices[13] });
    }

    [Fact]
    public async Task AModelWithoutScenesOrNodesReadsEveryMeshAsItIs()
    {
        var model = Model();
        model.Remove("scenes");
        model.Remove("scene");
        model.Remove("nodes");
        var mesh = await ReadAsync(model);
        Assert.Equal(Positions, mesh.Vertices);
    }

    // Points and lines carry no surface, so a model of only those makes no shape.
    [Fact]
    public async Task APrimitiveOfPointsIsSkipped()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ReadAsync(Model(mode: 0)));
        Assert.Equal("Expected finite positions and indexed triangles.", error.Message);
    }

    private static string Chain(int length) =>
        "[" + string.Join(",", Enumerable.Range(0, length).Select(i => i == length - 1 ? """{"mesh":0}""" : $$"""{"children":[{{i + 1}}]}""")) + "]";

    private static GltfMap Edited(Action<GltfMap> edit)
    {
        var model = Model();
        edit(model);
        return model;
    }

    private static List<object?> Meshes(GltfMap model) => (List<object?>)model["meshes"]!;

    private static List<object?> Primitives(GltfMap model) => (List<object?>)((GltfMap)Meshes(model)[0]!)["primitives"]!;

    private static GltfMap Primitive(GltfMap model) => (GltfMap)Primitives(model)[0]!;

    private static List<GltfMap> Views(GltfMap model) => [.. ((List<object?>)model["bufferViews"]!).Cast<GltfMap>()];
}
