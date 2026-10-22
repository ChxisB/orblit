namespace Orblit.Asset;

/// <summary>
/// What a project calls one of its assets: <c>models/robot.glb</c>. A logical name rather than a
/// file path, with one way to spell it. Anything that could be spelt two ways is refused rather
/// than tidied up, because an id is a key in a manifest, a cache and somebody's scene file.
/// </summary>
public sealed class AssetId : IEquatable<AssetId>
{
    private readonly string _path;

    private AssetId(string path) => _path = path;

    /// <summary>The last part of the path: <c>robot.glb</c>.</summary>
    public string Name => _path[(_path.LastIndexOf('/') + 1)..];

    /// <summary>Everything before the name, without a trailing slash. Empty at the root of the project.</summary>
    public string Directory
    {
        get
        {
            var slash = _path.LastIndexOf('/');
            return slash < 0 ? "" : _path[..slash];
        }
    }

    /// <summary>
    /// What kind of file the name says it is, lower-case and without the dot. Only the last dot
    /// counts, and a name that only starts with a dot has none.
    /// </summary>
    public string Extension
    {
        get
        {
            var name = Name;
            var dot = name.LastIndexOf('.');
            return dot <= 0 ? "" : TextRules.ToLower(name[(dot + 1)..]);
        }
    }

    /// <summary>Reads an id. Throws <see cref="AssetFormatException"/> saying what is wrong when it is not one.</summary>
    public static AssetId Parse(string text) =>
        ProblemWith(text) is var (message, offset) ? throw new AssetFormatException(message, text, offset) : new AssetId(text);

    /// <summary>Reads an id, or gives null when it is not one.</summary>
    public static AssetId? TryParse(string text) => ProblemWith(text) is null ? new AssetId(text) : null;

    /// <summary>
    /// The asset a glTF file, or anything like one, means by <paramref name="relative"/>: the
    /// URI is percent-decoded, joined against <see cref="Directory"/>, and <c>.</c> and <c>..</c>
    /// are collapsed. Null for anything that does not land on a valid id inside the project.
    /// </summary>
    public AssetId? Resolve(string relative)
    {
        string decoded;
        try
        {
            decoded = TextRules.DecodeUriComponent(relative);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        if (decoded.Length == 0 || decoded[0] == '/')
        {
            return null;
        }

        var segments = new List<string>();
        if (Directory.Length > 0)
        {
            segments.AddRange(Directory.Split('/'));
        }

        foreach (var segment in decoded.Split('/'))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment != "..")
            {
                segments.Add(segment);
            }
            else if (segments.Count == 0)
            {
                return null;
            }
            else
            {
                segments.RemoveAt(segments.Count - 1);
            }
        }

        return TryParse(string.Join('/', segments));
    }

    public bool Equals(AssetId? other) => other is not null && other._path == _path;

    public override bool Equals(object? obj) => Equals(obj as AssetId);

    public override int GetHashCode() => _path.GetHashCode(StringComparison.Ordinal);

    /// <summary>The id as it is written: <c>models/robot.glb</c>.</summary>
    public override string ToString() => _path;

    public static bool operator ==(AssetId? left, AssetId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(AssetId? left, AssetId? right) => !(left == right);

    private static (string Message, int? Offset)? ProblemWith(string text)
    {
        if (text.Length == 0)
        {
            return ("An empty string is not an asset id.", null);
        }

        var backslash = text.IndexOf('\\', StringComparison.Ordinal);
        if (backslash >= 0)
        {
            return (
                $"\"{text}\" is not an asset id, because it has a backslash in it. Asset ids separate folders with forward slashes on every platform.",
                backslash);
        }

        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            return (
                $"\"{text}\" is not an asset id, because it has a \":\" in it, which makes it a URL or a drive rather than a name inside the project.",
                colon);
        }

        return text[0] == '/'
            ? ($"\"{text}\" is not an asset id, because it starts with \"/\". An asset id is relative to the root of the project.", 0)
            : SegmentProblem(text);
    }

    private static (string Message, int? Offset)? SegmentProblem(string text)
    {
        var offset = 0;
        foreach (var segment in text.Split('/'))
        {
            var reason = segment switch
            {
                "" => "an empty folder name in it — a doubled slash, or one at the end",
                "." => "a \".\" folder in it, which is only another way of spelling the same name",
                ".." => "a \"..\" in it. An id names a file directly, not a route to it",
                _ => null,
            };
            if (reason is not null)
            {
                return ($"\"{text}\" is not an asset id, because it has {reason}.", offset);
            }

            offset += segment.Length + 1;
        }

        return null;
    }
}
