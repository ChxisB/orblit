using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

// Building a store of kits: the entries are read, every release is fetched and checked, and the files a
// browser reads are written whole or not at all.
public sealed class KitStoreTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3];
    private static readonly byte[] Jpg = [0xff, 0xd8, 0xff, 0xe0, 1, 2, 3];
    private static readonly byte[] Webp = [.. "RIFF"u8, 1, 0, 0, 0, .. "WEBP"u8];

    // A name that is not a plain file name, built here because the escape would be written out by the tools.
    private static readonly string Esc = ((char)27).ToString();

    public static bool NotWindows => !OperatingSystem.IsWindows();

    private readonly KitWorkspace _work = new();
    private readonly Dictionary<WebUrl, TransportPage> _pages = [];

    public KitStoreTests() => Directory.CreateDirectory(_work.At("entries"));

    public void Dispose() => _work.Dispose();

    private readonly record struct Served(string Url, ContentHash Hash, int Bytes);

    private static JsonObject ManifestOf(string publisher, string slug, string version, string[] previews) => new()
    {
        ["slug"] = slug,
        ["publisher"] = publisher,
        ["name"] = $"Kit {slug}",
        ["summary"] = "Some files.",
        ["version"] = version,
        ["licence"] = new JsonObject { ["spdx"] = "MIT", ["url"] = "https://example.com/l" },
        ["engine"] = new JsonObject { ["from"] = "0.30.0", ["to"] = "0.40.0" },
        ["tags"] = new JsonArray("props"),
        ["previews"] = new JsonArray([.. previews.Select(name => JsonValue.Create(name))]),
    };

    private Served Serve(string name, byte[] bytes, ContentHash hash)
    {
        var url = $"https://example.com/{name}.opack";
        _pages[WebUrl.Parse(url)] = new TransportPage(bytes);
        return new Served(url, hash, bytes.Length);
    }

    // A kit packed from a folder and served over the fake network.
    private async Task<Served> KitFileAsync(
        string publisher, string slug, string version = "1.0.0", Dictionary<string, byte[]>? files = null, string[]? previews = null)
    {
        var folder = _work.At($"src_{_work.Next()}");
        foreach (var (name, bytes) in files ?? new() { ["models/crate.obj"] = "v 0"u8.ToArray() })
        {
            var file = Path.Combine(folder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(folder, "kit.json"),
            ManifestOf(publisher, slug, version, previews ?? []).ToJsonString(),
            TestContext.Current.CancellationToken);
        var output = _work.At($"pack_{_work.Next()}.opack");
        var summary = await KitPacking.PackAsync(folder, output);
        return Serve(
            $"{publisher}-{slug}-{version}",
            await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken),
            summary.Hash);
    }

    // A pack that KitPacking would refuse to write, built by hand. manifest replaces the manifest, which is
    // how a kit with no licence is made.
    private async Task<Served> BrokenKitAsync(
        string name, JsonObject? manifest = null, Dictionary<string, byte[]>? files = null)
    {
        var packed = ManifestOf("amber", "rocks", "1.0.0", []);
        packed["kind"] = KitManifest.Kind;
        packed["formatVersion"] = 1;
        packed["containsScripts"] = false;
        var bytes = Encoding.UTF8.GetBytes((manifest ?? packed).ToJsonString());
        var contents = (files ?? new() { ["a.txt"] = [1] }).ToDictionary(file => AssetId.Parse(file.Key), file => file.Value);
        var entries = new Dictionary<AssetId, AssetEntry>
        {
            [KitManifest.ManifestId] = new AssetEntry(ContentHash.Of(bytes), bytes.Length),
        };
        foreach (var (id, value) in contents)
        {
            entries[id] = new AssetEntry(ContentHash.Of(value), value.Length);
        }

        var output = _work.At($"hand_{_work.Next()}.opack");
        var summary = await PackWriter.WriteAsync(
            new AssetManifest(entries), output, KitManifest.Target, id => Task.FromResult(contents.GetValueOrDefault(id) ?? bytes));
        return Serve(name, await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken), summary.Hash);
    }

    private static JsonObject Release(Served served, string version = "1.0.0", Action<JsonObject>? change = null)
    {
        var release = new JsonObject
        {
            ["version"] = version,
            ["url"] = served.Url,
            ["hash"] = served.Hash.Hex,
            ["bytes"] = served.Bytes,
            ["released"] = "2026-10-19",
            ["notes"] = $"Changes {version}.",
        };
        change?.Invoke(release);
        return release;
    }

    private void Entry(string publisher, string slug, params JsonObject[] releases) =>
        Write($"entries/{publisher}/{slug}.json", new JsonObject { ["releases"] = new JsonArray([.. releases]) }.ToJsonString());

    private void Write(string name, string text)
    {
        var file = _work.At(name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    private byte[] Read(string name) => File.ReadAllBytes(_work.At(name));

    private async Task<KitStoreBuilt> BuildAsync()
    {
        using var transport = new MapTransport(_pages);
        return await KitStoreBuilder.BuildAsync(_work.At("entries"), _work.At("out"), transport);
    }

    // Builds and expects a refusal that holds fragment and leaves no folder.
    private async Task FailsAsync(string fragment)
    {
        var error = await Assert.ThrowsAnyAsync<FormatException>(BuildAsync);
        Assert.Contains(fragment, error.Message);
        Assert.False(Directory.Exists(_work.At("out")));
        Assert.Empty(Staged());
    }

    // Names in the workspace that a build leaves behind while it writes.
    private string[] Staged() =>
        [.. Directory.EnumerateFileSystemEntries(_work.At("")).Where(path => path.EndsWith(".partial", StringComparison.Ordinal))];

    // A network that never answers.
    private sealed class Stuck : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            new TaskCompletionSource<FetchReply>().Task;

        public void Dispose()
        {
        }
    }

    // A network that runs before when a request is sent.
    private sealed class Before(IAssetTransport inner, Action before) : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            before();
            return inner.SendAsync(request, cancellation);
        }

        public void Dispose() => inner.Dispose();
    }

    private async Task SampleAsync()
    {
        var crates1 = await KitFileAsync(
            "brass-lamp", "crates", files: new() { ["models/crate.obj"] = "crate one"u8.ToArray(), ["shots/a.png"] = Png }, previews: ["shots/a.png"]);
        var crates2 = await KitFileAsync(
            "brass-lamp", "crates", "1.1.0", new() { ["models/crate.obj"] = "crate two"u8.ToArray(), ["shots/a.png"] = Png }, ["shots/a.png"]);
        var lamps = await KitFileAsync("brass-lamp", "lamps", "0.2.0", new() { ["scripts/flicker.js"] = "1"u8.ToArray() });
        var rocks = await KitFileAsync(
            "amber", "rocks", "3.0.0", new() { ["a.jpg"] = Jpg, ["b.webp"] = Webp, ["rock.obj"] = [1] }, ["a.jpg", "b.webp"]);
        Entry("brass-lamp", "crates", Release(crates2, "1.1.0"), Release(crates1));
        Entry("brass-lamp", "lamps", Release(lamps, "0.2.0"));
        Entry("amber", "rocks", Release(rocks, "3.0.0"));
    }

    [Fact]
    public async Task BuildsThreeKitsIntoFilesABrowserReadsFromAFolder()
    {
        await SampleAsync();

        Assert.Equal(new KitStoreBuilt(3, 4), await BuildAsync());

        // Read back from a plain directory, as the editor does with no network.
        var index = KitIndex.Parse(Read("out/index.json"));
        Assert.Equal(["rocks", "crates", "lamps"], index.Rows.Select(row => row.Manifest.Slug));
        var crates = index.Rows[1];
        Assert.Equal("1.1.0", crates.Manifest.Version);
        Assert.Equal("Kit crates", crates.Manifest.Name);
        Assert.Equal("2026-10-19", crates.Updated);
        Assert.Equal("kits/brass-lamp/crates/preview-0.png", crates.Thumbnail);
        Assert.True(index.Rows[2].Manifest.ContainsScripts);
        Assert.Null(index.Rows[2].Thumbnail);

        var list = KitReleases.Parse(Read("out/kits/brass-lamp/crates/releases.json"));
        Assert.Equal(["1.1.0", "1.0.0"], list.Releases.Select(release => release.Release.Version));
        Assert.Equal("Changes 1.1.0.", list.Releases[0].Release.Notes);
        Assert.Equal(["preview-0.png"], list.Previews);
        Assert.Equal(Png, Read("out/kits/brass-lamp/crates/preview-0.png"));

        var rocks = KitReleases.Parse(Read("out/kits/amber/rocks/releases.json"));
        Assert.Equal(["preview-0.jpg", "preview-1.webp"], rocks.Previews);
        Assert.Equal(Webp, Read("out/kits/amber/rocks/preview-1.webp"));
        Assert.True(KitReleases.Parse(Read("out/kits/brass-lamp/lamps/releases.json")).Releases.Single().ContainsScripts);
        Assert.Equal(7, Directory.EnumerateFiles(_work.At("out"), "*", SearchOption.AllDirectories).Count());
        Assert.Empty(Staged());
    }

    [Fact]
    public async Task WritesIntoAFolderNamedWithATrailingSlash()
    {
        await SampleAsync();
        using var transport = new MapTransport(_pages);

        await KitStoreBuilder.BuildAsync(_work.At("entries"), _work.At("out") + Path.DirectorySeparatorChar, transport);

        Assert.True(File.Exists(_work.At("out", "index.json")));
        Assert.Empty(Staged());
    }

    [Fact]
    public async Task AnEmptyEntriesFolderBuildsAnEmptyIndex()
    {
        Write("entries/.gitkeep", "");

        Assert.Equal(new KitStoreBuilt(0, 0), await BuildAsync());

        Assert.Empty(KitIndex.Parse(Read("out/index.json")).Rows);
    }

    [Fact]
    public async Task RefusesAnOutputFolderThatIsThereAndAMissingEntriesFolder()
    {
        Directory.CreateDirectory(_work.At("out"));
        await Assert.ThrowsAsync<ArgumentException>(BuildAsync);
        Assert.True(Directory.Exists(_work.At("out")));
        using var transport = new MapTransport(_pages);

        await Assert.ThrowsAsync<ArgumentException>(
            () => KitStoreBuilder.BuildAsync(_work.At("nowhere"), _work.At("out2"), transport));
    }

    // Each refusal: how the entries are set up, and what the message holds.
    private static readonly Dictionary<string, (Func<KitStoreTests, Task> Setup, string Fragment)> Cases = new()
    {
        ["a hash that is not the pack's"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks");
            t.Entry("amber", "rocks", Release(served, change: release => release["hash"] = ContentHash.Of([9]).Hex));
        }, "amber/rocks 1.0.0 could not be fetched"),
        ["a size that is not the file's"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks");
            t.Entry("amber", "rocks", Release(served, change: release => release["bytes"] = served.Bytes + 1));
        }, "amber/rocks 1.0.0: the file is"),
        ["a file that is not there"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks");
            t._pages.Clear();
            t.Entry("amber", "rocks", Release(served));
        }, "could not be fetched"),
        ["a blob changed after packing"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks", files: new() { ["rock.obj"] = "UNIQUE-ROCK-TEXT"u8.ToArray() });
            var bytes = t._pages.Values.Single().Bytes;
            bytes[Encoding.Latin1.GetString(bytes).IndexOf("UNIQUE", StringComparison.Ordinal)] = 0x58;
            t.Entry("amber", "rocks", Release(served));
        }, "could not be fetched"),
        ["a kit with no licence"] = (async t =>
        {
            var manifest = ManifestOf("amber", "rocks", "1.0.0", []);
            manifest["kind"] = KitManifest.Kind;
            manifest["formatVersion"] = 1;
            manifest["containsScripts"] = false;
            manifest.Remove("licence");
            t.Entry("amber", "rocks", Release(await t.BrokenKitAsync("rocks", manifest)));
        }, "licence"),
        ["a slug that is not the entry's"] = (async t =>
            t.Entry("amber", "rocks", Release(await t.KitFileAsync("amber", "lamps"))), "slug \"lamps\" and the entry says \"rocks\""),
        ["a publisher that is not the entry's"] = (async t =>
            t.Entry("amber", "rocks", Release(await t.KitFileAsync("brass-lamp", "rocks"))), "publisher \"brass-lamp\" and the entry says \"amber\""),
        ["a version that is not the entry's"] = (async t =>
            t.Entry("amber", "rocks", Release(await t.KitFileAsync("amber", "rocks", "1.1.0"))), "version \"1.1.0\" and the entry says \"1.0.0\""),
        ["a kit that breaks a rule"] = (async t =>
            t.Entry("amber", "rocks", Release(await t.BrokenKitAsync("rocks", files: new() { [".hidden"] = [1] }))), "starts with a dot"),
        ["a preview that is too big"] = (async t =>
        {
            var served = await t.KitFileAsync(
                "amber", "rocks", files: new() { ["a.png"] = [.. Png, .. new byte[KitStoreRules.PreviewBytes + 1 - Png.Length]] }, previews: ["a.png"]);
            t.Entry("amber", "rocks", Release(served));
        }, "A preview is at most"),
        ["a webp that is too short to be one"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks", files: new() { ["a.webp"] = "RIFF"u8.ToArray() }, previews: ["a.webp"]);
            t.Entry("amber", "rocks", Release(served));
        }, "does not start like a webp image"),
        ["a preview that is not a picture"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks", files: new() { ["a.png"] = "not a png"u8.ToArray() }, previews: ["a.png"]);
            t.Entry("amber", "rocks", Release(served));
        }, "does not start like a png image"),
        ["an entry that is not JSON"] = (t =>
        {
            t.Write("entries/amber/rocks.json", "{");
            return Task.CompletedTask;
        }, "The entry \"amber/rocks\" is not JSON"),
        ["an entry with a typo"] = (async t =>
        {
            var served = await t.KitFileAsync("amber", "rocks");
            t.Write("entries/amber/rocks.json", new JsonObject { ["release"] = new JsonArray(Release(served)) }.ToJsonString());
        }, "not a key of this file"),
        ["a file that is not an entry"] = (t =>
        {
            t.Write("entries/amber/notes.txt", "hello");
            return Task.CompletedTask;
        }, "\"amber/notes.txt\" is not an entry"),
        ["a file beside the publisher folders"] = (t =>
        {
            t.Write("entries/README.md", "hello");
            return Task.CompletedTask;
        }, "\"README.md\" is not a folder of entries"),
    };

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Refuses(string name)
    {
        await Cases[name].Setup(this);

        await FailsAsync(Cases[name].Fragment);
    }

    [Fact]
    public async Task ListsEveryProblemNotTheFirst()
    {
        var rocks = await KitFileAsync("amber", "rocks");
        var crates = await KitFileAsync("brass-lamp", "crates");
        Entry("amber", "rocks", Release(rocks, change: release => release["bytes"] = rocks.Bytes + 1));
        Entry("brass-lamp", "crates", Release(crates, "1.0.1"));
        await FailsAsync("amber/rocks 1.0.0: the file is");

        var error = await Assert.ThrowsAnyAsync<FormatException>(BuildAsync);

        Assert.Contains("brass-lamp/crates 1.0.1", error.Message);
    }

    [Fact(Skip = "links need extra privileges on Windows", SkipUnless = nameof(NotWindows))]
    public async Task RefusesALinkWhereAnEntryBelongs()
    {
        var served = await KitFileAsync("amber", "rocks");
        Write("real.json", new JsonObject { ["releases"] = new JsonArray(Release(served)) }.ToJsonString());
        Directory.CreateDirectory(_work.At("entries/amber"));
        File.CreateSymbolicLink(_work.At("entries/amber/rocks.json"), _work.At("real.json"));

        await FailsAsync("is not an entry");
    }

    [Fact(Skip = "links need extra privileges on Windows", SkipUnless = nameof(NotWindows))]
    public async Task RefusesALinkWhereAFolderOfEntriesBelongs()
    {
        var served = await KitFileAsync("amber", "rocks");
        Write("real/rocks.json", new JsonObject { ["releases"] = new JsonArray(Release(served)) }.ToJsonString());
        Directory.CreateSymbolicLink(_work.At("entries/amber"), _work.At("real"));

        await FailsAsync("\"amber\" is not a folder of entries");
    }

    [Fact]
    public async Task ReadsThePreviewsOfTheLatestReleaseOnly()
    {
        var older = await KitFileAsync("amber", "rocks", files: new() { ["a.png"] = "not a png"u8.ToArray() }, previews: ["a.png"]);
        var newer = await KitFileAsync("amber", "rocks", "1.1.0", new() { ["a.png"] = Png }, ["a.png"]);
        Entry("amber", "rocks", Release(newer, "1.1.0"), Release(older));

        Assert.Equal(new KitStoreBuilt(1, 2), await BuildAsync());

        Assert.Equal(Png, Read("out/kits/amber/rocks/preview-0.png"));
    }

    [Fact]
    public async Task TakesAPreviewOfExactlyTheLimit()
    {
        var exact = new byte[KitStoreRules.PreviewBytes];
        Png.CopyTo(exact, 0);
        Entry("amber", "rocks", Release(await KitFileAsync("amber", "rocks", files: new() { ["a.png"] = exact }, previews: ["a.png"])));

        Assert.Equal(new KitStoreBuilt(1, 1), await BuildAsync());
    }

    [Fact]
    public async Task KeepsTheExtensionOfAJpegPreview()
    {
        Entry("amber", "rocks", Release(await KitFileAsync("amber", "rocks", files: new() { ["a.jpeg"] = Jpg }, previews: ["a.jpeg"])));

        await BuildAsync();

        Assert.Equal(Jpg, Read("out/kits/amber/rocks/preview-0.jpeg"));
    }

    [Fact]
    public async Task StopsReadingAReleaseAtTheSizeTheEntryGives()
    {
        var served = await KitFileAsync("amber", "rocks");
        Entry("amber", "rocks", Release(served, change: release => release["bytes"] = served.Bytes - 1));

        await FailsAsync("amber/rocks 1.0.0 could not be fetched");
    }

    [Fact]
    public async Task GivesUpOnAReleaseThatNeverArrives()
    {
        var served = await KitFileAsync("amber", "rocks");
        Entry("amber", "rocks", Release(served));
        using var transport = new Stuck();

        var error = await Assert.ThrowsAnyAsync<FormatException>(
            () => KitStoreBuilder.BuildAsync(_work.At("entries"), _work.At("out"), transport, TimeSpan.FromMilliseconds(50)));

        Assert.Contains("amber/rocks 1.0.0 could not be fetched", error.Message);
        Assert.Contains("TimeoutException", error.Message);
        Assert.False(Directory.Exists(_work.At("out")));
    }

    [Fact]
    public async Task LeavesAloneAFolderThatAppearedWhileItRan()
    {
        var served = await KitFileAsync("amber", "rocks");
        Entry("amber", "rocks", Release(served));
        using var transport = new Before(new MapTransport(_pages), () => Write("out/theirs.txt", "mine"));

        await Assert.ThrowsAnyAsync<IOException>(
            () => KitStoreBuilder.BuildAsync(_work.At("entries"), _work.At("out"), transport));

        Assert.Equal("mine", await File.ReadAllTextAsync(_work.At("out/theirs.txt"), TestContext.Current.CancellationToken));
        Assert.Empty(Staged());
    }

    [Fact]
    public async Task RefusesAnEntryFileThatIsTooLarge()
    {
        Write("entries/amber/big.json", new string('a', (256 * 1024) + 1));

        await FailsAsync("\"amber/big.json\" is over 262144 bytes");
    }

    [Fact(Skip = "there is no mkfifo on Windows", SkipUnless = nameof(NotWindows))]
    public async Task RefusesAPipeWhereAnEntryBelongs()
    {
        Directory.CreateDirectory(_work.At("entries/amber"));
        using (var made = Process.Start("mkfifo", _work.At("entries/amber/pipe.json")))
        {
            await made!.WaitForExitAsync(TestContext.Current.CancellationToken);
        }

        await FailsAsync("\"amber/pipe.json\" is not an entry");
    }

    [Fact(Skip = "Windows does not allow these file names", SkipUnless = nameof(NotWindows))]
    public async Task EscapesAControlCharacterInAFileName()
    {
        Write($"entries/am{Esc}ber/rocks.json", "nope");
        Write($"entries/{Esc}note.txt", "hello");
        Write($"entries/amber/{Esc}x.txt", "hello");
        const string Backslash = "\\";

        var error = await Assert.ThrowsAnyAsync<FormatException>(BuildAsync);

        Assert.Contains($"am{Backslash}u001bber/rocks", error.Message);
        Assert.Contains($"\"amber/{Backslash}u001bx.txt\" is not an entry", error.Message);
        Assert.Contains($"\"{Backslash}u001bnote.txt\" is not a folder", error.Message);
        Assert.DoesNotContain(Esc, error.Message);
    }

    [Fact]
    public async Task RefusesAReleaseAtAnAddressThatIsNotHttps()
    {
        var served = await KitFileAsync("amber", "rocks");
        const string Elsewhere = "http://example.com/amber-rocks-1.0.0.opack";
        _pages[WebUrl.Parse(Elsewhere)] = _pages[WebUrl.Parse(served.Url)];
        Entry("amber", "rocks", Release(served, change: release => release["url"] = Elsewhere));

        await FailsAsync("not an https address");
    }
}
