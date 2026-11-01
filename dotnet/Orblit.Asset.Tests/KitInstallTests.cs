namespace Orblit.Asset.Tests;

// Installing a kit into a project: where the files go, which of them, and the scripts a kit holds.
public sealed class KitInstallTests : IDisposable
{
    private readonly KitWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    private static HashSet<AssetId> Ids(params string[] names) => [.. names.Select(AssetId.Parse)];

    private static string[] Names(IEnumerable<AssetId> list) => [.. list.Select(id => id.ToString())];

    private static Task<string> TextOfAsync(string path) => File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

    [Fact]
    public async Task WritesThePickedFilesUnderTheFolder()
    {
        var kit = await _work.KitOfAsync(new() { ["models/crate.obj"] = "v 0 0 0", ["readme.txt"] = "Twelve." });
        var mine = await _work.ProjectAsync([]);
        var written = await KitExtraction.ExtractAsync(
            kit, mine, new KitExtractionOptions { Into = "assets/kits/crate-pack", Only = Ids("models/crate.obj") });
        Assert.Equal(["models/crate.obj"], Names(written));
        Assert.Equal("v 0 0 0", await TextOfAsync(Path.Combine(mine, "assets/kits/crate-pack/models/crate.obj")));
        Assert.False(File.Exists(Path.Combine(mine, "assets/kits/crate-pack/readme.txt")));
        Assert.False(File.Exists(Path.Combine(mine, "models/crate.obj")));
    }

    [Fact]
    public async Task LooksForConflictsAtTheNewPlaceAndInThePickOnly()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "kit a", ["b.txt"] = "kit b" });
        var mine = await _work.ProjectAsync(
            new() { ["kits/a.txt"] = "mine", ["kits/b.txt"] = "kit b", ["b.txt"] = "elsewhere" });
        var source = new DirectoryAssetSource(mine);
        Assert.Equal(["a.txt"], Names(await kit.ConflictsInAsync(source, "kits")));
        Assert.Empty(await kit.ConflictsInAsync(source, "kits", Ids("b.txt")));
        Assert.Equal(["b.txt"], Names(await kit.ConflictsInAsync(source)));
    }

    [Fact]
    public async Task AConflictOutsideThePickDoesNotStopTheInstall()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "kit a", ["b.txt"] = "kit b" });
        var mine = await _work.ProjectAsync(new() { ["a.txt"] = "mine" });
        await KitExtraction.ExtractAsync(kit, mine, new KitExtractionOptions { Only = Ids("b.txt") });
        Assert.Equal("mine", await TextOfAsync(Path.Combine(mine, "a.txt")));
        Assert.Equal("kit b", await TextOfAsync(Path.Combine(mine, "b.txt")));
    }

    [Fact]
    public async Task AConflictInsideThePickStopsItAndWritesNothing()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "kit a", ["b.txt"] = "kit b" });
        var mine = await _work.ProjectAsync(new() { ["kits/a.txt"] = "mine" });
        await Assert.ThrowsAsync<KitConflictsException>(
            () => KitExtraction.ExtractAsync(kit, mine, new KitExtractionOptions { Into = "kits" }));
        Assert.False(File.Exists(Path.Combine(mine, "kits/b.txt")));
    }

    [Fact]
    public async Task RefusesANameTheKitDoesNotHold()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "kit a" });
        var mine = await _work.ProjectAsync([]);
        await Assert.ThrowsAsync<ArgumentException>(
            () => KitExtraction.ExtractAsync(kit, mine, new KitExtractionOptions { Only = Ids("kit.json") }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => kit.ConflictsInAsync(new DirectoryAssetSource(mine), only: Ids("x.txt")));
    }

    [Fact]
    public async Task RefusesAFolderThatWouldWriteSomewhereHidden()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "kit a" });
        var mine = await _work.ProjectAsync([]);
        foreach (var into in new[] { ".git/hooks", "a//b", "/abs", "dir/", "nul" })
        {
            await Assert.ThrowsAnyAsync<FormatException>(
                () => KitExtraction.ExtractAsync(kit, mine, new KitExtractionOptions { Into = into }));
        }

        Assert.Empty(Directory.EnumerateFileSystemEntries(mine));
    }

    [Fact]
    public async Task RefusesAFolderThatIsALink()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "kit a" });
        var mine = await _work.ProjectAsync([]);
        var outside = Directory.CreateDirectory(_work.At("outside"));
        Directory.CreateSymbolicLink(Path.Combine(mine, "kits"), outside.FullName);
        await Assert.ThrowsAnyAsync<FormatException>(
            () => KitExtraction.ExtractAsync(kit, mine, new KitExtractionOptions { Into = "kits" }));
        Assert.Empty(outside.EnumerateFileSystemInfos());
    }

    [Fact]
    public async Task ListsTheFilesAPersonCouldRun()
    {
        var kit = await _work.KitOfAsync(
            new() { ["models/crate.obj"] = "v 0 0 0", ["tool/build.sh"] = "echo hi", ["run.js"] = "1" });
        Assert.Equal(["run.js", "tool/build.sh"], Names(kit.Scripts));
        Assert.True(kit.Manifest.ContainsScripts);
    }

    [Fact]
    public async Task HasNoScriptsInAKitOfArt()
    {
        Assert.Empty((await _work.KitOfAsync(new() { ["models/crate.obj"] = "v 0 0 0" })).Scripts);
    }
}
