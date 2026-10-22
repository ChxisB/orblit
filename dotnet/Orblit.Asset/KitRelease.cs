using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>
/// One published version of a kit: where its file is and what it must be. The constructor refuses a value
/// no page could show safely or no download should trust, so a release that exists is one a reader can use.
/// </summary>
public sealed partial class KitRelease
{
    private const int MaxUrl = 300;
    private const int MaxNotes = 300;

    private static readonly HashSet<string> InsideNames = ["localhost", "local", "internal"];

    /// <summary>Throws a <see cref="FormatException"/> naming the field that is wrong.</summary>
    // F2: six parameters, all named at each call.
    public KitRelease(string version, string url, ContentHash hash, long bytes, string released, string notes = "")
    {
        KitVersion.Check("version", version);
        CheckUrl(url);
        if (bytes < 1 || bytes > KitRules.PackLimit)
        {
            throw new FormatException($"\"bytes\" is {bytes}. A kit file is 1 to {KitRules.PackLimit} bytes.");
        }

        KitText.CheckDate("released", released);
        if (notes.Length > MaxNotes || KitText.IsUnsafe(notes))
        {
            throw new FormatException($"\"notes\" must be one line of up to {MaxNotes} characters with no control characters.");
        }

        Version = version;
        Url = url;
        Hash = hash;
        Bytes = bytes;
        Released = released;
        Notes = notes;
    }

    /// <summary>Three whole numbers, such as <c>1.2.0</c>.</summary>
    public string Version { get; }

    /// <summary>An https address of the kit file.</summary>
    public string Url { get; }

    /// <summary>The hash of the pack, which <c>orblit-kit pack</c> prints and <c>AssetPack.Hash</c> answers.</summary>
    public ContentHash Hash { get; }

    /// <summary>The size of the kit file.</summary>
    public long Bytes { get; }

    /// <summary>The day it came out, as <c>2026-10-19</c>.</summary>
    public string Released { get; }

    /// <summary>One line saying what changed. May be empty.</summary>
    public string Notes { get; }

    /// <summary>What <c>PackFolder.DownloadAsync</c> takes.</summary>
    public PackOffer Offer => new(WebUrl.Parse(Url), Hash);

    /// <summary>
    /// What <c>PackFolder.DownloadAsync</c> takes with <see cref="Offer"/>: the host the request goes to first,
    /// and a limit of the size the release gives. The transport follows a redirect to any host and the policy
    /// does not look at where it leads, so a release on a host that redirects, such as a GitHub release,
    /// works. The pack hash is what vouches for the bytes.
    /// </summary>
    public AssetOrigin Origin => new(WebUrl.Parse($"https://{WebUrl.Parse(Url).Host}"), new FetchPolicy(maxBytes: Bytes));

    // The end of the input only, so \z, where .NET's $ also matches before a last newline.
    [GeneratedRegex(@"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HashShape();

    [GeneratedRegex(@"\A[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex HostLabel();

    // The set holds U+FEFF and not U+0085, which .NET's \s does the other way about, so it is written out.
    [GeneratedRegex(@"[\\#@\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff]", RegexOptions.CultureInvariant)]
    private static partial Regex UrlTrouble();

    /// <summary>
    /// Reads one release. Keys it does not know are left alone, so the caller chooses whether a typo is an
    /// error.
    /// </summary>
    internal static KitRelease FromJson(KitJsonObject json)
    {
        var hash = json.Text("hash");
        if (!HashShape().IsMatch(hash))
        {
            throw json.Problem($"has a \"hash\" of \"{KitText.Show(hash)}\", not 64 hexadecimal digits");
        }

        return json.Read(() => new KitRelease(
            version: json.Text("version"),
            url: json.Text("url"),
            hash: ContentHash.Parse(hash),
            bytes: json.Integer("bytes"),
            released: json.Text("released"),
            notes: json.OptionalText("notes") ?? ""));
    }

    internal OrderedDictionary<string, object?> ToJson() =>
        new()
        {
            ["bytes"] = Bytes,
            ["hash"] = Hash.Hex,
            ["notes"] = Notes,
            ["released"] = Released,
            ["url"] = Url,
            ["version"] = Version,
        };

    /// <summary>
    /// Throws unless <paramref name="text"/> is an https address of a public host name. The builder fetches
    /// it from a CI runner, so an address, <c>localhost</c> or a port other than 443 is refused before
    /// anything is sent. A backslash, <c>#</c> or <c>@</c> is refused because parsers disagree about what
    /// follows it.
    /// </summary>
    /// <remarks>
    /// shortcut: a public name can still point at a private address in DNS, and a redirect can lead anywhere.
    /// The review of the pull request reads the url and the hash has to match what comes back. Resolve the
    /// name and refuse private addresses when a store takes entries from people nobody reviews.
    /// </remarks>
    private static void CheckUrl(string text)
    {
        var good = WebUrl.TryParse(text, out var url)
            && url.Scheme == "https"
            && url.Port is null
            && IsPublicName(url.Host)
            && text.Length <= MaxUrl
            && !UrlTrouble().IsMatch(text)
            && !KitText.IsUnsafe(text);
        if (!good)
        {
            throw new FormatException(
                $"The url \"{KitText.Show(text)}\" is not an https address of up to {MaxUrl} characters at a public host name. "
                + "It has no port, space, backslash, # or @.");
        }
    }

    private static bool IsPublicName(string host)
    {
        var labels = host.Split('.');

        // A last label that starts with a digit reads as part of an address, as in 127.1 or 0x7f.0.0.1.
        return labels.Length >= 2
            && labels.All(label => HostLabel().IsMatch(label))
            && labels[^1][0] is >= 'a' and <= 'z'
            && !InsideNames.Contains(labels[^1]);
    }
}
