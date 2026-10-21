using System.Text;

namespace Orblit.Asset.Tests;

public sealed class PackFolderTests : IDisposable
{
    private readonly PackFixture _fixture = new();
    private readonly string _folderPath;
    private readonly PackFolder _folder;

    public PackFolderTests()
    {
        _folderPath = _fixture.At("downloads");
        _folder = new PackFolder(_folderPath);
    }

    public void Dispose() => _fixture.Dispose();

    // Long enough that half of it is well inside the blobs.
    private static string Body(string name) => string.Join(' ', Enumerable.Repeat(name, 400));

    private static Dictionary<string, string> State(string wall = "wall v1") => new()
    {
        ["textures/wall.ktx2"] = Body(wall),
        ["models/tree.glb"] = Body("tree"),
        ["scenes/town.oscene"] = Body("town"),
    };

    private async Task<(BuiltPack Shipped, BuiltPack Patch)> Pair()
    {
        var shipped = await _fixture.BuildAsync("game", State());
        var patch = await _fixture.BuildAsync("patch", State(wall: "wall v2"), [shipped.Pack]);
        return (shipped, patch);
    }

    private string PackFile(ContentHash hash) => Path.Combine(_folderPath, $"{hash.Hex}.opack");

    private string PartFile(ContentHash hash) => Path.Combine(_folderPath, $"{hash.Hex}.part");

    private static async Task<string> Text(MountedPacks source, string id) =>
        Encoding.UTF8.GetString(await source.ReadAsync(AssetId.Parse(id)));

    // list

    [Fact]
    public async Task ListIsEmptyWhenTheFolderIsNotThere()
    {
        var listing = await _folder.ListAsync();

        Assert.Empty(listing.Packs);
        Assert.Empty(listing.Unreadable);
        Assert.Empty(listing.Unfinished);
    }

    [Fact]
    public async Task OpensPacksAndReportsWhatIsNotOneWithoutStopping()
    {
        var token = TestContext.Current.CancellationToken;
        var both = await Pair();
        Directory.CreateDirectory(_folderPath);
        await File.WriteAllBytesAsync(PackFile(both.Shipped.Hash), both.Shipped.Bytes, token);
        await File.WriteAllBytesAsync(PackFile(both.Patch.Hash), both.Patch.Bytes, token);

        // A pack under a name that is not its hash.
        var renamed = Path.Combine(_folderPath, $"{new string('a', 64)}.opack");
        await File.WriteAllBytesAsync(renamed, both.Patch.Bytes, token);
        var junk = Path.Combine(_folderPath, $"{new string('b', 64)}.opack");
        await File.WriteAllBytesAsync(junk, Enumerable.Repeat((byte)7, 100).ToArray(), token);
        var cut = Path.Combine(_folderPath, $"{new string('c', 64)}.opack");
        await File.WriteAllBytesAsync(cut, both.Patch.Bytes[..200], token);
        var unfinishedHash = ContentHash.Of(Encoding.UTF8.GetBytes("unfinished"));
        await File.WriteAllBytesAsync(PartFile(unfinishedHash), [1, 2, 3], token);
        await File.WriteAllTextAsync(Path.Combine(_folderPath, "notes.txt"), "hello", token);

        var listing = await _folder.ListAsync();

        Assert.Equal(
            new HashSet<ContentHash> { both.Shipped.Hash, both.Patch.Hash },
            listing.Packs.Select(p => p.Hash).ToHashSet());
        Assert.Equal(new HashSet<string> { renamed, junk, cut }, listing.Unreadable.Select(u => u.Path).ToHashSet());
        Assert.Contains(both.Patch.Hash.Hex, listing.Unreadable.First(u => u.Path == renamed).Reason);
        Assert.Equal([unfinishedHash], listing.Unfinished);
    }

    [Fact]
    public async Task ListsInTheOrderOfTheHashes()
    {
        var token = TestContext.Current.CancellationToken;
        var both = await Pair();
        var more = new List<BuiltPack>();
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            more.Add(await _fixture.BuildAsync(name, State(wall: $"mod {name}")));
        }

        Directory.CreateDirectory(_folderPath);
        foreach (var built in new[] { both.Patch }.Concat(more).Append(both.Shipped))
        {
            await File.WriteAllBytesAsync(PackFile(built.Hash), built.Bytes, token);
        }

        var listing = await _folder.ListAsync();

        var hashes = listing.Packs.Select(p => p.Hash.Hex).ToList();
        Assert.Equal(6, hashes.Count);
        Assert.Equal(hashes.Order(StringComparer.Ordinal), hashes);
    }

    // start-up

    [Fact]
    public async Task MountsWhatIsOnDiskInChainOrderAndSkipsAnOrphan()
    {
        var token = TestContext.Current.CancellationToken;
        var shipped = await _fixture.BuildAsync("game", State());
        var one = await _fixture.BuildAsync("one", State(wall: "wall v2"), [shipped.Pack]);
        var two = await _fixture.BuildAsync("two", Tree(State(wall: "wall v2"), "tree v2"), [shipped.Pack, one.Pack]);
        var three = await _fixture.BuildAsync(
            "three", Tree(State(wall: "wall v3"), "tree v2"), [shipped.Pack, one.Pack, two.Pack]);

        // Built on a pack that was never downloaded.
        var lost = await _fixture.BuildAsync(
            "lost", Tree(State(wall: "wall v9"), "tree v9"), [shipped.Pack, one.Pack, two.Pack, three.Pack]);
        var orphan = await _fixture.BuildAsync(
            "orphan",
            Tree(State(wall: "wall v10"), "tree v10"),
            [shipped.Pack, one.Pack, two.Pack, three.Pack, lost.Pack]);
        Directory.CreateDirectory(_folderPath);
        foreach (var built in new[] { three, orphan, one, two })
        {
            await File.WriteAllBytesAsync(PackFile(built.Hash), built.Bytes, token);
        }

        var listing = await _folder.ListAsync();
        var chain = PatchChain.Of(shipped.Pack, listing.Packs);
        var packs = new MountedPacks(CookTargets.Android);
        packs.Start(shipped.Pack);
        foreach (var pack in chain.Ordered)
        {
            packs.Patch(pack);
        }

        Assert.Equal([one.Hash, two.Hash, three.Hash], chain.Ordered.Select(p => p.Hash));
        Assert.Equal([orphan.Hash], chain.Skipped.Select(s => s.Pack.Hash));
        Assert.Equal(Body("wall v3"), await Text(packs, "textures/wall.ktx2"));
        Assert.Equal(Body("tree v2"), await Text(packs, "models/tree.glb"));
    }

    private static Dictionary<string, string> Tree(Dictionary<string, string> files, string tree)
    {
        files["models/tree.glb"] = Body(tree);
        return files;
    }

    // remove

    [Fact]
    public async Task DeletesAPackAndItsPartAndSaysSo()
    {
        var token = TestContext.Current.CancellationToken;
        var both = await Pair();
        Directory.CreateDirectory(_folderPath);
        await File.WriteAllBytesAsync(PackFile(both.Patch.Hash), both.Patch.Bytes, token);
        await File.WriteAllBytesAsync(PartFile(both.Patch.Hash), [1], token);

        Assert.True(await _folder.RemoveAsync(both.Patch.Hash));

        Assert.Empty(Directory.GetFileSystemEntries(_folderPath));
        Assert.False(await _folder.RemoveAsync(both.Patch.Hash));
    }
}
