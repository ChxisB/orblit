using System.Buffers.Binary;
using System.Text;

namespace Orblit.Mesh.Tests;

public class TrianglesTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    // triangulating

    [Fact]
    public void AQuadBecomesTwoTriangles()
    {
        var tris = new Shape { Kind = ShapeKind.Plane, WidthCuts = 0, HeightCuts = 0 }.Build().Triangulate();

        Assert.Equal(2, tris.TriangleCount);
        Assert.Equal(4, tris.VertexCount);
    }

    [Fact]
    public void ABoxKeepsItsCornersApartSoItLooksLikeABox()
    {
        var tris = Cube().Triangulate();

        // Eight corners, twenty-four vertices: each corner needs three normals, and sharing them is
        // what makes a box look like a deflated ball.
        Assert.Equal(24, tris.VertexCount);
        Assert.Equal(12, tris.TriangleCount);
    }

    [Fact]
    public void EveryIndexNamesAVertexThatIsThere()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var tris = Shape.Of(kind).Build().Triangulate();
            foreach (var index in tris.Indices)
            {
                Assert.True(index < tris.VertexCount, kind.ToString());
            }
        }
    }

    [Fact]
    public void ThereIsANormalAndATextureCoordinateForEveryVertex()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var tris = Shape.Of(kind).Build().Triangulate();
            Assert.True(tris.Normals.Length == tris.Positions.Length, kind.ToString());
            Assert.True(tris.Uvs.Length / 2 == tris.VertexCount, kind.ToString());
        }
    }

    [Fact]
    public void EveryNormalIsADirectionRatherThanNothing()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var tris = Shape.Of(kind).Build().Triangulate();
            for (var i = 0; i + 2 < tris.Normals.Length; i += 3)
            {
                var length = new Vector3(tris.Normals[i], tris.Normals[i + 1], tris.Normals[i + 2]).Length;
                Assert.True(Math.Abs(length - 1) <= 1e-4, $"{kind}: {length}");
            }
        }
    }

    [Fact]
    public void ASmoothSideShadesRoundRatherThanFaceted()
    {
        var tris = new Shape { Kind = ShapeKind.Cylinder, Sides = 16 }.Build().Triangulate();

        // On a smooth cylinder a vertex's normal points away from the axis. On a faceted one it
        // points the way its own flat face does, which at the seam between two faces is visibly the
        // wrong way by half a segment.
        var checkedCount = 0;
        for (var i = 0; i < tris.VertexCount; i++)
        {
            var at = new Vector3(tris.Positions[i * 3], tris.Positions[(i * 3) + 1], tris.Positions[(i * 3) + 2]);
            var normal = new Vector3(tris.Normals[i * 3], tris.Normals[(i * 3) + 1], tris.Normals[(i * 3) + 2]);

            // Only the sides: the ends are flat on purpose.
            if (Math.Abs(normal.Y) > 0.1)
            {
                continue;
            }

            var radial = new Vector3(at.X, 0, at.Z);
            if (radial.Length < 1e-6)
            {
                continue;
            }

            Assert.True(MeshTriangles.AngleBetween(normal, radial) < 0.02);
            checkedCount++;
        }

        Assert.True(checkedCount > 16);
    }

    [Fact]
    public void AFlatShapeDoesNot()
    {
        var tris = Cube().Triangulate();

        // Six faces, six directions, and no averaging between them.
        static int Rounded(float number) => (int)Math.Round(number, MidpointRounding.AwayFromZero);
        var directions = new HashSet<string>();
        for (var i = 0; i < tris.VertexCount; i++)
        {
            directions.Add($"{Rounded(tris.Normals[i * 3])}{Rounded(tris.Normals[(i * 3) + 1])}{Rounded(tris.Normals[(i * 3) + 2])}");
        }

        Assert.Equal(6, directions.Count);
    }

    [Fact]
    public void AFaceWithFewerThanThreeCornersIsSkippedNotDrawn()
    {
        var mesh = new Mesh([Vector3.Zero(), new Vector3(1, 0, 0)]);
        mesh.AddFace([0, 1]);

        Assert.Equal(0, mesh.Triangulate().TriangleCount);
    }

    // the glb it writes

    [Fact]
    public void StartsWithTheMagicWordAndSaysItIsVersionTwo()
    {
        var glb = Cube().ToGlb();

        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(0)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4)));
        Assert.Equal((uint)glb.Length, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8)));
    }

    [Fact]
    public void EveryChunkIsAWholeNumberOfFourByteWords()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var glb = Shape.Of(kind).Build().ToGlb();
            Assert.True(glb.Length % 4 == 0, kind.ToString());
            Assert.True(BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12)) % 4 == 0, kind.ToString());
        }
    }

    [Fact]
    public void DescribesOneMeshWithTheThreeAttributesARendererWants()
    {
        var header = GlbJson.Of(Cube().ToGlb());
        var attributes = GlbJson.Map(GlbJson.Map(GlbJson.PrimitivesOf(header)[0])["attributes"]);

        Assert.Contains("POSITION", attributes.Keys);
        Assert.Contains("NORMAL", attributes.Keys);
        Assert.Contains("TEXCOORD_0", attributes.Keys);
    }

    [Fact]
    public void TheAccessorsCountWhatIsActuallyInTheBuffer()
    {
        var mesh = Cube();
        var tris = mesh.Triangulate();
        var accessors = GlbJson.List(GlbJson.Of(mesh.ToGlb())["accessors"]);

        Assert.Equal((double)tris.Indices.Length, GlbJson.Number(GlbJson.Map(accessors[0])["count"]));
        Assert.Equal((double)tris.VertexCount, GlbJson.Number(GlbJson.Map(accessors[1])["count"]));
        Assert.Equal((double)tris.VertexCount, GlbJson.Number(GlbJson.Map(accessors[2])["count"]));
    }

    [Fact]
    public void TheBufferViewsStayInsideTheBuffer()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var header = GlbJson.Of(Shape.Of(kind).Build().ToGlb());
            var length = GlbJson.Number(GlbJson.Map(GlbJson.List(header["buffers"])[0])["byteLength"]);

            foreach (var item in GlbJson.List(header["bufferViews"]))
            {
                var view = GlbJson.Map(item);
                var at = GlbJson.Number(view["byteOffset"]);
                var size = GlbJson.Number(view["byteLength"]);
                Assert.True(at + size <= length, kind.ToString());

                // Float accessors have to start on a four-byte boundary.
                Assert.True(at % 4 == 0, kind.ToString());
            }
        }
    }

    [Fact]
    public void PositionsCarryTheBoundsWhichTheFormatRequires()
    {
        var mesh = new Shape { Kind = ShapeKind.Cube, Width = 4 }.Build();
        var positions = GlbJson.Map(GlbJson.List(GlbJson.Of(mesh.ToGlb())["accessors"])[1]);

        Assert.Equal(-2, GlbJson.Number(GlbJson.List(positions["min"])[0]), 1e-6);
        Assert.Equal(2, GlbJson.Number(GlbJson.List(positions["max"])[0]), 1e-6);
    }

    [Fact]
    public void CountsAreWrittenAsIntegersNotAs12Point0()
    {
        // A count written with a decimal point is a file some loaders refuse.
        var glb = Cube().ToGlb();
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        var text = Encoding.UTF8.GetString(glb, 20, length);

        Assert.DoesNotContain(".0,", text);
        Assert.Contains("\"version\":\"2.0\"", text);
    }

    [Fact]
    public void AnEmptyMeshStillWritesAFileRatherThanThrowing()
    {
        var glb = new Mesh().ToGlb();
        Assert.True(glb.Length > 20);
        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(0)));
    }

    [Fact]
    public void AnEmptyMeshWritesAnEmptySceneNotAnEmptyAccessor()
    {
        // A buffer view of nought bytes and an accessor counting nought elements are both errors in
        // the format, so the mesh nobody has drawn in yet has to be a node on its own rather than a
        // mesh made of nothing. Nine validator errors, before it was.
        var header = GlbJson.Of(new Mesh().ToGlb());

        Assert.False(header.ContainsKey("accessors"));
        Assert.False(header.ContainsKey("bufferViews"));
        Assert.False(header.ContainsKey("buffers"));
        Assert.False(GlbJson.Map(Assert.Single(GlbJson.List(header["nodes"]))).ContainsKey("mesh"));
    }
}
