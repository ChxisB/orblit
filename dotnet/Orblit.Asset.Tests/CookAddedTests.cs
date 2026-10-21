using System.Text;

namespace Orblit.Asset.Tests;

// What the Dart cook does and its tests never said, and what the C# port does differently on purpose.
public class CookAddedTests
{
    private static AssetId Id(string text) => AssetId.Parse(text);

    private static MemoryAssetSource Project(params (string Id, string Text)[] files) =>
        new(files.ToDictionary(file => Id(file.Id), file => Encoding.UTF8.GetBytes(file.Text)));

    private static Cook CookWith(IAssetSource source, ICookCache cache, Importer importer, int concurrency = 4) =>
        new(source, cache, new ImporterRegistry([importer]), CookTarget.Any, concurrency: concurrency);

    [Fact]
    public async Task AnImportFailureResultNamesTheImporterTheKeyAndTheError()
    {
        var importer = new RecordingImporter { Transform = request => throw new ImportFailureException(request.Id, "no good") };
        var result = await CookWith(Project(("a.txt", "alpha")), new MemoryCookCache(), importer).CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.False(result.Ok);
        Assert.Same(importer, result.Importer);
        Assert.NotNull(result.Key);
        Assert.IsType<ImportFailureException>(result.Error);
        Assert.Null(result.Asset);
    }

    [Fact]
    public async Task AFailureBeforeTheKeyNamesTheImporterAndHasNoKey()
    {
        var importer = new RecordingImporter
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>> { [Id("a.txt")] = new HashSet<AssetId> { Id("gone.txt") } },
        };
        var cache = new MemoryCookCache();
        var result = await CookWith(Project(("a.txt", "alpha")), cache, importer).CookOneAsync(Id("a.txt"));
        Assert.Same(importer, result.Importer);
        Assert.Null(result.Key);
    }

    [Fact]
    public async Task ACachedResultCarriesTheKeyTheAssetAndTheDependencies()
    {
        var importer = new RecordingImporter
        {
            Discovers = new Dictionary<AssetId, IReadOnlySet<AssetId>> { [Id("a.txt")] = new HashSet<AssetId> { Id("b.txt") } },
        };
        var cook = CookWith(Project(("a.txt", "alpha"), ("b.txt", "beta")), new MemoryCookCache(), importer);
        await cook.CookOneAsync(Id("a.txt"));
        var again = await cook.CookOneAsync(Id("a.txt"));
        Assert.Equal(CookStatus.Cached, again.Status);
        Assert.Same(importer, again.Importer);
        Assert.NotNull(again.Key);
        Assert.NotNull(again.Asset);
        Assert.Equal([Id("b.txt")], again.Dependencies);
    }

    [Fact]
    public async Task NotesComeThroughFromTheImporter()
    {
        var importer = new RecordingImporter { Notes = ["dropped a mip level"] };
        var result = await CookWith(Project(("a.txt", "alpha")), new MemoryCookCache(), importer).CookOneAsync(Id("a.txt"));
        Assert.Equal(["dropped a mip level"], result.Notes);
    }

    [Fact]
    public async Task ACacheThatCannotRecordAFailureDoesNotReplaceTheFailure()
    {
        var importer = new RecordingImporter { Transform = request => throw new ImportFailureException(request.Id, "no good") };
        var cook = CookWith(Project(("a.txt", "alpha")), new FailureRefusingCache(new MemoryCookCache()), importer);
        var result = await cook.CookOneAsync(Id("a.txt"));
        Assert.IsType<ImportFailureException>(result.Error);
    }

    [Fact]
    public async Task ASettingsReaderPassedInIsTheOneUsed()
    {
        var reader = new ImportSettingsReader(Project(("a.txt.import.json", """{"settings": {"mode": "special"}}""")));
        var cook = new Cook(
            Project(("a.txt", "alpha")),
            new MemoryCookCache(),
            new ImporterRegistry([new RecordingImporter()]),
            CookTarget.Any,
            reader);
        var result = await cook.CookOneAsync(Id("a.txt"));
        Assert.Equal("special", result.Key!.Settings["mode"]);
    }

    [Fact]
    public async Task TheCallbackIsNeverRunTwiceAtOnce()
    {
        var gated = new GatedImporter();
        var ids = Enumerable.Range(0, 4).Select(i => Id($"{i}.txt")).ToList();
        var cook = CookWith(
            new MemoryAssetSource(ids.ToDictionary(id => id, id => Encoding.UTF8.GetBytes(id.ToString()))),
            new MemoryCookCache(),
            gated);
        var inside = 0;
        var overlapped = false;
        var done = cook.CookAllAsync(ids, _ =>
        {
            overlapped |= Interlocked.Increment(ref inside) > 1;
            Thread.Sleep(50);
            Interlocked.Decrement(ref inside);
        });

        await Waits.Until(() => gated.Parked == 4, "four importers at the gate");
        while (gated.ReleaseOne())
        {
        }

        await done;
        Assert.False(overlapped);
    }

    [Fact]
    public async Task EachResultSaysWhatHappenedInWords()
    {
        var cache = new MemoryCookCache();
        var importer = new RecordingImporter { Transform = request => request.Id == Id("b.txt") ? throw new ImportFailureException(request.Id, "no good") : request.Bytes };
        var cook = CookWith(Project(("a.txt", "alpha"), ("b.txt", "beta"), ("README.md", "read me")), cache, importer);
        Assert.Equal("cooked a.txt with recording v1", (await cook.CookOneAsync(Id("a.txt"))).ToString());
        Assert.Equal("cached a.txt", (await cook.CookOneAsync(Id("a.txt"))).ToString());
        Assert.Equal("skipped README.md", (await cook.CookOneAsync(Id("README.md"))).ToString());
        Assert.StartsWith("failed b.txt: ", (await cook.CookOneAsync(Id("b.txt"))).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportOfCookedAndSkippedAssetsIsOk()
    {
        var cook = CookWith(Project(("a.txt", "alpha"), ("README.md", "read me")), new MemoryCookCache(), new RecordingImporter());
        var report = await cook.CookAllAsync([Id("a.txt"), Id("README.md")]);
        Assert.True(report.Ok);
        Assert.Equal("1 cooked, 1 skipped", report.ToString());
    }

    [Fact]
    public void ASummaryListsEveryStatusInTheOrderOfTheEnum()
    {
        var report = new CookReport(
        [
            new CookResult { Id = Id("a.txt"), Status = CookStatus.Failed },
            new CookResult { Id = Id("b.txt"), Status = CookStatus.Skipped },
            new CookResult { Id = Id("c.txt"), Status = CookStatus.Cached },
            new CookResult { Id = Id("d.txt"), Status = CookStatus.Cooked },
            new CookResult { Id = Id("e.txt"), Status = CookStatus.Cooked },
        ]);
        Assert.Equal("2 cooked, 1 cached, 1 skipped, 1 failed", report.Summary);
    }

    [Fact]
    public async Task OneWorkerIsAllowedAndCooksEveryAsset()
    {
        var cook = CookWith(Project(("a.txt", "a"), ("b.txt", "b"), ("c.txt", "c")), new MemoryCookCache(), new RecordingImporter(), concurrency: 1);
        var report = await cook.CookAllAsync([Id("a.txt"), Id("b.txt"), Id("c.txt")]);
        Assert.Equal(1, cook.Concurrency);
        Assert.Equal(3, report.Count(CookStatus.Cooked));
    }

    [Fact]
    public void ZeroIsNotAPositiveTextureSize()
    {
        var target = new CookTarget("zero", new Dictionary<string, object?> { ["maxTextureSize"] = 0L });
        Assert.Equal("maxTextureSize", Assert.Throws<ArgumentException>(() => target.MaxTextureSize()).ParamName);
    }
}
