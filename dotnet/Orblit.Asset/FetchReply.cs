namespace Orblit.Asset;

/// <summary>What a server said.</summary>
public sealed class FetchReply
{
    /// <summary>A reply with no body, for a 304 or a failure.</summary>
    public static FetchReply Empty(int status, string? etag = null) =>
        new() { Status = status, Body = NoChunks(), ETag = etag, Length = 0 };

    public required int Status { get; init; }

    /// <summary>
    /// The bytes, which may not have arrived yet. A stream of chunks rather than one array so that a fetch
    /// can be abandoned partway without waiting for the rest, and so that a length limit can stop one while
    /// it is still arriving rather than after. Ending the enumeration early is what cancels the request:
    /// every transport here closes the connection when nobody is reading any more.
    /// </summary>
    public required IAsyncEnumerable<byte[]> Body { get; init; }

    public string? ETag { get; init; }

    /// <summary>
    /// How many bytes the whole asset is, when the server says. The whole asset, not this reply: a resumed
    /// request answers 206 with the remaining bytes, and this is still the total, read out of the
    /// content-range. Callers checking a size limit want the total, and the two being different is exactly
    /// the mistake worth designing out.
    /// </summary>
    public long? Length { get; init; }

    /// <summary>The byte this reply's body starts at, for a resumed request.</summary>
    public long? From { get; init; }

    public bool IsNotModified => Status == 304;

    /// <summary>
    /// Whether asking again later might work. A 5xx, a 408 or a 429 is a server having a moment. A 404 or a
    /// 403 is an answer: retrying it is a slower way to fail, and on a metered connection an expensive one.
    /// </summary>
    public bool MayWorkLater => Status >= 500 || Status is 408 or 425 or 429;

    /// <summary>
    /// Lets go of a body nobody will read. Starting and disposing an enumerator is what closes the
    /// connection behind it, so a reply that is turned away does not hold a socket open.
    /// </summary>
    internal async Task DiscardAsync()
    {
        await using var enumerator = Body.GetAsyncEnumerator();
    }

    private static async IAsyncEnumerable<byte[]> NoChunks()
    {
        await Task.CompletedTask;
        yield break;
    }
}
