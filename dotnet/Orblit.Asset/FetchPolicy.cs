namespace Orblit.Asset;

/// <summary>
/// What an app will fetch, and how much of it. Every field is a limit rather than a permission, and the
/// defaults are the cautious ones. A game that loads assets from a server is downloading somebody else's
/// bytes and handing them to a decoder, and for several of the formats that decoder is written in C. The
/// cheapest defence is to not fetch the thing at all, and the second cheapest is to stop reading it
/// before it has been given to anything that parses.
/// </summary>
public sealed class FetchPolicy
{
    public FetchPolicy(
        IEnumerable<string>? hosts = null,
        bool allowInsecure = false,
        long maxBytes = 256 << 20,
        long maxPixels = 64 << 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPixels);
        Hosts = new HashSet<string>(hosts ?? []);
        AllowInsecure = allowInsecure;
        MaxBytes = maxBytes;
        MaxPixels = maxPixels;
    }

    /// <summary>
    /// Hosts that may be fetched from besides the origin's own. Usually empty. An origin already allows the
    /// host its base URL names, so this is for the second place a project keeps things, such as textures on
    /// a CDN beside a manifest served from the app's own domain. Matched exactly and case-insensitively:
    /// <c>cdn.example.com</c> does not allow <c>evil.cdn.example.com</c>, because a suffix match is how an
    /// allow-list becomes an allow-anything.
    /// </summary>
    public IReadOnlySet<string> Hosts { get; }

    /// <summary>
    /// Whether <c>http:</c> is allowed as well as <c>https:</c>. Off. A texture arriving over plain HTTP can
    /// be replaced in flight by anyone between here and the server, and a texture is bytes fed to a decoder.
    /// Worth turning on for a local test server and nowhere else.
    /// </summary>
    public bool AllowInsecure { get; }

    /// <summary>
    /// The most bytes one asset may be. Checked against the length the server declares before the body is
    /// read, and again as the body arrives. A server that says one thing and sends another is exactly the
    /// case worth catching, and a server that declares no length at all is the case where only the second
    /// check exists.
    /// </summary>
    public long MaxBytes { get; }

    /// <summary>
    /// The most pixels a picture may have, across every mip level. Separate from <see cref="MaxBytes"/>
    /// because the two are not related: a 64,000 by 64,000 PNG of flat colour compresses to a few hundred
    /// kilobytes and decodes to sixteen gigabytes. A size limit stops a slow download. Only a dimension
    /// limit stops that.
    /// </summary>
    public long MaxPixels { get; }

    /// <summary>This policy with <paramref name="more"/> added to the hosts already allowed.</summary>
    public FetchPolicy Allowing(IEnumerable<string> more) => new(Hosts.Concat(more), AllowInsecure, MaxBytes, MaxPixels);
}
