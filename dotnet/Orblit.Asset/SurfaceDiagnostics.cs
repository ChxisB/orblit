using System.Globalization;
using System.Text.RegularExpressions;
using static Orblit.Asset.DartPattern;

namespace Orblit.Asset;

/// <summary>What matc said about a translated surface, in the author's own lines.</summary>
internal static partial class SurfaceDiagnostics
{
    [GeneratedRegex($"{LineStart}ERROR: {Digit}+:({Digit}+): ({Any}*){LineEnd}")]
    private static partial Regex MatcError();

    /// <summary>
    /// What matc said, with each line number replaced by the file and line the author wrote. A message matc repeats
    /// for one line is told once. Empty when matc's text names no line.
    /// </summary>
    internal static List<string> Of(string matcOutput, SurfaceTranslation translation) => MatcError()
        .Matches(matcOutput)
        .Where(match => !match.Groups[2].Value.StartsWith("'' : compilation terminated", StringComparison.Ordinal))
        .Select(match => $"{translation.OriginOf(long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))}: {match.Groups[2].Value}")
        .Distinct()
        .ToList();
}
