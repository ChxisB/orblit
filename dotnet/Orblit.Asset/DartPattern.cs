namespace Orblit.Asset;

/// <summary>
/// The classes a Dart pattern means by its short escapes. .NET reads <c>\s</c>, <c>\w</c>, <c>\d</c>, <c>.</c>
/// and <c>$</c> more widely, and a pattern built from these reads the same text as Dart does.
/// </summary>
internal static class DartPattern
{
    /// <summary><c>\s</c>: the space of a trim, less the next-line character.</summary>
    internal const string Space = "[\\t\\n\\v\\f\\r \\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000\\ufeff]";

    /// <summary><c>\w</c>, which is ASCII only.</summary>
    internal const string Word = "[A-Za-z0-9_]";

    /// <summary><c>\W</c>.</summary>
    internal const string NotWord = "[^A-Za-z0-9_]";

    /// <summary><c>\d</c>, which is ASCII only.</summary>
    internal const string Digit = "[0-9]";

    /// <summary><c>.</c>, which stops at any line terminator.</summary>
    internal const string Any = "[^\\n\\r\\u2028\\u2029]";

    /// <summary>Where <c>^</c> matches with the multi-line flag: the start, or after a line terminator.</summary>
    internal const string LineStart = "(?<![^\\n\\r\\u2028\\u2029])";

    /// <summary>Where <c>$</c> matches with the multi-line flag: the end, or before a line terminator.</summary>
    internal const string LineEnd = "(?![^\\n\\r\\u2028\\u2029])";
}
