using System.Net;

namespace Orblit.Asset.Tests;

public sealed class NetRangePackTests : IDisposable
{
    private static readonly WebUrl Url = WebUrl.Parse("https://cdn.example.com/game.web.opack");

    // Reads made together are joined. Threads have no turn, so a read waits long enough for
    // the others of its test to be made.
    private static readonly RangeBatching Joining = new() { Delay = TimeSpan.FromMilliseconds(50) };

    private readonly PackFixture _fixture = new();
    private int _built;

    public void Dispose() => _fixture.Dispose();

    // Bytes for one asset, long enough that a blob is not the header.
    private static byte[] Content(string name, int bytes = 300)
    {
        var seed = 0L;
        foreach (var unit in name)
        {
            seed = unchecked((seed * 31) + unit);
        }

        return [.. Enumerable.Range(0, bytes).Select(i => (byte)(((i * 7) + seed) & 0xff))];
    }

    private async Task<byte[]> PackAsync(Dictionary<string, byte[]> files)
    {
        var name = $"pack_{_built++}";
        var folder = await _fixture.BundleAsync($"bundle_{name}", files);
        return (await _fixture.PackFolderAsync(folder, name, null, "web")).Bytes;
    }

    private static Dictionary<string, byte[]> Game(string texture = "a") => new()
    {
        ["textures/wall.ktx2"] = Content($"wall{texture}"),
        ["models/robot.glb"] = Content("robot", 700),
        ["scenes/main.oscene"] = Content("main", 120),
    };

    private static MapTransport Serving(byte[] bytes) => new(new Dictionary<WebUrl, TransportPage> { [Url] = new TransportPage(bytes) });

    private static async Task<byte[]> BodyOf(FetchReply reply)
    {
        var all = new List<byte>();
        await foreach (var chunk in reply.Body)
        {
            all.AddRange(chunk);
        }

        return [.. all];
    }

    // MapTransport

    [Fact]
    public async Task AnswersABoundedRangeWithTheBytesBetweenInclusive()
    {
        using var transport = Serving([.. Enumerable.Range(0, 100).Select(i => (byte)i)]);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 10, through: 19), TestContext.Current.CancellationToken);

        Assert.Equal(206, reply.Status);
        Assert.Equal<long?>(10, reply.From);
        Assert.Equal<long?>(100, reply.Length);
        Assert.Equal(Enumerable.Range(10, 10).Select(i => (byte)i), await BodyOf(reply));
    }

    [Fact]
    public async Task Says206ForARangeThatStartsAtNought()
    {
        using var transport = Serving(new byte[100]);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 0, through: 31), TestContext.Current.CancellationToken);

        Assert.Equal(206, reply.Status);
        Assert.Equal<long?>(0, reply.From);
    }

    [Fact]
    public async Task CutsARangeThatRunsPastTheEndAtTheEnd()
    {
        using var transport = Serving([.. Enumerable.Range(0, 40).Select(i => (byte)i)]);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 30, through: 99), TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(30, 10).Select(i => (byte)i), await BodyOf(reply));
    }

    [Fact]
    public async Task RefusesARangeThatStartsAfterTheEnd()
    {
        using var transport = Serving(new byte[10]);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 11, through: 12), TestContext.Current.CancellationToken);

        Assert.Equal(416, reply.Status);
    }

    [Fact]
    public void AnEndWithoutAStartIsAMistake() =>
        Assert.Throws<ArgumentException>(() => new FetchRequest(Url, through: 5));

    [Fact]
    public void AnEndBeforeTheStartIsAMistake() =>
        Assert.Throws<ArgumentException>(() => new FetchRequest(Url, from: 5, through: 4));

    // HttpTransport

    [Fact]
    public async Task SendsBothEndsOfABoundedRange()
    {
        var handler = new Stub(() => Reply(HttpStatusCode.PartialContent, [1, 2, 3], "bytes 10-12/100"));
        using var client = new HttpClient(handler);
        using var transport = new HttpTransport(client);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 10, through: 12), TestContext.Current.CancellationToken);

        Assert.Equal("bytes=10-12", handler.Range);
        Assert.Equal(206, reply.Status);
        Assert.Equal<long?>(10, reply.From);
        Assert.Equal<long?>(100, reply.Length);
    }

    [Fact]
    public async Task LeavesTheEndOpenForAResume()
    {
        var handler = new Stub(() => Reply(HttpStatusCode.PartialContent, [], "bytes 900-999/1000"));
        using var client = new HttpClient(handler);
        using var transport = new HttpTransport(client);

        await transport.SendAsync(new FetchRequest(Url, from: 900), TestContext.Current.CancellationToken);

        Assert.Equal("bytes=900-", handler.Range);
    }

    [Fact]
    public async Task ReportsWhereTheServerStartedNotWhereItWasAskedTo()
    {
        using var client = new HttpClient(new Stub(() => Reply(HttpStatusCode.PartialContent, [], "bytes 0-31/500")));
        using var transport = new HttpTransport(client);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 64, through: 95), TestContext.Current.CancellationToken);

        Assert.Equal<long?>(0, reply.From);
    }

    [Fact]
    public async Task GivesNoLengthForA206ThatHidesItsContentRange()
    {
        using var client = new HttpClient(new Stub(() => Reply(HttpStatusCode.PartialContent, [1, 2, 3])));
        using var transport = new HttpTransport(client);

        var reply = await transport.SendAsync(new FetchRequest(Url, from: 0, through: 2), TestContext.Current.CancellationToken);

        Assert.Null(reply.Length);
    }

    [Fact]
    public async Task TakesTheContentLengthOfA200ForTheLength()
    {
        using var client = new HttpClient(new Stub(() => Reply(HttpStatusCode.OK, [1, 2, 3])));
        using var transport = new HttpTransport(client);

        var reply = await transport.SendAsync(new FetchRequest(Url), TestContext.Current.CancellationToken);

        Assert.Equal<long?>(3, reply.Length);
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, byte[] body, string? contentRange = null)
    {
        var reply = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
        if (contentRange is not null)
        {
            reply.Content.Headers.TryAddWithoutValidation("Content-Range", contentRange);
        }

        return reply;
    }

    // RangePackBytes

    [Fact]
    public async Task ReadsTheHeaderTheDirectoryAndThreeBlobsAndNothingElse()
    {
        var bytes = await PackAsync(Game());
        using var transport = Serving(bytes);

        var opened = await AssetPack.OpenAsync(new RangePackBytes(transport, Url));
        foreach (var path in Game().Keys)
        {
            await opened.ReadAsync(AssetId.Parse(path));
        }

        var header = PackHeader.Read(bytes);
        Assert.Equal(5, transport.Sent.Count);
        Assert.All(transport.Sent, request => Assert.NotNull(request.From));
        Assert.All(transport.Sent, request => Assert.NotNull(request.Through));
        Assert.Equal<long?>(0, transport.Sent[0].From);
        Assert.Equal<long?>(PackHeader.Size - 1, transport.Sent[0].Through);
        Assert.Equal<long?>(header.DirectoryAt, transport.Sent[1].From);
        Assert.Equal<long?>(header.DirectoryAt + header.DirectoryBytes - 1, transport.Sent[1].Through);
        foreach (var blob in transport.Sent.Skip(2))
        {
            Assert.True(blob.From >= PackHeader.Size);
            Assert.True(blob.Through < header.DirectoryAt);
        }
    }

    [Fact]
    public async Task ReadsTheSameBytesThePackHolds()
    {
        var files = Game();
        using var transport = Serving(await PackAsync(files));
        var opened = await AssetPack.OpenAsync(new RangePackBytes(transport, Url));

        foreach (var (key, value) in files)
        {
            Assert.Equal(value, await opened.ReadAsync(AssetId.Parse(key)));
        }

        Assert.Empty(await opened.FindCorruptAsync());
    }

    [Fact]
    public async Task LearnsItsLengthFromTheFirstRequestAndAsksNoMore()
    {
        var bytes = await PackAsync(Game());
        using var transport = Serving(bytes);
        var ranged = new RangePackBytes(transport, Url);

        Assert.Equal(bytes.Length, await ranged.LengthAsync());
        Assert.Equal(bytes[..PackHeader.Size], await ranged.ReadAsync(0, PackHeader.Size));
        Assert.Equal(bytes.Length, await ranged.LengthAsync());

        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task SharesOneFirstRequestBetweenCallersWhoAskTogether()
    {
        using var transport = Serving(await PackAsync(Game()));
        var ranged = new RangePackBytes(transport, Url);

        await Task.WhenAll(ranged.LengthAsync(), ranged.LengthAsync(), ranged.ReadAsync(0, 8));

        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task AsksAgainAfterTheFirstRequestFailed()
    {
        var bytes = await PackAsync(Game());
        using var transport = new MapTransport(new Dictionary<WebUrl, TransportPage>
        {
            [Url] = new TransportPage(bytes, faults: [Fault.Thrown()]),
        });
        var ranged = new RangePackBytes(transport, Url);

        await Assert.ThrowsAsync<SocketFailureException>(ranged.LengthAsync);
        Assert.Equal(bytes.Length, await ranged.LengthAsync());
    }

    [Fact]
    public async Task SendsReadsMadeTogetherAsFewerRequests()
    {
        var bytes = await PackAsync(Game());
        using var transport = Serving(bytes);
        var ranged = new RangePackBytes(transport, Url, Joining);
        const int at = PackHeader.Size;

        var parts = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => ranged.ReadAsync(at + (i * 20), 20)));

        // The first request is the header and the second is every blob.
        Assert.Equal(2, transport.Sent.Count);
        Assert.Equal<long?>(at, transport.Sent[^1].From);
        Assert.Equal<long?>(at + (40 * 20) - 1, transport.Sent[^1].Through);
        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(bytes[(at + (i * 20))..(at + (i * 20) + 20)], parts[i]);
        }
    }

    [Fact]
    public async Task AsksForAnUrgentReadBeforeOneThatCanWait()
    {
        using var transport = Serving(await PackAsync(Game()));
        var ranged = new RangePackBytes(
            transport, Url, new RangeBatching { Parallel = 1, MergeGap = 0, Delay = TimeSpan.FromMilliseconds(50) });
        const int at = PackHeader.Size;

        await Task.WhenAll(
            ranged.ReadAsync(at, 10, RangePriority.Later),
            ranged.ReadAsync(at + 100, 10, RangePriority.Later),
            ranged.ReadAsync(at + 200, 10));

        Assert.Equal<long?[]>([at + 200, at, at + 100], [.. transport.Sent.Skip(1).Select(request => request.From)]);
    }

    [Fact]
    public async Task RefusesReadingPastTheEnd()
    {
        var bytes = await PackAsync(Game());
        using var transport = Serving(bytes);
        var ranged = new RangePackBytes(transport, Url);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ranged.ReadAsync(bytes.Length - 4, 8));
    }

    [Fact]
    public async Task SaysSoWhenTheHostIgnoresTheRangeAndStopsTheDownload()
    {
        using var transport = new RangeIgnorer(await PackAsync(Game()));
        var ranged = new RangePackBytes(transport, Url);

        var error = await Assert.ThrowsAsync<PackRangeFailedException>(ranged.LengthAsync);

        Assert.Contains("ignored the range", error.Reason);
        Assert.True(transport.Cancelled);
    }

    [Fact]
    public async Task TakesAWholeFileRangeAnsweredWith200()
    {
        var bytes = await PackAsync(Game());
        using var transport = new WholeAs200(Serving(bytes));
        var ranged = new RangePackBytes(transport, Url);

        Assert.Equal(bytes, await ranged.ReadAsync(0, bytes.Length));
    }

    [Fact]
    public async Task RefusesAnAnswerThatStartsSomewhereElse()
    {
        using var transport = new Shifted(Serving(await PackAsync(Game())));
        var ranged = new RangePackBytes(transport, Url);

        await Assert.ThrowsAsync<PackRangeFailedException>(() => AssetPack.OpenAsync(ranged));
    }

    [Fact]
    public async Task RefusesAPackThatWasReplacedWhileItWasBeingRead()
    {
        var first = await PackAsync(Game());
        var second = await PackAsync(Replaced());
        var pages = new Dictionary<WebUrl, TransportPage> { [Url] = new TransportPage(first) };
        using var transport = new MapTransport(pages);
        var opened = await AssetPack.OpenAsync(new RangePackBytes(transport, Url));

        pages[Url] = new TransportPage(second);

        var error = await Assert.ThrowsAsync<PackRangeFailedException>(() => opened.ReadAsync(AssetId.Parse("models/robot.glb")));
        Assert.Contains("changed", error.Reason);
    }

    [Fact]
    public async Task FailsEveryReadOfAJoinedRequestOnAReplacedPack()
    {
        var first = await PackAsync(Game());
        var second = await PackAsync(Replaced());
        var pages = new Dictionary<WebUrl, TransportPage> { [Url] = new TransportPage(first) };
        using var transport = new MapTransport(pages);
        var ranged = new RangePackBytes(transport, Url, Joining);
        await ranged.LengthAsync();
        pages[Url] = new TransportPage(second);
        const int at = PackHeader.Size;

        var reads = new[] { ranged.ReadAsync(at, 20), ranged.ReadAsync(at + 20, 20) };

        foreach (var read in reads)
        {
            await Assert.ThrowsAsync<PackRangeFailedException>(() => read);
        }

        Assert.Equal(2, transport.Sent.Count);
    }

    [Fact]
    public async Task NamesTheHeaderACrossOriginHostHasToExpose()
    {
        using var transport = new HidesLength(Serving(await PackAsync(Game())));
        var ranged = new RangePackBytes(transport, Url);

        var error = await Assert.ThrowsAsync<PackRangeFailedException>(ranged.LengthAsync);

        Assert.Contains("Access-Control-Expose-Headers", error.Reason);
    }

    [Fact]
    public async Task SaysWhatAMissingFileDid()
    {
        using var transport = new MapTransport(new Dictionary<WebUrl, TransportPage>());
        var ranged = new RangePackBytes(transport, Url);

        var error = await Assert.ThrowsAsync<PackRangeFailedException>(ranged.LengthAsync);

        Assert.Contains("404", error.Reason);
    }

    private static Dictionary<string, byte[]> Replaced()
    {
        var files = Game("b");
        files["extra/new.bin"] = Content("x");
        return files;
    }

    // A pack read over the network with a store

    private static readonly AssetId Wall = AssetId.Parse("textures/wall.ktx2");
    private static readonly AssetId Robot = AssetId.Parse("models/robot.glb");
    private static readonly AssetId Main = AssetId.Parse("scenes/main.oscene");

    private static async Task ReadAll(AssetPack opened)
    {
        foreach (var id in new[] { Wall, Robot, Main })
        {
            await opened.ReadAsync(id);
        }
    }

    [Fact]
    public async Task ASecondVisitFetchesTheDirectoryAndNoBlob()
    {
        var bytes = await PackAsync(Game());
        var store = new MemoryContentStore();
        using var first = Serving(bytes);
        await ReadAll(await AssetPack.OpenAsync(new RangePackBytes(first, Url), store));

        using var second = Serving(bytes);
        var opened = await AssetPack.OpenAsync(new RangePackBytes(second, Url), store);
        await ReadAll(opened);

        Assert.Equal(2, second.Sent.Count);
        Assert.Equal(Game()["models/robot.glb"], await opened.ReadAsync(Robot));
    }

    [Fact]
    public async Task ARebuiltPackFetchesOnlyTheBlobThatChanged()
    {
        var store = new MemoryContentStore();
        using var first = Serving(await PackAsync(Game()));
        await ReadAll(await AssetPack.OpenAsync(new RangePackBytes(first, Url), store));

        var rebuiltBytes = await PackAsync(Game("b"));
        using var rebuilt = Serving(rebuiltBytes);
        var opened = await AssetPack.OpenAsync(new RangePackBytes(rebuilt, Url), store);
        await ReadAll(opened);

        var header = PackHeader.Read(rebuiltBytes);
        Assert.Equal(3, rebuilt.Sent.Count);
        Assert.True(rebuilt.Sent[^1].From < header.DirectoryAt);
        Assert.Equal(Game("b")["textures/wall.ktx2"], await opened.ReadAsync(Wall));
    }

    [Fact]
    public async Task WithoutAStoreEveryReadGoesToTheServer()
    {
        using var transport = Serving(await PackAsync(Game()));
        var opened = await AssetPack.OpenAsync(new RangePackBytes(transport, Url));

        await opened.ReadAsync(Robot);
        await opened.ReadAsync(Robot);

        Assert.Equal(4, transport.Sent.Count);
    }

    [Fact]
    public async Task BytesThatAreNotTheBlobAreRefusedAndNotKeptAsIt()
    {
        var bytes = await PackAsync(Game());
        var header = PackHeader.Read(bytes);
        var plain = await AssetPack.OpenAsync(new MemoryPackBytes(bytes));
        var hash = plain.Manifest.Entries[Robot].Hash;
        var damaged = (byte[])bytes.Clone();
        for (var i = PackHeader.Size; i < header.DirectoryAt; i++)
        {
            damaged[i] ^= 0xff;
        }

        var store = new MemoryContentStore();
        using var transport = Serving(damaged);
        var opened = await AssetPack.OpenAsync(new RangePackBytes(transport, Url), store);

        await Assert.ThrowsAsync<FormatException>(() => opened.ReadAsync(Robot));
        Assert.False(await store.ContainsAsync(hash));
    }

    // Answers with whatever the test hands it, and keeps the range it was asked for.
    private sealed class Stub(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public string? Range { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Range = request.Headers.Range?.ToString();
            return Task.FromResult(answer());
        }
    }

    // A host that does not know about ranges and sends the file. The body is its own enumerator so that
    // starting it and letting it go, which is how a refused reply is turned away, is seen.
    private sealed class RangeIgnorer(byte[] bytes) : IAssetTransport, IAsyncEnumerable<byte[]>, IAsyncEnumerator<byte[]>
    {
        private bool _sent;

        public bool Cancelled { get; private set; }

        public byte[] Current { get; private set; } = [];

        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            Task.FromResult(new FetchReply { Status = 200, Body = this, Length = bytes.Length });

        public IAsyncEnumerator<byte[]> GetAsyncEnumerator(CancellationToken cancellation = default) => this;

        public ValueTask<bool> MoveNextAsync()
        {
            Current = bytes;
            return ValueTask.FromResult(!_sent && (_sent = true));
        }

        public ValueTask DisposeAsync()
        {
            Cancelled = true;
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    // A host that answers a range that covers the whole file with a 200.
    private sealed class WholeAs200(IAssetTransport inner) : IAssetTransport
    {
        public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            var reply = await inner.SendAsync(request, cancellation);
            return reply.Status != 206 || reply.From != 0 || reply.Length != request.Through + 1
                ? reply
                : new FetchReply { Status = 200, Body = reply.Body, Length = reply.Length };
        }

        public void Dispose() => inner.Dispose();
    }

    // A host whose content-range a page on another origin cannot read.
    private sealed class HidesLength(IAssetTransport inner) : IAssetTransport
    {
        public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            var reply = await inner.SendAsync(request, cancellation);
            return new FetchReply { Status = reply.Status, Body = reply.Body, From = reply.From };
        }

        public void Dispose() => inner.Dispose();
    }

    // A host that answers from the start of the file whatever was asked for.
    private sealed class Shifted(IAssetTransport inner) : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            var length = request.Through - request.From + 1;
            return inner.SendAsync(new FetchRequest(request.Url, from: 0, through: length - 1), cancellation);
        }

        public void Dispose() => inner.Dispose();
    }
}
