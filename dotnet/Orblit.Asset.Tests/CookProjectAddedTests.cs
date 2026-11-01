using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Orblit.Json;

namespace Orblit.Asset.Tests;

public sealed class CookProjectAddedTests : IDisposable
{
    private const string NoLinks = "links need extra privileges on Windows";
    private const string NoDenial = "a folder cannot be made unreadable on Windows or to a privileged user";

    private readonly CookProjectWork _work = new();

    public void Dispose() => _work.Dispose();

    public static bool CanLink => !OperatingSystem.IsWindows();

    public static bool CanNameABackslash => !OperatingSystem.IsWindows();

    public static bool CanDeny => !OperatingSystem.IsWindows() && !Environment.IsPrivilegedProcess;

    private static AssetId Id(string text) => AssetId.Parse(text);

    private static string CachePathFor(Dictionary<string, string?> environment) =>
        CookProject.DefaultCachePath(name => environment.GetValueOrDefault(name));

    // the text of an invalid argument

    [Fact]
    public void AStringValueIsQuoted()
    {
        var error = InvalidArgument.Value("a \"b\"\n", "name", "is not right");
        Assert.StartsWith("Invalid argument (name): is not right: \"a \\\"b\\\"\\n\"", error.Message);
        Assert.Equal("name", error.ParamName);
    }

    [Theory]
    [InlineData(2.5, "2.5")]
    [InlineData(7L, "7")]
    [InlineData(true, "true")]
    [InlineData(null, "null")]
    public void ANumberABoolOrNullPrintsPlain(object? value, string printed) =>
        Assert.StartsWith($"Invalid argument (n): r: {printed}", InvalidArgument.Value(value, "n", "r").Message);

    // the cache

    [Fact]
    public void TheCacheIsWhereTheEnvironmentSaysWhenItSaysAnywhere()
    {
        var environment = new Dictionary<string, string?> { ["ORBLIT_CACHE"] = "/mine", ["HOME"] = "/home/me" };
        Assert.Equal("/mine", CachePathFor(environment));
    }

    [Fact]
    public void ACacheVariableOfOneCharacterIsAPlace()
    {
        var environment = new Dictionary<string, string?> { ["ORBLIT_CACHE"] = "c", ["HOME"] = "/home/me" };
        Assert.Equal("c", CachePathFor(environment));
    }

    [Fact]
    public void AnEmptyCacheVariableIsNotAPlace()
    {
        var environment = new Dictionary<string, string?> { ["ORBLIT_CACHE"] = "", ["HOME"] = "/home/me" };
        Assert.Equal(Path.Combine("/home/me", ".cache", "orblit-cook"), CachePathFor(environment));
    }

    [Fact]
    public void TheCacheIsUnderTheHomeFolderOtherwise()
    {
        var environment = new Dictionary<string, string?> { ["HOME"] = "/home/me", ["USERPROFILE"] = "C:\\Users\\me" };
        Assert.Equal(Path.Combine("/home/me", ".cache", "orblit-cook"), CachePathFor(environment));
    }

    [Fact]
    public void WindowsHomeIsUsedWhenThereIsNoHome()
    {
        var environment = new Dictionary<string, string?> { ["USERPROFILE"] = "C:\\Users\\me" };
        Assert.Equal(Path.Combine("C:\\Users\\me", ".cache", "orblit-cook"), CachePathFor(environment));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TheCacheIsInTheTemporaryFolderWhenThereIsNoHome(string? home)
    {
        var environment = new Dictionary<string, string?> { ["HOME"] = home };
        var expected = Path.Combine(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), "orblit-cook-cache");
        Assert.Equal(expected, CachePathFor(environment));
    }

    [Fact]
    public void ADefaultProjectFindsItsCacheFromTheProcessEnvironment()
    {
        var project = new CookProject(_work.At("assets"), _work.At("out"), CookTargets.Macos);
        Assert.Equal(CookProject.DefaultCachePath(Environment.GetEnvironmentVariable), project.CachePath);
    }

    // what a project is built with

    [Fact]
    public void ADefaultProjectCooksWithinTwoGibibytesOfCacheAndFourAtATime()
    {
        var project = _work.ProjectFor(CookTargets.Macos);
        Assert.Equal(2L * 1024 * 1024 * 1024, project.LimitBytes);
        Assert.Equal(4, project.Concurrency);
    }

    [Fact]
    public void ABundleGoesInAFolderNamedForTheTarget() =>
        Assert.Equal(_work.At("out/ios"), _work.ProjectFor(CookTargets.Ios).BundlePath);

    [Fact]
    public async Task AConcurrencyOfNothingIsRefusedWhenTheProjectRuns()
    {
        _work.WriteAsset("a.png", "a");
        var project = new CookProject(
            assetsPath: _work.At("assets"),
            outPath: _work.At("out"),
            target: CookTargets.Macos,
            cachePath: _work.At("cache"),
            importers: new ImporterRegistry([new FamilyImporter()]),
            concurrency: 0);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => project.RunAsync());
    }

    [Fact]
    public void TheDefaultImportersClaimEveryKindOfSourceThatIsPortedWithoutATool()
    {
        var registry = CookProject.DefaultImporters();
        foreach (var name in new[] { "gltf", "scene", "atlas", "terrain" }.Concat(NativeImporters.Create().Select(i => i.Name)))
        {
            Assert.NotNull(registry.ByName(name));
        }
    }

    [Fact]
    public void AProjectGivenNoImportersUsesTheDefaultOnes()
    {
        var project = new CookProject(_work.At("assets"), _work.At("out"), CookTargets.Macos, _work.At("cache"));
        Assert.IsType<TerrainImporter>(project.Importers.ByName("terrain"));
    }

    [Fact]
    public void ANullLimitMeansTheCacheMayGrowWithoutOne()
    {
        var project = new CookProject(_work.At("assets"), _work.At("out"), CookTargets.Macos, _work.At("cache"), limitBytes: null);
        Assert.Null(project.LimitBytes);
    }

    // the assets of a project

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ALinkIsNotFollowedSoAFolderOutsideTheProjectIsNotCooked()
    {
        _work.WriteAsset("a.png", "a");
        var outside = _work.At("elsewhere");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.png"), "s", TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(_work.At("assets/linked"), outside);
        File.CreateSymbolicLink(_work.At("assets/alias.png"), Path.Combine(outside, "secret.png"));

        Assert.Equal([Id("a.png")], await _work.ProjectFor(CookTargets.Macos).AssetsAsync());
    }

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanNameABackslash))]
    public async Task AFileWhoseNameIsNotAnAssetIdIsLeftOut()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("odd\\name.png", "x");
        Assert.Equal([Id("a.png")], await _work.ProjectFor(CookTargets.Macos).AssetsAsync());
    }

    [Fact]
    public async Task AHiddenFolderBelowAnotherFolderIsLeftOutToo()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("deep/.cache/b.png", "b");
        _work.WriteAsset("deep/c.png", "c");
        Assert.Equal([Id("a.png"), Id("deep/c.png")], await _work.ProjectFor(CookTargets.Macos).AssetsAsync());
    }

    [Fact]
    public async Task AFolderThatIsNotThereIsRefusedByName()
    {
        var project = new CookProject(
            _work.At("missing"), _work.At("out"), CookTargets.Macos, _work.At("cache"), Family());
        var error = await Assert.ThrowsAsync<ArgumentException>(() => project.AssetsAsync());
        Assert.StartsWith($"Invalid argument (assetsPath): is not a directory: {JsonText.Encode(_work.At("missing"))}", error.Message);
        Assert.Equal("assetsPath", error.ParamName);
    }

    [Fact(Skip = NoDenial, SkipUnless = nameof(CanDeny))]
    [UnsupportedOSPlatform("windows")]
    public async Task AFolderThatCannotBeReadStopsTheListingRatherThanHidingAssets()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("locked/b.png", "b");
        var locked = _work.At("assets/locked");
        var mode = File.GetUnixFileMode(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _work.ProjectFor(CookTargets.Macos).AssetsAsync());
        }
        finally
        {
            File.SetUnixFileMode(locked, mode);
        }
    }

    [Fact]
    public async Task RunReportsEachAssetAsItFinishes()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("b.png", "b");
        var seen = new List<AssetId>();
        var report = await _work.ProjectFor(CookTargets.Macos).RunAsync(result =>
        {
            lock (seen)
            {
                seen.Add(result.Id);
            }
        });
        Assert.Equal(Ordered.ById(report.Results.Select(result => result.Id)), Ordered.ById(seen));
    }

    // writing the bundle

    [Fact]
    public async Task WriteReturnsTheManifestItWrote()
    {
        _work.WriteAsset("a.png", "a");
        var project = _work.ProjectFor(CookTargets.Macos);
        await project.RunAsync();
        var written = AssetManifest.Read(await File.ReadAllTextAsync(
            _work.At("out/macos/manifest.json"), TestContext.Current.CancellationToken)).Manifest;

        var report = await new Cook(
            new DirectoryAssetSource(_work.At("assets")),
            new DirectoryCookCache(_work.At("cache")),
            project.Importers,
            CookTargets.Macos).CookAllAsync([Id("a.png")]);
        var returned = await project.WriteAsync(report, new DirectoryCookCache(_work.At("cache")));

        Assert.Equal(written.Encode(), returned.Encode());
    }

    [Fact]
    public async Task ACacheThatLostTheBytesIsAnErrorThatSaysWhichOutput()
    {
        _work.WriteAsset("a.png", "a");
        var project = _work.ProjectFor(CookTargets.Macos);
        var report = await project.RunAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => project.WriteAsync(report, new MemoryCookCache()));
        Assert.Equal(
            "The cache reported a.png as cooked but no longer has the bytes for bc.ktx2. "
            + "Something removed them underneath this cook. Running it again will cook them afresh.",
            error.Message);
    }

    [Fact]
    public async Task ACacheTooSmallToKeepEveryAssetIsTheSameError()
    {
        // The cache never evicts the entry it has just filed, so it takes a second asset to push the first out.
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("b.png", "b");
        var project = new CookProject(
            assetsPath: _work.At("assets"),
            outPath: _work.At("out"),
            target: CookTargets.Macos,
            cachePath: _work.At("cache"),
            importers: Family(),
            limitBytes: 1);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => project.RunAsync());
        Assert.Contains("no longer has the bytes", error.Message);
    }

    [Fact]
    public async Task EveryTextureFamilyATargetAsksForIsWrittenAndNamedInTheManifest()
    {
        _work.WriteAsset("a.png", "a");
        await _work.ProjectFor(CookTargets.Android).RunAsync();
        Assert.True(File.Exists(_work.At("out/android/a.png.astc.ktx2")));
        Assert.True(File.Exists(_work.At("out/android/a.png.etc2.ktx2")));
        var manifest = AssetManifest.Read(await File.ReadAllTextAsync(
            _work.At("out/android/manifest.json"), TestContext.Current.CancellationToken)).Manifest;
        Assert.Equal([Id("a.png.astc.ktx2"), Id("a.png.etc2.ktx2")], Ordered.ById(manifest.Entries.Keys));
    }

    [Fact]
    public async Task TheManifestFileIsExactlyTheBytesOfTheManifestItReturns()
    {
        _work.WriteAsset("a.png", "a");
        var project = _work.ProjectFor(CookTargets.Macos);
        var report = await project.RunAsync();
        var returned = await project.WriteAsync(report, new DirectoryCookCache(_work.At("cache")));
        var bytes = await File.ReadAllBytesAsync(_work.At("out/macos/manifest.json"), TestContext.Current.CancellationToken);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(returned.Encode()), bytes);
    }

    [Fact]
    public async Task ABundleIsWrittenWithoutAByteOrderMarkInItsManifest()
    {
        _work.WriteAsset("a.png", "a");
        await _work.ProjectFor(CookTargets.Macos).RunAsync();
        var bytes = await File.ReadAllBytesAsync(_work.At("out/macos/manifest.json"), TestContext.Current.CancellationToken);
        Assert.Equal((byte)'{', bytes[0]);
    }

    // the hash of a bundle

    [Fact]
    public void AnEmptyBundleHashesTheBytesOfItsManifest()
    {
        var manifest = new AssetManifest(new Dictionary<AssetId, AssetEntry>());
        Assert.Equal(ContentHash.Of(System.Text.Encoding.UTF8.GetBytes(manifest.Encode())), BundleHash.Of(manifest));
    }

    [Fact]
    public void AnEntryThatDiffersByOneHashChangesTheBundleHash()
    {
        AssetManifest With(string text) => new(new Dictionary<AssetId, AssetEntry>
        {
            [Id("a.ktx2")] = new(ContentHash.Of(System.Text.Encoding.UTF8.GetBytes(text)), 1),
        });
        Assert.Equal(BundleHash.Of(With("x")), BundleHash.Of(With("x")));
        Assert.NotEqual(BundleHash.Of(With("x")), BundleHash.Of(With("y")));
    }

    // a failed cook

    [Fact]
    public async Task AFailedCookNamesEachFailureAndHowManyOfHowMany()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("b.png", "b");
        _work.WriteAsset("c.md", "c");
        var report = await _work.ProjectFor(CookTargets.Macos, CookProjectWork.Broken()).RunAsync();

        var error = new CookFailedException(report);

        Assert.Same(report, error.Report);
        Assert.Equal(
            "Cooking assets failed for 2 of 3:\n"
            + "  a.png: Could not import a.png: this importer never works\n"
            + "  b.png: Could not import b.png: this importer never works",
            error.Message);
    }

    // the platform

    [Theory]
    [InlineData("macos")]
    [InlineData("windows")]
    [InlineData("linux")]
    [InlineData("ios")]
    [InlineData("android")]
    public void EveryPlatformOrblitCooksForIsFound(string name) =>
        Assert.Equal(name, CookProject.CookTargetFor(name).Name);

    [Fact]
    public void APlatformOrblitDoesNotCookForIsRefusedWithTheOnesItDoes()
    {
        var error = Assert.Throws<NotSupportedException>(() => CookProject.CookTargetFor("fuchsia"));
        Assert.Equal(
            "Orblit does not cook assets for fuchsia. It cooks for macos, windows, linux, ios, android, web.",
            error.Message);
    }

    [Fact]
    public void TheCurrentTargetAgreesWithTheRuntimeAboutTheSystem()
    {
        var current = CookProject.CurrentCookTarget.Name;
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), current == "macos");
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), current == "windows");
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), current == "linux");
    }

    // the build hook

    [Fact]
    public async Task TheHookReadsFromAssetsAndWritesToAssetsCookedUnlessToldOtherwise()
    {
        _work.WriteAsset("a.png", "a");
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root, targetOs: "macos", cachePath: _work.At("cache"), importers: Family());
        Assert.True(File.Exists(_work.At("assets/cooked/macos/a.png.bc.ktx2")));
    }

    [Fact]
    public async Task TheHookNamesWhatItDidToTheLog()
    {
        _work.WriteAsset("a.png", "a");
        var lines = new List<string>();
        var report = await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "ios",
            cachePath: _work.At("cache"),
            @out: "out",
            importers: Family(),
            log: lines.Add);
        Assert.Equal([$"orblit: cooked ios: {report.Summary}"], lines);
    }

    [Fact]
    public async Task ThePackingHookNamesTheCookAndThePackInTheLog()
    {
        _work.WriteAsset("a.png", "a");
        var lines = new List<string>();
        var summary = await BuildHook.PackDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "ios",
            name: "levels",
            cachePath: _work.At("cache"),
            @out: "out",
            importers: Family(),
            log: lines.Add);
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("orblit: cooked ios: ", lines[0]);
        Assert.Equal($"orblit: packed ios: 1 assets in 1 blobs, {summary.Bytes} bytes", lines[1]);
        Assert.True(File.Exists(_work.At("out/levels.ios.opack")));
    }

    [Fact]
    public async Task TheHookTellsTheBuildWhatItReadEvenWhenTheCookFailed()
    {
        _work.WriteAsset("a.png", "a");
        IEnumerable<Uri>? watched = null;
        await Assert.ThrowsAsync<CookFailedException>(() => BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            importers: CookProjectWork.Broken(),
            dependencies: files => watched = files.ToList()));
        Assert.Contains(_work.At("assets/a.png"), watched!.Select(uri => uri.LocalPath));
    }

    [Fact]
    public async Task TheHookWatchesWhatAnAssetDependsOn()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("data/dep.bin", "d");
        var importer = new RecordingImporter(extension: "png")
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>>
            {
                [Id("a.png")] = new HashSet<AssetId> { Id("data/dep.bin") },
            },
        };
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            importers: new ImporterRegistry([importer]),
            dependencies: files => watched = files.ToList());
        Assert.Contains(_work.At("assets/data/dep.bin"), watched!.Select(uri => uri.LocalPath));
    }

    [Fact]
    public async Task APathWithAHashAPercentAndASpaceSurvivesTheJourneyThroughAUri()
    {
        var root = Path.Combine(_work.Root, "odd #1 100% root");
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        await File.WriteAllTextAsync(Path.Combine(root, "assets", "a.png"), "a", TestContext.Current.CancellationToken);
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            @out: "out",
            importers: Family(),
            dependencies: files => watched = files.ToList());
        var paths = watched!.Select(uri => uri.LocalPath).ToList();
        Assert.Contains(Path.Combine(root, "assets", "a.png"), paths);
        Assert.Contains(Path.Combine(root, "assets") + Path.DirectorySeparatorChar, paths);
    }

    [Fact]
    public async Task ASettingsFileIsReportedOnlyBesideTheAssetThatHasOne()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("a.png.import.json", "{}");
        _work.WriteAsset("b.png", "b");
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            @out: "out",
            importers: Family(),
            dependencies: files => watched = files.ToList());
        var paths = watched!.Select(uri => uri.LocalPath).ToList();
        Assert.Contains(_work.At("assets/a.png.import.json"), paths);
        Assert.DoesNotContain(_work.At("assets/b.png.import.json"), paths);
    }

    [Fact]
    public async Task ThePackingHookRefusesAPlatformBeforeItTouchesTheDisk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => BuildHook.PackDuringBuildAsync(
            packageRoot: _work.Root, targetOs: "fuchsia"));
        Assert.StartsWith("Invalid argument (targetOs): is not a platform Orblit cooks for.", error.Message);
        Assert.Contains("\"fuchsia\"", error.Message);
        Assert.False(Directory.Exists(_work.At("assets")));
    }

    [Fact]
    public async Task TheHookRefusesAnAbsentPlatformWithNullText()
    {
        _work.WriteAsset("a.png", "a");
        var report = await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root, targetOs: null, cachePath: _work.At("cache"), @out: "out", importers: Family());
        Assert.True(report.Ok);
        Assert.True(File.Exists(_work.At("out/web/a.png.basis.ktx2")));
    }

    [Fact]
    public async Task TheHookReadsAndWritesWhereItIsTold()
    {
        _work.WriteAsset("../src/a.png", "a");
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root, targetOs: "macos", assets: "src", @out: "dist", cachePath: _work.At("cache"),
            importers: Family());
        Assert.True(File.Exists(_work.At("dist/macos/a.png.bc.ktx2")));
    }

    [Fact]
    public async Task ThePackingHookReadsAndWritesWhereItIsTold()
    {
        _work.WriteAsset("../src/a.png", "a");
        await BuildHook.PackDuringBuildAsync(
            packageRoot: _work.Root, targetOs: "macos", assets: "src", @out: "dist", cachePath: _work.At("cache"),
            importers: Family());
        Assert.True(File.Exists(_work.At("dist/macos/a.png.bc.ktx2")));
        Assert.True(File.Exists(_work.At("dist/game.macos.opack")));
    }

    [Fact]
    public async Task TheHookCooksThroughTheCacheItIsGiven()
    {
        _work.WriteAsset("a.png", "a");
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root, targetOs: "macos", @out: "out", cachePath: _work.At("cache"), importers: Family());
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(_work.At("cache"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ThePackingHookCooksThroughTheCacheItIsGiven()
    {
        _work.WriteAsset("a.png", "a");
        await BuildHook.PackDuringBuildAsync(
            packageRoot: _work.Root, targetOs: "macos", @out: "out", cachePath: _work.At("cache"), importers: Family());
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(_work.At("cache"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task TheLogSaysWebWhenThePlatformIsAbsent()
    {
        _work.WriteAsset("a.png", "a");
        var lines = new List<string>();
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root, targetOs: null, cachePath: _work.At("cache"), @out: "out", importers: Family(),
            log: lines.Add);
        Assert.StartsWith("orblit: cooked web: ", Assert.Single(lines));
    }

    [Fact]
    public async Task TwoAssetsWithTheSameBytesArePackedAsOneBlob()
    {
        _work.WriteAsset("a.png", "same");
        _work.WriteAsset("b.png", "same");
        var lines = new List<string>();
        await BuildHook.PackDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            @out: "out",
            importers: new ImporterRegistry([new RecordingImporter(extension: "png")]),
            log: lines.Add);
        Assert.EndsWith("2 assets in 1 blobs, " + new FileInfo(_work.At("out/game.macos.opack")).Length + " bytes", lines[1]);
    }

    [Fact]
    public async Task AHiddenFileAnAssetDependsOnIsStillWatched()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset(".secret/dep.bin", "d");
        var importer = new RecordingImporter(extension: "png")
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>>
            {
                [Id("a.png")] = new HashSet<AssetId> { Id(".secret/dep.bin") },
            },
        };
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            importers: new ImporterRegistry([importer]),
            dependencies: files => watched = files.ToList());
        Assert.Contains(_work.At("assets/.secret/dep.bin"), watched!.Select(uri => uri.LocalPath));
    }

    [Fact]
    public async Task EveryVisibleFolderIsWatchedAndNoHiddenOneIs()
    {
        _work.WriteAsset("a.png", "a");
        _work.WriteAsset("deep/inner/c.png", "c");
        _work.WriteAsset(".hidden/d.png", "d");
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.Root,
            targetOs: "macos",
            cachePath: _work.At("cache"),
            @out: "out",
            importers: Family(),
            dependencies: files => watched = files.ToList());
        var paths = watched!.Select(uri => uri.LocalPath).ToList();
        Assert.Contains(_work.At("assets/deep") + Path.DirectorySeparatorChar, paths);
        Assert.Contains(_work.At("assets/deep/inner") + Path.DirectorySeparatorChar, paths);
        Assert.DoesNotContain(_work.At("assets/.hidden") + Path.DirectorySeparatorChar, paths);
    }

    [Fact]
    public async Task ARelativePackageRootAndAFolderWithATrailingSlashAreReportedAsAbsolutePaths()
    {
        _work.WriteAsset("a.png", "a");
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: Path.GetRelativePath(Environment.CurrentDirectory, _work.Root),
            targetOs: "macos",
            assets: "assets/",
            @out: "out",
            cachePath: _work.At("cache"),
            importers: Family(),
            dependencies: files => watched = files.ToList());
        var paths = watched!.Select(uri => uri.LocalPath).ToList();
        Assert.Contains(_work.At("assets/a.png"), paths);
        Assert.Contains(_work.At("assets") + Path.DirectorySeparatorChar, paths);
    }

    [Fact]
    public async Task APackageRootThatEndsInASeparatorIsReportedWithOneSeparator()
    {
        _work.WriteAsset("../flat/a.png", "a");
        IEnumerable<Uri>? watched = null;
        await BuildHook.CookDuringBuildAsync(
            packageRoot: _work.At("flat") + Path.DirectorySeparatorChar,
            targetOs: "macos",
            assets: "",
            @out: "../out",
            cachePath: _work.At("cache"),
            importers: Family(),
            dependencies: files => watched = files.ToList());
        Assert.Contains(_work.At("flat") + Path.DirectorySeparatorChar, watched!.Select(uri => uri.LocalPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TheHookRunsFourImportersAtOnceUnlessToldOtherwise(bool pack) => AssertAtOnceAsync(4, async importers =>
    {
        if (pack)
        {
            await BuildHook.PackDuringBuildAsync(
                packageRoot: _work.Root, targetOs: "macos", cachePath: _work.At("cache"), importers: importers);
        }
        else
        {
            await BuildHook.CookDuringBuildAsync(
                packageRoot: _work.Root, targetOs: "macos", cachePath: _work.At("cache"), importers: importers);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TheHookRunsAsManyImportersAtOnceAsItIsTold(bool pack) => AssertAtOnceAsync(2, async importers =>
    {
        if (pack)
        {
            await BuildHook.PackDuringBuildAsync(
                packageRoot: _work.Root, targetOs: "macos", cachePath: _work.At("cache"), importers: importers, concurrency: 2);
        }
        else
        {
            await BuildHook.CookDuringBuildAsync(
                packageRoot: _work.Root, targetOs: "macos", cachePath: _work.At("cache"), importers: importers, concurrency: 2);
        }
    });

    private static ImporterRegistry Family() => new([new FamilyImporter()]);

    // Parks the importers at a gate and checks that exactly this many get there. The pause lets a hook that
    // runs more than that reach the gate too, so that it cannot pass by being slow.
    private async Task AssertAtOnceAsync(int expected, Func<ImporterRegistry, Task> hook)
    {
        for (var i = 0; i < 8; i++)
        {
            _work.WriteAsset($"{i}.txt", $"{i}");
        }

        var gated = new GatedImporter();
        var done = hook(new ImporterRegistry([gated]));
        await Waits.Until(() => gated.Parked == expected, $"{expected} importers at the gate");
        await Waits.Settle();
        Assert.Equal(expected, gated.Parked);
        while (!done.IsCompleted)
        {
            if (!gated.ReleaseOne())
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        await done;
        Assert.Equal(expected, gated.Peak);
    }
}
