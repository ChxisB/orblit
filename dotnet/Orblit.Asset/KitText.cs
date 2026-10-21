using System.Globalization;
using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>
/// The words of a kit, which is somebody else's file. Whatever a message echoes goes through
/// <see cref="Show"/>, so a control character never reaches a terminal raw.
/// </summary>
internal static partial class KitText
{
    // Characters that change how a line is read or drawn: control characters, the C1 range, zero width and
    // bidirectional marks, and the line separators.
    [GeneratedRegex(@"[\u0000-\u001f\u007f-\u009f\u200b-\u200f\u2028-\u202e\u2060-\u2064\u2066-\u206f\ufeff\ufff9-\ufffb]", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeText();

    // Dart's $ is the end of the input only, so \z, where .NET's $ also matches before a last newline.
    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    // Letters, digits and the few marks an SPDX id or expression uses.
    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9.+() -]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Licence();

    [GeneratedRegex(@"\A([0-9]{4})-([0-9]{2})-([0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Day();

    internal static bool IsLicence(string text) => Licence().IsMatch(text);

    internal static bool IsUnsafe(string text) => UnsafeText().IsMatch(text);

    /// <summary><paramref name="text"/> with each unsafe character written as <c>\uXXXX</c>, for a message.</summary>
    internal static string Show(string text) =>
        UnsafeText().Replace(text, match => $"\\u{(int)match.Value[0]:x4}");

    /// <summary>Throws a <see cref="FormatException"/> unless <paramref name="text"/> is a day that exists, written as <c>2026-10-19</c>.</summary>
    internal static void CheckDate(string field, string text)
    {
        if (!IsDay(text))
        {
            throw new FormatException($"\"{field}\" is \"{Show(text)}\". Write a day that exists, such as 2026-10-19.");
        }
    }

    private static bool IsDay(string text)
    {
        var parts = Day().Match(text);
        if (!parts.Success)
        {
            return false;
        }

        var year = int.Parse(parts.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(parts.Groups[2].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(parts.Groups[3].Value, CultureInfo.InvariantCulture);

        // Leap years repeat every 400 years, and DateTime has no year 0, which Dart reads.
        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2000 + (year % 400), month);
    }

    /// <summary>
    /// Throws a <see cref="FormatException"/> unless <paramref name="value"/> is lower case letters and digits
    /// in groups joined by single hyphens, up to <paramref name="longest"/> characters.
    /// </summary>
    internal static void CheckWord(string field, string value, int longest)
    {
        if (value.Length > longest || !Word().IsMatch(value))
        {
            throw new FormatException(
                $"\"{field}\" is \"{Show(value)}\". Use lower case letters and digits in groups joined by single hyphens, up to {longest} characters.");
        }
    }
}
