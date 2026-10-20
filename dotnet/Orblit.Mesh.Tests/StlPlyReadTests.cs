using System.Buffers.Binary;
using System.Text;

namespace Orblit.Mesh.Tests;

public class StlPlyReadTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static void SameBox(Mesh read, Mesh written)
    {
        Assert.Equal(0, (read.Bounds.Min - written.Bounds.Min).Length, 1e-6);
        Assert.Equal(0, (read.Bounds.Max - written.Bounds.Max).Length, 1e-6);
    }

    /// <summary>A PLY with <paramref name="header"/> lines and then <paramref name="body"/>, the way a file is laid out.</summary>
    private static byte[] Ply(string[] header, byte[]? body = null)
    {
        var text = string.Join("\n", ["ply", .. header, "end_header"]) + "\n";
        return [.. Bytes(text), .. body ?? []];
    }

    // STL

    [Fact]
    public void ABinaryFileComesBackWithItsCornersWelded()
    {
        var cube = Cube();
        var mesh = StlFiles.ReadStl(cube.ToStl());
        Assert.Equal(12, mesh.FaceCount);
        Assert.Equal(8, mesh.VertexCount);
        Assert.All(mesh.Faces, f => Assert.True(f.IsTriangle));
        SameBox(mesh, cube);
    }

    [Fact]
    public void ATextFileReadsTheSameAsABinaryOne()
    {
        var cube = Cube();
        var mesh = StlFiles.ReadStl(cube.ToStl(binary: false));
        Assert.Equal(12, mesh.FaceCount);
        Assert.Equal(8, mesh.VertexCount);
        SameBox(mesh, cube);
    }

    [Fact]
    public void ABinaryHeaderThatBeginsWithSolidIsStillBinary()
    {
        var binary = Cube().ToStl();
        Bytes("solid").CopyTo(binary, 0);
        Assert.Equal(12, StlFiles.ReadStl(binary).FaceCount);
    }

    [Fact]
    public void AFileCutShortOrWithANumberThatIsNotOneIsRefused()
    {
        var binary = Cube().ToStl();
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl(binary[..^10]));
        var broken = (byte[])binary.Clone();
        BinaryPrimitives.WriteSingleLittleEndian(broken.AsSpan(96), float.NaN);
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl(broken));
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl(Bytes("solid a\nvertex 0 0 0\nvertex 1 0 0\n")));
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl([]));
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl([1, 2, 3]));
    }

    [Fact]
    public void ACountThatPromisesMoreThanTheFileHoldsIsRefused()
    {
        var lie = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(lie.AsSpan(80), 0xFFFFFFFF);
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl(lie));
    }

    [Fact]
    public void ATriangleWithTwoEqualCornersHasNoAreaAndIsDropped()
    {
        var mesh = StlFiles.ReadStl(Bytes(
            "solid a\n"
            + "vertex 0 0 0\nvertex 1 0 0\nvertex 0 0 0\n"
            + "vertex 0 0 0\nvertex 1 0 0\nvertex 0 1 -0\n"
            + "endsolid a\n"));
        Assert.Equal(1, mesh.FaceCount);
        Assert.Equal(3, mesh.VertexCount);
    }

    [Fact]
    public void AFileOverTheByteLimitIsRefused()
    {
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl(Cube().ToStl(), new ReadLimits { Bytes = 100 }));
    }

    [Fact]
    public void ATextLineOverTheLimitIsRefused()
    {
        Assert.ThrowsAny<FormatException>(() => StlFiles.ReadStl(
            Bytes($"solid a\n{new string(' ', 50)}vertex 0 0 0\n"),
            new ReadLimits { Line = 20 }));
    }

    // PLY

    [Fact]
    public void ABinaryFileKeepsItsFacesAndItsCoordinates()
    {
        var cube = Cube();
        var mesh = PlyFiles.ReadPly(cube.ToPly());
        Assert.Equal(12, mesh.FaceCount);
        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(3, mesh.Faces[0].Uv.Manual!.Count);
        SameBox(mesh, cube);
    }

    // The same name as the STL test above, in the other group.
    [Fact]
    public void PlyATextFileReadsTheSameAsABinaryOne()
    {
        var cube = Cube();
        var mesh = PlyFiles.ReadPly(cube.ToPly(binary: false));
        Assert.Equal(12, mesh.FaceCount);
        SameBox(mesh, cube);
    }

    [Fact]
    public void BigEndianOtherElementsAndOtherPropertiesAreReadPast()
    {
        var body = new byte[(3 * 13) + 8 + 13];
        var at = 0;
        double[][] points = [[0.0, 0.0, 0.0], [2.0, 0.0, 0.0], [0.0, 3.0, 0.0]];
        foreach (var point in points)
        {
            foreach (var value in point)
            {
                BinaryPrimitives.WriteSingleBigEndian(body.AsSpan(at), (float)value);
                at += 4;
            }

            body[at++] = 200;
        }

        foreach (var value in new[] { 7, 8 })
        {
            BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(at), value);
            at += 4;
        }

        body[at++] = 3;
        foreach (var index in new[] { 0, 1, 2 })
        {
            BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(at), index);
            at += 4;
        }

        var mesh = PlyFiles.ReadPly(Ply(
            [
                "format binary_big_endian 1.0",
                "element vertex 3",
                "property float x",
                "property float y",
                "property float z",
                "property uchar red",
                "element edge 1",
                "property int a",
                "property int b",
                "element face 1",
                "property list uchar int vertex_indices",
            ],
            body));
        Assert.Equal(new[] { 0, 1, 2 }, mesh.Faces.Single().Vertices);
        Assert.Equal(3.0, mesh.Bounds.Max.Y);
    }

    [Fact]
    public void AQuadStaysAQuad()
    {
        var mesh = PlyFiles.ReadPly(Ply(
            [
                "format ascii 1.0",
                "element vertex 4",
                "property float x",
                "property float y",
                "property float z",
                "element face 1",
                "property list uchar int vertex_index",
            ],
            Bytes("0 0 0\n1 0 0\n1 1 0\n0 1 0\n4 0 1 2 3\n")));
        Assert.Equal(4, mesh.Faces.Single().Length);
    }

    [Fact]
    public void SplatsAreTheSplatImportersAndTheMessageSaysSo()
    {
        var thrown = Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(Ply(
            [
                "format ascii 1.0",
                "element vertex 1",
                "property float x",
                "property float y",
                "property float z",
                "property float f_dc_0",
                "property float opacity",
            ],
            Bytes("0 0 0 1 1\n"))));
        Assert.Contains("splat", thrown.Message);
    }

    [Fact]
    public void PointsWithNoFacesAreNotAModel()
    {
        Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(Ply(
            [
                "format ascii 1.0",
                "element vertex 1",
                "property float x",
                "property float y",
                "property float z",
            ],
            Bytes("0 0 0\n"))));
    }

    [Fact]
    public void AFaceNamingAPointThatIsNotThereIsRefused()
    {
        Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(Ply(
            [
                "format ascii 1.0",
                "element vertex 3",
                "property float x",
                "property float y",
                "property float z",
                "element face 1",
                "property list uchar int vertex_indices",
            ],
            Bytes("0 0 0\n1 0 0\n0 1 0\n3 0 1 9\n"))));
    }

    [Fact]
    public void FacesWithMoreCornersThanTheLimitAllowsAreRefused()
    {
        var cube = Cube();
        Assert.Equal(12, PlyFiles.ReadPly(cube.ToPly(binary: false)).FaceCount);
        Assert.ThrowsAny<FormatException>(
            () => PlyFiles.ReadPly(cube.ToPly(binary: false), new ReadLimits { Indices = 35 }));
    }

    [Fact]
    public void AHeaderThatPromisesMoreThanTheLimitsAllowIsRefused()
    {
        foreach (var count in new[] { "4000000000", "99999999999999999999", "-1" })
        {
            Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(Ply(
            [
                "format binary_little_endian 1.0",
                $"element vertex {count}",
                "property float x",
                "property float y",
                "property float z",
                "element face 1",
                "property list uchar int vertex_indices",
            ])));
        }
    }

    [Fact]
    public void AFileWithNoHeaderOrABodyCutShortIsRefused()
    {
        Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(Bytes("ply\nformat ascii 1.0\n")));
        Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(Bytes("hello")));
        var binary = Cube().ToPly();
        Assert.ThrowsAny<FormatException>(() => PlyFiles.ReadPly(binary[..^20]));
    }

    // fuzz

    private static void Survives(byte[] bytes, Func<byte[], Mesh> read)
    {
        try
        {
            read(bytes);
        }
        catch (FormatException)
        {
            // A refusal is an answer. Anything else is the bug.
        }
    }

    [Theory]
    [InlineData("binary STL")]
    [InlineData("text STL")]
    [InlineData("binary PLY")]
    [InlineData("text PLY")]
    public void RandomBytesAndARealFileWithBytesChanged(string kind)
    {
        var cube = Cube();
        var (source, read) = kind switch
        {
            "binary STL" => (cube.ToStl(), bytes => StlFiles.ReadStl(bytes)),
            "text STL" => (cube.ToStl(binary: false), bytes => StlFiles.ReadStl(bytes)),
            "binary PLY" => (cube.ToPly(), (Func<byte[], Mesh>)(bytes => PlyFiles.ReadPly(bytes))),
            _ => (cube.ToPly(binary: false), bytes => PlyFiles.ReadPly(bytes)),
        };

        // Seeded, but a different stream from Dart's, so the bytes differ. The parity fixture covers the
        // shared cases.
        var random = new Random(21);
        for (var round = 0; round < 400; round++)
        {
            var noise = new List<byte>();
            for (var i = 0; i < random.Next(600); i++)
            {
                noise.Add((byte)random.Next(256));
            }

            Survives([.. noise], read);
            var changed = (byte[])source.Clone();
            for (var n = 0; n < 1 + random.Next(8); n++)
            {
                changed[random.Next(changed.Length)] = (byte)random.Next(256);
            }

            Survives(changed, read);
            Survives(changed[..random.Next(changed.Length)], read);
        }
    }
}
