using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Orblit.Editor.App;

/// <summary>One error or warning from a build. Line is zero when the build named no place in a file.</summary>
internal sealed record ScriptProblem(string Path, int Line, int Column, bool IsError, string Text);

/// <summary>Builds a project's C# with dotnet and reads each error and warning the build prints.</summary>
internal static partial class ScriptChecker
{
    /// <summary>Builds the project and answers its problems. Throws <see cref="IOException"/> when there is no project to build or no dotnet to build it.</summary>
    public static async Task<IReadOnlyList<ScriptProblem>> Check(string project)
    {
        // Named by its path, because dotnet refuses a folder that holds two project files.
        var file = Directory.EnumerateFiles(project, "*.csproj").Order(StringComparer.Ordinal).FirstOrDefault()
            ?? throw new IOException("The project has no C# project file to check.");
        var build = new ProcessStartInfo("dotnet") { WorkingDirectory = project, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "build", Path.GetFullPath(file), "-nologo", "-clp:NoSummary", "-v:q" })
        {
            build.ArgumentList.Add(argument);
        }

        try
        {
            using var running = Process.Start(build)!;
            var output = running.StandardOutput.ReadToEndAsync();
            var errors = running.StandardError.ReadToEndAsync();
            await running.WaitForExitAsync();
            return Read(await output + await errors, project, running.ExitCode);
        }
        catch (Win32Exception error)
        {
            throw new IOException("dotnet was not found, so the C# cannot be checked.", error);
        }
    }

    /// <summary>
    /// The problems in a build's output, each once. A file under the project is named by its res:// path. A build
    /// that failed with no problem it could place still says so.
    /// </summary>
    // shortcut: dotnet prints real paths, so a project opened through a link such as /tmp lists full paths that do not open. Resolve the project's real path when one is opened that way.
    public static IReadOnlyList<ScriptProblem> Read(string output, string project, int exitCode)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project)) + Path.DirectorySeparatorChar;
        var problems = output.Split('\n').Select(line => Problem(line.Trim(), root)).OfType<ScriptProblem>().Distinct().ToList();
        if (exitCode != 0 && !problems.Any(problem => problem.IsError))
        {
            problems.Add(new ScriptProblem("", 0, 0, true, $"The build failed with code {exitCode}. Output from dotnet build shows why."));
        }

        return problems;
    }

    static ScriptProblem? Problem(string line, string root)
    {
        var match = Diagnostic().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var file = match.Groups["file"].Value;
        var path = file.StartsWith(root, StringComparison.Ordinal) ? FileTree.Root + file[root.Length..].Replace('\\', '/') : file;
        return new ScriptProblem(
            path,
            match.Groups["line"].Success ? int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture) : 0,
            match.Groups["column"].Success ? int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture) : 0,
            match.Groups["kind"].Value == "error",
            $"{match.Groups["code"].Value}: {match.Groups["text"].Value}");
    }

    // MSBuild's form: file(line,column): error CS1002: text [project]. A problem with no place has only the file or tool name.
    [GeneratedRegex(@"^(?<file>.+?)(\((?<line>\d+),(?<column>\d+)\))?\s*:\s*(?<kind>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<text>.*?)(\s+\[[^\]]+\])?$")]
    private static partial Regex Diagnostic();
}
