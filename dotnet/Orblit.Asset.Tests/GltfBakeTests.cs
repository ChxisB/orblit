using Orblit.VectorMath;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// Ported from gltf_bake_test.dart.
public sealed class GltfBakeTests
{
    private static readonly Matrix4[] One = [Matrix4.Identity()];

    private static readonly Dictionary<string, Action<TriangleModel>> Unplaceable = new()
    {
        ["a skin"] = m => m.Json["skins"] = Map("""{"v": [{"joints": [0]}]}""")["v"],
        ["an animation"] = m => m.Json["animations"] = Map("""{"v": [{"channels": [], "samplers": []}]}""")["v"],
        ["a morph target"] = m => ((GltfMap)m.Primitives[0]!)["targets"] = Map("""{"v": [{}]}""")["v"],
        ["a compressed buffer"] = m => m.Json["extensionsRequired"] = Map("""{"v": ["EXT_meshopt_compression"]}""")["v"],
        ["a node that is instanced already"] = m => ((List<object?>)m.Json["nodes"]!)[0] =
            Map("""{"mesh": 0, "extensions": {"KHR_mesh_gpu_instancing": {}}}"""),
        ["a position stored in shorts"] = m =>
        {
            var at = Convert.ToInt32(((GltfMap)((GltfMap)m.Primitives[0]!)["attributes"]!)["POSITION"]);
            ((GltfMap)((List<object?>)m.Json["accessors"]!)[at]!)["componentType"] = 5122;
        },
    };

    private static Task<GltfDocument> Open(byte[] bytes) =>
        GltfDocument.ReadAsync(AssetId.Parse("baked.glb"), bytes, new MemoryAssetSource());

    private static async Task<GltfDocument> Baked(TriangleModel model, Matrix4[] placements, int maxVertices = 65536)
    {
        var bytes = await GltfBake.BakeInstancesAsync(model.ToGlb(), placements, maxVertices);
        return await Open(Assert.IsType<byte[]>(bytes));
    }

    private static List<GltfMap> PrimitivesOf(GltfDocument document) =>
        [.. ((List<object?>)document.List("meshes")[0]["primitives"]!).Cast<GltfMap>()];

    private static int Attribute(GltfMap primitive, string name) =>
        Convert.ToInt32(((GltfMap)primitive["attributes"]!)[name]);

    private static void Near(double[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(expected[i] - actual[i]) < 1e-5, $"[{i}] {actual[i]} is not within 1e-5 of {expected[i]}");
        }
    }

    [Fact]
    public async Task PutsTheModelWhereEachMatrixSays()
    {
        var model = new TriangleModel();
        model.Triangle();
        var turned = Matrix4.TranslationValues(0, 5, 0);
        turned.RotateZ(Math.PI / 2);
        var document = await Baked(model, [Matrix4.TranslationValues(10, 0, 0), turned]);

        var primitive = Assert.Single(PrimitivesOf(document));
        Near(
            [10, 0, 0, 11, 0, 0, 10, 1, 0, 0, 5, 0, 0, 6, 0, -1, 5, 0],
            document.ReadVec3(Attribute(primitive, "POSITION")));
        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5 }, document.ReadIndices(Convert.ToInt32(primitive["indices"])));
    }

    [Fact]
    public async Task TurnsNormalsAndTangentsWithThePlacement()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(model, [Matrix4.RotationX(Math.PI / 2)]);

        var primitive = Assert.Single(PrimitivesOf(document));
        Near([0, -1, 0], document.ReadVec3(Attribute(primitive, "NORMAL"))[..3]);
        Near([1, 0, 0, 1], Floats(document.AccessorBytes(Attribute(primitive, "TANGENT")))[..4]);
    }

    [Fact]
    public async Task KeepsANormalSquareToTheSurfaceUnderAStretch()
    {
        // A tilted normal on a surface stretched along x leans away from x. The plain matrix would lean it
        // towards x and light it wrongly.
        var lean = Math.Sqrt(0.5);
        var model = new TriangleModel();
        model.Triangle(normal: [lean, 0, lean]);
        var document = await Baked(model, [Matrix4.Diagonal3Values(4, 1, 1)]);

        var normal = document.ReadVec3(Attribute(Assert.Single(PrimitivesOf(document)), "NORMAL"));
        var length = Math.Sqrt((0.25 * 0.25) + 1);
        Near([0.25 / length, 0, 1 / length], normal[..3]);
    }

    [Fact]
    public async Task AMirroredPlacementKeepsTheFrontFaceFront()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(model, [Matrix4.Diagonal3Values(-1, 1, 1)]);

        var primitive = Assert.Single(PrimitivesOf(document));
        Assert.Equal(new uint[] { 0, 2, 1 }, document.ReadIndices(Convert.ToInt32(primitive["indices"])));
    }

    [Fact]
    public async Task LeavesTheTangentSignAloneAsTheRendererDoes()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(model, [Matrix4.Diagonal3Values(-1, 1, 1)]);

        var tangent = Floats(document.AccessorBytes(Attribute(Assert.Single(PrimitivesOf(document)), "TANGENT")));
        Assert.Equal(1, tangent[3]);
    }

    [Fact]
    public async Task KeepsTheModelNodesInTheBakedPositions()
    {
        var model = new TriangleModel();
        model.Triangle();
        var parts = Map("""
            {"nodes": [{"scale": [2, 2, 2], "children": [1]},
                       {"mesh": 0, "translation": [1, 0, 0]}],
             "scenes": [{"nodes": [0]}]}
            """);
        model.Json["nodes"] = parts["nodes"];
        model.Json["scenes"] = parts["scenes"];
        var document = await Baked(model, [Matrix4.Identity()]);

        var positions = document.ReadVec3(Attribute(Assert.Single(PrimitivesOf(document)), "POSITION"));
        // The parent scales what its child translated.
        Near([2, 0, 0, 4, 0, 0], positions[..6]);
    }

    [Fact]
    public async Task DrawsAsManyPrimitivesAsTheModelHasMaterials()
    {
        var model = new TriangleModel();
        var a = model.Material(Map("""{"name": "a"}"""));
        var b = model.Material(Map("""{"name": "b"}"""));
        for (var i = 0; i < 3; i++)
        {
            model.Triangle(a);
            model.Triangle(b);
        }

        var document = await Baked(model, [.. Enumerable.Range(0, 50).Select(i => Matrix4.TranslationValues(i * 2, 0, 0))]);

        var primitives = PrimitivesOf(document);
        Assert.Equal([a, b], primitives.Select(p => Convert.ToInt32(p["material"])));
        Assert.Equal(450, document.AccessorCount(Attribute(primitives[0], "POSITION")));
    }

    [Fact]
    public async Task StartsAnotherPrimitiveBeforeTheIndexWidthChanges()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(
            model,
            [.. Enumerable.Range(0, 5).Select(i => Matrix4.TranslationValues(i * 2, 0, 0))],
            maxVertices: 6);

        var primitives = PrimitivesOf(document);
        Assert.Equal([6, 6, 3], primitives.Select(p => document.AccessorCount(Attribute(p, "POSITION"))));
        Assert.Equal(new uint[] { 0, 1, 2 }, document.ReadIndices(Convert.ToInt32(primitives[^1]["indices"])));
    }

    [Fact]
    public async Task WritesOneNodeHoldingOneMesh()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(model, [Matrix4.Identity()]);

        Assert.Single(document.List("nodes"));
        Assert.Single(document.List("meshes"));
        Assert.Equal(0, GltfJson.AsNumber(document.Json["scene"]));
    }

    [Fact]
    public async Task BoundsThePositionsSoCullingBelievesThem()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(model, [Matrix4.TranslationValues(100, 0, 0)]);

        var accessor = document.Accessor(Attribute(Assert.Single(PrimitivesOf(document)), "POSITION"));
        Assert.Equal([100.0, 0.0, 0.0], Numbers(accessor["min"]));
        Assert.Equal([101.0, 1.0, 0.0], Numbers(accessor["max"]));
    }

    [Fact]
    public async Task SkipsAPlacementWithNoVolume()
    {
        var model = new TriangleModel();
        model.Triangle();
        var document = await Baked(model, [Matrix4.Diagonal3Values(0, 1, 1), Matrix4.Identity()]);

        Assert.Equal(3, document.AccessorCount(Attribute(Assert.Single(PrimitivesOf(document)), "POSITION")));
    }

    [Fact]
    public async Task LeavesNothingToPlaceAlone()
    {
        var model = new TriangleModel();
        model.Triangle();
        Assert.Null(await GltfBake.BakeInstancesAsync(model.ToGlb(), []));
        Assert.Null(await GltfBake.BakeInstancesAsync(model.ToGlb(), [Matrix4.Diagonal3Values(0, 1, 1)]));
    }

    [Theory]
    [InlineData("a skin")]
    [InlineData("an animation")]
    [InlineData("a morph target")]
    [InlineData("a compressed buffer")]
    [InlineData("a node that is instanced already")]
    [InlineData("a position stored in shorts")]
    public async Task LeavesAloneWhatItCannotPlace(string name)
    {
        var model = new TriangleModel();
        model.Triangle();
        Unplaceable[name](model);
        Assert.Null(await GltfBake.BakeInstancesAsync(model.ToGlb(), One));
    }

    [Fact]
    public async Task LeavesBytesThatAreNotAModelAlone() =>
        Assert.Null(await GltfBake.BakeInstancesAsync([1, 2, 3], One));

    [Fact]
    public async Task LeavesAPlacementThatIsNotANumberAlone()
    {
        var model = new TriangleModel();
        model.Triangle();
        // Column 3 holds the translation, so entries 12 and 13 are (0,3) and (1,3).
        var nan = Matrix4.Identity();
        nan[12] = double.NaN;
        var far = Matrix4.Identity();
        far[13] = double.PositiveInfinity;
        Assert.Null(await GltfBake.BakeInstancesAsync(model.ToGlb(), [nan, far]));

        var document = await Baked(model, [nan, Matrix4.Identity(), far]);
        Assert.Equal(3, document.AccessorCount(Attribute(Assert.Single(PrimitivesOf(document)), "POSITION")));
    }

    [Fact]
    public async Task LeavesABufferItCannotReadAlone()
    {
        var model = new TriangleModel();
        model.Triangle();
        model.Json["buffers"] = Map("""{"v": [{"uri": "beside.bin", "byteLength": 10}]}""")["v"];
        Assert.Null(await GltfBake.BakeInstancesAsync(Glb(model.Json, []), One));
    }

    [Fact]
    public async Task RefusesAnIndexThatDoesNotFitThirtyOneBits()
    {
        // An index too large for a 32-bit buffer is refused.
        var model = new TriangleModel();
        model.Triangle();
        model.IndexWith(0x8000_0000u, 1, 2);

        await Assert.ThrowsAsync<OverflowException>(() => Baked(model, One));
    }

    private static double[] Numbers(object? list) => [.. ((List<object?>)list!).Select(v => GltfJson.AsNumber(v)!.Value)];
}
