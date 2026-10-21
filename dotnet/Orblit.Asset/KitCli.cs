using System.Text;
using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>What the kit command takes from the machine it runs on, so a test can give it a clock and a network.</summary>
/// <param name="Output">Where the results go.</param>
/// <param name="Error">Where the problems go.</param>
/// <param name="Clock">The time a release is dated with, in UTC.</param>
/// <param name="Transport">The network <c>index</c> downloads releases over. It is made when <c>index</c> runs.</param>
internal sealed record KitCliEnvironment(
    TextWriter Output, TextWriter Error, Func<DateTime> Clock, Func<IAssetTransport> Transport);

/// <summary>
/// The command line over kits: packs a folder as a kit, checks one, installs one into a project, lists a
/// kit for the store and builds the store index from a folder of entries.
/// </summary>
/// <remarks>
/// Exit codes: 0 when it worked, 1 when a kit or a folder was bad or an install would replace a file,
/// 2 when the arguments did not make sense. A build reads the code. The text is for a person.
/// </remarks>
internal static partial class KitCli
{
    private const string Usage = """
        Usage: orblit-kit <command>

          pack <folder> -o <file>
              Packs <folder> as a kit. The folder holds the files to give a project
              and a kit.json. A hidden file, such as .DS_Store, stops it.
          check <file>
              Reads the manifest, applies the kit rules and hashes every blob.
          extract <file> <project> [--overwrite]
              Writes the files of a kit into <project>. A file the project already
              has with other contents stops it, unless --overwrite is given.
          publish <folder> --url <url> [-o <file>] [--entries <folder>]
                  [--notes <text>]
              Packs <folder>, checks the file as CI will, and prints the entry that
              lists it for the store. <url> is where you will host the file. The
              file is named after the last part of <url>, or by -o. With --entries,
              the entry is written to <entries>/<publisher>/<slug>.json. Nothing is
              uploaded and no git command runs.
          index <entries> -o <folder>
              Downloads every release the entries in <entries> list, checks each
              one, and writes index.json and the kit pages into <folder>, which must
              not exist yet. Any bad release stops it and writes nothing.
        """;

    private const int Failed = 1;
    private const int BadUsage = 2;

    private static readonly HashSet<string> PublishOptions = ["--url", "-o", "--out", "--entries", "--notes"];

    // A file name a url can end in and a shell can take as it stands.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex PlainName();

    /// <summary>Runs the command and returns its exit code.</summary>
    public static async Task<int> RunAsync(string[] args, KitCliEnvironment environment)
    {
        if (args.Length == 0 || args[0] == "--help")
        {
            Line(environment.Output, Usage);
            return args.Length == 0 ? BadUsage : 0;
        }

        try
        {
            return await RunCommandAsync(args[0], args[1..], environment).ConfigureAwait(false);
        }
        catch (CliUsageException e)
        {
            Line(environment.Error, $"kit: {e.Message}");
            Line(environment.Error, Usage);
            return BadUsage;
        }
        catch (KitConflictsException e)
        {
            Line(environment.Error, $"kit: {e.Message}");
            Line(environment.Error, "kit: run again with --overwrite to replace them.");
            return Failed;
        }
        catch (Exception e) when (IsBadInput(e))
        {
            Line(environment.Error, $"kit: {e.Message}");
            return Failed;
        }
    }

    private static bool IsBadInput(Exception e) =>
        e is FormatException or ArgumentException or IOException or UnauthorizedAccessException;

    private static Task<int> RunCommandAsync(string command, string[] rest, KitCliEnvironment environment) =>
        command switch
        {
            "pack" => PackAsync(rest, environment),
            "check" => CheckAsync(Only(command, rest, 1)[0], environment),
            "extract" => ExtractAsync(rest, environment),
            "publish" => PublishAsync(rest, environment),
            "index" => IndexAsync(rest, environment),
            _ => throw new CliUsageException($"unknown command \"{command}\""),
        };

    private static void Line(TextWriter writer, string text) => writer.Write(text + "\n");

    private static string[] Only(string command, IEnumerable<string> arguments, int count)
    {
        var all = arguments.ToArray();
        return all.Length == count ? all : throw new CliUsageException($"{command} takes {count} arguments");
    }

    // A folder and the -o that goes with it, for the commands that write one.
    private static (string Folder, string Out) FolderAndOut(string command, string[] arguments)
    {
        string? folder = null;
        string? outPath = null;
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is "-o" or "--out")
            {
                outPath = ++i < arguments.Length ? arguments[i] : throw new CliUsageException("-o needs a value");
            }
            else
            {
                folder = folder is null ? arguments[i] : throw new CliUsageException("one folder at a time");
            }
        }

        return folder is not null && outPath is not null
            ? (folder, outPath)
            : throw new CliUsageException($"{command} needs a folder and -o");
    }

    private static async Task<int> PackAsync(string[] arguments, KitCliEnvironment environment)
    {
        var (folder, outPath) = FolderAndOut("pack", arguments);
        var summary = await KitPacking.PackAsync(folder, outPath).ConfigureAwait(false);
        Line(environment.Output, $"{summary.Assets - 1} files, {summary.Bytes} bytes\n{summary.Hash}  {outPath}");
        return 0;
    }

    // The folder and the options of publish, with --out spelt -o.
    private static (string Folder, Dictionary<string, string> Options) PublishArguments(string[] arguments)
    {
        string? folder = null;
        var options = new Dictionary<string, string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (PublishOptions.Contains(argument))
            {
                options[argument == "--out" ? "-o" : argument] =
                    ++i < arguments.Length ? arguments[i] : throw new CliUsageException($"{argument} needs a value");
            }
            else if (argument.StartsWith('-'))
            {
                throw new CliUsageException($"unknown option \"{argument}\"");
            }
            else
            {
                folder = folder is null ? argument : throw new CliUsageException("one folder at a time");
            }
        }

        return (folder ?? throw new CliUsageException("publish needs a folder"), options);
    }

    // The file publish writes: -o, or the last part of the url when it is a plain name.
    private static string PublishedFile(Dictionary<string, string> options, string url)
    {
        if (options.TryGetValue("-o", out var outPath))
        {
            return outPath;
        }

        var path = WebUrl.TryParse(url, out var parsed) ? parsed.Path : "";
        var name = path[(path.LastIndexOf('/') + 1)..];
        return PlainName().IsMatch(name)
            ? name
            : throw new CliUsageException("the url does not end in a file name, so give one with -o");
    }

    private static async Task<int> PublishAsync(string[] arguments, KitCliEnvironment environment)
    {
        var (folder, options) = PublishArguments(arguments);
        var url = options.GetValueOrDefault("--url")
            ?? throw new CliUsageException("publish needs --url, where the file will be");
        var file = PublishedFile(options, url);
        var entries = options.GetValueOrDefault("--entries");
        var published = await KitPacking.PublishAsync(new KitPublishRequest
        {
            Folder = folder,
            Url = url,
            OutPath = file,
            // The clock is read here, where the command is wired.
            Released = environment.Clock(),
            Notes = options.GetValueOrDefault("--notes", ""),
            EntriesPath = entries,
        }).ConfigureAwait(false);
        var entry = published.Entry;
        Line(
            environment.Output,
            $"Packed {file}: {published.Summary.Assets - 1} files, {published.Summary.Bytes} bytes\n"
            + $"{published.Summary.Hash}\n\n"
            + $"The entry for {entry.Publisher}/{entry.Slug}:\n"
            + Encoding.UTF8.GetString(entry.Encode()));
        environment.Output.Write(NextSteps(file, url, entries, published.EntryPath));
        return 0;
    }

    // What the person does after publish, which runs none of it.
    private static string NextSteps(string file, string url, string? entries, string? written)
    {
        var entry = written is null
            ? "Save the entry above as kits/<publisher>/<slug>.json in the "
                + "orblit-kits repository. If that file lists releases already, "
                + "add this release to them instead."
            : $"Wrote the entry to {written}.";
        return $"{entry}\n\n"
            + "Next, by hand:\n"
            + $"  1. Upload {file} so that it is served at {url}\n"
            + $"  2. Check the listing with orblit-kit index {entries ?? "kits"} -o <new folder>\n"
            + "  3. Commit the entry and open a pull request.\n";
    }

    private static async Task<int> IndexAsync(string[] arguments, KitCliEnvironment environment)
    {
        var (folder, outPath) = FolderAndOut("index", arguments);
        using var transport = environment.Transport();
        var built = await KitStoreBuilder.BuildAsync(folder, outPath, transport).ConfigureAwait(false);
        Line(environment.Output, $"{built.Kits} kits, {built.Releases} releases written to {outPath}");
        return 0;
    }

    private static async Task<Kit> OpenAsync(FilePackBytes source, string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"There is no such file: {path}", path);
        }

        return await Kit.OpenAsync(await AssetPack.OpenAsync(source).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static async Task<int> CheckAsync(string path, KitCliEnvironment environment)
    {
        await using var source = new FilePackBytes(path);
        var kit = await OpenAsync(source, path).ConfigureAwait(false);
        var problems = await kit.CheckAsync().ConfigureAwait(false);
        if (problems.Count > 0)
        {
            foreach (var line in problems)
            {
                Line(environment.Error, $"  {line}");
            }

            Line(environment.Error, $"kit: {problems.Count} problems.");
            return Failed;
        }

        var manifest = kit.Manifest;
        Line(
            environment.Output,
            $"{manifest.Name} {manifest.Version}, {kit.Files.Count} files, "
            + $"all intact{(manifest.ContainsScripts ? ", holds scripts" : "")}");
        return 0;
    }

    private static async Task<int> ExtractAsync(string[] arguments, KitCliEnvironment environment)
    {
        var overwrite = arguments.Contains("--overwrite");
        var paths = Only("extract", arguments.Where(argument => argument != "--overwrite"), 2);
        await using var source = new FilePackBytes(paths[0]);
        var kit = await OpenAsync(source, paths[0]).ConfigureAwait(false);
        var written = await KitExtraction.ExtractAsync(kit, paths[1], new KitExtractionOptions { Overwrite = overwrite })
            .ConfigureAwait(false);
        Line(environment.Output, $"{written.Count} files written to {paths[1]}");
        return 0;
    }
}
