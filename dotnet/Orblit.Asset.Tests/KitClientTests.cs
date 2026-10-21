using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

// Reading a store of kits over a network that answers from a map.
public sealed class KitClientTests : IAsyncLifetime
{
    private static readonly WebUrl StoreBase = WebUrl.Parse("https://store.example/store/");
    private static readonly byte[] Png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3];

    private readonly KitWorkspace _work = new();
    private readonly Dictionary<WebUrl, TransportPage> _pages = [];
    private readonly MapTransport _transport;
    private readonly KitStoreClient _client;

    public KitClientTests()
    {
        _transport = new MapTransport(_pages);
        _client = new KitStoreClient(StoreBase, _transport);
    }

    // A network whose replies do not say how long they are.
    private sealed class NoLength(IAssetTransport inner) : IAssetTransport
    {
        public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            var reply = await inner.SendAsync(request, cancellation);
            return new FetchReply { Status = reply.Status, Body = reply.Body };
        }

        public void Dispose() => inner.Dispose();
    }

    // A network that never answers.
    private sealed class Silent : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            new TaskCompletionSource<FetchReply>().Task;

        public void Dispose()
        {
        }
    }

    // A network that fails the way a socket does.
    private sealed class TimesOut : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            throw new TimeoutException("the socket timed out");

        public void Dispose()
        {
        }
    }

    // A network that gives one reply to every request.
    private sealed class Fixed(FetchReply reply) : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            Task.FromResult(reply);

        public void Dispose()
        {
        }
    }

    // A body that notes it was let go of.
    private sealed class Watched : IAsyncEnumerable<byte[]>
    {
        public bool Released { get; private set; }

        public IAsyncEnumerator<byte[]> GetAsyncEnumerator(CancellationToken cancellation = default) => new Handle(this);

        private sealed class Handle(Watched owner) : IAsyncEnumerator<byte[]>
        {
            public byte[] Current => [];

            public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);

            public ValueTask DisposeAsync()
            {
                owner.Released = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    public async ValueTask InitializeAsync() => await PublishAsync();

    public ValueTask DisposeAsync()
    {
        _transport.Dispose();
        _work.Dispose();
        return ValueTask.CompletedTask;
    }

    private static WebUrl Under(string path) => StoreBase.Resolve(WebUrl.Parse(path));

    // Packs a kit, lists it in an entry, builds a store from it and serves the pack and the store's files
    // over the fake network.
    private async Task PublishAsync()
    {
        var cancel = TestContext.Current.CancellationToken;
        var folder = _work.At("src");
        Directory.CreateDirectory(Path.Combine(folder, "models"));
        Directory.CreateDirectory(Path.Combine(folder, "shots"));
        await File.WriteAllTextAsync(Path.Combine(folder, "models/crate.obj"), "crate", cancel);
        await File.WriteAllBytesAsync(Path.Combine(folder, "shots/a.png"), Png, cancel);
        var manifest = new JsonObject
        {
            ["slug"] = "crates",
            ["publisher"] = "brass-lamp",
            ["name"] = "Crates",
            ["summary"] = "Some crates.",
            ["version"] = "1.0.0",
            ["licence"] = new JsonObject { ["spdx"] = "MIT", ["url"] = "https://example.com/l" },
            ["engine"] = new JsonObject { ["from"] = "0.30.0", ["to"] = "0.40.0" },
            ["tags"] = new JsonArray("props"),
            ["previews"] = new JsonArray("shots/a.png"),
        };
        await File.WriteAllTextAsync(Path.Combine(folder, "kit.json"), manifest.ToJsonString(), cancel);
        var summary = await KitPacking.PackAsync(folder, _work.At("crates.opack"));
        var pack = await File.ReadAllBytesAsync(_work.At("crates.opack"), cancel);
        const string PackUrl = "https://example.com/crates.opack";
        _pages[WebUrl.Parse(PackUrl)] = new TransportPage(pack);
        var entry = new JsonObject
        {
            ["releases"] = new JsonArray(new JsonObject
            {
                ["version"] = "1.0.0",
                ["url"] = PackUrl,
                ["hash"] = summary.Hash.Hex,
                ["bytes"] = pack.Length,
                ["released"] = "2026-10-19",
                ["notes"] = "First.",
            }),
        };
        Directory.CreateDirectory(_work.At("entries/brass-lamp"));
        await File.WriteAllTextAsync(_work.At("entries/brass-lamp/crates.json"), entry.ToJsonString(), cancel);
        await KitStoreBuilder.BuildAsync(_work.At("entries"), _work.At("out"), _transport);
        foreach (var file in Directory.EnumerateFiles(_work.At("out"), "*", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(_work.At("out"), file).Replace('\\', '/');
            _pages[Under(name)] = new TransportPage(await File.ReadAllBytesAsync(file, cancel));
        }
    }

    [Fact]
    public async Task ReadsTheIndexTheReleasesAPreviewAndTheFileList()
    {
        var row = (await _client.IndexAsync()).Rows.Single();
        Assert.Equal("Crates", row.Manifest.Name);

        var releases = await _client.ReleasesAsync(row);

        Assert.Equal(["preview-0.png"], releases.Previews);
        Assert.Equal(Png, await _client.PreviewAsync(row, "preview-0.png"));
        Assert.Equal(Png, await _client.ThumbnailAsync(row));
        var kit = await _client.OpenAsync(releases.Releases.Single().Release);
        Assert.Equal(["models/crate.obj", "shots/a.png"], kit.Files.Keys.Select(id => id.ToString()));
    }

    [Fact]
    public async Task TakesABaseWithNoTrailingSlashAsAFolder()
    {
        var other = new KitStoreClient(WebUrl.Parse("https://store.example/store"), _transport);

        Assert.Single((await other.IndexAsync()).Rows);
    }

    [Fact]
    public void RefusesABaseThatIsNotAPlainHttpsAddress()
    {
        foreach (var text in new[]
        {
            "http://store.example/store/",
            "https://store.example/store/?a=1",
            "https://store.example/store/#a",
            "https://me@store.example/store/",
            "/store/",
        })
        {
            var error = Record.Exception(() => new KitStoreClient(WebUrl.Parse(text), _transport));

            Assert.True(error?.GetType() == typeof(ArgumentException), text);
            Assert.NotNull(KitStoreClient.AddressProblem(WebUrl.Parse(text)));
        }
    }

    [Fact]
    public void AnAddressWithNothingWrongWithItHasNoProblem() =>
        Assert.Null(KitStoreClient.AddressProblem(StoreBase));

    [Fact]
    public async Task RefusesAStatusOtherThan200()
    {
        _pages.Remove(Under("index.json"));

        await Assert.ThrowsAsync<KitStoreFailedException>(() => _client.IndexAsync());
    }

    [Fact]
    public async Task RefusesABodyOverTheLimitByHeaderAndByCount()
    {
        var row = (await _client.IndexAsync()).Rows.Single();
        _pages[Under("kits/brass-lamp/crates/preview-0.png")] = new TransportPage(new byte[2 * 1024 * 1024], cut: 4096);
        var unsaid = new KitStoreClient(StoreBase, new NoLength(_transport));

        foreach (var (reader, reason) in new[] { (_client, "bytes and the most is"), (unsaid, "bytes allowed") })
        {
            var error = await Assert.ThrowsAsync<KitStoreFailedException>(() => reader.PreviewAsync(row, "preview-0.png"));
            Assert.Contains(reason, error.Reason);
        }
    }

    [Fact]
    public async Task RefusesAPreviewNameThatIsNotAPreview()
    {
        var row = (await _client.IndexAsync()).Rows.Single();

        foreach (var name in new[] { "../index.json", "releases.json", "preview-9.png" })
        {
            var error = await Record.ExceptionAsync(() => _client.PreviewAsync(row, name));

            Assert.True(error?.GetType() == typeof(ArgumentException), name);
        }
    }

    [Fact]
    public async Task AStoreWithANewerFormatIsRefused()
    {
        var text = Encoding.UTF8.GetString(_pages[Under("index.json")].Bytes);
        var newer = text.Replace("\"formatVersion\":1", "\"formatVersion\":9", StringComparison.Ordinal);
        Assert.NotEqual(text, newer);
        _pages[Under("index.json")] = new TransportPage(Encoding.UTF8.GetBytes(newer));

        await Assert.ThrowsAnyAsync<FormatException>(() => _client.IndexAsync());
    }

    [Fact]
    public async Task GivesUpOnAStoreThatDoesNotAnswer()
    {
        using var silent = new Silent();
        var client = new KitStoreClient(StoreBase, silent, TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<KitStoreFailedException>(() => client.IndexAsync());
    }

    [Fact]
    public async Task RefusesAFileListThatAsksToReadTooMuchAtOnce()
    {
        var row = (await _client.IndexAsync()).Rows.Single();
        var release = (await _client.ReleasesAsync(row)).Releases.Single().Release;
        // A header that says the directory is 5 MiB, in a file that is long enough to hold it.
        var bytes = new byte[6 * 1024 * 1024];
        new PackHeader(DirectoryAt: 32, DirectoryBytes: 5 * 1024 * 1024).Encode().CopyTo(bytes, 0);
        _pages[WebUrl.Parse(release.Url)] = new TransportPage(bytes);

        var error = await Assert.ThrowsAnyAsync<FormatException>(() => _client.OpenAsync(release));

        Assert.Contains("the most is", error.Message);
    }

    [Theory]
    [InlineData(404, null, "answered 404")]
    [InlineData(200, 20_000_000L, "bytes and the most is")]
    public async Task LetsGoOfAReplyItTurnsAway(int status, long? length, string reason)
    {
        var body = new Watched();
        using var fixedReply = new Fixed(new FetchReply { Status = status, Length = length, Body = body });
        var client = new KitStoreClient(StoreBase, fixedReply);

        var error = await Assert.ThrowsAsync<KitStoreFailedException>(() => client.IndexAsync());

        Assert.Contains(reason, error.Reason);
        Assert.True(body.Released);
    }

    [Fact]
    public void RefusesABaseWithNoHost()
    {
        Assert.Throws<ArgumentException>(() => new KitStoreClient(WebUrl.Parse("https:///store/"), _transport));
        Assert.NotNull(KitStoreClient.AddressProblem(WebUrl.Parse("https:///store/")));
    }

    [Fact]
    public async Task PassesOnATimeoutTheReadRaisedItself()
    {
        using var timesOut = new TimesOut();
        var client = new KitStoreClient(StoreBase, timesOut);

        await Assert.ThrowsAsync<TimeoutException>(() => client.IndexAsync());
    }

    [Fact]
    public async Task ReadsAnIndexLargerThanAReleasesFileAllows()
    {
        _pages[Under("index.json")] = new TransportPage(new byte[2 * 1024 * 1024]);

        await Assert.ThrowsAnyAsync<FormatException>(() => _client.IndexAsync());
    }

    [Fact]
    public async Task RefusesAnIndexOverItsLimit()
    {
        _pages[Under("index.json")] = new TransportPage(new byte[KitStoreRules.IndexLimit + 1]);

        await Assert.ThrowsAsync<KitStoreFailedException>(() => _client.IndexAsync());
    }

    [Theory]
    [InlineData(4 * 1024 * 1024, false)]
    [InlineData(4 * 1024 * 1024 + 1, true)]
    public async Task RefusesADirectoryOnlyPastTheOpenLimit(int directoryBytes, bool refused)
    {
        var row = (await _client.IndexAsync()).Rows.Single();
        var release = (await _client.ReleasesAsync(row)).Releases.Single().Release;
        var bytes = new byte[6 * 1024 * 1024];
        new PackHeader(DirectoryAt: 32, DirectoryBytes: directoryBytes).Encode().CopyTo(bytes, 0);
        _pages[WebUrl.Parse(release.Url)] = new TransportPage(bytes);

        var error = await Assert.ThrowsAnyAsync<FormatException>(() => _client.OpenAsync(release));

        Assert.Equal(refused, error.Message.Contains("the most is", StringComparison.Ordinal));
    }
}
