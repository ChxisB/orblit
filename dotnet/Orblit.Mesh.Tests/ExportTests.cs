using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Orblit.Mesh.Tests;

public class ExportTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static Written Only(List<Written> files, string extension) =>
        files.First(one => one.Name.EndsWith(extension, StringComparison.Ordinal));

    private static string Latin1(byte[] bytes, int count) => Encoding.Latin1.GetString(bytes, 0, Math.Min(count, bytes.Length));

    private static List<string> LinesStarting(string text, string prefix) =>
        text.Split('\n').Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    private static int Occurrences(string text, string needle) => text.Split(needle).Length - 1;

    // OBJ

    [Fact]
    public void AQuadStaysAQuad()
    {
        var text = Only(Cube().ToObj(), ".obj").Text;
        var faces = LinesStarting(text, "f ");

        Assert.Equal(6, faces.Count);
        foreach (var face in faces)
        {
            // four corners and the f, not two triangles
            Assert.Equal(5, Regex.Split(face.Trim(), @"\s+").Length);
        }
    }

    [Fact]
    public void EveryCornerHasAPositionACoordinateAndANormal()
    {
        var text = Only(Cube().ToObj(), ".obj").Text;
        foreach (var line in LinesStarting(text, "f "))
        {
            foreach (var corner in Regex.Split(line[2..].Trim(), @"\s+"))
            {
                var parts = corner.Split('/');
                Assert.Equal(3, parts.Length);
                foreach (var part in parts)
                {
                    // one-based, which is what OBJ has always been
                    Assert.True(int.TryParse(part, out var number) && number > 0, part);
                }
            }
        }
    }

    [Fact]
    public void TheCountsAddUp()
    {
        var text = Only(Cube().ToObj(), ".obj").Text;

        // a cube has eight corners
        Assert.Equal(8, LinesStarting(text, "v ").Count);

        // One coordinate and one normal a face corner: six faces of four.
        Assert.Equal(24, LinesStarting(text, "vt ").Count);
        Assert.Equal(24, LinesStarting(text, "vn ").Count);
    }

    [Fact]
    public void MaterialsComeWithALibraryThatNamesThem()
    {
        var mesh = Cube();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            mesh.Faces[i].Material = i < 3 ? 0 : 1;
        }

        var files = mesh.ToObj(
            name: "wall",
            materials:
            [
                new GlbMaterial { Name = "Red brick", Colour = [0.6, 0.2, 0.1, 1] },
                new GlbMaterial { Name = "Glass", Roughness = 0.05, Colour = [0.8, 0.9, 1, 0.4] },
            ]);

        Assert.Equal(2, files.Count);
        Assert.Equal("wall.obj", files[0].Name);
        Assert.Equal("wall.mtl", files[^1].Name);
        Assert.Contains("mtllib wall.mtl", Only(files, ".obj").Text);

        var mtl = Only(files, ".mtl").Text;

        // a space in a name splits the line
        Assert.Contains("newmtl Red_brick", mtl);
        Assert.Contains("newmtl Glass", mtl);

        // the see-through one says so
        Assert.Contains("d 0.4", mtl);
    }

    [Fact]
    public void AMeshWithNoMaterialsWritesNoLibrary()
    {
        Assert.Single(Cube().ToObj());
    }

    [Fact]
    public void FacesAreGroupedSoEachMaterialIsSaidOnce()
    {
        var mesh = Cube();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            mesh.Faces[i].Material = i % 2 == 0 ? 0 : 1;
        }

        var text = Only(mesh.ToObj(materials: [new GlbMaterial(), new GlbMaterial()]), ".obj").Text;

        // not back and forth down the file
        Assert.Equal(2, LinesStarting(text, "usemtl").Count);
    }

    [Fact]
    public void NothingIsWrittenInExponentForm()
    {
        var mesh = new Mesh(
            [new Vector3(0.0000001, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)],
            [new Face([0, 1, 2])]);
        var text = Only(mesh.ToObj(), ".obj").Text;
        Assert.DoesNotContain("e-", text);
        Assert.DoesNotContain("e+", text);
    }

    // STL

    [Fact]
    public void BinarySaysHowManyTrianglesAndThenHasThatMany()
    {
        var bytes = Cube().ToStl();
        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80));

        // six quads cut into two each
        Assert.Equal(12u, count);
        Assert.Equal(84 + ((int)count * 50), bytes.Length);
    }

    [Fact]
    public void ABinaryFileDoesNotStartWithTheWordSolid()
    {
        // Half the readers there are decide which kind it is by looking, and a binary file that
        // starts with "solid" is read as text and fails.
        var bytes = Cube().ToStl();
        Assert.NotEqual("solid", Latin1(bytes, 5));
    }

    [Fact]
    public void TheTextOneIsASolidWithFacetsInIt()
    {
        var text = Latin1(Cube().ToStl(binary: false), int.MaxValue);
        Assert.StartsWith("solid", text, StringComparison.Ordinal);
        Assert.EndsWith("endsolid mesh", text.Trim(), StringComparison.Ordinal);
        Assert.Equal(12, Occurrences(text, "facet normal"));
        Assert.Equal(36, Occurrences(text, "vertex "));
    }

    [Fact]
    public void EveryFacetHasADirectionOfUnitLength()
    {
        var bytes = Cube().ToStl();
        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80));

        for (var i = 0; i < count; i++)
        {
            var at = 84 + (i * 50);
            var normal = new Vector3(
                BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at + 4)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at + 8)));
            Assert.Equal(1, normal.Length, 1e-5);
        }
    }

    // PLY

    [Fact]
    public void TheHeaderSaysWhatIsInItAndTheCountsMatch()
    {
        var text = Latin1(Cube().ToPly(binary: false), int.MaxValue);
        Assert.StartsWith("ply", text, StringComparison.Ordinal);
        Assert.Contains("format ascii 1.0", text);
        Assert.Contains("element vertex 24", text);
        Assert.Contains("element face 12", text);

        var body = text.Split("end_header\n").Last().Trim().Split('\n');
        Assert.Equal(24 + 12, body.Length);
        Assert.StartsWith("3 ", body[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void BinaryIsTheHeaderAsTextAndTheRestAsNumbers()
    {
        var bytes = Cube().ToPly();

        // Enough to hold the whole header, which is nearly three hundred bytes: cut it short and
        // the search for its end finds nothing.
        var text = Latin1(bytes, 500);
        Assert.StartsWith("ply", text, StringComparison.Ordinal);
        Assert.Contains("binary_little_endian", text);

        var at = text.IndexOf("end_header\n", StringComparison.Ordinal) + "end_header\n".Length;

        // Eight floats a vertex, and a byte plus three ints a face.
        Assert.Equal((24 * 32) + (12 * 13), bytes.Length - at);
    }

    [Fact]
    public void TheFirstCornerReadsBackAsACornerOfTheCube()
    {
        var bytes = Cube().ToPly();
        var text = Latin1(bytes, 500);
        var at = text.IndexOf("end_header\n", StringComparison.Ordinal) + "end_header\n".Length;
        var data = bytes.AsSpan(at);

        var point = new Vector3(
            BinaryPrimitives.ReadSingleLittleEndian(data),
            BinaryPrimitives.ReadSingleLittleEndian(data[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[8..]));
        var box = Cube().Bounds;
        (double Value, double Low, double High)[] axes =
        [
            (point.X, box.Min.X, box.Max.X),
            (point.Y, box.Min.Y, box.Max.Y),
            (point.Z, box.Min.Z, box.Max.Z),
        ];
        foreach (var axis in axes)
        {
            Assert.True(axis.Value >= axis.Low - 1e-5);
            Assert.True(axis.Value <= axis.High + 1e-5);
        }
    }

    // reading a size back

    [Fact]
    public void AFileSaysHowBigItIsWithoutBeingDecoded()
    {
        var mesh = Shape.Of(ShapeKind.Stairs).Build();
        var box = MeshExport.BoundsOfGlb(mesh.ToGlb())!.Value;
        var actual = mesh.Bounds;

        Assert.Equal(actual.Min.X, box.Min.X, 1e-5);
        Assert.Equal(actual.Max.Y, box.Max.Y, 1e-5);
        Assert.Equal(actual.Max.Z, box.Max.Z, 1e-5);
    }

    [Fact]
    public void SeveralMaterialsAreSeveralPrimitivesAndItTakesThemAll()
    {
        var mesh = Cube();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            mesh.Faces[i].Material = i;
        }

        var box = MeshExport.BoundsOfGlb(mesh.ToGlb(materials: Enumerable.Repeat(new GlbMaterial(), 6).ToList()))!.Value;
        Assert.Equal(Cube().Bounds.Max.X, box.Max.X, 1e-5);
    }

    [Fact]
    public void NormalsAreNotMistakenForPositions()
    {
        // Every normal is between minus one and one, so folding those in would swallow anything
        // smaller than a two-metre cube.
        var small = Cube();
        foreach (var at in small.Positions)
        {
            at.Scale(0.1);
        }

        var box = MeshExport.BoundsOfGlb(small.ToGlb())!.Value;
        Assert.True(box.Max.X < 0.2);
    }

    [Fact]
    public void AGltfIsReadTheSameAsTheGlbOfTheSameDocument()
    {
        // The two formats are the same document in different containers, and the half that carries
        // the size is identical. Pulling the JSON chunk out of a glb and reading it as a gltf is the
        // cheapest way to say so.
        var mesh = Shape.Of(ShapeKind.Stairs).Build();
        var glb = mesh.ToGlb();
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        var json = Encoding.UTF8.GetString(glb, 20, length);

        var asGltf = MeshExport.BoundsOfGltf(json)!.Value;
        var asGlb = MeshExport.BoundsOfGlb(glb)!.Value;
        Assert.Equal(asGlb.Min.X, asGltf.Min.X, 1e-9);
        Assert.Equal(asGlb.Max.Y, asGltf.Max.Y, 1e-9);
        Assert.Equal(asGlb.Max.Z, asGltf.Max.Z, 1e-9);
    }

    [Fact]
    public void AGltfNobodyHereWroteIsReadToo()
    {
        // Hand-written rather than exported, because the file this exists for comes from somewhere
        // else: a model library publishes a .gltf beside its textures, and the editor has to frame
        // one it did not make.
        const string Gltf = """
            {
              "asset": {"version": "2.0"},
              "meshes": [
                {"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1}}]}
              ],
              "accessors": [
                {"type": "VEC3", "min": [-0.5, 0, -1.25], "max": [0.5, 3, 1.25]},
                {"type": "VEC3", "min": [-1, -1, -1], "max": [1, 1, 1]}
              ]
            }
            """;

        var box = MeshExport.BoundsOfGltf(Gltf)!.Value;
        Assert.Equal(-0.5, box.Min.X, 1e-9);

        // the normal is not a size
        Assert.Equal(3, box.Max.Y, 1e-9);
        Assert.Equal(1.25, box.Max.Z, 1e-9);
    }

    [Fact]
    public void SomethingThatIsNotAGltfIsNothingNotAGuess()
    {
        Assert.Null(MeshExport.BoundsOfGltf(""));
        Assert.Null(MeshExport.BoundsOfGltf("not json at all"));

        // JSON, but not a document
        Assert.Null(MeshExport.BoundsOfGltf("[1, 2, 3]"));

        // a document with no meshes has no size to report
        Assert.Null(MeshExport.BoundsOfGltf("{\"asset\": {\"version\": \"2.0\"}}"));
    }

    [Fact]
    public void SomethingThatIsNotAGlbIsNothingNotAGuess()
    {
        Assert.Null(MeshExport.BoundsOfGlb([]));
        Assert.Null(MeshExport.BoundsOfGlb([1, 2, 3, 4, 5]));

        // a file of zeros has no magic number
        Assert.Null(MeshExport.BoundsOfGlb(new byte[64]));
    }

    [Fact]
    public void ATruncatedFileIsNothingRatherThanACrash()
    {
        var whole = Cube().ToGlb();
        Assert.Null(MeshExport.BoundsOfGlb(whole[..30]));
    }

    // choosing a format

    [Fact]
    public void EachOneComesBackNamedAfterItself()
    {
        foreach (var format in Enum.GetValues<MeshFormat>())
        {
            var files = Cube().WriteAs(format, name: "thing");
            Assert.NotEmpty(files);
            Assert.Equal($"thing.{format.Extension}", files[0].Name);
        }
    }

    [Fact]
    public void OnlyObjBringsASecondFileAndOnlyWhenItHasMaterials()
    {
        Assert.Equal(2, Cube().WriteAs(MeshFormat.Obj, materials: [new GlbMaterial()]).Count);
        Assert.Single(Cube().WriteAs(MeshFormat.Obj));

        // STL has nowhere to put one
        Assert.Single(Cube().WriteAs(MeshFormat.Stl, materials: [new GlbMaterial()]));
    }

    [Fact]
    public void TheTwoThatCarryMaterialsSaySo()
    {
        Assert.True(MeshFormat.Obj.HasMaterials);
        Assert.True(MeshFormat.Glb.HasMaterials);
        Assert.False(MeshFormat.Stl.HasMaterials);
        Assert.False(MeshFormat.Ply.HasMaterials);
        Assert.True(MeshFormat.Obj.HasSidecar);
        Assert.False(MeshFormat.Glb.HasSidecar);
    }

    [Fact]
    public void AnEmptyMeshWritesAFileRatherThanThrowing()
    {
        foreach (var format in Enum.GetValues<MeshFormat>())
        {
            var files = new Mesh().WriteAs(format);
            Assert.NotEmpty(files);
            Assert.NotEmpty(files[0].Bytes);
        }
    }
}
