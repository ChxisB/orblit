using Orblit.Json;
using Orblit.Scene;

namespace Orblit.Test;

/// <summary>Text that a test keeps in a file and compares with, such as what a frame changed.</summary>
public static class Golden
{
    private const string UpdateVariable = "ORBLIT_UPDATE_GOLDENS";

    /// <summary>
    /// What changed from <paramref name="before"/> to <paramref name="after"/>, as text. A scene that
    /// did not change is <c>[]</c>. It is the diff the editor and the renderer apply, so a golden
    /// file of it is a scene's frame without pixels.
    /// </summary>
    public static string DescribeChange(SceneDocument before, SceneDocument after) =>
        $"{JsonText.Encode(SceneDiff.Between(before, after).ToJson(), "  ")}\n";

    /// <summary>
    /// <paramref name="actual"/> equals what the file at <paramref name="path"/> holds. A relative
    /// path is read from the folder the tests run in.
    /// </summary>
    /// <remarks>
    /// With <paramref name="update"/> true, or with <c>ORBLIT_UPDATE_GOLDENS=1</c> in the
    /// environment when it is left out, the file is written and the match passes. A file that
    /// changes is named on stderr.
    /// </remarks>
    public static void MatchesTextFile(string actual, string path, bool? update = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var held = File.Exists(path) ? ReadAsLf(path) : null;
        if (held == actual)
        {
            return;
        }

        if (!(update ?? Environment.GetEnvironmentVariable(UpdateVariable) == "1"))
        {
            throw new ExpectationException($"Expected the text in {path}, but {Mismatch(held, actual)}.");
        }

        // F5, on purpose: a second command to rewrite goldens would drift from the check.
        new FileInfo(path).Directory?.Create();
        File.WriteAllText(path, actual);
        Console.Error.WriteLine($"wrote {path}");
    }

    // A checkout may turn the file's line endings into CRLF.
    private static string ReadAsLf(string path) => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Mismatch(string? held, string actual)
    {
        if (held is null)
        {
            return $"there is no such file. Run with {UpdateVariable}=1 to write it";
        }

        var want = Lines(held);
        var got = Lines(actual);
        var line = Enumerable.Range(0, Math.Max(want.Length, got.Length))
            .FirstOrDefault(i => i >= want.Length || i >= got.Length || want[i] != got[i], -1);
        return line < 0
            ? "differs only in its line endings"
            : $"differs at line {line + 1}: the file has {Quoted(want, line)} and this has {Quoted(got, line)}";
    }

    private static string Quoted(string[] lines, int line) => line < lines.Length ? $"\"{lines[line]}\"" : "nothing";

    // Lines end at CRLF, CR or LF, and a last empty piece after a final break is not a line.
    private static string[] Lines(string text)
    {
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
