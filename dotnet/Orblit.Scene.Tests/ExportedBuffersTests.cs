using System.Buffers.Binary;

namespace Orblit.Scene.Tests;

public class ExportedBuffersTests
{
    // One VEC2 float accessor of two elements at byte 4, then two unsigned short indices at byte 20.
    private static readonly JsonMap Header = new Dictionary<string, object?>
    {
        ["bufferViews"] = new List<object?>
        {
            new Dictionary<string, object?> { ["byteOffset"] = 4L },
            new Dictionary<string, object?> { ["byteOffset"] = 20L },
        },
        ["accessors"] = new List<object?>
        {
            new Dictionary<string, object?> { ["bufferView"] = 0L, ["componentType"] = 5126L, ["count"] = 2L, ["type"] = "VEC2" },
            new Dictionary<string, object?> { ["bufferView"] = 1L, ["componentType"] = 5123L, ["count"] = 2L, ["type"] = "SCALAR" },
        },
    };

    private static byte[] Buffer(float a, float b, float c, float d, ushort first, ushort second)
    {
        var bytes = new byte[24];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), a);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), b);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), c);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16), d);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), first);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), second);
        return bytes;
    }

    [Fact]
    public void AFloatThatShouldBeZeroTakesTheFixturesZero()
    {
        var fixture = Buffer(0f, 0.5f, 1f, -0.5f, 3, 1);
        var written = Buffer(3.4e-16f, 0.5f, 1f, -0.5f, 3, 1);

        Assert.Equal(fixture, ExportedBuffers.ToFixtureWhereClose(Header, written, fixture));
    }

    [Fact]
    public void AFloatThatReallyDiffersStaysAsWritten()
    {
        var fixture = Buffer(0f, 0.5f, 1f, -0.5f, 3, 1);
        var written = Buffer(0f, 0.6f, 1f, -0.5f, 3, 1);

        Assert.Equal(written, ExportedBuffers.ToFixtureWhereClose(Header, written, fixture));
    }

    [Fact]
    public void AnIndexIsNeverTakenFromTheFixture()
    {
        var fixture = Buffer(0f, 0.5f, 1f, -0.5f, 3, 1);
        var written = Buffer(0f, 0.5f, 1f, -0.5f, 3, 2);

        Assert.Equal(written, ExportedBuffers.ToFixtureWhereClose(Header, written, fixture));
    }

    [Fact]
    public void WithoutAFixtureOfTheSameLengthNothingChanges()
    {
        var written = Buffer(3.4e-16f, 0.5f, 1f, -0.5f, 3, 1);

        Assert.Equal(written, ExportedBuffers.ToFixtureWhereClose(Header, written, null));
        Assert.Equal(written, ExportedBuffers.ToFixtureWhereClose(Header, written, new byte[8]));
    }
}
