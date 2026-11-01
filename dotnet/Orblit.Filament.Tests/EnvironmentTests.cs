using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>The environment, the probes and the field as they cross.</summary>
public sealed class EnvironmentTests
{
    private static RenderScene SceneWith(
        IReadOnlyList<Probe>? probes = null,
        IrradianceField? field = null,
        RenderEnvironment? environment = null) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5)) with
        {
            Probes = probes ?? [],
            Field = field ?? IrradianceField.None,
            Environment = environment ?? RenderEnvironment.None,
        };

    private static float[] Floats(RenderScene scene, string key) =>
        SceneBuilders.Read<float[]>(scene.ToMessage(), key);

    [Fact]
    public void ProbeCrossesWithEverythingTheCaptureNeeds()
    {
        var message = SceneWith(
            probes:
            [
                new Probe
                {
                    Key = 7,
                    Position = new Vector3(1, 2, 3),
                    Radius = 9,
                    Resolution = 128,
                    Version = 4,
                    Layers = 0x0F,
                    Intensity = 2,
                },
            ]).ToMessage();

        Assert.Equal([7L], SceneBuilders.Read<long[]>(message, "probeKeys"));
        var packed = SceneBuilders.Read<float[]>(message, "probeParams");
        Assert.Equal(Probe.Stride, packed.Length);
        // Where it is taken from, how far it reaches, the size of a face, the version, which
        // layers it draws and how much of it counts.
        SceneBuilders.NearAll(packed, 1, 2, 3, 9, 128, 4, 0x0F, 2);
    }

    [Fact]
    public void ASceneWithNoProbesStillSaysSo()
    {
        // Empty and not absent: the far side walks these arrays, and a key missing from the
        // message is a different thing to a key that is there and empty.
        var message = SceneWith().ToMessage();

        Assert.Empty(SceneBuilders.Read<long[]>(message, "probeKeys"));
        Assert.Empty(SceneBuilders.Read<float[]>(message, "probeParams"));
    }

    [Fact]
    public void TheProbeVersionIsWhatAsksForANewPhotograph()
    {
        // Six renders of the whole scene is not a per frame cost, so a probe is captured and
        // kept. The version is the only thing that says otherwise, and two probes alike but for
        // it have to differ on the wire.
        float VersionOn(int version) => Floats(
            SceneWith(probes: [new Probe { Key = 1, Position = Vector3.Zero(), Version = version }]),
            "probeParams")[5];

        Assert.NotEqual(VersionOn(0), VersionOn(1));
    }

    [Fact]
    public void AProbeIsPhotographedAtTwoHundredAndFiftySixByDefault()
    {
        Assert.Equal(256, new Probe { Key = 1, Position = Vector3.Zero() }.Resolution);
    }

    [Fact]
    public void AFieldThatSaysNothingStillSendsItsNumbers()
    {
        // The renderer reads a fixed stride whether or not there is a field, so off has to be a
        // number in the block and not an absent block.
        var packed = Floats(SceneWith(), "fieldParams");

        Assert.Equal(IrradianceField.Stride, packed.Length);
        Assert.Equal(0f, packed[0]);
    }

    [Fact]
    public void TheLatticeCrossesInTheOrderTheRendererReadsIt()
    {
        var packed = Floats(
            SceneWith(
                field: new IrradianceField
                {
                    Enabled = true,
                    Origin = new Vector3(-4, -2, -4),
                    Spacing = new Vector3(1.5, 2, 2.5),
                    Counts = new Vector3(5, 4, 6),
                    Intensity = 1.6,
                    Retention = 0.9,
                    Bias = 0.3,
                }),
            "fieldParams");

        Assert.Equal(1f, packed[0]);
        // The corner probe, the metres between, how many, how much reaches, what survives a
        // frame and the normal bias.
        SceneBuilders.NearAll(packed[1..13], -4, -2, -4, 1.5, 2, 2.5, 5, 4, 6, 1.6, 0.9, 0.3);
    }

    [Fact]
    public void TheFieldNamesTheTargetItFillsItselfFrom()
    {
        // A field is built by reading the picture the scene drew, so there has to be one to
        // read. The name travels with it.
        var message = SceneWith(field: new IrradianceField { Enabled = true, From = "lit" }).ToMessage();

        Assert.Equal("lit", message["fieldFrom"]);
    }

    [Fact]
    public void TheFieldProbeCountIsTheLatticeMultipliedOut()
    {
        Assert.Equal(120, new IrradianceField { Counts = new Vector3(5, 4, 6) }.ProbeCount);
    }

    [Fact]
    public void NothingNamedIsNothingSet()
    {
        Assert.False(RenderEnvironment.None.IsSet);
        Assert.False(new RenderEnvironment { Radiance = "" }.IsSet);
    }

    [Fact]
    public void EitherHalfOnItsOwnIsARealEnvironment()
    {
        // Light with no visible sky is what an interior lit through a window wants, and a
        // backdrop that lights nothing is a matte painting.
        Assert.True(new RenderEnvironment { Radiance = "/k_ibl.ktx" }.IsSet);
        Assert.True(new RenderEnvironment { Skybox = "/k_skybox.ktx" }.IsSet);
    }

    [Fact]
    public void TheEnvironmentIsStatedInTheSameUnitAsEveryOtherLight()
    {
        var outdoors = new RenderEnvironment { Radiance = "/day_ibl.ktx", Intensity = 80000 };

        Assert.Equal(80000f, outdoors.Packed[0]);
    }

    [Fact]
    public void TurningTheEnvironmentIsWhatMakesOneBakeServeAnyScene()
    {
        var turned = new RenderEnvironment { Radiance = "/a.ktx", Rotation = 1.5 };

        Assert.Equal(1.5f, turned.Packed[1]);
    }

    [Fact]
    public void APictureNamesItselfAsBothTheLightAndTheBackdrop()
    {
        // One image is both halves: the renderer filters the reflections and converts the
        // backdrop out of the same decode.
        var picture = RenderEnvironment.FromImage("/env/kitchen.hdr");

        Assert.Equal("/env/kitchen.hdr", picture.Radiance);
        Assert.Equal("/env/kitchen.hdr", picture.Skybox);
        Assert.True(picture.IsSet);
        // The default constructor's intensity, and the device chooses the size.
        Assert.Equal(30000, picture.Intensity);
        Assert.True(picture.ShowSkybox);
        Assert.Equal(0, picture.Size);
    }

    [Fact]
    public void TheSizeAPictureIsFilteredAtCrossesAsTheFourthFloat()
    {
        // Nought is what a baked environment sends, and means the device's own choice.
        Assert.Equal(0f, new RenderEnvironment { Radiance = "/a.ktx" }.Packed[3]);
        var small = RenderEnvironment.FromImage("/a.exr") with { Size = 128 };
        Assert.Equal(RenderEnvironment.Stride, small.Packed.Length);
        Assert.Equal(128f, small.Packed[3]);
    }

    [Fact]
    public void TurningOrDimmingAPictureKeepsItsSize()
    {
        // A size lost when a volume blends intensity and rotation is the picture filtered again
        // at another size.
        var picture = RenderEnvironment.FromImage("/a.hdr") with { Size = 64 };
        var turned = picture with { Rotation = 1, Intensity = 5000 };

        Assert.Equal(64, turned.Size);
        Assert.Equal("/a.hdr", turned.Radiance);
        Assert.Equal(32, (picture with { Size = 32 }).Size);
    }

    [Fact]
    public void TheBackdropCanBeSampledWithoutBeingDrawn()
    {
        var hidden = new RenderEnvironment { Radiance = "/a.ktx", Skybox = "/a_sky.ktx", ShowSkybox = false };

        Assert.Equal(0f, hidden.Packed[2]);
        Assert.Equal(1f, new RenderEnvironment { Radiance = "/a.ktx" }.Packed[2]);
    }

    [Fact]
    public void ASceneThatSaysNothingSendsAnEnvironmentOfNothing()
    {
        var message = SceneWith().ToMessage();

        Assert.Equal("", message["environmentRadiance"]);
        Assert.Equal("", message["environmentSkybox"]);
        // The numbers still cross, so the renderer never reads a short array.
        Assert.Equal(RenderEnvironment.Stride, SceneBuilders.Read<float[]>(message, "environmentParams").Length);
    }

    [Fact]
    public void ThePathsCrossAsPathsAndTheNumbersAsNumbers()
    {
        var message = SceneWith(
            environment: new RenderEnvironment
            {
                Radiance = "/env/kitchen_ibl.ktx",
                Skybox = "/env/kitchen_skybox.ktx",
                Intensity = 12000,
            }).ToMessage();

        Assert.Equal("/env/kitchen_ibl.ktx", message["environmentRadiance"]);
        Assert.Equal("/env/kitchen_skybox.ktx", message["environmentSkybox"]);
        Assert.Equal(12000f, SceneBuilders.Read<float[]>(message, "environmentParams")[0]);
    }

    [Fact]
    public void ChangingOnlyTheBrightnessLeavesTheFilesAlone()
    {
        // What the renderer leans on to avoid reading a cubemap off disk on every frame of a
        // slider drag.
        var before = new RenderEnvironment { Radiance = "/a.ktx", Intensity = 1000 };
        var after = before with { Intensity = 2000 };

        Assert.Equal(before.Radiance, after.Radiance);
        Assert.Equal(2000f, after.Packed[0]);
    }
}
