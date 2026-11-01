namespace Orblit.Asset;

/// <summary>
/// A pack on a server, read by asking for the bytes wanted. An <see cref="AssetPack"/> needs a header, a
/// directory and then one blob per asset read. Over a connection that is a few kilobytes up front and the
/// rest as the game asks, where fetching the file whole is the whole pack before the first frame. The first
/// request takes the header, which also says how long the file is, so <see cref="LengthAsync"/> costs
/// nothing a read would not.
/// </summary>
/// <remarks>
/// The server has to answer a <c>Range</c> header with <c>206</c>. A host that ignores it sends the whole
/// file to every request, so this refuses that reply and stops reading it, and says so in
/// <see cref="PackRangeFailedException"/>. Serve <c>.opack</c> as <c>application/octet-stream</c> with no
/// content encoding, since a range of an encoded reply counts the encoded bytes.
/// <para>
/// Reads made close together are joined into fewer, larger requests, and an urgent read goes before one
/// that can wait. A read with no <see cref="RangePriority"/> is urgent, which is what an
/// <see cref="AssetPack"/> asks for. A failure is not retried here. A caller that wants retries over a
/// flaky connection wraps the transport.
/// </para>
/// </remarks>
public sealed class RangePackBytes : IPackBytes
{
    private readonly IAssetTransport _transport;
    private readonly RangeQueue _queue;
    private readonly Lock _gate = new();
    private Task<Head>? _head;

    public RangePackBytes(IAssetTransport transport, WebUrl url, RangeBatching? batching = null)
    {
        _transport = transport;
        Url = url;
        _queue = new RangeQueue(FetchBatchAsync, batching);
    }

    /// <summary>Where the pack is.</summary>
    public WebUrl Url { get; }

    public async Task<long> LengthAsync() => (await HeadOfAsync().ConfigureAwait(false)).Total;

    public Task<byte[]> ReadAsync(long at, long bytes) => ReadAsync(at, bytes, RangePriority.Now);

    public async Task<byte[]> ReadAsync(long at, long bytes, RangePriority priority)
    {
        if (at < 0 || bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), $"Cannot read {bytes} bytes at {at} from a pack.");
        }

        var head = await HeadOfAsync().ConfigureAwait(false);
        // The sum wraps, and a wrapped one is refused here.
        var end = unchecked(at + bytes);
        if (end < at || end > head.Total)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes), $"Cannot read {bytes} bytes at {at} from a pack of {head.Total}.");
        }

        if (bytes == 0)
        {
            return [];
        }

        return end <= head.Start.Length
            ? head.Start.AsSpan((int)at, (int)bytes).ToArray()
            : await _queue.WantAsync(at, bytes, priority).ConfigureAwait(false);
    }

    private async Task<byte[]> FetchBatchAsync(long at, long bytes)
    {
        var head = await HeadOfAsync().ConfigureAwait(false);
        return (await FetchAsync(at, bytes, head.Total).ConfigureAwait(false)).Bytes;
    }

    // The first request's answer, asked for once and shared. A failure is not remembered, so the next call
    // asks again.
    private async Task<Head> HeadOfAsync()
    {
        Task<Head> head;
        lock (_gate)
        {
            head = _head ??= FetchHeadAsync();
        }

        try
        {
            return await head.ConfigureAwait(false);
        }
        catch (Exception)
        {
            lock (_gate)
            {
                // Only its own failure: a later head may already be on its way.
                if (ReferenceEquals(_head, head))
                {
                    _head = null;
                }
            }

            throw;
        }
    }

    private async Task<Head> FetchHeadAsync()
    {
        // The task is made under the gate, so the request must not start before the gate is let go.
        await Task.Yield();
        var part = await FetchAsync(0, PackHeader.Size, known: null).ConfigureAwait(false);
        return part.Total is { } total
            ? new Head(total, part.Bytes)
            : throw new PackRangeFailedException(
                Url,
                "the server did not say how long it is. From another origin it has to list Content-Range in Access-Control-Expose-Headers");
    }

    // Asks for bytes bytes at at and checks the answer is that range. known is how long the pack was said to
    // be by an earlier answer. A later answer that says otherwise is a pack that was replaced on the server
    // part way through a read, and its blobs are not the directory's.
    private async Task<Part> FetchAsync(long at, long bytes, long? known)
    {
        var reply = await _transport.SendAsync(new FetchRequest(Url, from: at, through: at + bytes - 1)).ConfigureAwait(false);
        if (!IsTheRange(reply, at, bytes))
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw new PackRangeFailedException(Url, WhyNot(reply, at));
        }

        var total = reply.Length ?? known;
        if (known is not null && total != known)
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw new PackRangeFailedException(Url, $"the pack changed from {known} to {total} bytes while it was being read");
        }

        var got = await ReadBodyAsync(reply, bytes).ConfigureAwait(false);
        var wanted = total is { } whole ? Math.Clamp(whole - at, 0, bytes) : (long?)null;
        return wanted is { } expected && got.Length != expected
            ? throw new PackRangeFailedException(Url, $"the server sent {got.Length} bytes where {expected} were asked for")
            : new Part(got, total);
    }

    private async Task<byte[]> ReadBodyAsync(FetchReply reply, long bytes)
    {
        using var body = new MemoryStream();
        await foreach (var chunk in reply.Body.ConfigureAwait(false))
        {
            body.Write(chunk);
            if (body.Length > bytes)
            {
                throw new PackRangeFailedException(Url, "the server sent more than was asked for");
            }
        }

        return body.ToArray();
    }

    // Whether reply is the bytes asked for, and not the file or an error. A 200 is the whole file, which is
    // the range asked only when the range was the whole file. A server may answer that either way.
    private static bool IsTheRange(FetchReply reply, long at, long bytes) =>
        reply.Status == 206 ? reply.From == at : reply.Status == 200 && at == 0 && reply.Length == bytes;

    private static string WhyNot(FetchReply reply, long at) => reply.Status switch
    {
        200 => "the server ignored the range and sent the whole file",
        206 => $"the server sent the bytes from {reply.From} and not from {at}",
        _ => $"the server answered {reply.Status}",
    };

    // Start is the bytes the first request returned, kept so that the header is not asked for twice.
    private sealed record Head(long Total, byte[] Start);

    private sealed record Part(byte[] Bytes, long? Total);
}
