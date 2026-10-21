using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>The shape of a kit store: which files it holds, and the most of each that a writer makes or a reader takes.</summary>
public static partial class KitStoreRules
{
    /// <summary>The format of <c>index.json</c> and <c>releases.json</c> this code writes, and the newest it reads.</summary>
    public const int Format = 1;

    /// <summary>The most releases one kit lists, in an entry and in <c>releases.json</c>.</summary>
    public const int ReleaseLimit = 200;

    /// <summary>The largest entry file read.</summary>
    public const int EntryLimit = 256 * 1024;

    /// <summary>The most bytes of <c>index.json</c> a reader takes.</summary>
    public const int IndexLimit = 16 * 1024 * 1024;

    /// <summary>The most bytes of one <c>releases.json</c> a reader takes.</summary>
    public const int ReleasesLimit = 1024 * 1024;

    /// <summary>The most bytes of one preview image a store holds or a reader takes.</summary>
    public const int PreviewBytes = 1024 * 1024;

    [GeneratedRegex(@"\Apreview-[0-5]\.(png|jpg|jpeg|webp)\z", RegexOptions.CultureInvariant)]
    private static partial Regex PreviewName();

    /// <summary>Whether <paramref name="name"/> is a file name a store may give a preview image.</summary>
    public static bool IsPreviewName(string name) => PreviewName().IsMatch(name);
}
