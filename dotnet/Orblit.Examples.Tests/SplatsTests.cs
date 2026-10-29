using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// What the environment and the device tell the splats example. The fixture
/// ran with neither, so these cover what the gallery sees on a real device or
/// with a capture named.
/// </summary>
public sealed class SplatsTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    private static readonly DeviceProfile Medium = new()
    {
        FeatureLevel = 3,
        MaxTextureSize = 4096,
        WorkerThreads = 4,
        SystemMemoryMegabytes = 4096,
    };

    [Fact]
    public void TheEnvironmentSetsWhereTheControlsStart()
    {
        var example = new SplatsExample(Environment(
            ("ORBLIT_SPLAT_COUNT", "100000"),
            ("ORBLIT_SPLAT_HARMONICS", "7"),
            ("ORBLIT_SPLAT_SORT", "0"),
            ("ORBLIT_SPLAT_PILLAR", "0"),
            ("ORBLIT_SPLAT_COARSE", "1"),
            ("ORBLIT_SPLAT_DEVICE", "0")));
        example.Device = new DeviceProfile();

        var cloud = Cloud(example, 0);
        example.Control<Choice>("Cloud").Select?.Invoke("Ring");

        Assert.Equal(100000 * 32, cloud.Data?.Length);
        Assert.Equal("100k", example.Control<Choice>("Splats").Selected);
        Assert.False(cloud.Sorted);
        Assert.True(cloud.CoarseOrder);
        Assert.Equal(3, cloud.Harmonics);
        Assert.Null(cloud.Limit);
        Assert.Equal([1L], example.Scene(Camera, 0).Objects.Select(item => item.Key));
    }

    [Fact]
    public void NothingTheEnvironmentCannotMeanIsTaken()
    {
        var example = new SplatsExample(Environment(
            ("ORBLIT_SPLAT", ""),
            ("ORBLIT_SPLAT_COUNT", "0"),
            ("ORBLIT_SPLAT_HARMONICS", "two")));

        var cloud = Cloud(example, 0);

        Assert.Null(cloud.Path);
        Assert.Equal(300000 * 32, cloud.Data?.Length);
        Assert.Equal(2, cloud.Harmonics);
        Assert.True(cloud.Sorted);
        Assert.False(cloud.CoarseOrder);
    }

    [Theory]
    [InlineData("-1", 0)]
    [InlineData("3", 3)]
    public void HarmonicsFromTheEnvironmentAreKeptInRange(string harmonics, int wanted)
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_HARMONICS", harmonics)));

        Assert.Equal(wanted, Cloud(example, 0).Harmonics);
    }

    /// <summary>The example's own limit wins over the device's, and none leaves the device's.</summary>
    [Theory]
    [InlineData("0", 250_000)]
    [InlineData("5000", 5000)]
    [InlineData("99999999", SplatCloud.MaxLimit)]
    public void ALimitFromTheEnvironmentWinsWithinRange(string limit, int wanted)
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_LIMIT", limit))) { Device = new DeviceProfile() };

        Assert.Equal(wanted, Cloud(example, 0).Limit);
    }

    [Fact]
    public void ACaptureIsReadFromItsPathTheRightWayUp()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT", "garden.ply")));

        var cloud = Cloud(example, 0);
        example.Control<Choice>("Harmonics").Select?.Invoke("3");

        Assert.Equal("garden.ply", cloud.Path);
        Assert.Null(cloud.Data);
        Assert.Equal(-1, cloud.Transform[5], 12);
        Assert.Equal(3, Cloud(example, 0).Harmonics);
        Assert.Equal(
            [true, true, false],
            example.Settings().OfType<Choice>().Select(choice => choice.Select is null));
    }

    [Fact]
    public void TheDeviceHoldsTheCloudToWhatItCanCarry()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000")));
        var unknown = Cloud(example, 0);
        var waiting = example.Control<Toggle>("Device limits").Note;

        example.Device = new DeviceProfile();
        var low = Cloud(example, 0);

        Assert.Null(unknown.Limit);
        Assert.Equal(2, unknown.Harmonics);
        Assert.False(unknown.CoarseOrder);
        Assert.Equal("Waiting for the renderer to say what this device can do.", waiting);
        Assert.Equal(250_000, low.Limit);
        Assert.Equal(0, low.Harmonics);
        Assert.True(low.CoarseOrder);
        // A limit is applied as a cloud is read, so a new one sends the cloud again.
        Assert.Equal(unknown.Revision + 1, low.Revision);
        Assert.Equal(
            "Low-tier device: at most 250,000 splats, harmonics to degree 0, a coarse sort.",
            example.Control<Toggle>("Device limits").Note);

        example.Control<Toggle>("Device limits").Change(false);
        Assert.False(Cloud(example, 0).CoarseOrder);
    }

    [Fact]
    public void AFullSortIsNamedAndTheDeviceCanBeIgnored()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000"))) { Device = Medium };
        var held = Cloud(example, 0);
        var note = example.Control<Toggle>("Device limits").Note;

        example.Control<Toggle>("Device limits").Change(false);
        var free = Cloud(example, 0);

        Assert.Equal(1_000_000, held.Limit);
        Assert.False(held.CoarseOrder);
        Assert.Equal("Medium-tier device: at most 1,000,000 splats, harmonics to degree 2, a full sort.", note);
        Assert.Null(free.Limit);
        Assert.Equal(
            "Medium-tier device, ignored: every splat, at the degree chosen below.",
            example.Control<Toggle>("Device limits").Note);
    }

    [Fact]
    public void TheCloudIsSentAgainOnlyWhenItChanges()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000"))) { Device = Medium };
        var first = Cloud(example, 0);
        var again = Cloud(example, 1);

        example.Control<Choice>("Cloud").Select?.Invoke("Ring");
        var ring = Cloud(example, 2);

        Assert.Equal(first.Revision, again.Revision);
        Assert.Same(first.Data, again.Data);
        Assert.Equal(first.Revision + 1, ring.Revision);
        Assert.Equal(first.Data?.Length, ring.Data?.Length);
        Assert.NotEqual(first.Data, ring.Data);
    }

    [Fact]
    public void AChosenCountIsGeneratedAndShown()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000")));
        var first = Cloud(example, 0);

        example.Control<Choice>("Splats").Select?.Invoke("100k");
        var chosen = Cloud(example, 1);

        Assert.Equal(1000 * 32, first.Data?.Length);
        Assert.Equal(100000 * 32, chosen.Data?.Length);
        Assert.Equal(first.Revision + 1, chosen.Revision);
        Assert.Equal("100k", example.Control<Choice>("Splats").Selected);
    }

    [Theory]
    [InlineData("Garden", "9cd955d1d1246ce0")]
    [InlineData("Ring", "2da8db520cea68f8")]
    public void TheSeedGivesTheSameCloudEveryRun(string shape, string pinned)
    {
        var clouds = Enumerable.Range(0, 2).Select(_ =>
        {
            var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000")));
            example.Control<Choice>("Cloud").Select?.Invoke(shape);
            return Cloud(example, 0).Data;
        }).ToList();

        Assert.Equal(clouds[0], clouds[1]);
        // Pinned from this port's own run. The Dart clouds came from another generator.
        Assert.Equal(pinned, SceneDigest.Raw(clouds[0] ?? []));
    }

    [Fact]
    public void ThePillarStandsInTheRingAndNowhereElse()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000")));
        var garden = example.Scene(Camera, 0).Objects;
        var shown = example.Control<Toggle>("Solid pillar");

        example.Control<Choice>("Cloud").Select?.Invoke("Ring");
        var ring = example.Scene(Camera, 0).Objects;
        var enabled = example.Control<Toggle>("Solid pillar");
        enabled.Change(false);

        Assert.Equal([1L], garden.Select(item => item.Key));
        Assert.False(shown.Value);
        Assert.False(shown.Enabled);
        Assert.Equal([1L, 2], ring.Select(item => item.Key));
        Assert.True(enabled.Value);
        Assert.True(enabled.Enabled);
        Assert.Equal([1L], example.Scene(Camera, 0).Objects.Select(item => item.Key));
    }

    [Fact]
    public void TheSortNoteSaysWhatTheSwitchDoesNow()
    {
        var example = new SplatsExample(Environment(("ORBLIT_SPLAT_COUNT", "1000")));
        var sorted = example.Control<Toggle>("Sort back to front").Note;

        example.Control<Toggle>("Sort back to front").Change(false);

        Assert.StartsWith("Re-sorted off the render thread", sorted, StringComparison.Ordinal);
        Assert.StartsWith(
            "Drawn in the order generated",
            example.Control<Toggle>("Sort back to front").Note,
            StringComparison.Ordinal);
        Assert.False(Cloud(example, 0).Sorted);
    }

    // Every generator fills its count exactly, so only a direct test reaches
    // these two guards.
    [Fact]
    public void ARecordNotWrittenIsTurnedByTheIdentity() =>
        Assert.Equal(SplatCloud.Pack(new float[6], new float[6], new float[8]), new Records(2).Pack());

    [Fact]
    public void ADiscPastTheLastRecordIsLeftOut()
    {
        var records = new Records(1);
        var disc = new Disc(new Vector3(1, 2, 3), Matrix3.RotationY(0.5), new Vector3(0.1, 0.2, 0.3), new Vector3(1, 0, 0), 0.5);

        records.Put(disc);
        var one = records.Pack();
        records.Put(disc with { At = new Vector3(4, 5, 6) });

        Assert.Equal(one, records.Pack());
    }

    private static SplatCloud Cloud(Example example, double seconds) => Assert.Single(example.Scene(Camera, seconds).Splats);

    private static Func<string, string?> Environment(params (string Name, string Value)[] set) =>
        name => set.SingleOrDefault(pair => pair.Name == name).Value;
}
