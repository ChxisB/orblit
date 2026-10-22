using System.Globalization;

namespace Orblit.Save;

/// <summary>How a time is written and read at the edges of the history tools.</summary>
internal static class Times
{
    // A restore writes saves, so a time is read strictly. Only these forms are
    // accepted, because a form with no zone would mean local time.
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd HH:mm:ssK",
        "yyyy-MM-dd HH:mm:ss.FFFFFFFK",
    ];

    /// <summary>ISO 8601 in UTC with milliseconds, as <c>list</c> prints it.</summary>
    public static string Iso(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>The form the save format writes for a UTC time.</summary>
    public static string Plain(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>Reads <paramref name="text"/> as a time, or throws <see cref="FormatException"/>.</summary>
    public static DateTimeOffset Parse(string text) =>
        DateTimeOffset.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var at)
            ? at
            : throw new FormatException("Invalid date format");
}
