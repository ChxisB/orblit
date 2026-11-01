using System.Runtime.InteropServices;

namespace Orblit.Asset.Tests;

public sealed class CookProjectTests : IDisposable
{
    private readonly CookProjectWork _work = new();

    public void Dispose() => _work.Dispose();

    private static AssetId Id(string text) => AssetId.Parse(text);

    private string At(string name) => _work.At(name);

    private void WriteAsset(string name, string contents) => _work.WriteAsset(name, contents);

    private CookProject ProjectFor(CookTarget target, ImporterRegistry? importers = null) =>
        _work.ProjectFor(target, importers);

    private static ImporterRegistry Broken() => CookProjectWork.Broken();

    private static List<string> FileNamesUnder(string folder) =>
        [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(file => Path.GetFileName(file))];

    private AssetManifest ManifestOf(string target) =>
        AssetManifest.Read(File.ReadAllText(At($"out/{target}/manifest.json"))).Manifest;

    // assets()

    [Fact]
    public async Task FindsEverythingUnderTheFolderInAFixedOrder()
    {
        WriteAsset("b.png", "b");
        WriteAsset("a.png", "a");
        WriteAsset("nested/deep/c.png", "c");
        Assert.Equal(
            [Id("a.png"), Id("b.png"), Id("nested/deep/c.png")],
            await ProjectFor(CookTargets.Macos).AssetsAsync());
    }

    [Fact]
    public async Task LeavesOutSettingsFilesWhichAreInputsAndNotAssets()
    {
        WriteAsset("a.png", "a");
        WriteAsset("a.png.import.json", "{}");
        WriteAsset(".import.json", "{}");
        Assert.Equal([Id("a.png")], await ProjectFor(CookTargets.Macos).AssetsAsync());
    }

    [Fact]
    public async Task LeavesOutHiddenFilesAndFolders()
    {
        WriteAsset("a.png", "a");
        WriteAsset(".DS_Store", "junk");
        WriteAsset(".git/objects/thing", "junk");
        Assert.Equal([Id("a.png")], await ProjectFor(CookTargets.Macos).AssetsAsync());
    }

    [Fact]
    public async Task AMissingAssetsFolderIsAnErrorThatNamesIt()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ProjectFor(CookTargets.Macos).AssetsAsync());
        Assert.Equal("assetsPath", error.ParamName);
    }

    // a cooked bundle

    private void WriteTheBundleAssets()
    {
        WriteAsset("textures/wall.png", "wall");
        WriteAsset("README.md", "not an asset");
    }

    [Fact]
    public async Task ABundleCarriesOnlyItsOwnTargetsFormats()
    {
        WriteTheBundleAssets();
        await ProjectFor(CookTargets.Ios).RunAsync();
        await ProjectFor(CookTargets.Macos).RunAsync();

        var ios = FileNamesUnder(At("out/ios"));
        var macos = FileNamesUnder(At("out/macos"));

        Assert.Contains("wall.png.astc.ktx2", ios);
        Assert.DoesNotContain("wall.png.bc.ktx2", ios);
        Assert.Contains("wall.png.bc.ktx2", macos);
        Assert.DoesNotContain("wall.png.astc.ktx2", macos);
    }

    [Fact]
    public async Task ABundleKeepsTheSourceNameInTheMiddleSoAFileSaysWhereItCameFrom()
    {
        WriteTheBundleAssets();
        await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.True(File.Exists(At("out/macos/textures/wall.png.bc.ktx2")));
    }

    [Fact]
    public async Task ABundleHasAManifestNamingEveryFileItHolds()
    {
        WriteTheBundleAssets();
        await ProjectFor(CookTargets.Macos).RunAsync();
        var manifest = ManifestOf("macos");
        Assert.Equal([Id("textures/wall.png.bc.ktx2")], manifest.Entries.Keys);
        Assert.True(manifest.Entries.Values.Single().Bytes > 0);
    }

    [Fact]
    public async Task ABundleDoesNotCarryAnAssetNobodyClaimed()
    {
        WriteTheBundleAssets();
        await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.False(File.Exists(At("out/macos/README.md")));
    }

    [Fact]
    public async Task ABundleLosesYesterdaysFilesRatherThanShippingThem()
    {
        WriteTheBundleAssets();
        await ProjectFor(CookTargets.Macos).RunAsync();
        var stale = At("out/macos/gone.ktx2");
        await File.WriteAllTextAsync(stale, "from a cook that no longer applies", TestContext.Current.CancellationToken);
        Assert.True(File.Exists(stale));

        await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.False(File.Exists(stale));
    }

    // the claims a build depends on

    private void WriteThreeAssets()
    {
        WriteAsset("a.png", "a");
        WriteAsset("b.png", "b");
        WriteAsset("c.png", "c");
    }

    [Fact]
    public async Task ARebuildWithNothingChangedCooksNothing()
    {
        WriteThreeAssets();
        var first = await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.Equal(3, first.Count(CookStatus.Cooked));

        var second = await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.Equal(0, second.Count(CookStatus.Cooked));
        Assert.Equal(3, second.Count(CookStatus.Cached));
    }

    [Fact]
    public async Task ChangingOneAssetRecooksOnlyThatOne()
    {
        WriteThreeAssets();
        await ProjectFor(CookTargets.Macos).RunAsync();
        WriteAsset("b.png", "b changed");

        var report = await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.Equal([Id("b.png")], report.WithStatus(CookStatus.Cooked).Select(result => result.Id));
        Assert.Equal(2, report.Count(CookStatus.Cached));
    }

    [Fact]
    public async Task ACleanCookProducesTheSameHashesAsTheOneBeforeIt()
    {
        WriteThreeAssets();
        var first = await ProjectFor(CookTargets.Macos).RunAsync();
        var firstManifest = ManifestOf("macos");

        // A clean cook: no cache, no bundle, nothing carried over.
        Directory.Delete(At("cache"), recursive: true);
        Directory.Delete(At("out"), recursive: true);

        var second = await ProjectFor(CookTargets.Macos).RunAsync();
        var secondManifest = ManifestOf("macos");

        Assert.Equal(first.Count(CookStatus.Cooked), second.Count(CookStatus.Cooked));
        Assert.Equal(BundleHash.Of(firstManifest), BundleHash.Of(secondManifest));

        // And the other half of the claim, without which the first half would hold for a hash that never
        // changes at all.
        WriteAsset("b.png", "b changed");
        await ProjectFor(CookTargets.Macos).RunAsync();
        Assert.NotEqual(BundleHash.Of(firstManifest), BundleHash.Of(ManifestOf("macos")));
    }

    [Fact]
    public async Task TwoTargetsCookedInTheSameRunDoNotDisturbEachOther()
    {
        WriteThreeAssets();
        await ProjectFor(CookTargets.Ios).RunAsync();
        await ProjectFor(CookTargets.Macos).RunAsync();
        await ProjectFor(CookTargets.Ios).RunAsync();

        var ios = ManifestOf("ios").Entries.Keys.Select(id => $"{id}").ToList();
        var macos = ManifestOf("macos").Entries.Keys.Select(id => $"{id}").ToList();
        Assert.Equal(3, ios.Count);
        Assert.Equal(3, macos.Count);
        Assert.All(ios, id => Assert.Contains("astc", id));
        Assert.All(macos, id => Assert.Contains("bc", id));
    }

    [Fact]
    public async Task AFailingAssetDoesNotStopTheBundleAndIsReported()
    {
        WriteThreeAssets();
        var report = await ProjectFor(CookTargets.Macos, Broken()).RunAsync();
        Assert.False(report.Ok);
        Assert.Equal(3, report.Count(CookStatus.Failed));
        Assert.True(File.Exists(At("out/macos/manifest.json")), "the bundle is still written, without what failed");
    }

    // cooking during a build

    private Task<CookReport> CookIn(
        string? targetOs, Action<IEnumerable<Uri>>? dependencies = null, ImporterRegistry? importers = null) =>
        BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: targetOs,
            cachePath: At("cache"),
            @out: "out/cooked",
            importers: importers ?? new ImporterRegistry([new FamilyImporter()]),
            dependencies: dependencies);

    private void WriteTheBuildAssets()
    {
        WriteAsset("a.png", "a");
        WriteAsset("a.png.import.json", "{}");
    }

    private static async Task<HashSet<string>> WatchedBy(Func<Action<IEnumerable<Uri>>, Task> run)
    {
        IEnumerable<Uri>? watched = null;
        await run(files => watched = files.ToList());
        return watched!.Select(uri => uri.LocalPath).ToHashSet();
    }

    [Fact]
    public async Task CooksForThePlatformTheBuildNamesAsTheHookSpellsIt()
    {
        WriteTheBuildAssets();
        await CookIn("ios");
        Assert.True(File.Exists(At("out/cooked/ios/a.png.astc.ktx2")));
    }

    [Fact]
    public async Task ABuildWithNoPlatformIsAWebBuildWhichIsTheOneThatHasNone()
    {
        WriteTheBuildAssets();
        await CookIn(null);
        Assert.True(Directory.Exists(At("out/cooked/web")));
    }

    [Fact]
    public async Task RefusesAPlatformOrblitDoesNotCookFor()
    {
        WriteTheBuildAssets();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CookIn("fuchsia"));
        Assert.Equal("targetOs", error.ParamName);
        Assert.Contains("android", error.Message);
    }

    [Fact]
    public async Task TellsTheBuildEveryFileItReadSoAnEditReRunsIt()
    {
        WriteTheBuildAssets();
        var paths = await WatchedBy(watch => CookIn("macos", watch));
        Assert.Contains(At("assets/a.png"), paths);
        Assert.Contains(At("assets/a.png.import.json"), paths);
    }

    [Fact]
    public async Task WatchesTheFolderAMissingSettingsFileWouldGoInWhichIsHowOneBeingAddedIsNoticed()
    {
        WriteTheBuildAssets();
        WriteAsset("nested/b.png", "b");
        var paths = await WatchedBy(watch => CookIn("macos", watch));
        Assert.Contains(At("assets/nested") + Path.DirectorySeparatorChar, paths);
        Assert.Contains(At("assets") + Path.DirectorySeparatorChar, paths);
    }

    [Fact]
    public async Task ReportsOnlyPathsThatExistSinceABuildHookRunsAgainOnEveryBuildForAPathThatDoesNot()
    {
        WriteTheBuildAssets();
        WriteAsset("nested/b.png", "b");
        WriteAsset(".hidden/c.png", "c");
        var paths = await WatchedBy(watch => CookIn("macos", watch));
        foreach (var path in paths)
        {
            var exists = path.EndsWith(Path.DirectorySeparatorChar) ? Directory.Exists(path) : File.Exists(path);
            Assert.True(exists, $"{path} is reported");
        }

        Assert.DoesNotContain(paths, path => path.Contains(".hidden"));
    }

    [Fact]
    public async Task FailsTheBuildRatherThanShippingAnAppMissingAnAsset()
    {
        WriteTheBuildAssets();
        var error = await Assert.ThrowsAsync<CookFailedException>(() => CookIn("macos", importers: Broken()));
        Assert.Contains("a.png", error.Failures.Select(failure => $"{failure.Id}"));
    }

    // packing during a build

    private Task<PackSummary> PackIn(
        string? targetOs, Action<IEnumerable<Uri>>? dependencies = null, ImporterRegistry? importers = null) =>
        BuildHook.PackDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: targetOs,
            cachePath: At("cache"),
            @out: "out/cooked",
            importers: importers ?? new ImporterRegistry([new FamilyImporter()]),
            dependencies: dependencies);

    private string PackFor(string target) => At($"out/cooked/game.{target}.opack");

    private void WriteThePackAssets()
    {
        WriteAsset("a.png", "a");
        WriteAsset("b.png", "b");
    }

    [Fact]
    public async Task WritesOnePackNamedForThePlatformHoldingTheCookedAssets()
    {
        var token = TestContext.Current.CancellationToken;
        WriteThePackAssets();
        var summary = await PackIn("ios");
        await using var bytes = new FilePackBytes(PackFor("ios"));
        var pack = await AssetPack.OpenAsync(bytes);
        Assert.Equal("ios", pack.Target);
        Assert.Equal(2, summary.Assets);
        Assert.Equal(new FileInfo(PackFor("ios")).Length, summary.Bytes);
        Assert.Equal([Id("a.png.astc.ktx2"), Id("b.png.astc.ktx2")], pack.Manifest.Entries.Keys);
        var cooked = await File.ReadAllBytesAsync(At("out/cooked/ios/a.png.astc.ktx2"), token);
        Assert.Equal(cooked, await pack.ReadAsync(Id("a.png.astc.ktx2")));
        Assert.Empty(await pack.FindCorruptAsync());
    }

    [Fact]
    public async Task ASecondPlatformWritesItsOwnFileAndLeavesTheFirstAlone()
    {
        var token = TestContext.Current.CancellationToken;
        WriteThePackAssets();
        await PackIn("ios");
        var before = await File.ReadAllBytesAsync(PackFor("ios"), token);
        await PackIn("macos");
        Assert.True(File.Exists(PackFor("macos")));
        Assert.Equal(before, await File.ReadAllBytesAsync(PackFor("ios"), token));
        await using var bytes = new FilePackBytes(PackFor("macos"));
        Assert.Equal("macos", (await AssetPack.OpenAsync(bytes)).Target);
    }

    [Fact]
    public async Task ABuildWithNoPlatformPacksForTheWeb()
    {
        WriteThePackAssets();
        await PackIn(null);
        await using var bytes = new FilePackBytes(PackFor("web"));
        Assert.Equal("web", (await AssetPack.OpenAsync(bytes)).Target);
    }

    [Fact]
    public async Task PacksTheSameBytesEveryTime()
    {
        var token = TestContext.Current.CancellationToken;
        WriteThePackAssets();
        await PackIn("macos");
        var first = await File.ReadAllBytesAsync(PackFor("macos"), token);
        await PackIn("macos");
        Assert.Equal(first, await File.ReadAllBytesAsync(PackFor("macos"), token));
    }

    [Fact]
    public async Task DoesNotReportThePackAsADependencySinceAHookThatChangedOneIsRunAgainOnEveryBuild()
    {
        WriteThePackAssets();
        var paths = await WatchedBy(watch => PackIn("macos", watch));
        Assert.Contains(At("assets/a.png"), paths);
        Assert.DoesNotContain(PackFor("macos"), paths);
    }

    [Fact]
    public async Task WritesNoPackWhenTheCookFailed()
    {
        WriteThePackAssets();
        await Assert.ThrowsAsync<CookFailedException>(() => PackIn("macos", importers: Broken()));
        Assert.False(File.Exists(PackFor("macos")));
    }

    [Fact]
    public async Task RefusesAPlatformOrblitDoesNotCookForBeforeCookingAnything()
    {
        WriteThePackAssets();
        await Assert.ThrowsAsync<ArgumentException>(() => PackIn("fuchsia"));
        Assert.False(Directory.Exists(At("out")));
    }

    [Fact]
    public void NamesTheFileTheWayTheAppThatReadsItDoes()
    {
        Assert.Equal("game.android.opack", PackName.FileName(CookTargets.Android));
        Assert.Equal("levels.windows.opack", PackName.FileName(CookTargets.Windows, "levels"));
    }

    // what the editor shows beside each asset

    private static void AssertStates(IReadOnlyDictionary<AssetId, CookState> actual, params (string Id, CookState State)[] expected) =>
        Assert.Equal(
            expected.ToDictionary(pair => Id(pair.Id), pair => pair.State),
            actual.ToDictionary(pair => pair.Key, pair => pair.Value));

    [Fact]
    public async Task AProjectThatHasNeverBeenCookedIsAllStale()
    {
        WriteAsset("a.png", "a");
        WriteAsset("b.png", "b");
        AssertStates(
            await ProjectFor(CookTargets.Macos).StatesOfAsync(),
            ("a.png", CookState.Stale),
            ("b.png", CookState.Stale));
    }

    [Fact]
    public async Task AfterACookTheyAreCooked()
    {
        WriteAsset("a.png", "a");
        WriteAsset("b.png", "b");
        await ProjectFor(CookTargets.Macos).RunAsync();
        AssertStates(
            await ProjectFor(CookTargets.Macos).StatesOfAsync(),
            ("a.png", CookState.Cooked),
            ("b.png", CookState.Cooked));
    }

    [Fact]
    public async Task ChangingOneAssetMakesOnlyThatOneStale()
    {
        WriteAsset("a.png", "a");
        WriteAsset("b.png", "b");
        await ProjectFor(CookTargets.Macos).RunAsync();
        WriteAsset("a.png", "changed");

        AssertStates(
            await ProjectFor(CookTargets.Macos).StatesOfAsync(),
            ("a.png", CookState.Stale),
            ("b.png", CookState.Cooked));
    }

    [Fact]
    public async Task CookedForOneTargetSaysNothingAboutAnother()
    {
        WriteAsset("a.png", "a");
        await ProjectFor(CookTargets.Macos).RunAsync();
        AssertStates(await ProjectFor(CookTargets.Ios).StatesOfAsync(), ("a.png", CookState.Stale));
    }

    [Fact]
    public async Task AnAssetNoImporterClaimsIsIgnoredNotStale()
    {
        WriteAsset("a.png", "a");
        WriteAsset("notes.md", "read me");
        AssertStates(
            await ProjectFor(CookTargets.Macos).StatesOfAsync(),
            ("a.png", CookState.Stale),
            ("notes.md", CookState.Ignored));
    }

    [Fact]
    public async Task AnAssetACookCouldNotCookIsFailed()
    {
        WriteAsset("a.png", "a");
        CookProject Broke() => ProjectFor(CookTargets.Macos, Broken());

        // Nothing has tried yet, and nobody can know without trying.
        AssertStates(await Broke().StatesOfAsync(), ("a.png", CookState.Stale));

        // The cook remembered, so the editor can say so without one.
        await Broke().RunAsync();
        AssertStates(await Broke().StatesOfAsync(), ("a.png", CookState.Failed));
    }

    [Fact]
    public async Task FixingTheAssetClearsTheFailure()
    {
        WriteAsset("a.png", "a");
        await ProjectFor(CookTargets.Macos, Broken()).RunAsync();
        WriteAsset("a.png", "fixed");

        // A different asset is different work, not the failed work.
        AssertStates(
            await ProjectFor(CookTargets.Macos, Broken()).StatesOfAsync(), ("a.png", CookState.Stale));
    }

    [Fact]
    public async Task AFailureIsRememberedAgainstOneTargetNotAll()
    {
        WriteAsset("a.png", "a");
        await ProjectFor(CookTargets.Macos, Broken()).RunAsync();

        AssertStates(
            await ProjectFor(CookTargets.Ios, Broken()).StatesOfAsync(), ("a.png", CookState.Stale));
    }

    [Fact]
    public async Task ItCanBeAskedAboutAFewRatherThanAllOfThem()
    {
        WriteAsset("a.png", "a");
        WriteAsset("deep/b.png", "b");

        // A browser showing one folder should not hash the project.
        AssertStates(
            await ProjectFor(CookTargets.Macos).StatesOfAsync([Id("deep/b.png")]),
            ("deep/b.png", CookState.Stale));
    }

    [Fact]
    public async Task AskingDoesNotCookAnything()
    {
        WriteAsset("a.png", "a");
        await ProjectFor(CookTargets.Macos).StatesOfAsync();

        // No bundle, because no importer ran.
        Assert.False(Directory.Exists(At("out")));
        AssertStates(await ProjectFor(CookTargets.Macos).StatesOfAsync(), ("a.png", CookState.Stale));
    }

    // the machine this is running on

    [Fact]
    public void TheMachineHasATargetAndItIsThePlatformWeAreOn()
    {
        var expected = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : "linux";
        Assert.Equal(expected, CookProject.CurrentCookTarget.Name);
        Assert.Contains(CookProject.CurrentCookTarget.Name, CookTargets.Names);
    }

    [Fact]
    public void ThatTargetCanDecodeTheTexturesCookedForIt() =>
        Assert.NotEmpty(CookProject.CurrentCookTarget.TextureFamilies());
}
