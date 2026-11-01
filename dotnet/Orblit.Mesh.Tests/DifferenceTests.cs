using System.Buffers.Binary;

namespace Orblit.Mesh.Tests;

/// <summary>
/// Where the port differs from the fixture on purpose: a sort that stays stable past 32 faces
/// (decision 75) and one quiet NaN in every file. The fixture promises neither, so none holds them.
/// </summary>
public class DifferenceTests
{
    private static Mesh Strip(int faces, Func<int, int> material)
    {
        var positions = new List<Vector3>();
        var list = new List<Face>();
        for (var i = 0; i < faces; i++)
        {
            positions.Add(new Vector3(i, 0, 0));
            positions.Add(new Vector3(i, 1, 0));
            positions.Add(new Vector3(i + 0.5, 0, 0));
            list.Add(new Face([i * 3, (i * 3) + 1, (i * 3) + 2], material(i), false, null));
        }

        return new Mesh(positions, list);
    }

    [Fact]
    public void FacesOfOneMaterialKeepTheirOrderPastThirtyTwoFaces()
    {
        var tris = Strip(50, i => i % 2).Triangulate();
        foreach (var group in tris.Groups)
        {
            var xs = Enumerable.Range(group.Start, group.Count)
                .Where(k => k % 3 == 0)
                .Select(k => tris.Positions[tris.Indices[k] * 3])
                .ToList();
            Assert.Equal(xs.Order(), xs);
        }

        Assert.Equal(2, tris.Groups.Count);
    }

    private static bool HasBits(byte[] file, byte[] bits) => file.AsSpan().IndexOf(bits) >= 0;

    private static readonly byte[] PositiveNan = [0x00, 0x00, 0xC0, 0x7F];
    private static readonly byte[] NegativeNan = [0x00, 0x00, 0xC0, 0xFF];

    [Theory]
    [InlineData(MeshFormat.Stl)]
    [InlineData(MeshFormat.Ply)]
    [InlineData(MeshFormat.Glb)]
    public void ABinaryFileHoldsThePositiveNanWhateverSignItWasMadeWith(MeshFormat format)
    {
        var mesh = new Mesh(
            [new Vector3(double.NaN, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)],
            [new Face([0, 1, 2], 0, false, null)]);
        var file = mesh.WriteAs(format, "n", [], true).Single().Bytes;
        Assert.True(HasBits(file, PositiveNan));
        Assert.False(HasBits(file, NegativeNan));
    }

    [Fact]
    public void AWholeNumberAccessorOfFourBytesIsReadWithoutRoundingThroughFloat()
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 3196491344);
        var read = new GltfAccessors(
            new Dictionary<string, object?>
            {
                ["bufferViews"] = new List<object?> { new Dictionary<string, object?> { ["buffer"] = 0L, ["byteLength"] = 4L } },
                ["accessors"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["bufferView"] = 0L,
                        ["componentType"] = 5125L,
                        ["count"] = 1L,
                        ["type"] = "SCALAR",
                    },
                },
            },
            bytes);
        Assert.Equal([3196491344.0], read.Floats(0, 1));
    }
}
