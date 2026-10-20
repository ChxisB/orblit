namespace Orblit.Mesh.Tests;

public class MaterialsTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    [Fact]
    public void AMeshNobodyPaintedIsOneRunOfEverything()
    {
        var tris = Cube().Triangulate();
        Assert.Single(tris.Runs);
        Assert.Equal(0, tris.Runs.Single().Material);
        Assert.Equal(tris.Indices.Length, tris.Runs.Single().Count);
    }

    [Fact]
    public void FacesAreSortedSoEachMaterialIsOneRun()
    {
        var mesh = Cube();

        // Deliberately out of order, which is how somebody painting faces one at a time would leave them.
        mesh.Faces[0].Material = 2;
        mesh.Faces[1].Material = 0;
        mesh.Faces[2].Material = 2;
        mesh.Faces[3].Material = 1;

        var tris = mesh.Triangulate();
        var materials = tris.Runs.Select(run => run.Material).ToList();

        // in order, each appearing once
        Assert.Equal(new[] { 0, 1, 2 }, materials);

        // Contiguous and covering everything: a gap would be triangles nobody draws, and an overlap would be
        // triangles drawn twice.
        var at = 0;
        foreach (var run in tris.Runs)
        {
            Assert.Equal(at, run.Start);
            at += run.Count;
        }

        Assert.Equal(tris.Indices.Length, at);
    }

    [Fact]
    public void TheSameTrianglesComeOutHoweverTheyAreGrouped()
    {
        var plain = Cube().Triangulate();
        var painted = Cube();
        for (var i = 0; i < painted.Faces.Count; i++)
        {
            painted.Faces[i].Material = i % 3;
        }

        Assert.Equal(plain.Indices.Length, painted.Triangulate().Indices.Length);
        Assert.Equal(plain.VertexCount, painted.Triangulate().VertexCount);
    }

    [Fact]
    public void APaintedMeshWritesOnePrimitiveAMaterial()
    {
        var mesh = Cube();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            mesh.Faces[i].Material = i < 2 ? 0 : 1;
        }

        var json = GlbJson.Of(mesh.ToGlb(materials:
        [
            new GlbMaterial { Name = "Brick", Colour = [0.6, 0.2, 0.15, 1] },
            new GlbMaterial { Name = "Glass", Metallic = 1, Roughness = 0.05 },
        ]));

        var primitives = GlbJson.PrimitivesOf(json);
        Assert.Equal(2, primitives.Count);
        Assert.Equal(0, GlbJson.Number(GlbJson.Map(primitives[0])["material"]));
        Assert.Equal(1, GlbJson.Number(GlbJson.Map(primitives[1])["material"]));

        var materials = GlbJson.List(json["materials"]);
        Assert.Equal("Brick", GlbJson.Map(materials[0])["name"]);
        Assert.Equal(1, GlbJson.Number(GlbJson.Map(GlbJson.Map(materials[1])["pbrMetallicRoughness"])["metallicFactor"]));
    }

    [Fact]
    public void EachPrimitiveReadsItsOwnSliceOfTheIndices()
    {
        var mesh = Cube();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            mesh.Faces[i].Material = i < 2 ? 0 : 1;
        }

        var json = GlbJson.Of(mesh.ToGlb(materials: [new GlbMaterial(), new GlbMaterial()]));

        var accessors = GlbJson.List(json["accessors"]);
        var first = GlbJson.Map(accessors[0]);
        var second = GlbJson.Map(accessors[1]);

        Assert.Equal(0, GlbJson.Number(first["bufferView"]));

        // the same indices, offset into
        Assert.Equal(0, GlbJson.Number(second["bufferView"]));
        Assert.Equal(0, first.TryGetValue("byteOffset", out var offset) ? GlbJson.Number(offset) : 0);

        // Two quads is four triangles is twelve indices, four bytes each.
        //
        // Four rather than two since indices went to thirty-two bits: sixteen wraps at sixty-five thousand
        // vertices, which a generated mesh passes without saying anything and then draws nothing at all.
        Assert.Equal(48, GlbJson.Number(second["byteOffset"]));
        Assert.Equal(12, GlbJson.Number(first["count"]));
    }

    [Fact]
    public void AFacePaintedWithAMaterialNobodyListedStillGetsDrawn()
    {
        var mesh = Cube();
        mesh.Faces[0].Material = 9;

        var json = GlbJson.Of(mesh.ToGlb(materials: [new GlbMaterial()]));
        var primitives = GlbJson.PrimitivesOf(json);

        // Two primitives, and the one nobody named has no material: the file's own default, which is a
        // surface somebody can see and fix.
        Assert.Equal(2, primitives.Count);
        Assert.Single(primitives, one => GlbJson.Map(one).ContainsKey("material"));
    }

    [Fact]
    public void AMeshWithNoMaterialsWritesNoneAtAll()
    {
        var json = GlbJson.Of(Cube().ToGlb());
        Assert.False(json.ContainsKey("materials"));
        var primitives = GlbJson.PrimitivesOf(json);
        Assert.Single(primitives);
        Assert.False(GlbJson.Map(primitives.Single()).ContainsKey("material"));
    }

    [Fact]
    public void TheAttributeAccessorsComeAfterTheIndexOnes()
    {
        var mesh = Cube();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            mesh.Faces[i].Material = i;
        }

        var json = GlbJson.Of(mesh.ToGlb(materials: [.. Enumerable.Range(0, 6).Select(_ => new GlbMaterial())]));

        var primitives = GlbJson.PrimitivesOf(json);
        Assert.Equal(6, primitives.Count);

        var attributes = GlbJson.Map(GlbJson.Map(primitives[0])["attributes"]);

        // six index accessors, then it
        Assert.Equal(6, GlbJson.Number(attributes["POSITION"]));

        var accessors = GlbJson.List(json["accessors"]);
        Assert.Equal("VEC3", GlbJson.Map(accessors[6])["type"]);
        Assert.IsAssignableFrom<IReadOnlyList<object?>>(GlbJson.Map(accessors[6])["min"]);
    }

    [Fact]
    public void AnEmissiveMaterialSaysSoAndAPlainOneDoesNot()
    {
        Assert.False(new GlbMaterial().ToGltf().ContainsKey("emissiveFactor"));
        Assert.Equal(
            new object?[] { 1.0, 0.5, 0.0 },
            (List<object?>)new GlbMaterial { Emissive = [1, 0.5, 0] }.ToGltf()["emissiveFactor"]!);
        Assert.Equal("MASK", new GlbMaterial { Cutout = true }.ToGltf()["alphaMode"]);
    }

    [Fact]
    public void AMeshPastSixtyFiveThousandVerticesStillDraws()
    {
        // What this is really about. Sixteen-bit indices wrap at 65,536, and the failure is silent in every
        // direction: the file writes, a loader reads it without complaint, and the mesh renders nothing,
        // because every triangle past that point names the wrong corners.
        //
        // Built as loose quads rather than a shape, because sharing points is exactly what a generated mesh
        // does not do.
        var mesh = new Mesh();
        const int quads = 20000; // 80,000 vertices
        for (var i = 0; i < quads; i++)
        {
            var at = mesh.Positions.Count;
            double x = i;
            mesh.Positions.AddRange(
            [
                new Vector3(x, 0, 0),
                new Vector3(x + 1, 0, 0),
                new Vector3(x + 1, 1, 0),
                new Vector3(x, 1, 0),
            ]);
            mesh.Faces.Add(new Face([at, at + 1, at + 2, at + 3]));
        }

        var tris = mesh.Triangulate();
        Assert.True(tris.VertexCount > 65536);

        // The last triangle must point at the last corners, not at wrapped ones.
        var highest = tris.Indices.Max();
        Assert.True(
            highest == (uint)(tris.VertexCount - 1),
            "an index that wrapped is an index pointing at the wrong vertex");

        var glb = mesh.ToGlb();
        Assert.True(glb.Length > 0);
        var box = MeshExport.BoundsOfGlb(glb)!.Value;

        // the far end of the mesh should be where it was put
        Assert.Equal((double)quads, box.Max.X, 1.0);
    }
}
