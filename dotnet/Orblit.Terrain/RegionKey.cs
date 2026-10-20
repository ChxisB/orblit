using System.Globalization;
using System.Text.RegularExpressions;

namespace Orblit.Terrain;

/// <summary>
/// Which square of the world a region is: the <c>X</c>th along and the <c>Z</c>th across,
/// counting in regions. Region (0, 0) starts at the origin and runs towards +x and +z; region
/// (-1, 0) is the one just short of it.
/// </summary>
public readonly partial record struct RegionKey(long X, long Z)
{
    /// <summary>What a region's file name ends in.</summary>
    public const string Extension = ".oregion";

    /// <summary>The name this region is written under, beside the terrain's own file.</summary>
    public string FileName => FormattableString.Invariant($"x{X}_z{Z}{Extension}");

    /// <summary>
    /// The region holding texel (<paramref name="i"/>, <paramref name="j"/>) of a terrain whose
    /// regions are <paramref name="size"/> texels across.
    /// </summary>
    public static RegionKey Containing(long i, long j, long size) =>
        new(TerrainMath.FloorDiv(i, size), TerrainMath.FloorDiv(j, size));

    /// <summary>
    /// The region a file is named for, or null when the name is not one a region is written
    /// under.
    /// </summary>
    public static RegionKey? FromFileName(string name)
    {
        var match = FileNamePattern().Match(name);
        if (!match.Success)
        {
            return null;
        }

        return new RegionKey(Parse(match.Groups[1].Value), Parse(match.Groups[2].Value));
    }

    public override string ToString() => FormattableString.Invariant($"RegionKey({X}, {Z})");

    // Dart's int.parse throws a FormatException for digits past 64 bits where long.Parse
    // throws an OverflowException.
    private static long Parse(string digits) =>
        long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"Not a 64-bit number: {digits}");

    // \z and not $, which in .NET also matches before a final newline.
    [GeneratedRegex(@"^x(-?[0-9]+)_z(-?[0-9]+)\.oregion\z")]
    private static partial Regex FileNamePattern();
}
