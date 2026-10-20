using System.Text.RegularExpressions;

namespace Orblit.Save;

internal static partial class SaveKey
{
    // \z and not $, because $ also matches before a final new line.
    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>
    /// Throws unless <paramref name="key"/> is a name a file store can use:
    /// letters, digits and <c>._-</c>, 128 at most, not starting with a dot. A
    /// key comes from a game, but it often starts as a player's name.
    /// </summary>
    internal static void Check(string key)
    {
        if (!Pattern().IsMatch(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "is not a save key");
        }
    }
}
