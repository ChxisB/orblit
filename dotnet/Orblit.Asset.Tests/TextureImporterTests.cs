namespace Orblit.Asset.Tests;

public sealed class TextureImporterTests : NativeToolWork
{
    private static readonly TextureImporter Plain = new();

    private static IReadOnlyDictionary<string, object?> Resolve(TextureImporter importer, Dictionary<string, object?>? values = null) =>
        importer.ResolveSettings(new ImportSettings(values: values));

    private static CookTarget Target(string name, string property, object? value) =>
        new(name, new Dictionary<string, object?> { [property] = value });

    private TextureImporter ImporterWriting(string files) => new(Fake(files));

    [ShellFact]
    public async Task WritesOnlyTheFamiliesTheTargetAsksFor()
    {
        var importer = ImporterWriting("writeInto=1:wall.astc.ktx2");
        var result = await importer.ImportAsync(RequestFor(
            "textures/wall.png", Resolve(importer), Target("ios", "textureFamilies", new object?[] { "astc" })));
        AssertInOrder(OnlyRun(), "--targets", "astc");
        Assert.Equal(["wall.astc.ktx2"], result.Outputs.Keys);
    }

    [ShellFact]
    public async Task ATargetThatNamesNoFamiliesGetsAllOfThemWhichIsWhatAProjectWideCookWants()
    {
        var importer = ImporterWriting("writeInto=1:wall.ktx2");
        await importer.ImportAsync(RequestFor("wall.png", Resolve(importer)));
        var run = OnlyRun();
        var targets = run[run.IndexOf("--targets") + 1].Split(',');
        Assert.Subset(targets.ToHashSet(), new HashSet<string> { "astc", "bc", "etc2", "basis" });
    }

    [ShellFact]
    public async Task PassesTheSettingsThroughAsFlags()
    {
        var importer = ImporterWriting("writeInto=1:n.ktx2");
        var settings = Resolve(
            importer,
            new Dictionary<string, object?>
            {
                ["normal"] = true,
                ["twoChannelNormals"] = true,
                ["wrap"] = true,
                ["mips"] = false,
                ["colourSpace"] = "linear",
                ["cutout"] = 0.5,
                ["maxSize"] = 1024L,
                ["uastc"] = 4L,
                ["zstd"] = 22L,
            });
        await importer.ImportAsync(RequestFor("n.png", settings));
        var run = OnlyRun();
        foreach (var flag in new[] { "--normal", "--two-channel-normals", "--wrap", "--no-mips", "--linear" })
        {
            Assert.Contains(flag, run);
        }

        AssertInOrder(run, "--cutout", "0.5");
        AssertInOrder(run, "--max-size", "1024");
        AssertInOrder(run, "--uastc", "4");
        AssertInOrder(run, "--zstd", "22");
    }

    [ShellFact]
    public async Task MipsAreOnUnlessTurnedOffBecauseATextureWithoutThemShimmers()
    {
        var importer = ImporterWriting("writeInto=1:a.ktx2");
        await importer.ImportAsync(RequestFor("a.png", Resolve(importer)));
        Assert.DoesNotContain("--no-mips", OnlyRun());
    }

    [ShellFact]
    public async Task TheTargetsSizeLimitAppliesWhenTheSettingsDoNotSetOne()
    {
        var importer = ImporterWriting("writeInto=1:a.ktx2");
        var result = await importer.ImportAsync(RequestFor(
            "a.png", Resolve(importer), Target("web", "maxTextureSize", 2048L)));
        AssertInOrder(OnlyRun(), "--max-size", "2048");
        Assert.Contains("2048", Assert.Single(result.Notes));
    }

    [ShellFact]
    public async Task ASettingBeatsTheTargetsLimitAndSaysNothingAboutIt()
    {
        var importer = ImporterWriting("writeInto=1:a.ktx2");
        var settings = Resolve(importer, new Dictionary<string, object?> { ["maxSize"] = 512L });
        var result = await importer.ImportAsync(RequestFor("a.png", settings, Target("web", "maxTextureSize", 2048L)));
        AssertInOrder(OnlyRun(), "--max-size", "512");
        Assert.Empty(result.Notes);
    }

    [ShellFact]
    public async Task LosslessAsksForNoFamilyAtAll()
    {
        var importer = ImporterWriting("writeInto=1:a.ktx2");
        await importer.ImportAsync(RequestFor("a.png", Resolve(importer, new Dictionary<string, object?> { ["lossless"] = true })));
        Assert.Contains("--lossless", OnlyRun());
        Assert.DoesNotContain("--targets", OnlyRun());
    }

    [ShellFact]
    public async Task AFamilyNobodyHasHeardOfIsRefusedBeforeAnythingRuns()
    {
        var importer = ImporterWriting("");
        var target = Target("odd", "textureFamilies", new object?[] { "astc", "dxt1" });
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => importer.ImportAsync(RequestFor("a.png", Resolve(importer), target)));
        Assert.Contains("dxt1", failure.Reason);
        Assert.Empty(Runs());
    }

    [ShellFact]
    public async Task ACookThatWritesNothingIsAFailureNotAnEmptyAsset()
    {
        var importer = ImporterWriting("");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => importer.ImportAsync(RequestFor("a.png", Resolve(importer))));
        Assert.Contains("no files", failure.Reason);
    }

    [Fact]
    public void SettingsFillInTheirDefaultsSoAKeyCoversThem()
    {
        Assert.Equal(true, Resolve(Plain)["mips"]);
        Assert.Equal(2L, Resolve(Plain)["uastc"]);
    }

    [Fact]
    public void SettingsReadColorSpaceAsWellAsColourSpaceBecauseBothSpellingsGetTyped()
    {
        var resolved = Resolve(Plain, new Dictionary<string, object?> { ["colorSpace"] = "srgb" });
        Assert.Equal("srgb", resolved["colourSpace"]);
    }

    [Fact]
    public void SettingsRefuseAMaxSizeThatIsNotAPowerOfTwo() =>
        Assert.Throws<FormatException>(() => Resolve(Plain, new Dictionary<string, object?> { ["maxSize"] = 1000L }));

    [Fact]
    public void SettingsRefuseACutoutOutsideZeroToOne() =>
        Assert.Throws<FormatException>(() => Resolve(Plain, new Dictionary<string, object?> { ["cutout"] = 2L }));

    [Fact]
    public void SettingsRefuseAFlagThatIsNotTrueOrFalse() =>
        Assert.Throws<FormatException>(() => Resolve(Plain, new Dictionary<string, object?> { ["normal"] = "yes" }));

    [Fact]
    public void SettingsReadAWholeNumberCutoutAsTheFractionItMeans() =>
        Assert.Equal(1.0, Assert.IsType<double>(Resolve(Plain, new Dictionary<string, object?> { ["cutout"] = 1L })["cutout"]));
}
