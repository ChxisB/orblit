using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>
/// The ordinary transport: <see cref="HttpClient"/>, which speaks to a server on every platform Orblit runs
/// on. Thin on purpose. Everything that decides what to ask for (the queue, the retries, the range on a
/// resume, the ETag on a revalidation) is in <see cref="AssetFetcher"/>, and this only turns one of those
/// decisions into a request and the answer back into a reply. A transport that made decisions would be a
/// second place they were made.
/// </summary>
public sealed partial class HttpTransport : IAssetTransport
{
    // Enough to follow a CDN's redirect to a region and no further. A chain longer than this is either a loop
    // or a server being used to bounce a request somewhere it should not go.
    private const int MaxRedirects = 5;

    private readonly HttpClient _client;
    private readonly bool _owned;

    /// <summary>
    /// A transport over <paramref name="client"/>, or over one of its own. Pass a client to share
    /// connections with the rest of an app, to add authentication, or to pin a certificate. A transport that
    /// made its own is a second connection pool to the same server. The redirect limit is the handler's, so
    /// it only holds for a client this made.
    /// </summary>
    public HttpTransport(HttpClient? client = null)
    {
        _owned = client is null;
        _client = client ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = MaxRedirects });
    }

    public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, request.Url.ToString());
        if (request.IfNoneMatch is not null)
        {
            message.Headers.TryAddWithoutValidation("If-None-Match", request.IfNoneMatch);
        }

        if (request.From is not null)
        {
            // Open-ended unless the request names an end. A resumed download wants the rest of the file, and
            // naming an end would mean having to trust the length the last attempt was told. A part of a file
            // names both.
            message.Headers.Range = new RangeHeaderValue(request.From, request.Through);
        }

        var reply = await SendMessageAsync(message, cancellation).ConfigureAwait(false);
        var status = (int)reply.StatusCode;
        if (status is 304 or 204)
        {
            var etag = HeaderOf(reply, "ETag");
            reply.Dispose();
            return FetchReply.Empty(status, etag);
        }

        return new FetchReply
        {
            Status = status,
            Body = new ReplyBody(reply),
            ETag = HeaderOf(reply, "ETag"),
            Length = WholeLength(reply),
            From = status == 206 ? StartOf(reply) ?? request.From : null,
        };
    }

    public void Dispose()
    {
        if (_owned)
        {
            _client.Dispose();
        }
    }

    // Named by this package so that the fetcher's message reads as a connection failure rather than as the
    // internals of a client.
    private async Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage message, CancellationToken cancellation)
    {
        try
        {
            return await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        }
        catch (HttpRequestException error)
        {
            throw new SocketFailureException(error.Message);
        }
    }

    // The first byte this reply holds, read out of the content-range. The server's word for it, not an echo of
    // what was asked. A server that answers a different range than the one sent is a reader that gets the
    // wrong bytes without knowing, and this is what lets the caller see it.
    private static long? StartOf(HttpResponseMessage reply) =>
        HeaderOf(reply, "Content-Range") is { } range && RangeStart().Match(range) is { Success: true } match
            ? ParseNumber(match.Groups[1].Value)
            : null;

    // How long the whole asset is, which is not the length of this reply. A resumed request answers 206 with
    // a content-length of what is left and a content-range of "bytes 900-999/1000". Reading the wrong one of
    // those makes a progress bar that fills up to the last tenth and then claims to be finished, and makes a
    // size limit that a large file walks through by being asked for in two halves.
    //
    // A 206 with no content-range gives null, not the content-length. A page on another origin cannot read
    // content-range unless the server lists it in Access-Control-Expose-Headers, and the content-length it
    // can read is the part's, which would pass for the whole file.
    private static long? WholeLength(HttpResponseMessage reply)
    {
        var range = HeaderOf(reply, "Content-Range");
        var slash = range?.LastIndexOf('/') ?? -1;
        if (range is not null && slash >= 0 && ParseNumber(range[(slash + 1)..].Trim()) is { } whole)
        {
            return whole;
        }

        return reply.StatusCode == System.Net.HttpStatusCode.PartialContent ? null : reply.Content.Headers.ContentLength;
    }

    // A header as the server wrote it, or null. Two of the same name are one value joined by a comma.
    private static string? HeaderOf(HttpResponseMessage reply, string name) =>
        reply.Headers.TryGetValues(name, out var values) || reply.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : null;

    // shortcut: a number in another form, such as 0x10, is read as no number, and then a content-range
    // total in hex falls back to the content-length. Upgrade when a server is found that writes one.
    private static long? ParseNumber(string text) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;

    [GeneratedRegex(@"^\s*bytes\s+([0-9]+)-", RegexOptions.CultureInvariant)]
    private static partial Regex RangeStart();

    // The body, read once. It is its own enumerator so that starting it and letting it
    // go without a read, which is how a refused reply is turned away, still closes the connection. An
    // iterator method would not: its cleanup runs only if it was started.
    private sealed class ReplyBody(HttpResponseMessage reply) : IAsyncEnumerable<byte[]>, IAsyncEnumerator<byte[]>
    {
        private const int ChunkSize = 16 * 1024;

        private readonly byte[] _buffer = new byte[ChunkSize];
        private CancellationToken _cancellation;
        private Stream? _stream;

        public byte[] Current { get; private set; } = [];

        public IAsyncEnumerator<byte[]> GetAsyncEnumerator(CancellationToken cancellation = default)
        {
            _cancellation = cancellation;
            return this;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            try
            {
                _stream ??= await reply.Content.ReadAsStreamAsync(_cancellation).ConfigureAwait(false);
                var read = await _stream.ReadAsync(_buffer, _cancellation).ConfigureAwait(false);
                Current = _buffer[..read];
                return read > 0;
            }
            catch (IOException error)
            {
                // A connection that dropped mid-body is the same failure as one that dropped before it.
                throw new SocketFailureException(error.Message);
            }
        }

        public ValueTask DisposeAsync()
        {
            reply.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
