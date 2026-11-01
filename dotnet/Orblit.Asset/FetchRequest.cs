namespace Orblit.Asset;

/// <summary>
/// One request for an asset's bytes. Deliberately smaller than HTTP. Only four things vary between one
/// asset fetch and another, and a transport that took arbitrary headers and methods would be a transport
/// that could be asked to do something this package has no way to check.
/// </summary>
public sealed class FetchRequest
{
    public FetchRequest(WebUrl url, string? ifNoneMatch = null, long? from = null, long? through = null)
    {
        if (from < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(from), "from is a byte offset and cannot be negative");
        }

        if (through is not null && (from is null || through < from))
        {
            throw new ArgumentException(
                "through needs a from, and is the last byte wanted so it cannot come before it", nameof(through));
        }

        Url = url;
        IfNoneMatch = ifNoneMatch;
        From = from;
        Through = through;
    }

    public WebUrl Url { get; }

    /// <summary>
    /// The ETag of the copy already held, if there is one. The server answers 304 when the copy is still
    /// current, which costs a round trip and no body. That is the difference between a cold start and a warm
    /// one on a connection where the bytes are the expensive part.
    /// </summary>
    public string? IfNoneMatch { get; }

    /// <summary>
    /// The byte to resume from, for a download that stopped partway, or the first byte of a part of a file.
    /// </summary>
    public long? From { get; }

    /// <summary>
    /// The last byte wanted, counted as HTTP counts it: this one is included. Null asks for everything from
    /// <see cref="From"/> on, which is what a resume wants. A reader that takes a part of a large file, such
    /// as one asset out of a pack, names both ends so that the server stops sending there.
    /// </summary>
    public long? Through { get; }

    public override string ToString()
    {
        var parts = new List<string> { Url.ToString() };
        if (IfNoneMatch is not null)
        {
            parts.Add($"if-none-match: {IfNoneMatch}");
        }

        if (From is not null)
        {
            parts.Add($"from: {From}");
        }

        if (Through is not null)
        {
            parts.Add($"through: {Through}");
        }

        return string.Join(", ", parts);
    }
}
