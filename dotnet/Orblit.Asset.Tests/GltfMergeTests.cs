using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// Ported from gltf_merge_test.dart.
public sealed class GltfMergeTests
{
    private static Task<GltfDocument> Open(QuadModel model) =>
        GltfDocument.ReadAsync(AssetId.Parse("models/thing.glb"), model.ToGlb(), new MemoryAssetSource());

    private static List<GltfMap> PrimitivesOf(GltfDocument document) =>
        [.. ((List<object?>)document.List("meshes")[0]["primitives"]!).Cast<GltfMap>()];

    private static int?[] MaterialsOf(GltfDocument document) =>
        [.. PrimitivesOf(document).Select(p => p["material"] is { } m ? Convert.ToInt32(m) : (int?)null)];

    private static int Position(GltfMap primitive) => Convert.ToInt32(((GltfMap)primitive["attributes"]!)["POSITION"]);

    [Fact]
    public async Task FoldsMaterialsThatSayTheSameThingIntoOne()
    {
        var model = new QuadModel();
        var a = model.Material(Map("""{"doubleSided": true, "name": "left"}"""));
        var b = model.Material(Map("""{"doubleSided": true, "name": "right"}"""));
        var c = model.Material(Map("""{"doubleSided": false}"""));
        model.Quad(a);
        model.Quad(b, x: 2);
        model.Quad(c, x: 4);

        var document = await Open(model);
        Assert.Equal(1, GltfMerge.MergeMaterials(document));
        // The name is not what a material draws like, so two materials that differ only by it are one.
        Assert.Equal([a, a, c], MaterialsOf(document));
    }

    [Fact]
    public async Task KeepsTwoMaterialsThatDifferAnywhereApart()
    {
        var model = new QuadModel();
        var a = model.Material(Map("""{"pbrMetallicRoughness": {"baseColorFactor": [1.0, 0.0, 0.0, 1.0]}}"""));
        var b = model.Material(Map("""{"pbrMetallicRoughness": {"baseColorFactor": [1.0, 0.0, 0.0, 0.9]}}"""));
        model.Quad(a);
        model.Quad(b, x: 2);

        var document = await Open(model);
        Assert.Equal(0, GltfMerge.MergeMaterials(document));
        Assert.Equal([a, b], MaterialsOf(document));
    }

    [Fact]
    public async Task DoesNotRenumberSoNothingThatPointsAtAMaterialBreaks()
    {
        var model = new QuadModel();
        var a = model.Material(Map("""{"doubleSided": true}"""));
        var b = model.Material(Map("""{"doubleSided": true}"""));
        model.Quad(a);
        model.Quad(b, x: 2);

        var document = await Open(model);
        GltfMerge.MergeMaterials(document);
        // An unused material costs nothing. A shifted index costs a variant, an extension or an extras key
        // pointing at the wrong one.
        Assert.Equal(2, document.List("materials").Count);
    }

    [Fact]
    public async Task FollowsAVariantMappingToTheMaterialItNowNames()
    {
        var model = new QuadModel();
        var a = model.Material(Map("""{"doubleSided": true}"""));
        var b = model.Material(Map("""{"doubleSided": true}"""));
        model.Quad(a);
        ((GltfMap)model.Primitives[0]!)["extensions"] = Map($$$"""
            {"KHR_materials_variants": {"mappings": [{"material": {{{b}}}, "variants": [0]}]}}
            """);

        var document = await Open(model);
        GltfMerge.MergeMaterials(document);
        var extensions = (GltfMap)PrimitivesOf(document)[0]["extensions"]!;
        var mappings = (List<object?>)((GltfMap)extensions["KHR_materials_variants"]!)["mappings"]!;
        Assert.Equal(a, Convert.ToInt32(((GltfMap)mappings.Single()!)["material"]));
    }

    [Fact]
    public async Task JoinsTwoDrawsOfOneMaterialIntoOne()
    {
        var model = new QuadModel();
        var one = model.Material(Map("""{"doubleSided": true}"""));
        model.Quad(one);
        model.Quad(one, x: 2);

        var document = await Open(model);
        Assert.Equal(1, GltfMerge.MergePrimitives(document));

        var primitive = Assert.Single(PrimitivesOf(document));
        Assert.Equal(
            new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 2, 0, 0, 3, 0, 0, 3, 1, 0, 2, 1, 0 },
            document.ReadVec3(Position(primitive)));
        // The second quad's indices must have moved up by its four vertices, or it draws the first quad twice.
        Assert.Equal(
            new uint[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 },
            document.ReadIndices(Convert.ToInt32(primitive["indices"])));

        var position = document.Accessor(Position(primitive));
        Assert.Equal([0.0, 0, 0], Numbers(position["min"]));
        // A stale bound is a mesh that culls when it is on screen.
        Assert.Equal([3.0, 1, 0], Numbers(position["max"]));
    }

    [Fact]
    public async Task KeepsTheOrderAModelDrawsIn()
    {
        var model = new QuadModel();
        var a = model.Material(Map("""{"name": "a"}"""));
        var b = model.Material(Map("""{"name": "b"}"""));
        model.Quad(a);
        model.Quad(b, x: 2);
        model.Quad(a, x: 4);

        var document = await Open(model);
        Assert.Equal(1, GltfMerge.MergePrimitives(document));
        // The two `a` draws join where the first of them was, so `b` still draws after them. A glTF
        // expresses no other draw order.
        Assert.Equal([a, b], MaterialsOf(document));
    }

    [Fact]
    public async Task SplitsABatchBeforeItsIndicesNeedAThirdByte()
    {
        var model = new QuadModel();
        var one = model.Material(Map("""{"doubleSided": true}"""));
        model.Quad(one);
        model.Quad(one, x: 2);
        model.Quad(one, x: 4);

        var document = await Open(model);
        Assert.Equal(1, GltfMerge.MergePrimitives(document, maxVertices: 8));
        var primitives = PrimitivesOf(document);
        Assert.Equal(2, primitives.Count);
        Assert.Equal(8, document.AccessorCount(Position(primitives[0])));
        Assert.Equal(4, document.AccessorCount(Position(primitives[^1])));
    }

    [Fact]
    public async Task WillNotJoinPrimitivesWhoseAttributesDiffer()
    {
        var model = new QuadModel();
        var one = model.Material(Map("""{"doubleSided": true}"""));
        model.Quad(one);
        model.Quad(one, x: 2);
        // A second UV set on one of them: joining would leave half the vertices with no value for it.
        var attributes = (GltfMap)((GltfMap)model.Primitives[^1]!)["attributes"]!;
        attributes["TEXCOORD_1"] = attributes["TEXCOORD_0"];

        var document = await Open(model);
        Assert.Equal(0, GltfMerge.MergePrimitives(document));
        Assert.Equal(2, PrimitivesOf(document).Count);
    }

    [Fact]
    public async Task LeavesSkinnedMorphedAndVariantDrawsAlone()
    {
        foreach (var spoiler in new[]
        {
            """{"targets": [{"POSITION": 0}]}""",
            """{"extensions": {"KHR_materials_variants": {"mappings": []}}}""",
            """{"extensions": {"KHR_draco_mesh_compression": {"bufferView": 0}}}""",
            """{"mode": 5}""",
        })
        {
            var model = new QuadModel();
            var one = model.Material(Map("""{"doubleSided": true}"""));
            model.Quad(one);
            model.Quad(one, x: 2);
            foreach (var (key, value) in Map(spoiler))
            {
                ((GltfMap)model.Primitives[^1]!)[key] = value;
            }

            var document = await Open(model);
            Assert.True(GltfMerge.MergePrimitives(document) == 0, $"must not join a draw carrying {spoiler}");
            Assert.Equal(2, PrimitivesOf(document).Count);
        }
    }

    [Fact]
    public async Task LeavesASkinnedDrawAloneEvenWhenBothSidesAreSkinned()
    {
        var model = new QuadModel();
        var one = model.Material(Map("""{"doubleSided": true}"""));
        model.Quad(one);
        model.Quad(one, x: 2);
        foreach (var primitive in model.Primitives)
        {
            var attributes = (GltfMap)((GltfMap)primitive!)["attributes"]!;
            attributes["JOINTS_0"] = attributes["POSITION"];
            attributes["WEIGHTS_0"] = attributes["POSITION"];
        }

        var document = await Open(model);
        // Joining would renumber vertices out from under the skin's joint indices, which is a mesh that
        // animates into a knot.
        Assert.Equal(0, GltfMerge.MergePrimitives(document));
    }

    private static double[] Numbers(object? list) => [.. ((List<object?>)list!).Select(v => GltfJson.AsNumber(v)!.Value)];

    [Fact]
    public async Task RefusesAnIndexThatDoesNotFitThirtyOneBits()
    {
        // An index too large for a 32-bit buffer is refused.
        var model = new QuadModel();
        var a = model.Material(Map("{}"));
        model.Quad(a);
        model.Quad(a, x: 2);
        model.IndexWith(0x8000_0000u, 1, 2);

        var document = await Open(model);

        Assert.Throws<OverflowException>(() => GltfMerge.MergePrimitives(document));
    }
}
