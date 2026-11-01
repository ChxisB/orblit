namespace Orblit.Filament.Tests;

/// <summary>
/// What a device can do, read from the answers the renderer
/// sends, and the tier and budgets worked out from them.
/// </summary>
public sealed class DeviceProfileTests
{
    // In Capability order: backend, feature level, texture size, array layers, formats, half
    // float, threads, megabytes.
    private static readonly int[] Desktop = [1, 3, 16384, 2048, 0x3C, 1, 12, 36864];
    private static readonly int[] Simulator = [1, 2, 8192, 2048, 0x3, 1, 12, 36864];
    private static readonly int[] Phone = [2, 3, 4096, 2048, 0x3, 1, 8, 4096];
    private static readonly int[] Browser = [3, 1, 4096, 256, 0x1, 0, 1, 0];

    [Fact]
    public void EveryAnswerLandsInItsOwnField()
    {
        var profile = DeviceProfile.FromCapabilities(Desktop);

        Assert.Equal(Backend.Metal, profile.Api);
        Assert.Equal(3, profile.FeatureLevel);
        Assert.Equal(16384, profile.MaxTextureSize);
        Assert.Equal(2048, profile.MaxArrayTextureLayers);
        Assert.True(profile.HalfFloatTextures);
        Assert.Equal(12, profile.WorkerThreads);
        Assert.Equal(36864, profile.SystemMemoryMegabytes);
    }

    [Fact]
    public void TheFormatBitsBecomeFamilies()
    {
        var profile = DeviceProfile.FromCapabilities(Desktop);

        Assert.Equal(FormatFamily.Bc13 | FormatFamily.Bc45 | FormatFamily.Bc6H | FormatFamily.Bc7, profile.TextureFamilies);
        Assert.False(profile.Supports(FormatFamily.Astc));
    }

    [Fact]
    public void ADeviceThatWillNotSayHowMuchMemoryItHasIsUnknownNotEmpty() =>
        Assert.Null(DeviceProfile.FromCapabilities(Browser).SystemMemoryMegabytes);

    [Fact]
    public void MissingAndUnknownAnswersAreReadAsTheLeastADeviceHas()
    {
        var profile = DeviceProfile.FromCapabilities([-1, -1]);

        Assert.Equal(1, profile.FeatureLevel);
        Assert.Equal(2048, profile.MaxTextureSize);
        Assert.Equal(default, profile.TextureFamilies);
        Assert.Equal(1, profile.WorkerThreads);
        Assert.Equal(DeviceTier.Low, profile.Tier);
    }

    [Fact]
    public void ADesktopThatProvesEverythingIsHigh() =>
        Assert.Equal(DeviceTier.High, DeviceProfile.FromCapabilities(Desktop).Tier);

    [Fact]
    public void AnythingThatCannotDrawTheStandardSurfaceIsLowHoweverManyCoresItHas()
    {
        var profile = DeviceProfile.FromCapabilities(Simulator);

        Assert.True(profile.SlimSurface);
        Assert.Equal(DeviceTier.Low, profile.Tier);
    }

    [Fact]
    public void ACapablePhoneIsMedium() =>
        Assert.Equal(DeviceTier.Medium, DeviceProfile.FromCapabilities(Phone).Tier);

    [Fact]
    public void ABrowserIsLow() =>
        Assert.Equal(DeviceTier.Low, DeviceProfile.FromCapabilities(Browser).Tier);

    [Fact]
    public void HighNeedsMemoryItCanProve()
    {
        var unsaid = (int[])Desktop.Clone();
        unsaid[7] = 0;

        Assert.Equal(DeviceTier.Medium, DeviceProfile.FromCapabilities(unsaid).Tier);
    }

    [Fact]
    public void GrowWithTheTier()
    {
        var low = DeviceProfile.FromCapabilities(Browser);
        var high = DeviceProfile.FromCapabilities(Desktop);

        Assert.True(low.SplatBudget < high.SplatBudget);
        Assert.Equal(0, low.HarmonicDegree);
        Assert.Equal(3, high.HarmonicDegree);
    }

    [Fact]
    public void SortSplatsCoarselyOnlyWhereTheTierIsLow()
    {
        Assert.True(DeviceProfile.FromCapabilities(Browser).CoarseSplatOrder);
        Assert.False(DeviceProfile.FromCapabilities(Phone).CoarseSplatOrder);
        Assert.False(DeviceProfile.FromCapabilities(Desktop).CoarseSplatOrder);
    }

    [Fact]
    public void NeverAskForATextureLargerThanTheDeviceCanHold()
    {
        var tiny = new DeviceProfile { FeatureLevel = 3, MaxTextureSize = 512, WorkerThreads = 4 };

        Assert.Equal(512, tiny.TextureSizeBudget);
    }

    [Fact]
    public void UploadMoreTextureAFrameTheHigherTheTier()
    {
        var low = DeviceProfile.FromCapabilities(Browser);
        var medium = DeviceProfile.FromCapabilities(Phone);
        var high = DeviceProfile.FromCapabilities(Desktop);

        Assert.True(low.TextureUploadKilobytes < medium.TextureUploadKilobytes);
        Assert.True(medium.TextureUploadKilobytes < high.TextureUploadKilobytes);
    }

    [Fact]
    public void TheFilesToFetchAreTheSiblingsOfTheFamiliesTheDeviceHasBestFirstAndTheUniversalFileLast()
    {
        var everything = new DeviceProfile
        {
            TextureFamilies = FormatFamily.Astc | FormatFamily.Bc7 | FormatFamily.Bc45 | FormatFamily.Etc2,
        };

        Assert.Equal(
            ["textures/wood.astc.ktx2", "textures/wood.bc.ktx2", "textures/wood.etc2.ktx2", "textures/wood.ktx2"],
            everything.TextureCandidates("textures/wood.ktx2"));
    }

    [Fact]
    public void LeaveOutAFamilyTheDeviceCannotSample()
    {
        Assert.Equal(["a/b.bc.ktx2", "a/b.ktx2"], DeviceProfile.FromCapabilities(Desktop).TextureCandidates("a/b.ktx2"));
        Assert.Equal(["b.astc.ktx2", "b.etc2.ktx2", "b.ktx2"], DeviceProfile.FromCapabilities(Phone).TextureCandidates("b.ktx2"));

        // A browser with no compressed formats gets the universal file.
        Assert.Equal(["b.ktx2"], DeviceProfile.FromCapabilities([3, 1, 4096, 256, 0, 0, 1, 0]).TextureCandidates("b.ktx2"));
    }

    [Fact]
    public void AreOnlyBcWhenBc7OrBc5IsThereWhichIsWhatABcSiblingHolds()
    {
        var s3tcOnly = new DeviceProfile { TextureFamilies = FormatFamily.Bc13 };

        Assert.Equal(["b.ktx2"], s3tcOnly.TextureCandidates("b.ktx2"));
    }

    [Fact]
    public void KeepTheExtensionAsItWasWritten()
    {
        var astc = new DeviceProfile { TextureFamilies = FormatFamily.Astc };

        Assert.Equal(["B.astc.KTX2", "B.KTX2"], astc.TextureCandidates("B.KTX2"));
    }

    [Fact]
    public void AreJustTheNameForAnythingButACookedSet()
    {
        var profile = DeviceProfile.FromCapabilities(Phone);

        foreach (var path in new[] { "wood.png", "wood.astc.ktx2", "wood.BC.ktx2", "wood.etc2.ktx2", "environment.ktx" })
        {
            Assert.Equal([path], profile.TextureCandidates(path));
        }
    }
}
