using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

// Kits, as the pack and the kit rules see them. Packing a folder, extracting and the rest join these tests
// with the parts that write files.
public sealed class KitTests : IDisposable
{
    private readonly PackFixture _scratch = new();
    private int _made;

    public void Dispose() => _scratch.Dispose();

    // The manifest an author writes, with the three fields a pack adds.
    private static JsonObject Authored() => new()
    {
        ["slug"] = "crate-pack",
        ["publisher"] = "brass-lamp",
        ["name"] = "Crate pack",
        ["summary"] = "Twelve crates.",
        ["version"] = "1.0.0",
        ["licence"] = new JsonObject { ["spdx"] = "MIT", ["url"] = "https://example.com/l" },
        ["engine"] = new JsonObject { ["from"] = "0.30.0", ["to"] = "0.40.0" },
        ["tags"] = new JsonArray(),
        ["previews"] = new JsonArray(),
    };

    private static JsonObject Packed(Action<JsonObject>? change = null)
    {
        var manifest = Authored();
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

        var path = _scratch.At($"hand_{_made++}.opack");
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

    private async Task<Kit> CratesAsync() =>
        await Kit.OpenAsync(await OpenAsync(await HandBuiltAsync(files: new() { ["models/crate.obj"] = "v 0 0 0", ["readme.txt"] = "Twelve." })));

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
