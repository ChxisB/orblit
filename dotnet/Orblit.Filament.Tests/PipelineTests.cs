namespace Orblit.Filament.Tests;

/// <summary>The quality dials as the renderer receives them.</summary>
public sealed class PipelineTests
{
    private static readonly Detail[] Details = Enum.GetValues<Detail>();

    [Fact]
    public void TheDefaultPipelineDrawsShadowsAtEveryPixel()
    {
        var pipeline = new Pipeline();

        Assert.True(pipeline.Shadows.Enabled);
        Assert.Equal(1.0, pipeline.Resolution.Scale);
        Assert.False(pipeline.Resolution.Adaptive);
        // Multisampling is opt-in.
        Assert.Equal(1, pipeline.Samples);
        Assert.True(pipeline.Culling);
    }

    [Fact]
    public void TheFourNamedSettingsDifferOnlyInHowMuchNotInWhat()
    {
        var tiers = Details.Select(Pipeline.At).ToArray();

        // Nothing appears or disappears between them: a scene authored for one is the same scene
        // on all four.
        Assert.All(tiers, one => Assert.True(one.Shadows.Enabled));
        Assert.All(tiers, one => Assert.True(one.Culling));
        Assert.All(tiers, one => Assert.True(one.Refraction));

        // Each tier doubles.
        Assert.Equal([512, 1024, 2048, 4096], tiers.Select(one => one.Shadows.MapSize));
        Assert.Equal([1, 2, 3, 4], tiers.Select(one => one.Shadows.Cascades));
    }

    [Fact]
    public void OnlyTheLowestSettingIsAllowedToShrinkTheFrame()
    {
        Assert.True(Pipeline.At(Detail.Low).Resolution.Adaptive);
        foreach (var detail in new[] { Detail.Medium, Detail.High, Detail.Ultra })
        {
            // Every other setting holds its resolution.
            Assert.False(Pipeline.At(detail).Resolution.Adaptive);
        }
    }

    [Fact]
    public void AFixedScalePacksAsARangeWithNowhereToGo()
    {
        var fixedScale = new Pipeline { Resolution = new Resolution { Scale = 0.75 } }.Packed;

        // Not adaptive.
        Assert.Equal(0f, fixedScale[10]);
        Assert.Equal(0.75, fixedScale[11], 1e-6);
        Assert.Equal(0.75, fixedScale[12], 1e-6);

        var moving = new Pipeline
        {
            Resolution = new Resolution { Adaptive = true, MinScale = 0.6, MaxScale = 1 },
        }.Packed;
        Assert.Equal(1f, moving[10]);
        Assert.Equal(0.6, moving[11], 1e-6);
        Assert.Equal(1f, moving[12]);
    }

    [Fact]
    public void TheShadowFlagsPackTogether()
    {
        Assert.Equal(0f, new Pipeline().Packed[8]);
        Assert.Equal(3f, new Pipeline { Shadows = new ShadowSettings { Stable = true, Contact = true } }.Packed[8]);
        Assert.Equal(2f, new Pipeline { Shadows = new ShadowSettings { Contact = true } }.Packed[8]);
    }

    [Fact]
    public void TheVarianceFlagsPackBesideTheOlderTwo()
    {
        var packed = new Pipeline
        {
            Shadows = new ShadowSettings
            {
                Stable = true,
                Variance = new VarianceShadows { HighPrecision = true, Mipmapping = true, Exponential = true },
            },
        }.Packed;

        Assert.Equal(1 + 4 + 8 + 16, packed[8]);
    }

    [Fact]
    public void TheDefaultsAreTheOnesTheRendererHadBefore()
    {
        // A scene written before these dials existed must draw the same, so every default here is
        // the renderer's own.
        var packed = new Pipeline().Packed;

        Assert.Equal(30, packed.Length);
        // The split lambda places them.
        Assert.Equal([0f, 0f, 0f], packed[18..21]);
        // A physical penumbra falloff, no anisotropy, no blur.
        Assert.Equal(1f, packed[21]);
        Assert.Equal(0f, packed[22]);
        Assert.Equal(0f, packed[23]);
        Assert.Equal(0.15, packed[24], 1e-6);
        // One sample.
        Assert.Equal(1f, packed[25]);
        Assert.Equal(0.3, packed[26], 1e-6);
        Assert.Equal(8f, packed[27]);
        // The device's own textures.
        Assert.Equal([0f, 0f], packed[28..]);
    }

    [Fact]
    public void CascadeSplitsAndTheContactTraceLandWhereTheRendererLooks()
    {
        var packed = new Pipeline
        {
            Shadows = new ShadowSettings
            {
                Cascades = 4,
                Splits = [0.1, 0.3, 0.6],
                SoftnessFalloff = 2,
                ContactDistance = 0.8,
                ContactSteps = 16,
                Variance = new VarianceShadows { Blur = 3, Anisotropy = 2, Samples = 4, LightBleedReduction = 0.4 },
            },
        }.Packed;

        Assert.Equal(0.1, packed[18], 1e-6);
        Assert.Equal(0.3, packed[19], 1e-6);
        Assert.Equal(0.6, packed[20], 1e-6);
        Assert.Equal(2f, packed[21]);
        Assert.Equal(2f, packed[22]);
        Assert.Equal(3f, packed[23]);
        Assert.Equal(0.4, packed[24], 1e-6);
        Assert.Equal(4f, packed[25]);
        Assert.Equal(0.8, packed[26], 1e-6);
        Assert.Equal(16f, packed[27]);
    }

    [Fact]
    public void AShortSplitListFillsOnlyWhatItHas()
    {
        var packed = new Pipeline { Shadows = new ShadowSettings { Cascades = 2, Splits = [0.25] } }.Packed;

        Assert.Equal([0.25f, 0f, 0f], packed[18..21]);
    }

    [Fact]
    public void TheViewFlagsPackTogether()
    {
        // Culling and refraction.
        Assert.Equal(6f, new Pipeline().Packed[15]);
        Assert.Equal(1f, new Pipeline { Precise = true, Culling = false, Refraction = false }.Packed[15]);
    }

    [Fact]
    public void TextureLimitsAreTheDevicesOwnUnlessGivenWhichTravelsAsNought()
    {
        var packed = new Pipeline().Packed;

        Assert.Equal(0f, packed[28]);
        Assert.Equal(0f, packed[29]);
    }

    [Fact]
    public void TextureLimitsTravelAfterTheShadowsWhenGiven()
    {
        var packed = new Pipeline { Textures = new TextureLimits { MaxSize = 1024, UploadKilobytes = 8192 } }.Packed;

        Assert.Equal(1024f, packed[28]);
        Assert.Equal(8192f, packed[29]);
    }

    [Fact]
    public void TextureLimitsAreHeldToWhatAFloatCarriesExactly()
    {
        var packed = new Pipeline { Textures = new TextureLimits { MaxSize = -5, UploadKilobytes = 1 << 30 } }.Packed;

        Assert.Equal(0f, packed[28]);
        Assert.Equal((float)(1 << 24), packed[29]);
    }

    [Fact]
    public void ASceneCarriesAPipelineWhetherOrNotOneWasGiven()
    {
        var camera = SceneBuilders.CameraAt(0, 0, 5);
        var packed = SceneBuilders.Read<float[]>(SceneBuilders.Of([], camera).ToMessage(), "pipelineParams");
        Assert.Equal(Pipeline.Stride, packed.Length);

        var ultra = SceneBuilders.Of([], camera) with { Pipeline = Pipeline.At(Detail.Ultra) };
        Assert.Equal(4096f, SceneBuilders.Read<float[]>(ultra.ToMessage(), "pipelineParams")[2]);
    }

    [Fact]
    public void TheShadowKindTravelsAsItsOwnIndex()
    {
        foreach (var kind in Enum.GetValues<ShadowKind>())
        {
            var packed = new Pipeline { Shadows = new ShadowSettings { Kind = kind } }.Packed;

            Assert.Equal((float)(int)kind, packed[1]);
        }
    }

    [Fact]
    public void AHandheldIs1280By800AndThatIsNotSixteenByNine()
    {
        var deck = Display.Handheld;

        Assert.Equal(1280, deck.Width);
        Assert.Equal(800, deck.Height);
        Assert.Equal(1024000, deck.Pixels);
        // Sixteen by ten. A camera framed for sixteen by nine is stretched or cropped here.
        Assert.Equal(1.6, deck.Aspect, 1e-9);
        Assert.NotEqual(16.0 / 9, deck.Aspect, 1e-3);
    }

    [Fact]
    public void ASmallPanelGetsAShadowMapMatchedToIt()
    {
        // A shadow map is only as useful as the screen pixels it is stretched over, so the map a
        // 1440p frame needs is resolution a handheld cannot show.
        var named = Pipeline.At(Detail.High);
        var deck = Pipeline.ForDisplay(new Display(1280, 800, Detail.High));

        Assert.Equal(2048, named.Shadows.MapSize);
        Assert.Equal(1024, deck.Shadows.MapSize);
        Assert.Equal(named.Shadows.Cascades - 1, deck.Shadows.Cascades);
    }

    [Fact]
    public void TheMapNeverFallsBelowTheLowestSettingTheEngineHas()
    {
        // Low is already at 512, and halving it again would invent a rung.
        var deck = Pipeline.ForDisplay(new Display(1280, 800, Detail.Low));

        Assert.Equal(512, deck.Shadows.MapSize);
        // One cascade stays one.
        Assert.Equal(1, deck.Shadows.Cascades);
    }

    [Fact]
    public void TheFloorUnderAShrinkingFrameRisesOnASmallPanel()
    {
        // Half scale at 1440p is still 1280 by 720 of real pixels; half scale at 1280 by 800 is
        // 640 by 400, which is soft rather than slightly soft.
        var deck = Pipeline.ForDisplay(new Display(1280, 800, Detail.Low));

        Assert.True(deck.Resolution.Adaptive);
        Assert.Equal(0.7, deck.Resolution.MinScale, 1e-9);
        Assert.Equal(0.5, Pipeline.At(Detail.Low).Resolution.MinScale);
    }

    [Fact]
    public void MultisamplingComesOffSinceBandwidthIsWhatItCosts()
    {
        foreach (var detail in new[] { Detail.High, Detail.Ultra })
        {
            Assert.Equal(4, Pipeline.At(detail).Samples);
            // On a handheld.
            Assert.Equal(1, Pipeline.ForDisplay(new Display(1280, 800, detail)).Samples);
        }
    }

    [Fact]
    public void ALargeDisplayIsLeftExactlyAsTheNamedSettingHadIt()
    {
        // The profile is the display's half of the question only. A full sized screen settles
        // nothing, so nothing is changed.
        foreach (var detail in Details)
        {
            var named = Pipeline.At(detail);
            var big = Pipeline.ForDisplay(new Display(2560, 1440, detail));

            Assert.Equal(named.Shadows.MapSize, big.Shadows.MapSize);
            Assert.Equal(named.Shadows.Cascades, big.Shadows.Cascades);
            Assert.Equal(named.Samples, big.Samples);
            Assert.Equal(named.Resolution.MinScale, big.Resolution.MinScale);
        }
    }

    [Fact]
    public void NothingAppearsOrDisappearsOnAHandheld()
    {
        // The same promise the four named settings make: a scene authored once is the same scene
        // here, drawn with smaller numbers.
        foreach (var detail in Details)
        {
            var deck = Pipeline.ForDisplay(new Display(1280, 800, detail));

            Assert.True(deck.Shadows.Enabled);
            Assert.True(deck.Culling);
            Assert.True(deck.Refraction);
            Assert.Equal(Pipeline.Stride, deck.Packed.Length);
        }
    }
}
