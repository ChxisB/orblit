namespace Orblit.Asset;

/// <summary>
/// What was learned last time a URL was fetched. Small on purpose. The bytes themselves live in an
/// <see cref="IContentStore"/>, filed under their hash, and this is only the note that says which hash a URL
/// currently is and what to say to the server to find out whether that is still true. Keeping them apart is
/// what lets two URLs that serve the same bytes share one copy, and what lets the byte cache be emptied by
/// the system without these notes becoming lies. A record whose bytes have gone is a miss, not a wrong answer.
/// </summary>
public sealed class FetchRecord : IEquatable<FetchRecord>
{
    public FetchRecord(ContentHash hash, long bytes, string? etag = null, DateTimeOffset? fetched = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        Hash = hash;
        Bytes = bytes;
        ETag = etag;
        Fetched = fetched;
    }

    public ContentHash Hash { get; }

    /// <summary>How many bytes, for showing progress before any have arrived.</summary>
    public long Bytes { get; }

    /// <summary>
    /// What the server called this version, to hand back as <c>If-None-Match</c>. Null when the server did not
    /// say. Without one, a revalidation is a download, which is why a server not sending ETags costs a project
    /// more than it looks like it should.
    /// </summary>
    public string? ETag { get; }

    /// <summary>When it was last fetched or confirmed current.</summary>
    public DateTimeOffset? Fetched { get; }

    public FetchRecord ConfirmedAt(DateTimeOffset when) => new(Hash, Bytes, ETag, when);

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["hash"] = Hash.Hex, ["bytes"] = Bytes };
        if (ETag is not null)
        {
            json["etag"] = ETag;
        }

        if (Fetched is { } at)
        {
            json["fetched"] = TextRules.Iso(at);
        }

        return json;
    }

    /// <summary>
    /// Reads back what <see cref="ToJson"/> writes, or null for anything that is not one. Null rather than
    /// throwing: this is a cache, read at startup, and one corrupt line in it should cost that one entry
    /// rather than every asset the app has ever downloaded.
    /// </summary>
    public static FetchRecord? FromJson(object? json)
    {
        if (json is not IReadOnlyDictionary<string, object?> map
            || map.GetValueOrDefault("hash") is not string text
            || map.GetValueOrDefault("bytes") is not long bytes
            || bytes < 0)
        {
            return null;
        }

        ContentHash hash;
        try
        {
            hash = ContentHash.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }

        var fetched = map.GetValueOrDefault("fetched") is string when && TextRules.TryParseTime(when, out var at) ? at : (DateTimeOffset?)null;
        return new FetchRecord(hash, bytes, map.GetValueOrDefault("etag") as string, fetched);
    }

    // Equal when they name the same bytes, which is what a cache cares about. When it was last looked at
    // is not part of that.
    public bool Equals(FetchRecord? other) => other is not null && other.Hash == Hash && other.Bytes == Bytes && other.ETag == ETag;

    public override bool Equals(object? obj) => Equals(obj as FetchRecord);

    public override int GetHashCode() => HashCode.Combine(Hash, Bytes, ETag);
}
