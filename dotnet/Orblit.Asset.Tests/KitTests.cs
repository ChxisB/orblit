using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

// Kits, as the pack and the kit rules see them. Packing a folder, extracting and the rest join these tests
// with the parts that write files.
public sealed class KitTests : IDisposable
{
    private readonly KitWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    private static JsonObject Packed(Action<JsonObject>? change = null)
    {
        var manifest = KitWorkspace.Authored();
        manifest["kind"] = KitManifest.Kind;
        manifest["formatVersion"] = 1;
        manifest["containsScripts"] = false;
        change?.Invoke(manifest);
        return manifest;
    }

    // Hand built, because the tool never writes a pack that is not a kit. It holds files, each a name and
    // its text, and does not check their names. Returns the file.
    private async Task<string> HandBuiltAsync(
        string target = KitManifest.Target,
        JsonObject? manifest = null,
        Dictionary<string, string>? files = null,
        ContentHash? @base = null,
        bool withManifest = true)
    {
        var bytes = Encoding.UTF8.GetBytes((manifest ?? Packed()).ToJsonString());
        var texts = (files ?? []).ToDictionary(file => AssetId.Parse(file.Key), file => Encoding.UTF8.GetBytes(file.Value));
        var entries = new Dictionary<AssetId, AssetEntry>();
        if (withManifest)
        {
            entries[KitManifest.ManifestId] = new AssetEntry(ContentHash.Of(bytes), bytes.Length);
        }

        foreach (var (id, text) in texts)
        {
            entries[id] = new AssetEntry(ContentHash.Of(text), text.Length);
        }

        var path = _work.At($"hand_{_work.Next()}.opack");
        await PackWriter.WriteAsync(
            new AssetManifest(entries), path, target, id => Task.FromResult(texts.GetValueOrDefault(id) ?? bytes), @base);
        return path;
    }

    private static async Task<AssetPack> OpenAsync(string path, Func<IPackBytes, IPackBytes>? wrap = null)
    {
        IPackBytes bytes = new MemoryPackBytes(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        return await AssetPack.OpenAsync(wrap?.Invoke(bytes) ?? bytes);
    }

    // Changes from to to in the pack file at path, two texts of one length.
    private static async Task TamperAsync(string path, string from, string to)
    {
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var place = Encoding.Latin1.GetString(bytes).IndexOf(from, StringComparison.Ordinal);
        Assert.True(place >= 0, from);
        Encoding.Latin1.GetBytes(to).CopyTo(bytes, place);
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
    }

    private Task<Kit> CratesAsync() => _work.KitOfAsync(new() { ["models/crate.obj"] = "v 0 0 0", ["readme.txt"] = "Twelve." });

    private static async Task<string> TextOfAsync(string path) => await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

    private async Task<string> PackedFileAsync(Dictionary<string, string> files, string name)
    {
        var path = _work.At(name);
        await KitPacking.PackAsync(await _work.FolderAsync(files), path);
        return path;
    }

    [Fact]
    public async Task RefusesAPackMadeForAGameTarget() =>
        await Assert.ThrowsAsync<FormatException>(async () => await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(target: "android"))));

    [Fact]
    public async Task RefusesAPatch() =>
        await Assert.ThrowsAsync<FormatException>(
            async () => await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(@base: ContentHash.Of([0])))));

    [Fact]
    public async Task RefusesAPackWithNoKitJson() =>
        await Assert.ThrowsAsync<FormatException>(async () => await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(withManifest: false))));

    [Fact]
    public async Task RefusesAManifestThatSaysItIsSomethingElse()
    {
        var manifest = Packed(m => m["kind"] = "orblit.theme");

        await Assert.ThrowsAsync<FormatException>(async () => await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(manifest: manifest))));
    }

    [Fact]
    public async Task RefusesAKitJsonThatIsNotWhatThePackLists()
    {
        var path = await HandBuiltAsync();
        await TamperAsync(path, "Twelve crates.", "Twelve crates!");

        var error = await Assert.ThrowsAsync<FormatException>(async () => await Kit.OpenAsync(await OpenAsync(path)));

        Assert.Contains("does not match its hash", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAKitJsonOverTheSizeLimit()
    {
        var manifest = Packed(m => m["summary"] = new string('x', KitManifest.ManifestLimit + 1));

        await Assert.ThrowsAsync<FormatException>(async () => await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(manifest: manifest))));
    }

    [Fact]
    public async Task AGameDoesNotMountAKit()
    {
        var kit = await CratesAsync();
        var games = new MountedPacks(CookTargets.Macos);

        Assert.Throws<PackMountRefusedException>(() => games.Start(kit.Pack));
    }

    [Fact]
    public async Task CheckListsANameThatPackingWouldHaveRefused()
    {
        var kit = await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(files: new() { [".git/hooks/pre-commit"] = "x" })));

        Assert.Equal(
            [@""".git/hooks/pre-commit"" has a part, "".git"", that starts with a dot, so it would write a hidden file."],
            await kit.CheckAsync());
    }

    [Fact]
    public async Task ConflictsListsAFileWithOtherBytesAndReadsNoBlobOfTheKit()
    {
        var path = await HandBuiltAsync(files: new() { ["models/crate.obj"] = "v 0 0 0", ["same.txt"] = "ok" });
        CountingBytes? counting = null;
        var kit = await Kit.OpenAsync(await OpenAsync(path, inner => counting = new CountingBytes(inner)));
        var reads = counting!.Reads;
        var mine = new MemoryAssetSource(new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/crate.obj")] = "mine"u8.ToArray(),
            [AssetId.Parse("same.txt")] = "ok"u8.ToArray(),
        });

        var found = await kit.ConflictsInAsync(mine);

        Assert.Equal(["models/crate.obj"], found.Select(id => id.ToString()));
        Assert.Equal(reads, counting.Reads);
    }

    [Fact]
    public async Task PacksAFolderIntoAKitThatReadsBack()
    {
        var kit = await CratesAsync();

        Assert.Equal("crate-pack", kit.Manifest.Slug);
        Assert.Equal("source", kit.Pack.Target);
        Assert.Equal(["models/crate.obj", "readme.txt"], kit.Files.Keys.Select(id => id.ToString()));
        Assert.Equal("Twelve.", Encoding.UTF8.GetString(await kit.Pack.ReadAsync(AssetId.Parse("readme.txt"))));
        Assert.Empty(await kit.CheckAsync());
    }

    [Fact]
    public async Task WritesTheSameBytesForTheSameFolder()
    {
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one", ["b/c.txt"] = "two" });

        await KitPacking.PackAsync(source, _work.At("first.opack"));
        await KitPacking.PackAsync(source, _work.At("second.opack"));

        var token = TestContext.Current.CancellationToken;
        Assert.Equal(await File.ReadAllBytesAsync(_work.At("first.opack"), token), await File.ReadAllBytesAsync(_work.At("second.opack"), token));
    }

    [Fact]
    public async Task CountsTheScriptsItselfAndIgnoresWhatTheAuthorTyped()
    {
        var manifest = KitWorkspace.Authored();
        manifest["containsScripts"] = false;
        var source = await _work.FolderAsync(new() { ["tools/spawn.js"] = "run()" }, manifest);

        Assert.True((await _work.PackedAsync(source)).Manifest.ContainsScripts);
        Assert.False((await CratesAsync()).Manifest.ContainsScripts);
    }

    [Fact]
    public async Task TakesAFolderWrittenWithATrailingSlash()
    {
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one", ["b/c.txt"] = "two" });
        var path = _work.At("slash.opack");

        await KitPacking.PackAsync($"{source}/", path);

        var kit = await Kit.OpenAsync(await AssetPack.OpenAsync(new MemoryPackBytes(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken))));
        Assert.Equal(["a.txt", "b/c.txt"], kit.Files.Keys.Select(id => id.ToString()));
    }

    [Fact]
    public async Task DoesNotPackItsOwnOutputOnASecondRun()
    {
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one" });
        var path = Path.Combine(source, "crates.opack");
        await KitPacking.PackAsync(source, path);
        var first = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(path + ".partial", "left behind", TestContext.Current.CancellationToken);
        await KitPacking.PackAsync(source, path);

        Assert.Equal(first, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesAFileNameOutsideTheAllowedCharacters()
    {
        var source = await _work.FolderAsync(new() { ["caf\u00e9.png"] = "x" });

        var error = await Assert.ThrowsAnyAsync<FormatException>(async () => await KitPacking.PackAsync(source, _work.At("out.opack")));

        Assert.Contains("letters, digits", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAHiddenFileAndNamesIt()
    {
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one", [".DS_Store"] = "x" });

        var error = await Assert.ThrowsAnyAsync<FormatException>(async () => await KitPacking.PackAsync(source, _work.At("out.opack")));

        Assert.Contains(".DS_Store", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_work.At("out.opack")));
    }

    [Fact]
    public async Task RefusesAPreviewTheFolderDoesNotHold()
    {
        var manifest = KitWorkspace.Authored();
        manifest["previews"] = new JsonArray("shot.png");
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one" }, manifest);

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitPacking.PackAsync(source, _work.At("out.opack")));
    }

    [Fact]
    public async Task RefusesALinkWhichCouldLeadOutOfTheFolder()
    {
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one" });
        Directory.CreateSymbolicLink(Path.Combine(source, "outside"), _work.At("."));

        var error = await Assert.ThrowsAnyAsync<FormatException>(async () => await KitPacking.PackAsync(source, _work.At("out.opack")));

        Assert.Contains("is a link", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAFolderWithNoKitJson()
    {
        var source = await _work.FolderAsync(new() { ["a.txt"] = "one" });
        File.Delete(Path.Combine(source, "kit.json"));

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitPacking.PackAsync(source, _work.At("out.opack")));
    }

    [Fact]
    public async Task RefusesAFolderThatIsNotThere() =>
        await Assert.ThrowsAsync<ArgumentException>(async () => await KitPacking.PackAsync(_work.At("missing"), _work.At("out.opack")));

    [Fact]
    public async Task CheckListsAFileWhoseBytesChangedInThePack()
    {
        var path = await PackedFileAsync(new() { ["a.txt"] = "one" }, "out.opack");
        await TamperAsync(path, "one", "Xne");

        var kit = await Kit.OpenAsync(await OpenAsync(path));

        Assert.Equal(["\"a.txt\" does not match its hash."], await kit.CheckAsync());
    }

    [Fact]
    public async Task ExtractWritesTheFilesNeverTheManifestAndIsSafeToRepeat()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync([]);

        var first = await KitExtraction.ExtractAsync(kit, mine);

        Assert.Equal(["models/crate.obj", "readme.txt"], first.Select(id => id.ToString()));
        Assert.Equal("v 0 0 0", await TextOfAsync(Path.Combine(mine, "models", "crate.obj")));
        Assert.False(File.Exists(Path.Combine(mine, "kit.json")));
        Assert.Equal(2, Directory.EnumerateFiles(mine, "*", SearchOption.AllDirectories).Count());
        Assert.Empty(await KitExtraction.ExtractAsync(kit, mine));
    }

    [Fact]
    public async Task ExtractStopsOnAConflictBeforeItWritesAnything()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync(new() { ["readme.txt"] = "mine" });

        var error = await Assert.ThrowsAsync<KitConflictsException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Equal(["readme.txt"], error.Ids.Select(id => id.ToString()));
        Assert.False(File.Exists(Path.Combine(mine, "models", "crate.obj")));
        Assert.Equal("mine", await TextOfAsync(Path.Combine(mine, "readme.txt")));
    }

    [Fact]
    public async Task ExtractOverwritesAConflictWhenItIsToldTo()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync(new() { ["readme.txt"] = "mine" });

        await KitExtraction.ExtractAsync(kit, mine, new KitExtractionOptions { Overwrite = true });

        Assert.Equal("Twelve.", await TextOfAsync(Path.Combine(mine, "readme.txt")));
    }

    [Fact]
    public async Task ExtractDoesNotWriteThroughALinkInTheProject()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync([]);
        var outside = await _work.ProjectAsync([]);
        Directory.CreateSymbolicLink(Path.Combine(mine, "models"), outside);

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public async Task ExtractDoesNotWriteOverAFileThatIsInTheWayOfAFolder()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync(new() { ["models"] = "a file" });

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Equal("a file", await TextOfAsync(Path.Combine(mine, "models")));
    }

    [Fact]
    public async Task ExtractWritesNothingWhenALaterFileIsBlocked()
    {
        var kit = await _work.KitOfAsync(new() { ["a.txt"] = "one", ["z/b.txt"] = "two" });
        var mine = await _work.ProjectAsync(new() { ["z"] = "a file" });

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.False(File.Exists(Path.Combine(mine, "a.txt")));
    }

    [Fact]
    public async Task ExtractWritesNothingWhenALaterBlobIsCorrupt()
    {
        var path = await PackedFileAsync(new() { ["a.txt"] = "one", ["z.txt"] = "two" }, "late.opack");
        await TamperAsync(path, "two", "twX");
        var kit = await Kit.OpenAsync(await OpenAsync(path));
        var mine = await _work.ProjectAsync([]);

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Empty(Directory.EnumerateFileSystemEntries(mine));

        // A pack that changes between the check and the write is caught by the hash taken on the way out.
        var clean = await PackedFileAsync(new() { ["a.txt"] = "one" }, "clean.opack");
        var counting = (CountingBytes?)null;
        await Kit.OpenAsync(await OpenAsync(clean, inner => counting = new CountingBytes(inner)));
        var opened = counting!.Reads;
        var checkedKit = await Kit.OpenAsync(await OpenAsync(clean, inner => counting = new CountingBytes(inner)));
        await checkedKit.CheckAsync();
        var settled = counting.Reads;
        var swapped = await Kit.OpenAsync(await OpenAsync(clean, inner => new FlippingBytes(inner, settled)));

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(swapped, mine));

        Assert.Empty(Directory.EnumerateFileSystemEntries(mine));
        Assert.True(settled > opened);
    }

    [Fact]
    public async Task ExtractRefusesAPackWithANameThatPackingWouldRefuse()
    {
        var kit = await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(files: new() { [".git/hooks/pre-commit"] = "x" })));
        var mine = await _work.ProjectAsync([]);

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Empty(Directory.EnumerateFileSystemEntries(mine));
    }

    [Fact]
    public async Task ExtractDoesNotWriteThroughALinkAtThePlaceOfAFile()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync([]);
        var outside = await _work.ProjectAsync(new() { ["target.txt"] = "precious" });
        File.CreateSymbolicLink(Path.Combine(mine, "readme.txt"), Path.Combine(outside, "target.txt"));

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Equal("precious", await TextOfAsync(Path.Combine(outside, "target.txt")));
    }

    [Fact]
    public async Task ExtractDoesNotWriteOverAFolderAtThePlaceOfAFile()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync(new() { ["readme.txt/inner.txt"] = "x" });

        await Assert.ThrowsAnyAsync<FormatException>(async () => await KitExtraction.ExtractAsync(kit, mine));

        Assert.Equal("x", await TextOfAsync(Path.Combine(mine, "readme.txt", "inner.txt")));
    }

    [Fact]
    public async Task ExtractLeavesAFileThatHasTheTemporaryNameAlone()
    {
        var kit = await CratesAsync();
        var mine = await _work.ProjectAsync(new() { ["readme.txt.partial"] = "precious" });

        await KitExtraction.ExtractAsync(kit, mine);

        Assert.Equal("precious", await TextOfAsync(Path.Combine(mine, "readme.txt.partial")));
        Assert.Equal("Twelve.", await TextOfAsync(Path.Combine(mine, "readme.txt")));
    }

    [Fact]
    public void AConflictMessageShowsNoRawEscapeCharacter()
    {
        var text = new KitConflictsException([AssetId.Parse("a\u001b[31m.png")]).Message;

        Assert.DoesNotContain("\u001b", text, StringComparison.Ordinal);
        Assert.Contains(@"a\u001b[31m.png", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractRefusesAProjectFolderThatIsNotThere() =>
        await Assert.ThrowsAsync<ArgumentException>(async () => await KitExtraction.ExtractAsync(await CratesAsync(), _work.At("missing")));

    // Serves the pack as it is for the first reads, then every byte changed.
    private sealed class FlippingBytes(IPackBytes inner, int after) : IPackBytes
    {
        private int _reads;

        public Task<long> LengthAsync() => inner.LengthAsync();

        public async Task<byte[]> ReadAsync(long at, long bytes)
        {
            var read = await inner.ReadAsync(at, bytes);
            if (++_reads > after)
            {
                for (var i = 0; i < read.Length; i++)
                {
                    read[i] ^= 1;
                }
            }

            return read;
        }
    }

    private sealed class CountingBytes(IPackBytes inner) : IPackBytes
    {
        public int Reads { get; private set; }

        public Task<long> LengthAsync() => inner.LengthAsync();

        public Task<byte[]> ReadAsync(long at, long bytes)
        {
            Reads++;
            return inner.ReadAsync(at, bytes);
        }
    }
}
