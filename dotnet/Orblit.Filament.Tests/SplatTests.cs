using System.Runtime.InteropServices;
using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Gaussian splats as they cross to the renderer: the compact
/// records a cloud made in code is packed into, and the message that carries clouds. The records
/// are read on the far side by the same C++ that reads a <c>.splat</c> file, so the layout here has
/// to be that file's layout byte for byte.
/// </summary>
public sealed class SplatTests
{
    private static RenderScene SceneWith(params SplatCloud[] splats) =>
        SceneBuilders.Of([]) with { Splats = splats };

    private static byte[] Two() => SplatCloud.Pack(
        positions: [1, 2, 3, -4, 5, -6],
        scales: [0.1f, 0.2f, 0.3f, 1, 1, 1],
        colours: [1, 0.5f, 0, 0.25f, 0, 0, 0, 1],
        rotations: [1, 0, 0, 0, 0, 0, 0, 2]);

    private static Held HeldWith(int key, int revision)
    {
        var held = new Held();
        held.Splats[key] = revision;
        return held;
    }

    [Fact]
    public void PackingIsThirtyTwoBytesASplatInTheSplatLayout()
    {
        var bytes = Two();
        Assert.Equal(2 * SplatCloud.RecordBytes, bytes.Length);

        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
        Assert.Equal([1f, 2f, 3f], floats[..3]);
        SceneBuilders.Near(0.1, floats[3]);
        SceneBuilders.Near(0.2, floats[4]);
        SceneBuilders.Near(0.3, floats[5]);
        Assert.Equal([-4f, 5f, -6f], floats[8..11]);

        // Colour as bytes, alpha as the splat's peak opacity.
        Assert.Equal<byte>([255, 128, 0, 64], bytes[24..28]);
        Assert.Equal<byte>([0, 0, 0, 255], bytes[56..60]);
    }

    [Fact]
    public void PackingQuantisesAUnitQuaternionAsValueTimes128Plus128()
    {
        var bytes = Two();

        // The identity: w of one saturates at 255, the rest are the middle.
        Assert.Equal<byte>([255, 128, 128, 128], bytes[28..32]);

        // Normalised first: (0, 0, 0, 2) is a half turn about z.
        Assert.Equal<byte>([128, 128, 128, 255], bytes[60..64]);
    }

    [Fact]
    public void PackingWithNoRotationsIsTheIdentity()
    {
        var bytes = SplatCloud.Pack(positions: new float[3], scales: [1, 1, 1], colours: new float[4]);

        Assert.Equal<byte>([255, 128, 128, 128], bytes[28..32]);
    }

    [Fact]
    public void TheMessageSaysNothingAboutSplatsWhenThereAreNone()
    {
        var message = SceneWith().ToMessage(1);

        Assert.False(message.ContainsKey("splatKeys"));
    }

    [Fact]
    public void TheMessageCarriesAnInMemoryCloudUntilTheRendererHasIt()
    {
        var cloud = new SplatCloud(7, data: Two()) { Revision = 3 };
        var first = SceneWith(cloud).ToMessage(1);
        Assert.Equal([7], SceneBuilders.Read<int[]>(first, "splatKeys"));
        Assert.Equal([3], SceneBuilders.Read<int[]>(first, "splatRevisions"));
        Assert.Equal([7], SceneBuilders.Read<int[]>(first, "splatChanged"));
        Assert.Equal([2], SceneBuilders.Read<int[]>(first, "splatChangedCounts"));
        Assert.Equal(64, SceneBuilders.Read<byte[]>(first, "splatData").Length);

        var again = SceneWith(cloud).ToMessage(1, HeldWith(7, 3));
        Assert.Equal([7], SceneBuilders.Read<int[]>(again, "splatKeys"));
        Assert.Empty(SceneBuilders.Read<int[]>(again, "splatChanged"));
        Assert.Empty(SceneBuilders.Read<byte[]>(again, "splatData"));

        var moved = SceneWith(cloud).ToMessage(1, HeldWith(7, 2));
        Assert.Equal([7], SceneBuilders.Read<int[]>(moved, "splatChanged"));
    }

    [Fact]
    public void TheMessageSendsAFileByItsPathAndNoRecords()
    {
        var message = SceneWith(new SplatCloud(1, "/captures/garden.ply")).ToMessage(1);

        Assert.Equal(["/captures/garden.ply"], SceneBuilders.Read<string[]>(message, "splatPaths"));
        Assert.Empty(SceneBuilders.Read<int[]>(message, "splatChanged"));
    }

    [Fact]
    public void TheMessagePacksTheTransformOpacityAndBrightnessAtTheStride()
    {
        var message = SceneWith(
            new SplatCloud(1, "a.splat"),
            new SplatCloud(2, "b.splat")
            {
                Transform = Matrix4.TranslationValues(4, 5, 6),
                Opacity = 0.5,
                Brightness = 2,
                Sorted = false,
            }).ToMessage(1);
        var parameters = SceneBuilders.Read<float[]>(message, "splatParams");

        Assert.Equal(2 * SplatCloud.Stride, parameters.Length);
        const int second = SplatCloud.Stride;
        Assert.Equal([4f, 5f, 6f], parameters[(second + 12)..(second + 15)]);
        Assert.Equal(0.5f, parameters[second + 16]);
        Assert.Equal(2f, parameters[second + 17]);

        // Sorted and read to degree two, then unsorted and read to degree two.
        Assert.Equal([5, 4], SceneBuilders.Read<int[]>(message, "splatFlags"));
    }

    [Fact]
    public void TheFlagsCarryTheSortInTheLowBitAndTheDegreeAboveIt()
    {
        Assert.Equal(1 | (2 << 1), new SplatCloud(1, "a.ply").Flags);
        Assert.Equal(1, new SplatCloud(1, "a.ply") { Harmonics = 0 }.Flags);
        Assert.Equal(1 | (1 << 1), new SplatCloud(1, "a.ply") { Harmonics = 1 }.Flags);
        Assert.Equal(3 << 1, new SplatCloud(1, "a.ply") { Sorted = false, Harmonics = 3 }.Flags);
    }

    [Fact]
    public void TheFlagsCarryACoarseSortInBitThree()
    {
        Assert.Equal(1 | (2 << 1) | 8, new SplatCloud(1, "a.ply") { CoarseOrder = true }.Flags);
    }

    [Fact]
    public void TheFlagsCarryTheLimitFromBitEightUpAndNoughtForNone()
    {
        var limited = new SplatCloud(1, "a.ply") { Limit = 250000 };

        Assert.Equal(250000, limited.Flags >> SplatCloud.LimitShift);
        Assert.Equal(1 | (2 << 1), limited.Flags & 0xff);
        Assert.Equal(0, new SplatCloud(1, "a.ply").Flags >> SplatCloud.LimitShift);
    }

    [Fact]
    public void TheFlagsKeepTheLargestLimitWholeThroughASigned32BitArray()
    {
        // The top bit is set, so the wire holds a negative number. Read unsigned, as the
        // renderer reads it, it is the limit again.
        var message = SceneWith(new SplatCloud(1, "a.ply") { Limit = SplatCloud.MaxLimit, CoarseOrder = true }).ToMessage(1);

        var flags = SceneBuilders.Read<int[]>(message, "splatFlags")[0];

        Assert.True(flags < 0);
        var unsigned = unchecked((uint)flags);
        Assert.Equal((uint)SplatCloud.MaxLimit, unsigned >> SplatCloud.LimitShift);
        Assert.Equal(8u, unsigned & 8);
    }

    [Fact]
    public void TheFlagsRefuseALimitOfNothingOrPastWhatTheyCanHold()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1, "a.ply") { Limit = 0 });
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1, "a.ply") { Limit = SplatCloud.MaxLimit + 1 });
    }

    [Fact]
    public void TheFlagsRefuseADegreeNoCaptureIsTrainedTo()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1, "a.ply") { Harmonics = 4 });
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1, "a.ply") { Harmonics = -1 });
    }

    [Fact]
    public void ACloudIsAFileOrASetOfRecordsNeverBoth()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1, "a.ply", new byte[32]));
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1));
    }
}
