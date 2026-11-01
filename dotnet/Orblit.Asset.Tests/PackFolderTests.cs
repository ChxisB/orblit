using System.Text;

namespace Orblit.Asset.Tests;

public sealed class PackFolderTests : IAsyncDisposable
{
    private readonly PackFixture _fixture = new();
    private readonly List<AssetPack> _held = [];
    private readonly string _folderPath;
    private readonly PackFolder _folder;

    public PackFolderTests()
    {
        _folderPath = _fixture.At("downloads");
        _folder = new PackFolder(_folderPath);
    }

    // A pack read from the folder keeps its file open, and Windows will not delete a folder with an open file.
    public async ValueTask DisposeAsync()
    {
        foreach (var pack in _held)
        {
            await pack.CloseAsync();
        }

        _fixture.Dispose();
    }

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
        _held.AddRange(listing.Packs);
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

    // download

    private static readonly WebUrl Url = WebUrl.Parse("https://cdn.example.com/patches/one.opack");
    private static readonly AssetOrigin Origin = AssetOrigin.Parse("https://cdn.example.com/assets/");

    private static MapTransport Serving(byte[] bytes, int cut = 0, int? stopAfter = null, List<Fault>? faults = null) =>
        new(new Dictionary<WebUrl, TransportPage> { [Url] = new TransportPage(bytes, cut: cut, stopAfter: stopAfter, faults: faults) });

    // F2: only the pack and the transport are required, and the rest are named at the call.
    private async Task<AssetPack> Download(
        BuiltPack built,
        IAssetTransport transport,
        PackFolder? into = null,
        int attempts = 3,
        Action<FetchProgress>? onProgress = null,
        AssetOrigin? from = null)
    {
        var pack = await (into ?? _folder).DownloadAsync(
            new PackOffer(Url, built.Hash), transport, from ?? Origin, attempts, TimeSpan.Zero, NetWorld.NoNoise, onProgress);
        _held.Add(pack);
        return pack;
    }

    private async Task Leave(string file, byte[] bytes)
    {
        Directory.CreateDirectory(_folderPath);
        await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        var at = haystack.AsSpan().IndexOf(needle);
        return at >= 0 ? at : throw new InvalidOperationException("The marker is not in the pack.");
    }

    [Fact]
    public async Task FilesAWholePackUnderItsHash()
    {
        var built = (await Pair()).Patch;
        using var transport = Serving(built.Bytes);

        var pack = await Download(built, transport);

        Assert.Equal(built.Hash, pack.Hash);
        Assert.Equal(built.Bytes, await File.ReadAllBytesAsync(PackFile(built.Hash), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(PartFile(built.Hash)));
        Assert.Single(transport.Sent);
        Assert.Null(transport.Sent[0].From);
    }

    [Fact]
    public async Task IsCutOffHalfwayResumesChecksItAndMountsIt()
    {
        var both = await Pair();
        var half = both.Patch.Bytes.Length / 2;
        using var transport = Serving(both.Patch.Bytes, cut: 512, stopAfter: half);

        var pack = await Download(both.Patch, transport);

        Assert.Equal(2, transport.Sent.Count);
        Assert.Equal<long?>(half, transport.Sent[1].From);
        Assert.Equal(both.Patch.Hash, pack.Hash);
        Assert.False(File.Exists(PartFile(both.Patch.Hash)));

        var packs = new MountedPacks(CookTargets.Android);
        packs.Start(both.Shipped.Pack);
        packs.Patch(pack);
        Assert.Equal(Body("wall v2"), await Text(packs, "textures/wall.ktx2"));
        Assert.Equal(Body("tree"), await Text(packs, "models/tree.glb"));
    }

    [Fact]
    public async Task ResumesFromAPartFileAnEarlierRunLeft()
    {
        var built = (await Pair()).Patch;
        var held = built.Bytes.Length / 3;
        await Leave(PartFile(built.Hash), built.Bytes[..held]);
        using var transport = Serving(built.Bytes);

        var pack = await Download(built, transport, into: new PackFolder(_folderPath));

        Assert.Equal<long?>(held, transport.Sent.Single().From);
        Assert.Equal(built.Hash, pack.Hash);
        Assert.Equal(built.Bytes, await File.ReadAllBytesAsync(PackFile(built.Hash), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TakesAPartThatIsAlreadyWholeWhenTheServerSays416()
    {
        var built = (await Pair()).Patch;
        await Leave(PartFile(built.Hash), built.Bytes);
        using var transport = new Answers(416);

        var pack = await Download(built, transport);

        Assert.Equal<long?>(built.Bytes.Length, transport.Sent.Single().From);
        Assert.Equal(built.Hash, pack.Hash);
        Assert.False(File.Exists(PartFile(built.Hash)));
    }

    [Fact]
    public async Task DropsAPartTheServerHasNoByteForWhenItIsNotAPack()
    {
        var built = (await Pair()).Patch;
        await Leave(PartFile(built.Hash), new byte[built.Bytes.Length + 50]);
        using var transport = new Answers(416);

        await Assert.ThrowsAsync<FetchFailedException>(() => Download(built, transport, attempts: 2));

        Assert.False(File.Exists(PartFile(built.Hash)));
        Assert.Equal<long?[]>([built.Bytes.Length + 50, null], [.. transport.Sent.Select(request => request.From)]);
    }

    [Fact]
    public async Task StartsOverWhenTheServerIgnoresTheRange()
    {
        var both = await Pair();
        var half = both.Patch.Bytes.Length / 2;
        await Leave(PartFile(both.Patch.Hash), both.Patch.Bytes[..half]);
        using var transport = Serving(both.Patch.Bytes);

        var pack = await Download(both.Patch, new DropsTheRange(transport));

        Assert.Equal(both.Patch.Hash, pack.Hash);
        Assert.Equal(both.Patch.Bytes, await File.ReadAllBytesAsync(PackFile(both.Patch.Hash), TestContext.Current.CancellationToken));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task DoesNotTrustAServerThatResumesAtAnotherByte()
    {
        var both = await Pair();
        var held = both.Patch.Bytes.Length / 2;
        await Leave(PartFile(both.Patch.Hash), both.Patch.Bytes[..held]);
        using var transport = Serving(both.Patch.Bytes);
        var seen = new List<long>();

        var pack = await Download(both.Patch, new ResumesEarly(transport), onProgress: progress => seen.Add(progress.Received));

        Assert.Equal(both.Patch.Hash, pack.Hash);
        Assert.Equal(both.Patch.Bytes, await File.ReadAllBytesAsync(PackFile(both.Patch.Hash), TestContext.Current.CancellationToken));
        Assert.Equal<long?[]>([held - 10, null], [.. transport.Sent.Select(request => request.From)]);

        // The reply that began at the wrong byte is not written, so no progress counts it. Written, it would put
        // the file ten bytes past its own length before the check caught it.
        Assert.Equal([(long)both.Patch.Bytes.Length], seen);
    }

    [Fact]
    public async Task ReplacesAFileThatHasAPacksNameAndOtherContents()
    {
        var both = await Pair();
        await Leave(PackFile(both.Patch.Hash), both.Shipped.Bytes);
        using var transport = Serving(both.Patch.Bytes);

        var pack = await Download(both.Patch, transport);

        Assert.Equal(both.Patch.Hash, pack.Hash);
        Assert.Equal(both.Patch.Bytes, await File.ReadAllBytesAsync(PackFile(both.Patch.Hash), TestContext.Current.CancellationToken));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task ThrowsAwayAPartThatBelongsToADifferentFile()
    {
        var both = await Pair();
        var older = await _fixture.BuildAsync(
            "older",
            new Dictionary<string, string>
            {
                ["textures/wall.ktx2"] = Body("wall v0"),
                ["models/tree.glb"] = Body("tree"),
                ["scenes/town.oscene"] = Body("town"),
            },
            [both.Shipped.Pack]);
        await Leave(PartFile(both.Patch.Hash), older.Bytes[..60]);
        using var transport = Serving(both.Patch.Bytes);

        var pack = await Download(both.Patch, transport);

        Assert.Equal(both.Patch.Hash, pack.Hash);
        Assert.Equal<long?[]>([60, null], [.. transport.Sent.Select(request => request.From)]);
        Assert.Equal(both.Patch.Bytes, await File.ReadAllBytesAsync(PackFile(both.Patch.Hash), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesAPackThatIsNotTheOneOfferedWhole()
    {
        var both = await Pair();
        using var transport = Serving(both.Shipped.Bytes);

        var error = await Assert.ThrowsAsync<FetchCorruptException>(() => Download(both.Patch, transport));

        Assert.Equal(both.Patch.Hash, error.Wanted);
        Assert.Equal(both.Shipped.Hash, error.Got);
        Assert.Single(transport.Sent);
        Assert.Empty(Directory.GetFileSystemEntries(_folderPath));
    }

    [Fact]
    public async Task RefusesAPackWithADamagedBlobAndKeepsNothing()
    {
        var built = (await Pair()).Patch;
        var damaged = (byte[])built.Bytes.Clone();
        var at = IndexOf(damaged, Encoding.UTF8.GetBytes(Body("wall v2")));
        damaged[at + 10] ^= 0xff;
        using var transport = Serving(damaged);

        var error = await Assert.ThrowsAsync<FormatException>(() => Download(built, transport));

        Assert.Contains("textures/wall.ktx2", error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(_folderPath));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task DoesNotRetryA404()
    {
        var built = (await Pair()).Patch;
        using var transport = new MapTransport(new Dictionary<WebUrl, TransportPage>());

        var error = await Assert.ThrowsAsync<FetchFailedException>(() => Download(built, transport));

        Assert.Equal(404, error.Cause);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task TriesABusyServerAgainAndThenSucceeds()
    {
        var built = (await Pair()).Patch;
        using var transport = Serving(built.Bytes, faults: [Fault.OfStatus(503), Fault.Thrown()]);

        var pack = await Download(built, transport);

        Assert.Equal(built.Hash, pack.Hash);
        Assert.Equal(3, transport.Sent.Count);
    }

    [Fact]
    public async Task GivesUpAfterTheAttemptsItWasGiven()
    {
        var built = (await Pair()).Patch;
        using var transport = new Answers(503);

        var error = await Assert.ThrowsAsync<FetchFailedException>(() => Download(built, transport, attempts: 3));

        Assert.Equal(3, error.Attempts);
        Assert.Equal(503, error.Cause);
        Assert.Equal(3, transport.Sent.Count);
    }

    [Fact]
    public async Task RefusesAHostTheOriginDoesNotAllowBeforeAnyRequest()
    {
        var built = (await Pair()).Patch;
        using var transport = new MapTransport(new Dictionary<WebUrl, TransportPage>());
        var elsewhere = WebUrl.Parse("https://evil.example.net/one.opack");

        await Assert.ThrowsAsync<FetchRefusedException>(
            () => _folder.DownloadAsync(new PackOffer(elsewhere, built.Hash), transport, Origin));

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task RefusesAPackLongerThanThePolicyAllows()
    {
        var built = (await Pair()).Patch;
        using var transport = Serving(built.Bytes);
        var small = new AssetOrigin(WebUrl.Parse("https://cdn.example.com/assets/"), new FetchPolicy(maxBytes: 1000));

        await Assert.ThrowsAsync<FetchRefusedException>(() => Download(built, transport, from: small));

        Assert.Empty(Directory.GetFileSystemEntries(_folderPath));
    }

    [Fact]
    public async Task StopsReadingABodyThatRunsPastThePolicy()
    {
        var built = (await Pair()).Patch;
        using var transport = new NeverEnds();
        var small = new AssetOrigin(WebUrl.Parse("https://cdn.example.com/assets/"), new FetchPolicy(maxBytes: 1000));

        await Assert.ThrowsAsync<FetchRefusedException>(() => Download(built, transport, from: small));

        Assert.Empty(Directory.GetFileSystemEntries(_folderPath));
        Assert.True(transport.Pulled < 10);
    }

    [Fact]
    public async Task DropsWhatAServerSentPastTheLengthItDeclared()
    {
        var built = (await Pair()).Patch;
        using var transport = new Lies(built.Bytes);

        await Assert.ThrowsAsync<FetchFailedException>(() => Download(built, transport));

        Assert.Equal<long?[]>([null, null, null], [.. transport.Sent.Select(request => request.From)]);
        Assert.Empty(Directory.GetFileSystemEntries(_folderPath));
    }

    [Fact]
    public async Task AnswersFromTheFolderWithoutARequestWhenItHoldsThePack()
    {
        var built = (await Pair()).Patch;
        using var transport = Serving(built.Bytes);
        await Download(built, transport);

        var again = await Download(built, transport);

        Assert.Equal(built.Hash, again.Hash);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task SharesOneDownloadBetweenTwoCallers()
    {
        var built = (await Pair()).Patch;
        using var transport = Serving(built.Bytes);
        using var gated = new Gated(transport);

        var first = Download(built, gated);
        var second = Download(built, gated);
        gated.Open();
        var both = await Task.WhenAll(first, second);

        Assert.Equal([built.Hash, built.Hash], both.Select(pack => pack.Hash));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task ReportsHowMuchHasArrivedOfHowMuch()
    {
        var built = (await Pair()).Patch;
        using var transport = Serving(built.Bytes, cut: 1024);
        var seen = new List<FetchProgress>();

        await Download(built, transport, onProgress: seen.Add);

        Assert.True(seen.Count > 2);
        Assert.Equal(built.Bytes.Length, seen[^1].Received);
        Assert.Equal<long?>(built.Bytes.Length, seen[^1].Total);
        Assert.Equal(seen.Select(p => p.Received).Order(), seen.Select(p => p.Received));
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

    // Answers every request with a status, and keeps what was asked.
    private sealed class Answers(int status) : IAssetTransport
    {
        public List<FetchRequest> Sent { get; } = [];

        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            lock (Sent)
            {
                Sent.Add(request);
            }

            return Task.FromResult(FetchReply.Empty(status));
        }

        public void Dispose()
        {
        }
    }

    // A server that sends the whole file whatever range it is asked for.
    private sealed class DropsTheRange(MapTransport inner) : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            inner.SendAsync(new FetchRequest(request.Url), cancellation);

        public void Dispose() => inner.Dispose();
    }

    // A server that answers a range from a byte a little before the one asked for, and says so.
    private sealed class ResumesEarly(MapTransport inner) : IAssetTransport
    {
        private const int By = 10;

        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            request.From is not { } from || from < By
                ? inner.SendAsync(request, cancellation)
                : inner.SendAsync(new FetchRequest(request.Url, from: from - By), cancellation);

        public void Dispose() => inner.Dispose();
    }

    // A server that answers 200 with no length and a body far longer than any pack, counting the chunks it is
    // asked for. The size can only be found out as the body arrives, and only a reader that stops early pulls few.
    private sealed class NeverEnds : IAssetTransport
    {
        private int _pulled;

        public int Pulled => Volatile.Read(ref _pulled);

        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            Task.FromResult(new FetchReply { Status = 200, Body = Chunks() });

        public void Dispose()
        {
        }

        private async IAsyncEnumerable<byte[]> Chunks()
        {
            for (var i = 0; i < 5000; i++)
            {
                Interlocked.Increment(ref _pulled);
                await Task.Yield();
                yield return new byte[400];
            }
        }
    }

    // A server whose body is longer than the length it declares.
    private sealed class Lies(byte[] bytes) : IAssetTransport
    {
        public List<FetchRequest> Sent { get; } = [];

        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            lock (Sent)
            {
                Sent.Add(request);
            }

            return Task.FromResult(new FetchReply { Status = 200, Body = Single(), Length = bytes.Length - 100 });
        }

        public void Dispose()
        {
        }

        private async IAsyncEnumerable<byte[]> Single()
        {
            await Task.Yield();
            yield return bytes;
        }
    }
}
