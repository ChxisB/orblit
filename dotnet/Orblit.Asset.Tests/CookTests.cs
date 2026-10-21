using System.Collections.Concurrent;
using System.Text;

namespace Orblit.Asset.Tests;

public class CookTests
{
    private readonly MemoryCookCache _cache = new();
    private MemoryAssetSource _source = Project(("a.txt", "alpha"), ("b.txt", "beta"), ("README.md", "read me"));
    private RecordingImporter _importer = new();

    private static AssetId Id(string text) => AssetId.Parse(text);

    private static MemoryAssetSource Project(params (string Id, string Text)[] files) =>
        new(files.ToDictionary(file => Id(file.Id), file => Encoding.UTF8.GetBytes(file.Text)));

    private static HashSet<AssetId> Ids(params string[] ids) => ids.Select(Id).ToHashSet();

    private static void SameIds(IEnumerable<AssetId> actual, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            actual.Select(id => id.ToString()).Order(StringComparer.Ordinal));

    private static void HasMode(CookResult result, string mode)
    {
        Assert.Single(result.Key!.Settings);
        Assert.Equal(mode, result.Key.Settings["mode"]);
    }

    // A cooked output's actual bytes. CookOutput.Bytes is how many there are, not what they say, so reading one
    // goes back through the cache.
    private static async Task<string> TextOf(MemoryCookCache cache, CookResult result, string output) =>
        Encoding.UTF8.GetString((await cache.ReadAsync(result.Asset![output]!.Hash))!);

    private static byte[] Fail(ImportRequest request, string reason = "no good") =>
        throw new ImportFailureException(request.Id, reason);

    private Cook CookFor(CookTarget? target = null, RecordingImporter? importer = null)
    {
        _importer = importer ?? _importer;
        return new Cook(_source, _cache, new ImporterRegistry([_importer]), target ?? CookTarget.Any);
    }

    private void SceneProject()
    {
        _source = Project(("scene.txt", "scene"), ("a.txt", "alpha"), ("b.txt", "beta"));
        _importer = new RecordingImporter
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>> { [Id("scene.txt")] = Ids("a.txt", "b.txt") },
        };
    }

    [Fact]
    public async Task CooksAnAssetAndFilesTheResultUnderItsKey()
    {
        var result = await CookFor().CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Cooked, result.Status);
        Assert.Same(_importer, result.Importer);
        Assert.Equal("alpha", await TextOf(_cache, result, "out"));
        Assert.NotNull(await _cache.LookUpAsync(result.Key!));
    }

    [Fact]
    public async Task ASecondCookOfAnUnchangedAssetRunsNoImporter()
    {
        await CookFor().CookOneAsync(Id("a.txt"));
        Assert.Equal([Id("a.txt")], _importer.Imported);

        var again = await CookFor().CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Cached, again.Status);
        Assert.Equal("alpha", await TextOf(_cache, again, "out"));
        Assert.Equal([Id("a.txt")], _importer.Imported);
    }

    [Fact]
    public async Task ChangingTheSourceRecooksIt()
    {
        await CookFor().CookOneAsync(Id("a.txt"));
        _source = Project(("a.txt", "changed"));
        var result = await CookFor().CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Cooked, result.Status);
        Assert.Equal("changed", await TextOf(_cache, result, "out"));
    }

    [Fact]
    public async Task ChangingTheTargetRecooksIt()
    {
        await CookFor(new CookTarget("macos")).CookOneAsync(Id("a.txt"));
        var result = await CookFor(new CookTarget("android")).CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Cooked, result.Status);
    }

    [Fact]
    public async Task BumpingTheImporterVersionRecooksIt()
    {
        await CookFor(importer: new RecordingImporter()).CookOneAsync(Id("a.txt"));
        var result = await CookFor(importer: new RecordingImporter(version: 2)).CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Cooked, result.Status);
    }

    [Fact]
    public async Task AnUnclaimedAssetIsSkippedWithNoKeyAndNoOutputs()
    {
        var result = await CookFor().CookOneAsync(Id("README.md"));
        Assert.Equal(CookStatus.Skipped, result.Status);
        Assert.Null(result.Key);
        Assert.Null(result.Asset);
        Assert.Empty(_importer.Imported);
    }

    [Fact]
    public async Task SettingsAreResolvedBeforeTheKeySoADefaultCounts()
    {
        var result = await CookFor().CookOneAsync(Id("a.txt"));
        HasMode(result, "normal");
    }

    [Fact]
    public async Task SettingsComeFromTheAssetsOwnImportJson()
    {
        _source = Project(("a.txt", "alpha"), ("a.txt.import.json", """{"settings": {"mode": "special"}}"""));
        var result = await CookFor().CookOneAsync(Id("a.txt"));
        HasMode(result, "special");
    }

    [Fact]
    public async Task SettingsComeFromAFolderTooAndTheFileWinsOverTheFolder()
    {
        _source = Project(
            ("art/a.txt", "alpha"),
            ("art/b.txt", "beta"),
            ("art/.import.json", """{"settings": {"mode": "folder"}}"""),
            ("art/b.txt.import.json", """{"settings": {"mode": "file"}}"""));
        var cook = CookFor();
        HasMode(await cook.CookOneAsync(Id("art/a.txt")), "folder");
        HasMode(await cook.CookOneAsync(Id("art/b.txt")), "file");
    }

    [Fact]
    public async Task ChangingASettingRecooksTheAssetItAppliesTo()
    {
        _source = Project(("a.txt", "alpha"));
        await CookFor().CookOneAsync(Id("a.txt"));
        _source = Project(("a.txt", "alpha"), ("a.txt.import.json", """{"settings": {"mode": "special"}}"""));
        Assert.Equal(CookStatus.Cooked, (await CookFor().CookOneAsync(Id("a.txt"))).Status);
    }

    [Fact]
    public async Task ASettingTheImporterDoesNotUnderstandChangesNothing()
    {
        _source = Project(("a.txt", "alpha"));
        await CookFor().CookOneAsync(Id("a.txt"));
        _source = Project(("a.txt", "alpha"), ("a.txt.import.json", """{"settings": {"nonsense": 7}}"""));
        Assert.Equal(CookStatus.Cached, (await CookFor().CookOneAsync(Id("a.txt"))).Status);
    }

    [Fact]
    public async Task AMalformedImportJsonFailsThatAssetNamingTheFile()
    {
        _source = Project(("a.txt", "alpha"), ("a.txt.import.json", "{not json"));
        var result = await CookFor().CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("a.txt.import.json", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DependenciesGoIntoTheKeyAndComeBackInTheResult()
    {
        SceneProject();
        var result = await CookFor().CookOneAsync(Id("scene.txt"));
        SameIds(result.Dependencies, "a.txt", "b.txt");
        SameIds(result.Key!.Dependencies.Keys, "a.txt", "b.txt");
    }

    [Fact]
    public async Task ChangingADependencyRecooksWhatDependsOnIt()
    {
        SceneProject();
        await CookFor().CookOneAsync(Id("scene.txt"));
        _source = Project(("scene.txt", "scene"), ("a.txt", "ALPHA"), ("b.txt", "beta"));
        Assert.Equal(CookStatus.Cooked, (await CookFor().CookOneAsync(Id("scene.txt"))).Status);
    }

    [Fact]
    public async Task ChangingADependencyRecooksOnlyWhatDependsOnIt()
    {
        SceneProject();
        var ids = new[] { Id("scene.txt"), Id("a.txt"), Id("b.txt") };
        await CookFor().CookAllAsync(ids);
        _importer.Imported.Clear();

        _source = Project(("scene.txt", "scene"), ("a.txt", "ALPHA"), ("b.txt", "beta"));
        var report = await CookFor().CookAllAsync(ids);

        // b.txt did not change and nothing it feeds changed.
        SameIds(_importer.Imported, "scene.txt", "a.txt");
        Assert.Equal(1, report.Count(CookStatus.Cached));
    }

    [Fact]
    public async Task DependenciesAreAskedForBeforeTheCacheIsConsultedAndOnlyOncePerSourceFile()
    {
        SceneProject();
        await CookFor().CookAllAsync([Id("scene.txt"), Id("a.txt")]);
        Assert.Equal(2, _importer.Asked.Count);
    }

    [Fact]
    public async Task AMissingDependencyFailsTheAssetThatNamedIt()
    {
        SceneProject();
        _importer = new RecordingImporter
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>> { [Id("scene.txt")] = Ids("gone.txt") },
        };
        var result = await CookFor().CookOneAsync(Id("scene.txt"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.IsType<AssetNotFoundException>(result.Error);
    }

    [Fact]
    public async Task TheReportCollectsEveryDependencyForABuildToWatch()
    {
        SceneProject();
        var report = await CookFor().CookAllAsync([Id("scene.txt"), Id("a.txt")]);
        SameIds(report.Dependencies, "a.txt", "b.txt");
    }

    [Fact]
    public async Task AnAssetNothingClaimsIsIgnored() =>
        Assert.Equal(CookState.Ignored, await CookFor().StateOfAsync(Id("README.md")));

    [Fact]
    public async Task AnAssetThatHasNeverBeenCookedIsStale() =>
        Assert.Equal(CookState.Stale, await CookFor().StateOfAsync(Id("a.txt")));

    [Fact]
    public async Task AskingDoesNotCookIt()
    {
        await CookFor().StateOfAsync(Id("a.txt"));

        // The whole point is that an editor can ask a thousand times.
        Assert.Empty(_importer.Imported);
        var key = new CookKey(
            _importer.Name,
            _importer.Version,
            ContentHash.Of(Encoding.UTF8.GetBytes("alpha")),
            settings: new Dictionary<string, object?> { ["mode"] = "normal" },
            target: CookTarget.Any.Recipe);
        Assert.Null(await _cache.LookUpAsync(key));
    }

    [Fact]
    public async Task OneThatHasJustBeenCookedIsCooked()
    {
        await CookFor().CookOneAsync(Id("a.txt"));
        Assert.Equal(CookState.Cooked, await CookFor().StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task ChangingTheSourceMakesItStaleAgain()
    {
        await CookFor().CookOneAsync(Id("a.txt"));
        _source = Project(("a.txt", "changed"));
        Assert.Equal(CookState.Stale, await CookFor().StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task ChangingSomethingItDependsOnMakesItStale()
    {
        var discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>> { [Id("a.txt")] = Ids("b.txt") };
        _source = Project(("a.txt", "alpha"), ("b.txt", "beta"));
        await CookFor(importer: new RecordingImporter { Discovers = discovers }).CookOneAsync(Id("a.txt"));

        _source = Project(("a.txt", "alpha"), ("b.txt", "changed"));
        Assert.Equal(
            CookState.Stale,
            await CookFor(importer: new RecordingImporter { Discovers = discovers }).StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task ChangingItsSettingsMakesItStale()
    {
        await CookFor().CookOneAsync(Id("a.txt"));
        _source = Project(("a.txt", "alpha"), ("a.txt.import.json", """{"settings": {"mode": "special"}}"""));
        Assert.Equal(CookState.Stale, await CookFor().StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task CookedForOneTargetIsStillStaleForAnother()
    {
        await CookFor(new CookTarget("macos")).CookOneAsync(Id("a.txt"));
        Assert.Equal(CookState.Stale, await CookFor(new CookTarget("web")).StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task AnAssetThatIsNotThereIsFailed() =>
        Assert.Equal(CookState.Failed, await CookFor().StateOfAsync(Id("gone.txt")));

    [Fact]
    public async Task ADependencyThatIsNotThereIsFailed()
    {
        var importer = new RecordingImporter
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>> { [Id("a.txt")] = Ids("missing.txt") },
        };
        Assert.Equal(CookState.Failed, await CookFor(importer: importer).StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task AnImporterThatCannotReadTheAssetIsFailed()
    {
        var cook = new Cook(_source, _cache, new ImporterRegistry([new UnreadableImporter()]), CookTarget.Any);
        Assert.Equal(CookState.Failed, await cook.StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task AnAssetACookCouldNotCookIsFailedAfterwards()
    {
        var broken = new RecordingImporter { Transform = request => Fail(request) };

        // Nothing has tried yet, and nobody can know without trying.
        Assert.Equal(CookState.Stale, await CookFor(importer: broken).StateOfAsync(Id("a.txt")));

        await CookFor(importer: broken).CookOneAsync(Id("a.txt"));
        Assert.Equal(CookState.Failed, await CookFor(importer: broken).StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task ARememberedFailureDoesNotStopACookTryingAgain()
    {
        var attempts = 0;
        var flaky = new RecordingImporter
        {
            Transform = request => ++attempts == 1 ? Fail(request, "not installed") : request.Bytes,
        };
        Assert.Equal(CookStatus.Failed, (await CookFor(importer: flaky).CookOneAsync(Id("a.txt"))).Status);

        // Importers fail for reasons that are not in the key.
        Assert.Equal(CookStatus.Cooked, (await CookFor(importer: flaky).CookOneAsync(Id("a.txt"))).Status);
        Assert.Equal(CookState.Cooked, await CookFor(importer: flaky).StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task ChangingTheAssetClearsTheFailure()
    {
        var broken = new RecordingImporter { Transform = request => Fail(request) };
        await CookFor(importer: broken).CookOneAsync(Id("a.txt"));
        _source = Project(("a.txt", "fixed"));

        // A different asset is different work, not the failed work.
        Assert.Equal(CookState.Stale, await CookFor(importer: broken).StateOfAsync(Id("a.txt")));
    }

    [Fact]
    public async Task StateSaysTheSameThingTheCookGoesOnToDo()
    {
        Assert.Equal(CookState.Stale, await CookFor().StateOfAsync(Id("a.txt")));
        Assert.Equal(CookStatus.Cooked, (await CookFor().CookOneAsync(Id("a.txt"))).Status);
        Assert.Equal(CookState.Cooked, await CookFor().StateOfAsync(Id("a.txt")));
        Assert.Equal(CookStatus.Cached, (await CookFor().CookOneAsync(Id("a.txt"))).Status);
    }

    [Fact]
    public async Task StateOfAllAnswersForEveryOneItIsGiven()
    {
        await CookFor().CookOneAsync(Id("a.txt"));
        var states = await CookFor().StateOfAllAsync([Id("a.txt"), Id("b.txt"), Id("README.md"), Id("gone.txt")]);
        Assert.Equal(4, states.Count);
        Assert.Equal(CookState.Cooked, states[Id("a.txt")]);
        Assert.Equal(CookState.Stale, states[Id("b.txt")]);
        Assert.Equal(CookState.Ignored, states[Id("README.md")]);
        Assert.Equal(CookState.Failed, states[Id("gone.txt")]);
    }

    [Fact]
    public async Task StateOfAllReadsASharedDependencyOnceForTheWholeAnswer()
    {
        var reads = new ConcurrentDictionary<AssetId, int>();
        var bytes = new Dictionary<AssetId, byte[]>
        {
            [Id("a.txt")] = Encoding.UTF8.GetBytes("alpha"),
            [Id("b.txt")] = Encoding.UTF8.GetBytes("beta"),
            [Id("shared.txt")] = Encoding.UTF8.GetBytes("shared"),
        };
        var counting = new CallbackAssetSource(id =>
        {
            reads.AddOrUpdate(id, 1, (_, count) => count + 1);
            return Task.FromResult(bytes.TryGetValue(id, out var found) ? found.ToArray() : null);
        });
        var importer = new RecordingImporter
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>>
            {
                [Id("a.txt")] = Ids("shared.txt"),
                [Id("b.txt")] = Ids("shared.txt"),
            },
        };

        await new Cook(counting, _cache, new ImporterRegistry([importer]), CookTarget.Any)
            .StateOfAllAsync([Id("a.txt"), Id("b.txt")]);

        Assert.Equal(1, reads[Id("shared.txt")]);
    }

    [Fact]
    public async Task AnEmptyListIsAnEmptyAnswer() =>
        Assert.Empty(await CookFor().StateOfAllAsync([]));

    [Fact]
    public async Task CookAllReportsInTheOrderItWasGivenWhateverOrderTheyFinishIn()
    {
        var ids = new[] { Id("a.txt"), Id("README.md"), Id("b.txt") };
        var report = await CookFor().CookAllAsync(ids);
        Assert.Equal(ids, report.Results.Select(result => result.Id));
    }

    [Fact]
    public async Task CookAllCallsBackAsEachLands()
    {
        var seen = new List<AssetId>();
        await CookFor().CookAllAsync([Id("a.txt"), Id("b.txt")], result => seen.Add(result.Id));
        SameIds(seen, "a.txt", "b.txt");
    }

    [Fact]
    public async Task OneBrokenAssetDoesNotStopTheRest()
    {
        _importer = new RecordingImporter
        {
            Transform = request => request.Id == Id("b.txt") ? Fail(request) : request.Bytes,
        };
        var report = await CookFor().CookAllAsync([Id("a.txt"), Id("b.txt")]);
        Assert.False(report.Ok);
        Assert.Equal(1, report.Count(CookStatus.Cooked));
        Assert.Equal(1, report.Count(CookStatus.Failed));
        Assert.IsType<ImportFailureException>(report.WithStatus(CookStatus.Failed).Single().Error);
    }

    [Fact]
    public async Task CookAllSummarisesWhatItDid()
    {
        var report = await CookFor().CookAllAsync([Id("a.txt"), Id("b.txt"), Id("README.md")]);
        Assert.Equal("2 cooked, 1 skipped", report.Summary);
        Assert.Equal("nothing to cook", new CookReport([]).Summary);
    }

    [Fact]
    public async Task NothingToDoIsACleanReportNotAnEmptyFailure()
    {
        var report = await CookFor().CookAllAsync([]);
        Assert.True(report.Ok);
        Assert.Empty(report.Results);
    }

    [Fact]
    public async Task RunsAtMostConcurrencyImportersAtOnce()
    {
        var gated = new GatedImporter();
        var ids = Enumerable.Range(0, 8).Select(i => Id($"{i}.txt")).ToList();
        var cook = new Cook(
            new MemoryAssetSource(ids.ToDictionary(id => id, id => Encoding.UTF8.GetBytes(id.ToString()))),
            new MemoryCookCache(),
            new ImporterRegistry([gated]),
            CookTarget.Any,
            concurrency: 3);
        var done = cook.CookAllAsync(ids);

        // Let the first batch reach the gate, then release everything one at a time.
        await Waits.Until(() => gated.Parked == 3, "three importers at the gate");
        while (!done.IsCompleted)
        {
            if (!gated.ReleaseOne())
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        await done;
        Assert.Equal(3, gated.Peak);
    }

    [Fact]
    public void AConcurrencyBelowOneIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Cook(_source, _cache, new ImporterRegistry([_importer]), CookTarget.Any, concurrency: 0));

    [Fact]
    public async Task AnImporterSeesTheResolvedSettingsAndTheTarget()
    {
        IReadOnlyDictionary<string, object?>? seenSettings = null;
        CookTarget? seenTarget = null;
        _importer = new RecordingImporter
        {
            Transform = request =>
            {
                seenSettings = request.Settings;
                seenTarget = request.Target;
                return request.Bytes;
            },
        };
        var target = new CookTarget("web", new Dictionary<string, object?> { ["maxTextureSize"] = 2048L });
        await CookFor(target).CookOneAsync(Id("a.txt"));
        Assert.Equal("normal", Assert.Single(seenSettings!).Value);
        Assert.Equal("web", seenTarget!.Name);
        Assert.Equal(2048L, seenTarget.MaxTextureSize());
    }
}
