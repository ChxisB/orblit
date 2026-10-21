using System.Text;

namespace Orblit.Asset.Tests;

// Edges the Dart tests leave open: the name rules, settings laid over a file's own, layered sources and a cache
// that has dropped bytes.
public sealed class RuntimeImportAddedTests
{
    private readonly MemoryCookCache _cache = new();

    private static AssetId Id(string text) => AssetId.Parse(text);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static ImportSettings Tint(string tint) =>
        new(values: new Dictionary<string, object?> { ["tint"] = tint });

    private RuntimeImport Counting(ICookCache? cache = null) =>
        new(cache ?? _cache, CookTargets.Macos, new ImporterRegistry([new CountingImporter()]));

    // A source holding the settings file a project would keep beside robot.glb.
    private static MemoryAssetSource ShippedWith(string tint) =>
        new(new Dictionary<AssetId, byte[]> { [Id("robot.glb.import.json")] = Bytes(Tint(tint).Encode()) });

    private static async Task<string> TextOf(RuntimeImport runtime, CookResult result) =>
        Encoding.UTF8.GetString((await runtime.ReadOnlyAsync(result.Asset!))!);

    [Theory]
    [InlineData("  robot.glb  ", "robot.glb")]
    [InlineData("models/robot.glb/..", "robot.glb")]
    [InlineData("models/robot.glb/.", "robot.glb")]
    [InlineData("models/robot.glb/\uFEFF", "robot.glb")]
    [InlineData("a:b:c.glb", "a_b_c.glb")]
    [InlineData("C:/Users\\chris/robot.glb", "robot.glb")]
    [InlineData("robot.glb#a?b", "robot.glb")]
    [InlineData("https://example.com/robot.glb?next=/other.glb", "robot.glb")]
    [InlineData("a/b/c.glb", "c.glb")]
    [InlineData("dir/x", "x")]
    public void NameForTakesTheLastSegmentThatHasAName(string path, string name) =>
        Assert.Equal(Id(name), RuntimeImport.NameFor(path));

    [Theory]
    [InlineData("")]
    [InlineData("?v=2")]
    [InlineData("#frag")]
    [InlineData("/ / /")]
    [InlineData("./.")]
    [InlineData("\\")]
    public void NameForGivesNothingWhenNoSegmentHasAName(string path) => Assert.Null(RuntimeImport.NameFor(path));

    [Fact]
    public void WithNoImportersItBringsTheDefaultOnes()
    {
        var runtime = new RuntimeImport(_cache, CookTargets.Macos);

        Assert.True(runtime.Handles(Id("robot.gltf")));
        Assert.False(runtime.Handles(Id("notes.txt")));
    }

    [Fact]
    public void EachCallToDefaultImportersMakesItsOwnRegistry() =>
        Assert.NotSame(RuntimeImport.DefaultImporters(), RuntimeImport.DefaultImporters());

    [Fact]
    public void ItHoldsWhatItWasGiven()
    {
        var registry = new ImporterRegistry([new CountingImporter()]);

        var runtime = new RuntimeImport(_cache, CookTargets.Web, registry);

        Assert.Same(_cache, runtime.Cache);
        Assert.Same(CookTargets.Web, runtime.Target);
        Assert.Same(registry, runtime.Importers);
    }

    [Fact]
    public async Task TheImporterIsToldTheDevicesTarget()
    {
        var runtime = new RuntimeImport(
            _cache,
            CookTargets.Macos,
            new ImporterRegistry([new RecordingImporter { Transform = request => Bytes(request.Target.Name) }]));

        var result = await runtime.ImportAsync(Id("robot.txt"), Bytes("body"));

        Assert.Equal("macos", await TextOf(runtime, result));
    }

    [Fact]
    public async Task EachOutputIsReadByItsOwnName()
    {
        var runtime = new RuntimeImport(_cache, CookTargets.Macos, new ImporterRegistry([new TwoOutputsImporter()]));

        var result = await runtime.ImportAsync(Id("robot.glb"), Bytes("x"));

        Assert.Equal("a", Encoding.UTF8.GetString((await runtime.ReadAsync(result.Asset!, "a"))!));
        Assert.Equal("b", Encoding.UTF8.GetString((await runtime.ReadAsync(result.Asset!, "b"))!));
    }

    [Fact]
    public async Task SettingsNamingAnImporterChooseItOverTheExtension()
    {
        var runtime = Counting();
        var named = new ImportSettings(importer: "counting");

        var result = await runtime.ImportAsync(Id("notes.txt"), Bytes("hello"), named);

        Assert.True(runtime.Handles(Id("notes.txt"), named));
        Assert.Equal(CookStatus.Cooked, result.Status);
        Assert.Equal("hello tint=null", await TextOf(runtime, result));
    }

    [Fact]
    public async Task SettingsNamingAnImporterThatIsNotThereAreAFailedImportAndARefusedQuestion()
    {
        var runtime = Counting();
        var unknown = new ImportSettings(importer: "missing");

        var asked = Assert.Throws<ArgumentException>(() => runtime.Handles(Id("robot.glb"), unknown));
        var result = await runtime.ImportAsync(Id("robot.glb"), Bytes("body"), unknown);

        Assert.StartsWith("missing is named by the settings for robot.glb", asked.Message);
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("missing", $"{result.Error}");
    }

    [Fact]
    public async Task ASettingsFileBesideTheFileAppliesWhenTheCallSaysNothing()
    {
        var runtime = Counting();

        var result = await runtime.ImportAsync(Id("robot.glb"), Bytes("body"), alongside: ShippedWith("blue"));

        Assert.Equal("body tint=blue", await TextOf(runtime, result));
    }

    [Fact]
    public async Task SettingsAtTheCallWinOverTheFilesOwn()
    {
        var runtime = Counting();

        var result = await runtime.ImportAsync(Id("robot.glb"), Bytes("body"), Tint("red"), ShippedWith("blue"));

        Assert.Equal("body tint=red", await TextOf(runtime, result));
    }

    [Fact]
    public async Task SettingsAtTheCallThatSayNothingKeepTheFilesOwnAndShareTheImport()
    {
        var runtime = Counting();
        var first = await runtime.ImportAsync(Id("robot.glb"), Bytes("body"), alongside: ShippedWith("blue"));

        var again = await runtime.ImportAsync(
            Id("robot.glb"), Bytes("body"), new ImportSettings(), ShippedWith("blue"));

        Assert.Equal(CookStatus.Cached, again.Status);
        Assert.Equal(first.Key, again.Key);
        Assert.Equal("body tint=blue", await TextOf(runtime, again));
    }

    [Fact]
    public async Task WhatWasHandedOverHidesTheSameNameAlongside()
    {
        var runtime = Counting();
        var stale = new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [Id("robot.glb")] = Bytes("stale") });

        var result = await runtime.ImportAsync(Id("robot.glb"), Bytes("body"), alongside: stale);

        Assert.Equal("body tint=null", await TextOf(runtime, result));
    }

    [Fact]
    public async Task AFileTheCacheHasSinceDroppedReadsBackAsNothing()
    {
        var forgetful = new ForgetfulCache(_cache);
        var runtime = Counting(forgetful);
        var result = await runtime.ImportAsync(Id("robot.glb"), Bytes("body"));
        Assert.NotNull(await runtime.ReadAsync(result.Asset!, "out"));

        forgetful.Forgetting = true;

        Assert.Null(await runtime.ReadAsync(result.Asset!, "out"));
        Assert.Null(await runtime.ReadOnlyAsync(result.Asset!));
    }

    [Fact]
    public async Task NothingProducedReadsBackAsNothing()
    {
        var runtime = Counting();
        var empty = new CookedAsset([]);

        Assert.Null(await runtime.ReadAsync(empty, "out"));
        Assert.Null(await runtime.ReadOnlyAsync(empty));
    }

    // A cache whose bytes can be taken away, as the system does to a cache directory.
    private sealed class ForgetfulCache(ICookCache inner) : ICookCache
    {
        public bool Forgetting { get; set; }

        public Task<CookedAsset?> LookUpAsync(CookKey key) => inner.LookUpAsync(key);

        public Task<CookedAsset> StoreAsync(CookKey key, IReadOnlyDictionary<string, byte[]> outputs) =>
            inner.StoreAsync(key, outputs);

        public Task<byte[]?> ReadAsync(ContentHash hash) =>
            Forgetting ? Task.FromResult<byte[]?>(null) : inner.ReadAsync(hash);

        public Task<CookFailure?> LookUpFailureAsync(CookKey key) => inner.LookUpFailureAsync(key);

        public Task RecordFailureAsync(CookKey key, string reason) => inner.RecordFailureAsync(key, reason);
    }
}
