using System.Text;
using System.Text.RegularExpressions;
using Orblit.Mesh;
using static Orblit.Asset.CharClasses;

namespace Orblit.Asset;

/// <summary>A surface source as lines with comments blanked and every <c>#include</c> expanded.</summary>
internal static partial class SurfaceLines
{
    /// <summary>
    /// The longest chain of includes. Deep enough for any real layout and short enough that a cycle the check
    /// below missed would still stop.
    /// </summary>
    internal const int MaxIncludeDepth = 25;

    [GeneratedRegex($"^{Space}*#{Space}*include{Space}+\"([^\"]*)\"{Space}*\\z")]
    private static partial Regex IncludeLine();

    /// <summary>
    /// The source as lines with comments blanked and every <c>#include</c> replaced by the lines it names, in
    /// order. A comment becomes spaces rather than disappearing so that the columns and the line count of the
    /// author's text stay what the author sees. <paramref name="read"/> gives the text of a project path, or null
    /// when there is no such file. <paramref name="seen"/> collects the paths that were included.
    /// </summary>
    internal static List<SurfaceLine> Expand(
        string path, string text, Func<string, string?> read, OrderedSet<string>? seen = null) =>
        Splice(path, text, new Expansion(read, seen), []);

    /// <summary>
    /// The project paths the <c>#include</c> lines of <paramref name="text"/> name, in order. For a caller that
    /// has to read them before it can translate.
    /// </summary>
    internal static List<string> IncludePaths(string path, string text)
    {
        var lines = BlankComments(text).Split('\n');
        var paths = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = IncludeLine().Match(lines[i]);
            if (match.Success)
            {
                paths.Add(Resolve(path, match.Groups[1].Value, new SurfaceOrigin(path, i + 1)));
            }
        }

        return paths;
    }

    private static List<SurfaceLine> Splice(string path, string text, Expansion expansion, List<string> includes)
    {
        var output = new List<SurfaceLine>();
        var lines = BlankComments(text).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var at = new SurfaceOrigin(path, i + 1);
            var match = IncludeLine().Match(lines[i]);
            if (!match.Success)
            {
                output.Add(new SurfaceLine(at, lines[i]));
                continue;
            }

            var target = Resolve(path, match.Groups[1].Value, at);
            List<string> chain = [.. includes, path];
            CheckChain(chain, target, at);
            expansion.Seen?.Add(target);
            var body = expansion.Read(target) ?? throw new SurfaceException(at, $"no file `{target}` to include");
            output.AddRange(Splice(target, body, expansion, chain));
        }

        return output;
    }

    private static void CheckChain(List<string> chain, string target, SurfaceOrigin at)
    {
        if (chain.Contains(target))
        {
            throw new SurfaceException(at, $"include cycle: {string.Join(" includes ", chain.Append(target))}");
        }

        if (chain.Count >= MaxIncludeDepth)
        {
            throw new SurfaceException(at, $"includes nest more than {MaxIncludeDepth} deep");
        }
    }

    // A leading slash is the assets folder, where asset ids start. Anything else is beside the file that includes it.
    private static string Resolve(string includer, string named, SurfaceOrigin at)
    {
        if (!named.EndsWith(".oshaderinc", StringComparison.Ordinal))
        {
            throw new SurfaceException(at, "only .oshaderinc files can be included");
        }

        var parts = named.StartsWith('/') ? [] : includer.Split('/')[..^1].ToList();
        foreach (var part in named.Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    throw new SurfaceException(at, $"`{named}` leaves the assets folder");
                }

                parts.RemoveAt(parts.Count - 1);
            }
            else if (part.Length > 0 && part != ".")
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    private static string BlankComments(string text)
    {
        var output = new StringBuilder(text.Length);
        var block = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (block && At(text, i, "*/"))
            {
                block = false;
                output.Append("  ");
                i++;
            }
            else if (block)
            {
                output.Append(text[i] == '\n' ? '\n' : ' ');
            }
            else if (At(text, i, "/*"))
            {
                block = true;
                output.Append("  ");
                i++;
            }
            else if (At(text, i, "//"))
            {
                i = SkipLine(text, i, output);
            }
            else
            {
                output.Append(text[i]);
            }
        }

        return output.ToString();
    }

    private static bool At(string text, int index, string pair) =>
        string.CompareOrdinal(text, index, pair, 0, pair.Length) == 0;

    // Blanks to the end of the line and returns the index of its last character.
    private static int SkipLine(string text, int from, StringBuilder output)
    {
        var i = from;
        while (i < text.Length && text[i] != '\n')
        {
            output.Append(' ');
            i++;
        }

        if (i < text.Length)
        {
            output.Append('\n');
        }

        return i;
    }

    private sealed record Expansion(Func<string, string?> Read, OrderedSet<string>? Seen);
}
