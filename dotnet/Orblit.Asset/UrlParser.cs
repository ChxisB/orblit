using System.Text;
using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>
/// Reads a URL by the rules of RFC 3986, because the fetch rules decide on host, path and
/// scheme and two readers that differ on those are a way round them. Written from the RFC, not from
/// <see cref="System.Uri"/>.
/// </summary>
internal static partial class UrlParser
{
    private const string SubDelims = "!$&'()*+,;=";
    private const string PathExtra = SubDelims + ":@/";
    private const string QueryExtra = PathExtra + "?";
    private const string UserExtra = SubDelims + ":";

    // One percent-escape, one lone percent sign, or a run of anything else.
    [GeneratedRegex("%(?<hex>[0-9A-Fa-f]{2})|(?<bare>%)|(?<run>[^%]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Pieces();

    // An escape, kept as it is, or one upper-case letter.
    [GeneratedRegex("%[0-9A-F]{2}|[A-Z]", RegexOptions.CultureInvariant)]
    private static partial Regex EscapeOrUpper();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeShape();

    [GeneratedRegex("^(0|[1-9][0-9]{0,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Octet();

    [GeneratedRegex("^(?<name>[A-Za-z][A-Za-z0-9+.-]*):", RegexOptions.CultureInvariant)]
    private static partial Regex LooksLikeScheme();

    internal static UrlParts Parse(string text)
    {
        var (beforeFragment, fragment) = SplitAt(text, '#');
        var (beforeQuery, query) = SplitAt(beforeFragment, '?');
        var (scheme, afterScheme) = SplitScheme(beforeQuery.Replace('\\', '/'));
        var hasAuthority = afterScheme.StartsWith("//", StringComparison.Ordinal);
        var (authority, path) = hasAuthority ? SplitAuthority(afterScheme) : ("", afterScheme);
        var (userInfo, host, port) = hasAuthority ? ParseAuthority(authority) : ("", "", null);
        var tidy = NormalisePath(path);
        return new UrlParts(
            scheme,
            hasAuthority,
            userInfo,
            host,
            port,
            scheme.Length == 0 && !hasAuthority ? KeepFirstSegmentAPath(tidy) : tidy,
            query is null ? null : Normalise(query, QueryExtra),
            fragment is null ? null : Normalise(fragment, QueryExtra)).Normalised();
    }

    private static (string Authority, string Path) SplitAuthority(string afterScheme)
    {
        var end = afterScheme.IndexOf('/', 2);
        return end < 0 ? (afterScheme[2..], "") : (afterScheme[2..end], afterScheme[end..]);
    }

    // Removing dot segments can leave "a:b" at the front of a relative path, which would read back as a
    // scheme. Escaping the first colon keeps it a path.
    private static string KeepFirstSegmentAPath(string path) =>
        LooksLikeScheme().Replace(path, "${name}%3A", 1);

    internal static string NormalisePath(string path) => RemoveDotSegments(Normalise(path, PathExtra));

    internal static string RemoveDotSegments(string path)
    {
        var segments = path.Split('/');
        if (!segments.Any(IsDot))
        {
            return path;
        }

        var absolute = path.StartsWith('/');
        var output = new List<string>();
        foreach (var segment in segments.Skip(absolute ? 1 : 0))
        {
            if (segment == "..")
            {
                Climb(output, absolute);
            }
            else if (segment != ".")
            {
                output.Add(segment);
            }
        }

        if (IsDot(segments[^1]))
        {
            output.Add("");
        }

        // A relative path never starts with a slash, which would make it an absolute one.
        if (!absolute && output[0].Length == 0)
        {
            output.Insert(0, ".");
        }

        return (absolute ? "/" : "") + string.Join('/', output);
    }

    private static bool IsDot(string segment) => segment is "." or "..";

    private static void Climb(List<string> output, bool absolute)
    {
        if (output.Count > 0 && output[^1] != "..")
        {
            output.RemoveAt(output.Count - 1);
        }
        else if (!absolute)
        {
            output.Add("..");
        }
    }

    private static (string Before, string? After) SplitAt(string text, char separator)
    {
        var at = text.IndexOf(separator);
        return at < 0 ? (text, null) : (text[..at], text[(at + 1)..]);
    }

    private static (string Scheme, string Remainder) SplitScheme(string text)
    {
        var stop = text.IndexOfAny([':', '/']);
        if (stop < 0 || text[stop] == '/')
        {
            return ("", text);
        }

        var scheme = text[..stop];
        return SchemeShape().IsMatch(scheme)
            ? (scheme.ToLowerInvariant(), text[(stop + 1)..])
            : throw Invalid(text, "the scheme is not one");
    }

    private static (string UserInfo, string Host, int? Port) ParseAuthority(string authority)
    {
        var at = authority.IndexOf('@');
        var hostPort = at < 0 ? authority : authority[(at + 1)..];
        if (hostPort.Contains('@'))
        {
            throw Invalid(authority, "it has more than one @");
        }

        var userInfo = at < 0 ? "" : Normalise(NoBrackets(authority[..at], authority), UserExtra);
        var (host, port) = hostPort.StartsWith('[') ? SplitBracketed(hostPort) : SplitHostPort(hostPort);
        return (userInfo, host, ParsePort(port, authority));
    }

    private static (string Host, string Port) SplitBracketed(string hostPort)
    {
        var end = hostPort.IndexOf(']');
        if (end < 0 || !IsIPv6(hostPort[1..end]))
        {
            throw Invalid(hostPort, "the bracketed host is not an IPv6 address");
        }

        var rest = hostPort[(end + 1)..];
        return rest.Length == 0 || rest[0] == ':'
            ? (hostPort[1..end].ToLowerInvariant(), rest.Length == 0 ? "" : rest[1..])
            : throw Invalid(hostPort, "something follows the bracketed host");
    }

    private static (string Host, string Port) SplitHostPort(string hostPort)
    {
        var colon = hostPort.IndexOf(':');
        var host = NoBrackets(colon < 0 ? hostPort : hostPort[..colon], hostPort);
        return (LowerOutsideEscapes(Normalise(host, SubDelims)), colon < 0 ? "" : hostPort[(colon + 1)..]);
    }

    private static string NoBrackets(string text, string whole) =>
        text.AsSpan().IndexOfAny('[', ']') < 0 ? text : throw Invalid(whole, "it has a bracket outside an IPv6 host");

    private static int? ParsePort(string port, string whole)
    {
        if (port.Length == 0)
        {
            return null;
        }

        // A sign, a space or a number past 65535 is not a port.
        return port.All(char.IsAsciiDigit) && int.TryParse(port, out var number) && number <= 65535
            ? number
            : throw Invalid(whole, "the port is not a number up to 65535");
    }

    // RFC 3986 IPv6address. A zone id is not allowed.
    private static bool IsIPv6(string text)
    {
        var halves = text.Split("::");
        if (halves.Length > 2)
        {
            return false;
        }

        var groups = halves.SelectMany(h => h.Length == 0 ? [] : h.Split(':')).ToList();
        var dotted = groups.Count > 0 && groups[^1].Contains('.');
        var width = groups.Count + (dotted ? 1 : 0);
        return (halves.Length == 2 ? width <= 7 : width == 8)
            && (!dotted || IsIPv4(groups[^1]))
            && groups.SkipLast(dotted ? 1 : 0).All(IsHexGroup);
    }

    private static bool IsHexGroup(string group) =>
        group.Length is >= 1 and <= 4 && group.All(char.IsAsciiHexDigit);

    private static bool IsIPv4(string text)
    {
        var octets = text.Split('.');
        return octets.Length == 4 && octets.All(o => Octet().IsMatch(o) && int.Parse(o) <= 255);
    }

    internal static string Normalise(string text, string extra) =>
        Pieces().Replace(text, piece =>
            piece.Groups["hex"].Success ? Unescape(piece.Groups["hex"].Value)
            : piece.Groups["bare"].Success ? "%25"
            : EscapeRun(piece.Value, extra));

    // An escape for a character that never needed one is that character. Any other keeps its escape.
    private static string Unescape(string hex)
    {
        var value = Convert.ToInt32(hex, 16);
        return value < 128 && IsUnreserved((char)value) ? ((char)value).ToString() : "%" + hex.ToUpperInvariant();
    }

    private static string EscapeRun(string run, string extra)
    {
        var output = new StringBuilder(run.Length);
        foreach (var rune in run.EnumerateRunes())
        {
            if (rune.IsAscii && (IsUnreserved((char)rune.Value) || extra.Contains((char)rune.Value)))
            {
                output.Append((char)rune.Value);
                continue;
            }

            foreach (var b in Encoding.UTF8.GetBytes(rune.ToString()))
            {
                output.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return output.ToString();
    }

    private static bool IsUnreserved(char c) => char.IsAsciiLetterOrDigit(c) || "-._~".Contains(c);

    // The hex digits of an escape stay upper case, so only the letters outside escapes are lowered.
    private static string LowerOutsideEscapes(string text) =>
        EscapeOrUpper().Replace(text, m => m.Length == 1 ? m.Value.ToLowerInvariant() : m.Value);

    private static FormatException Invalid(string text, string reason) =>
        new($"\"{text}\" is not a URL: {reason}.");
}
