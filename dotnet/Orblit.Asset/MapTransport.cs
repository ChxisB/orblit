namespace Orblit.Asset;

/// <summary>
/// A transport that answers from a map, for tests and for offline demos. Understands enough of the protocol
/// to exercise the code that depends on it: an ETag it answers 304 to, a range it honours, and a list of
/// failures to hand out before it starts succeeding.
/// </summary>
public sealed class MapTransport(IDictionary<WebUrl, TransportPage> pages) : IAssetTransport
{
    private bool _closed;

    /// <summary>What is served, by URL.</summary>
    public IDictionary<WebUrl, TransportPage> Pages { get; } = pages;

    /// <summary>Every request that has been sent, in order, for a test to assert on.</summary>
    public List<FetchRequest> Sent { get; } = [];

    public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        // The answer comes later than the call, as a network's does.
        await Task.Yield();
        if (_closed)
        {
            throw new InvalidOperationException("This transport is closed.");
        }

        // Downloads run on pool threads, so two sends can land together.
        lock (Sent)
        {
            Sent.Add(request);
        }

        return Pages.TryGetValue(request.Url, out var page) ? Answer(request, page) : FetchReply.Empty(404);
    }

    public void Dispose() => _closed = true;

    private static FetchReply Answer(FetchRequest request, TransportPage page)
    {
        var fault = page.Take();
        if (fault is not null)
        {
            return fault.Status is { } status ? FetchReply.Empty(status) : throw fault.Error!;
        }

        return request.IfNoneMatch is not null && request.IfNoneMatch == page.ETag
            ? FetchReply.Empty(304, page.ETag)
            : Serve(request, page);
    }

    private static FetchReply Serve(FetchRequest request, TransportPage page)
    {
        var from = request.From ?? 0;
        if (from > page.Bytes.Length)
        {
            return FetchReply.Empty(416);
        }

        // A range that runs past the end is cut at the end, as a server does.
        var end = request.Through is { } through ? (int)Math.Clamp(through + 1, from, page.Bytes.Length) : page.Bytes.Length;
        var rest = page.Bytes[(int)from..end];

        // Armed for one reply only: the resumed request that follows is answered in full, which is the
        // sequence resuming has to survive.
        var stop = page.StopAfter;
        page.StopAfter = null;

        // A bounded request is always a range, even one that starts at nought.
        var whole = from == 0 && request.Through is null;
        return new FetchReply
        {
            Status = whole ? 200 : 206,
            Body = stop is { } after ? StopsShort(rest, after) : Chunks(rest, page.Cut),
            ETag = page.ETag,
            Length = page.Bytes.Length,
            From = whole ? null : from,
        };
    }

    private static async IAsyncEnumerable<byte[]> Chunks(byte[] rest, int cut)
    {
        if (cut <= 0)
        {
            await Task.Yield();
            yield return rest;
            yield break;
        }

        for (var at = 0; at < rest.Length; at += cut)
        {
            await Task.Yield();
            yield return rest[at..Math.Clamp(at + cut, 0, rest.Length)];
        }
    }

    // Hands over part of the bytes and then loses the connection, once. The failure that resuming exists
    // for, and the one that is invisible in a test that only ever fails before the body: a reply that
    // started, sent most of a large file and stopped.
    private static async IAsyncEnumerable<byte[]> StopsShort(byte[] bytes, int after)
    {
        await Task.Yield();
        yield return bytes[..Math.Clamp(after, 0, bytes.Length)];
        throw new SocketFailureException("the connection dropped partway");
    }
}
