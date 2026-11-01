using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Where a project's assets are served from. Holds the base URL and the <see cref="Policy"/> that applies
/// to anything reached from it, and is the only thing in this package that turns an <see cref="AssetId"/>
/// into a URL. Keeping that in one place is what makes the rules checkable: every fetch goes through
/// <see cref="UrlOf"/> or <see cref="Beside"/>, and neither can produce a URL the policy would not allow.
/// </summary>
public sealed class AssetOrigin
{
    /// <summary>
    /// An origin serving assets under <paramref name="base"/>, which is treated as a directory whether or
    /// not it ends in a slash, since <c>https://example.com/assets</c> and <c>https://example.com/assets/</c>
    /// are the same intention and only one of them resolves the way anybody expects.
    /// </summary>
    public AssetOrigin(WebUrl @base, FetchPolicy? policy = null)
    {
        Policy = policy ?? new FetchPolicy();
        Base = @base.Path.EndsWith('/') ? @base : @base.WithPath(@base.Path + "/");
        Check(Base, Base.ToString());
    }

    /// <summary>The base, always ending in a slash.</summary>
    public WebUrl Base { get; }

    public FetchPolicy Policy { get; }

    /// <summary>
    /// An origin from a URL written as text, throwing <see cref="FetchRefusedException"/> when that is not a
    /// URL this would ever fetch from.
    /// </summary>
    public static AssetOrigin Parse(string @base, FetchPolicy? policy = null) =>
        WebUrl.TryParse(@base, out var url)
            ? new AssetOrigin(url, policy)
            : throw new FetchRefusedException(@base, "it is not a URL");

    /// <summary>
    /// Where <paramref name="id"/> is served from: the id's own path, escaped, under <see cref="Base"/>. An id
    /// cannot contain <c>..</c> or start with a slash, because <see cref="AssetId"/> refuses both, so this
    /// cannot leave the base by construction. The check is there for the day that stops being true.
    /// </summary>
    public WebUrl UrlOf(AssetId id) => Under(WebUrl.Parse(Escape(id.ToString())), id.ToString(), null);

    /// <summary>
    /// What a file served from <paramref name="from"/> means by the relative reference
    /// <paramref name="uri"/>. This is the glTF case: a model at
    /// <c>https://cdn.example/models/robot.gltf</c> naming <c>../textures/metal.png</c> in a buffer or image.
    /// The reference is resolved the way a browser would, and then held to the same rules as anything else.
    /// That is the point, because the reference comes out of a file somebody else wrote. Refuses anything that
    /// leaves <see cref="Base"/>, names another scheme, or reaches the local machine.
    /// <c>file:///etc/passwd</c> and <c>../../../../etc/passwd</c> are the same attempt written two ways.
    /// </summary>
    public WebUrl Beside(WebUrl from, string uri) =>
        WebUrl.TryParse(uri, out var reference)
            ? Under(reference, uri, from)
            : throw new FetchRefusedException(uri, "it is not a URL");

    /// <summary>
    /// Whether <paramref name="url"/> is one this origin would fetch, without throwing. For deciding between
    /// two sources rather than for guarding a fetch, which <see cref="UrlOf"/> and <see cref="Beside"/> do and
    /// say why.
    /// </summary>
    public bool Allows(WebUrl url)
    {
        try
        {
            RequireAllowed(url);
            return true;
        }
        catch (FetchRefusedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Throws <see cref="FetchRefusedException"/>, saying why, unless this origin would fetch
    /// <paramref name="url"/>. Holds it to the scheme and host rules and not to the base's folder. A pack is
    /// served from wherever the game keeps its downloads, which need not sit under the folder the loose
    /// assets do.
    /// </summary>
    public void RequireAllowed(WebUrl url) => Check(url, url.ToString());

    private WebUrl Under(WebUrl reference, string wrote, WebUrl? from)
    {
        var url = (from ?? Base).Resolve(reference);
        Check(url, wrote);

        // Inside the base, not merely on the same host. A CDN serves more than one project, and a reference
        // that climbs out of this project's folder into another one resolves perfectly and is still wrong.
        if (SameHost(url.Host, Base.Host) && !url.Path.StartsWith(Base.Path, StringComparison.Ordinal))
        {
            throw new FetchRefusedException(wrote, $"it resolves to {url.Path}, which is outside {Base.Path}");
        }

        return url;
    }

    private void Check(WebUrl url, string wrote)
    {
        if (url.Scheme != "https" && !(url.Scheme == "http" && Policy.AllowInsecure))
        {
            throw new FetchRefusedException(wrote, SchemeReason(url.Scheme));
        }

        var host = PlainText.ToLower(url.Host);
        if (host.Length == 0)
        {
            throw new FetchRefusedException(wrote, "it names no host");
        }

        // An origin always allows its own host. The policy's list is the extras. Written this way round so
        // the ordinary case needs no list at all, and an empty list means "only where my assets are" rather
        // than "anywhere".
        if (!SameHost(host, Base.Host) && !Policy.Hosts.Any(one => PlainText.ToLower(one) == host))
        {
            throw new FetchRefusedException(
                wrote,
                $"nothing allows the host \"{host}\". Add it to FetchPolicy.Hosts if it is meant to serve this project");
        }
    }

    // A host keeps the case of its percent escapes, so two spellings of one name compare after lower-casing.
    private static bool SameHost(string one, string other) => PlainText.ToLower(one) == PlainText.ToLower(other);

    private static string SchemeReason(string scheme) => scheme switch
    {
        "http" => "it is plain HTTP, and this policy does not allow that. Set AllowInsecure only for a server on this machine",
        "" => "it names no scheme, so it is not a URL an asset can be fetched from",
        _ => $"assets are fetched over HTTPS, and this is \"{scheme}:\"",
    };

    // Each segment on its own, so a ? or # in an id's name stays part of the name and the slashes still separate.
    private static string Escape(string path) => string.Join('/', path.Split('/').Select(TextRules.EncodeComponent));

    public override string ToString() => $"AssetOrigin({Base})";
}
