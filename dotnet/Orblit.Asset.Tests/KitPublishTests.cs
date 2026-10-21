namespace Orblit.Asset.Tests;

// Publishing a kit: the file, the release it fills in and the entry that lists it.
public sealed class KitPublishTests : IDisposable
{
    private const string Url = "https://example.com/kits/crate-pack-1.0.0.opack";

    private static readonly DateTime Day = new(2026, 10, 19, 23, 59, 0, DateTimeKind.Utc);

    private readonly KitWorkspace _work = new();

    public KitPublishTests() => Directory.CreateDirectory(_work.At("kits"));

    public void Dispose() => _work.Dispose();

    // A folder holding a manifest at version and models/crate.obj.
    private async Task<string> SourceAsync(string version = "1.0.0")
    {
        var folder = _work.At("crates");
        var manifest = KitWorkspace.Authored();
        manifest["version"] = version;
        await KitWorkspace.WriteAsync(
            folder, new() { ["kit.json"] = manifest.ToJsonString(), ["models/crate.obj"] = "v 0 0 0" });
        return folder;
    }

    private async Task<KitPublished> PublishAsync(
        string version = "1.0.0", string address = Url, string notes = "", bool listed = true) =>
        await KitPacking.PublishAsync(new KitPublishRequest
        {
            Folder = await SourceAsync(version),
            Url = address,
            OutPath = _work.At($"crate-pack-{version}.opack"),
            Released = Day,
            Notes = notes,
            EntriesPath = listed ? _work.At("kits") : null,
        });

    private string EntryFile() => _work.At("kits", "brass-lamp/crate-pack.json");

    [Fact]
    public async Task PacksTheFolderAndFillsTheReleaseInFromTheFile()
    {
        var made = await PublishAsync(notes: "First release.", listed: false);

        var release = made.Release;
        Assert.Equal("1.0.0", release.Version);
        Assert.Equal(Url, release.Url);
        Assert.Equal(made.Summary.Hash, release.Hash);
        Assert.Equal(new FileInfo(_work.At("crate-pack-1.0.0.opack")).Length, release.Bytes);
        Assert.Equal("2026-10-19", release.Released);
        Assert.Equal("First release.", release.Notes);
        Assert.Equal("brass-lamp", made.Entry.Publisher);
        Assert.Equal("crate-pack", made.Entry.Slug);
        Assert.Single(made.Entry.Releases);
        Assert.Null(made.EntryPath);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_work.At("kits")));
    }

    [Fact]
    public async Task AddsAReleaseToTheEntryThatIsThere()
    {
        await PublishAsync();
        await PublishAsync("1.1.0", "https://example.com/kits/crate-pack-1.1.0.opack");

        var read = KitEntry.Parse(
            await File.ReadAllBytesAsync(EntryFile(), TestContext.Current.CancellationToken), "brass-lamp", "crate-pack");

        Assert.Equal(["1.1.0", "1.0.0"], read.Releases.Select(release => release.Version));
    }

    [Fact]
    public async Task RefusesAVersionThatIsListedAndKeepsTheFile()
    {
        await PublishAsync();
        var before = await File.ReadAllTextAsync(EntryFile(), TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAnyAsync<FormatException>(
            async () => await PublishAsync(address: "https://example.com/other.opack"));

        Assert.Contains("1.0.0", error.Message);
        Assert.Contains("kit.json", error.Message);
        Assert.Equal(before, await File.ReadAllTextAsync(EntryFile(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARefusalLeavesTheKitFileAsItWasAndNoneBesideIt()
    {
        await PublishAsync();
        var file = _work.At("crate-pack-1.0.0.opack");
        var before = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(await SourceAsync(), "models/more.obj"), "v 1 1 1", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<FormatException>(async () => await PublishAsync());

        Assert.Equal(before, await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(_work.At("")), path => path.EndsWith(".staged", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefusesAKitFileInsideTheFolderAndKeepsItsFiles()
    {
        var folder = await SourceAsync();
        await File.WriteAllTextAsync(Path.Combine(folder, "README.md"), "mine", TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAnyAsync<FormatException>(
            async () => await KitPacking.PublishAsync(new KitPublishRequest
            {
                Folder = folder,
                Url = "https://example.com/README.md",
                OutPath = Path.Combine(folder, "README.md"),
                Released = Day,
            }));

        Assert.Contains("inside", error.Message);
        Assert.Equal("mine", await File.ReadAllTextAsync(Path.Combine(folder, "README.md"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesAUrlThatCiWouldNotFetchAndWritesNoEntry()
    {
        await Assert.ThrowsAnyAsync<FormatException>(async () => await PublishAsync(address: "http://example.com/a.opack"));

        Assert.False(File.Exists(EntryFile()));
        Assert.False(File.Exists(_work.At("crate-pack-1.0.0.opack")));
    }

    [Fact]
    public async Task RefusesALongNoteAndAFolderThatBreaksARule()
    {
        await Assert.ThrowsAnyAsync<FormatException>(async () => await PublishAsync(notes: new string('x', 400)));
        await File.WriteAllTextAsync(
            Path.Combine(await SourceAsync(), ".DS_Store"), "x", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<FormatException>(async () => await PublishAsync());

        Assert.False(File.Exists(EntryFile()));
    }

    [Fact]
    public async Task AnEntriesFolderThatIsNotThereIsAnArgumentError() =>
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await KitPacking.PublishAsync(new KitPublishRequest
            {
                Folder = await SourceAsync(),
                Url = Url,
                OutPath = _work.At("crate-pack-1.0.0.opack"),
                Released = Day,
                EntriesPath = _work.At("nowhere"),
            }));

    [Fact]
    public async Task AnEntryThatIsThereAndBadIsNamedAndNotReplaced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(EntryFile())!);
        await File.WriteAllTextAsync(EntryFile(), "{\"releases\": [], \"extra\": 1}", TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAnyAsync<FormatException>(async () => await PublishAsync());

        Assert.Contains("brass-lamp/crate-pack", error.Message);
        Assert.Equal(
            "{\"releases\": [], \"extra\": 1}",
            await File.ReadAllTextAsync(EntryFile(), TestContext.Current.CancellationToken));
    }
}
