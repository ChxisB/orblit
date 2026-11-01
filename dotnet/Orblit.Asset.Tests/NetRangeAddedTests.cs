using System.Net;

namespace Orblit.Asset.Tests;

// What the fixtures do not say, and where the port makes a choice.
public class NetRangeAddedTests
{
    private static readonly WebUrl Url = WebUrl.Parse("https://cdn.example.com/one.opack");

    private static PackDownload DownloadWith(Random jitter) => new()
    {
        Offer = new PackOffer(Url, ContentHash.Of([1])),
        Transport = new MapTransport(new Dictionary<WebUrl, TransportPage>()),
        Origin = NetWorld.Origin,
        Part = "unused.part",
        Attempts = 4,
        Backoff = TimeSpan.FromSeconds(1),
        Jitter = jitter,
    };

    [Theory]
    [InlineData(1, 0.5, 1.0)]
    [InlineData(2, 0.5, 2.0)]
    [InlineData(3, 0.5, 4.0)]
    [InlineData(1, 0.0, 0.8)]
    [InlineData(1, 1.0, 1.2)]
    public void WaitsTwiceAsLongEachTimeGiveOrTakeAFifth(int attempt, double noise, double seconds)
    {
        var wait = DownloadWith(new Fixed(noise)).WaitBefore(attempt);

        Assert.Equal(TimeSpan.FromSeconds(seconds), wait);
    }

    [Fact]
    public void TheWaitStopsGrowingAtADayWhateverTheAttempt()
    {
        var download = DownloadWith(new Fixed(0.5));

        Assert.Equal(TimeSpan.FromDays(1), download.WaitBefore(100));
        Assert.Equal(TimeSpan.FromDays(1), download.WaitBefore(int.MaxValue));
    }

    // A download that may not try has nothing to do, so the call says so and the task does not.
    [Fact]
    public void RefusesNoAttemptsAtTheCall()
    {
        var folder = new PackFolder(Path.Join(Path.GetTempPath(), "orblit_never_made"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = folder.DownloadAsync(
                new PackOffer(Url, ContentHash.Of([1])), new MapTransport(new Dictionary<WebUrl, TransportPage>()), NetWorld.Origin, attempts: 0);
        });
    }

    [Fact]
    public async Task ReadingANegativeRangeFailsBeforeAnyRequest()
    {
        using var transport = new MapTransport(new Dictionary<WebUrl, TransportPage>());
        var ranged = new RangePackBytes(transport, Url);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ranged.ReadAsync(-1, 4));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ranged.ReadAsync(0, -4));

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task ALostConnectionBeforeAnyReplyIsASocketFailure()
    {
        using var client = new HttpClient(new Stub(_ => throw new HttpRequestException("no route to host")));
        using var transport = new HttpTransport(client);

        await Assert.ThrowsAsync<SocketFailureException>(
            () => transport.SendAsync(new FetchRequest(Url), TestContext.Current.CancellationToken));
    }

    // A connection that drops partway through a body has to look like one that dropped before it, because
    // that is what a download retries. A disk error is also an IOException, and is not retried.
    [Fact]
    public async Task ALostConnectionPartwayThroughABodyIsASocketFailure()
    {
        using var client = new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Dropping()) }));
        using var transport = new HttpTransport(client);
        var reply = await transport.SendAsync(new FetchRequest(Url), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<SocketFailureException>(async () =>
        {
            await foreach (var chunk in reply.Body)
            {
                Assert.NotEmpty(chunk);
            }
        });
    }

    [Fact]
    public async Task ABodyThatIsTurnedAwayUnreadClosesTheResponse()
    {
        var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var client = new HttpClient(stub);
        using var transport = new HttpTransport(client);
        var reply = await transport.SendAsync(new FetchRequest(Url), TestContext.Current.CancellationToken);

        await reply.DiscardAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => stub.Last!.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A304HasNoBodyAndKeepsItsETag()
    {
        using var client = new HttpClient(new Stub(_ =>
        {
            var reply = new HttpResponseMessage(HttpStatusCode.NotModified);
            reply.Headers.TryAddWithoutValidation("ETag", "\"abc\"");
            return reply;
        }));
        using var transport = new HttpTransport(client);

        var reply = await transport.SendAsync(new FetchRequest(Url, ifNoneMatch: "\"abc\""), TestContext.Current.CancellationToken);

        Assert.True(reply.IsNotModified);
        Assert.Equal("\"abc\"", reply.ETag);
    }

    private sealed class Fixed(double value) : Random
    {
        public override double NextDouble() => value;
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpResponseMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation) =>
            Task.FromResult(Last = answer(request));
    }

    // A stream that gives a few bytes and then loses the connection.
    private sealed class Dropping : Stream
    {
        private bool _gave;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            if (_gave)
            {
                throw new IOException("connection reset");
            }

            _gave = true;
            buffer.Span[0] = 7;
            return ValueTask.FromResult(1);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
