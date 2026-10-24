namespace Orblit.Filament.Tests;

public sealed class DeviceProfileAddedTests
{
    // A device that is high on every count, to move one count at a time.
    private static DeviceProfile High() =>
        new()
        {
            FeatureLevel = 3,
            MaxTextureSize = 8192,
            WorkerThreads = 8,
            SystemMemoryMegabytes = 8192,
        };

    [Fact]
    public void AnUntouchedProfileIsTheLeastADeviceHas()
    {
        var profile = new DeviceProfile();

        Assert.Equal(Backend.Default, profile.Api);
        Assert.Equal(1, profile.FeatureLevel);
        Assert.Equal(2048, profile.MaxTextureSize);
        Assert.Equal(256, profile.MaxArrayTextureLayers);
        Assert.Equal(default, profile.TextureFamilies);
        Assert.False(profile.HalfFloatTextures);
        Assert.Equal(1, profile.WorkerThreads);
        Assert.Null(profile.SystemMemoryMegabytes);
        Assert.Equal(DeviceTier.Low, profile.Tier);
    }

    [Fact]
    public void OnlyAFeatureLevelUnderThreeDrawsTheSlimSurface()
    {
        Assert.True(new DeviceProfile { FeatureLevel = 2 }.SlimSurface);
        Assert.False(new DeviceProfile { FeatureLevel = 3 }.SlimSurface);
    }

    [Fact]
    public void TheEdgesOfHighAreHigh() => Assert.Equal(DeviceTier.High, High().Tier);

    [Fact]
    public void OneStepUnderAnyEdgeOfHighIsMedium()
    {
        Assert.Equal(DeviceTier.Medium, (High() with { MaxTextureSize = 8191 }).Tier);
        Assert.Equal(DeviceTier.Medium, (High() with { WorkerThreads = 7 }).Tier);
        Assert.Equal(DeviceTier.Medium, (High() with { SystemMemoryMegabytes = 8191 }).Tier);
    }

    [Fact]
    public void OneStepUnderAnyEdgeOfMediumIsLow()
    {
        Assert.Equal(DeviceTier.Medium, (High() with { MaxTextureSize = 4096 }).Tier);
        Assert.Equal(DeviceTier.Low, (High() with { MaxTextureSize = 4095 }).Tier);
        Assert.Equal(DeviceTier.Medium, (High() with { WorkerThreads = 3 }).Tier);
        Assert.Equal(DeviceTier.Low, (High() with { WorkerThreads = 2 }).Tier);
        Assert.Equal(DeviceTier.Medium, (High() with { SystemMemoryMegabytes = 3072 }).Tier);
        Assert.Equal(DeviceTier.Low, (High() with { SystemMemoryMegabytes = 3071 }).Tier);
        Assert.Equal(DeviceTier.Low, (High() with { FeatureLevel = 2 }).Tier);
    }

    [Fact]
    public void ADeviceThatHidesItsMemoryCanStillBeMedium() =>
        Assert.Equal(DeviceTier.Medium, (High() with { SystemMemoryMegabytes = null }).Tier);

    [Fact]
    public void EachTierHasItsOwnBudgets()
    {
        var low = High() with { FeatureLevel = 2, MaxTextureSize = 16384 };
        var medium = High() with { WorkerThreads = 4, MaxTextureSize = 16384 };
        var high = High() with { MaxTextureSize = 16384 };

        Assert.Equal([1024, 2048, 4096], new[] { low, medium, high }.Select(profile => profile.TextureSizeBudget));
        Assert.Equal([4096, 16384, 32768], new[] { low, medium, high }.Select(profile => profile.TextureUploadKilobytes));
        Assert.Equal([250_000, 1_000_000, 3_000_000], new[] { low, medium, high }.Select(profile => profile.SplatBudget));
        Assert.Equal([0, 2, 3], new[] { low, medium, high }.Select(profile => profile.HarmonicDegree));
    }

    [Fact]
    public void ABackendTheEngineDoesNotKnowIsTheDefault()
    {
        Assert.Equal(Backend.Default, DeviceProfile.FromCapabilities([5]).Api);
        Assert.Equal(Backend.Default, DeviceProfile.FromCapabilities([-1]).Api);
    }

    [Theory]
    [InlineData(0, Backend.Default)]
    [InlineData(1, Backend.Metal)]
    [InlineData(2, Backend.Vulkan)]
    [InlineData(3, Backend.OpenGl)]
    [InlineData(4, Backend.WebGpu)]
    public void EveryBackendIsReadByItsNumber(int answer, Backend expected) =>
        Assert.Equal(expected, DeviceProfile.FromCapabilities([answer]).Api);

    [Fact]
    public void ABitThatIsNoFamilyIsDropped() =>
        Assert.Equal(
            FormatFamily.Etc2 | FormatFamily.Astc | FormatFamily.Bc13 | FormatFamily.Bc45 | FormatFamily.Bc6H | FormatFamily.Bc7,
            DeviceProfile.FromCapabilities([0, 0, 0, 0, 0xFF]).TextureFamilies);

    [Fact]
    public void OnlyAOneMeansHalfFloatTexturesAreThere()
    {
        Assert.False(DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0]).HalfFloatTextures);
        Assert.True(DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 1]).HalfFloatTextures);
        Assert.False(DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 2]).HalfFloatTextures);
    }

    [Fact]
    public void ADeviceWithNoThreadsHasOne()
    {
        Assert.Equal(1, DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0, 0]).WorkerThreads);
        Assert.Equal(1, DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0, -1]).WorkerThreads);
        Assert.Equal(2, DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0, 2]).WorkerThreads);
    }

    [Fact]
    public void MemoryThatIsNegativeOrNoughtIsUnknown()
    {
        Assert.Null(DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0, 0, -1]).SystemMemoryMegabytes);
        Assert.Null(DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0, 0, 0]).SystemMemoryMegabytes);
        Assert.Equal(1, DeviceProfile.FromCapabilities([0, 0, 0, 0, 0, 0, 0, 1]).SystemMemoryMegabytes);
    }

    [Fact]
    public void AListWithNoAnswersReadsAsTheLeastADeviceHas()
    {
        var profile = DeviceProfile.FromCapabilities([]);

        Assert.Equal(new DeviceProfile(), profile);
    }

    [Fact]
    public void ANoughtIsAnAnswerAndNotAGap()
    {
        var profile = DeviceProfile.FromCapabilities([0, 0, 0, 0]);

        Assert.Equal(0, profile.FeatureLevel);
        Assert.Equal(0, profile.MaxTextureSize);
        Assert.Equal(0, profile.MaxArrayTextureLayers);
    }

    [Fact]
    public void EitherBcFamilyAloneBringsTheBcSibling()
    {
        Assert.Equal(["b.bc.ktx2", "b.ktx2"], new DeviceProfile { TextureFamilies = FormatFamily.Bc7 }.TextureCandidates("b.ktx2"));
        Assert.Equal(["b.bc.ktx2", "b.ktx2"], new DeviceProfile { TextureFamilies = FormatFamily.Bc45 }.TextureCandidates("b.ktx2"));
        Assert.Equal(["b.ktx2"], new DeviceProfile { TextureFamilies = FormatFamily.Bc6H }.TextureCandidates("b.ktx2"));
        Assert.Equal(["b.etc2.ktx2", "b.ktx2"], new DeviceProfile { TextureFamilies = FormatFamily.Etc2 }.TextureCandidates("b.ktx2"));
    }

    [Fact]
    public void ASiblingNameInAnyCaseIsAlreadyASibling()
    {
        var everything = new DeviceProfile { TextureFamilies = FormatFamily.Astc | FormatFamily.Etc2 };

        Assert.Equal(["wood.ASTC.ktx2"], everything.TextureCandidates("wood.ASTC.ktx2"));
        Assert.Equal(["wood.Etc2.KTX2"], everything.TextureCandidates("wood.Etc2.KTX2"));
    }

    [Fact]
    public void ANameThatOnlyContainsASiblingWordStillGetsSiblings()
    {
        var astc = new DeviceProfile { TextureFamilies = FormatFamily.Astc };

        Assert.Equal(["astc.map.astc.ktx2", "astc.map.ktx2"], astc.TextureCandidates("astc.map.ktx2"));
    }

    [RendererFact]
    public void ARunningRendererAnswersWithAProfileItsOwnAnswersBuild()
    {
        using var renderer = new Renderer(64, 48);

        var profile = renderer.GetProfile();

        Assert.Equal(renderer.Backend, profile.Api);
        Assert.Equal(renderer.GetCapability(Capability.MaxTextureSize), profile.MaxTextureSize);
        Assert.Equal(renderer.GetCapability(Capability.WorkerThreads), profile.WorkerThreads);
        Assert.Equal((FormatFamily)renderer.GetCapability(Capability.CompressedFormats), profile.TextureFamilies);
    }
}
