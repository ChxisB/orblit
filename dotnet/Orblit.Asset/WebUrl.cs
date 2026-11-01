using System.Diagnostics.CodeAnalysis;

namespace Orblit.Asset;

/// <summary>
/// An absolute or relative URL, read by the rules of RFC 3986: the scheme and host in lower
/// case, percent-escapes in one form, dot segments removed, a default port left out. Equal URLs have equal
/// text. <see cref="System.Uri"/> is not used for the fetch rules because it reads some addresses
/// differently, and a rule that reads a URL differently from the thing that fetches it can be walked round.
/// </summary>
public sealed class WebUrl : IEquatable<WebUrl>
{
    private readonly UrlParts _parts;
    private readonly string _text;

    private WebUrl(UrlParts parts)
    {
        _parts = parts.Normalised();
        _text = _parts.ToString();
    }

    /// <summary>Lower case, without the colon. Empty for a relative reference.</summary>
    public string Scheme => _parts.Scheme;

    public bool HasAuthority => _parts.HasAuthority;

    public string UserInfo => _parts.UserInfo;

    /// <summary>Lower case, and an IPv6 address without its brackets.</summary>
    public string Host => _parts.Host;

    /// <summary>Null when the text names none, or names the one the scheme uses anyway.</summary>
    public int? Port => _parts.Port;

    public string Path => _parts.Path;

    /// <summary>Null when there is no <c>?</c>. Empty when there is one with nothing after it.</summary>
    public string? Query => _parts.Query;

    /// <summary>Null when there is no <c>#</c>.</summary>
    public string? Fragment => _parts.Fragment;

    /// <summary>Throws <see cref="FormatException"/> when the text is not a URL.</summary>
    public static WebUrl Parse(string text) => new(UrlParser.Parse(text));

    public static bool TryParse(string text, [NotNullWhen(true)] out WebUrl? url)
    {
        try
        {
            url = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            url = null;
            return false;
        }
    }

    /// <summary>What <paramref name="reference"/> means from this URL, by RFC 3986 section 5.2.</summary>
    public WebUrl Resolve(WebUrl reference)
    {
        var target = reference._parts;
        if (target.Scheme.Length > 0)
        {
            return reference;
        }

        if (target.HasAuthority)
        {
            return new WebUrl(target with { Scheme = Scheme });
        }

        var keepsPath = target.Path.Length == 0;
        return new WebUrl(_parts with
        {
            Path = keepsPath ? Path : UrlParser.RemoveDotSegments(MergedPath(target.Path)),
            Query = keepsPath && target.Query is null ? Query : target.Query,
            Fragment = target.Fragment,
        });
    }

    /// <summary>This URL with its path replaced.</summary>
    internal WebUrl WithPath(string path) => new(_parts with { Path = UrlParser.NormalisePath(path) });

    private string MergedPath(string relative)
    {
        if (relative.StartsWith('/'))
        {
            return relative;
        }

        return HasAuthority && Path.Length == 0 ? "/" + relative : Path[..(Path.LastIndexOf('/') + 1)] + relative;
    }

    public bool Equals(WebUrl? other) => other is not null && _text == other._text;

    public override bool Equals(object? obj) => Equals(obj as WebUrl);

    public override int GetHashCode() => _text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => _text;
}
