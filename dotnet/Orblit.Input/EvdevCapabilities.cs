using System.Text.RegularExpressions;

namespace Orblit.Input;

/// <summary>What a device says it can report: event types, buttons and axes, as sysfs prints them.</summary>
public sealed partial class EvdevCapabilities(IReadOnlySet<int> events, IReadOnlySet<int> keys, IReadOnlySet<int> axes)
{
    public static EvdevCapabilities None { get; } = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public IReadOnlySet<int> Events { get; } = events;

    public IReadOnlySet<int> Keys { get; } = keys;

    public IReadOnlySet<int> Axes { get; } = axes;

    public bool IsGamepad => Events.Contains(Evdev.Key) && Keys.Contains(Evdev.BtnGamepad);

    public static EvdevCapabilities Parse(string ev, string key, string abs) =>
        new(ParseBitmap(ev), ParseBitmap(key), ParseBitmap(abs));

    /// <summary>
    /// Reads a sysfs bitmap: hexadecimal words separated by spaces, most significant first, sixty-four
    /// bits to a word on a 64-bit kernel. A word that is not hexadecimal, or is wider than sixteen
    /// digits, is skipped.
    /// </summary>
    public static IReadOnlySet<int> ParseBitmap(string line)
    {
        var bits = new HashSet<int>();
        var words = Spaces().Split(line.Trim()).Where(word => word.Length > 0).Reverse().ToList();
        for (var w = 0; w < words.Count; w++)
        {
            AddWord(bits, w, words[w]);
        }

        return bits;
    }

    // Read as two 32-bit halves, so a word with its top bit set keeps that bit.
    private static void AddWord(HashSet<int> bits, int word, string text)
    {
        if (text.Length > 16)
        {
            return;
        }

        var at = text.Length > 8 ? text.Length - 8 : 0;
        var low = Hex(text[at..]);
        var high = at == 0 ? 0 : Hex(text[..at]);
        if (low is null || high is null)
        {
            return;
        }

        for (var bit = 0; bit < 32; bit++)
        {
            if ((low.Value & (1L << bit)) != 0)
            {
                bits.Add((word * 64) + bit);
            }

            if ((high.Value & (1L << bit)) != 0)
            {
                bits.Add((word * 64) + 32 + bit);
            }
        }
    }

    /// <summary>A hexadecimal number with an optional sign, or null.</summary>
    private static long? Hex(string text)
    {
        var match = HexShape().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = Convert.ToInt64(match.Groups[2].Value, 16);
        return match.Groups[1].Value == "-" ? -value : value;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^([+-]?)([0-9a-fA-F]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex HexShape();
}
