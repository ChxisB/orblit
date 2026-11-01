using static Orblit.Asset.CliOutput;

namespace Orblit.Asset;

/// <summary>
/// The command line over packs: packs a cooked bundle into one <c>.opack</c> file, and looks inside one.
/// </summary>
/// <remarks>
/// Exit codes: 0 when it worked, 1 when a pack or bundle was bad, 2 when the arguments did not make sense.
/// A diff also exits 1 when a removed name is still used or a document could not be read. A build reads
/// the code. The text is for a person.
/// </remarks>
internal static class PackCli
{
    private static readonly string Usage = $"""
        Usage: orblit-pack <command>

          build <bundle> --target <name> -o <file> [--base <pack>]...
              Packs the cooked bundle in <bundle> for one target.
              The target is one of: {TargetNames}
              With --base, writes a patch against the packs already shipped. Name
              them in the order they are mounted, the whole pack first.
          list <file>
              Prints what a pack holds.
          check <file>
              Hashes every blob. Exits 1 when a byte has changed.
          diff <old> <new> [--base <pack>]...
              Lists the assets added, removed and changed by name, and the documents
              in <new> that still name an asset it dropped. A patch is read with the
              packs it was built on. Give those with --base, in any order. Exits 1
              when a dropped name is still used or a document cannot be read.
        """;

    private const int Failed = 1;
    private const int BadUsage = 2;

    private static string TargetNames => string.Join(", ", CookTargets.Names);

    /// <summary>Runs the command and returns its exit code.</summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] == "--help")
        {
            Line(output, Usage);
            return args.Length == 0 ? BadUsage : 0;
        }

        try
        {
            return await RunCommandAsync(args, output, error).ConfigureAwait(false);
        }
        catch (CliUsageException e)
        {
            Line(error, $"pack: {e.Message}");
            Line(error, Usage);
            return BadUsage;
        }
        catch (Exception e) when (IsBadInput(e))
        {
            Line(error, $"pack: {e.Message}");
            return Failed;
        }
    }

    private static bool IsBadInput(Exception e) =>
        e is FormatException or PackMountRefusedException or IOException or UnauthorizedAccessException;

    private static Task<int> RunCommandAsync(string[] args, TextWriter output, TextWriter error) =>
        args[0] switch
        {
            "build" => BuildAsync(args[1..], output),
            "list" => ListAsync(OnlyPath(args), output),
            "check" => CheckAsync(OnlyPath(args), output, error),
            "diff" => DiffAsync(args[1..], output, error),
            _ => throw new CliUsageException($"unknown command \"{args[0]}\""),
        };

    private static string OnlyPath(string[] args) =>
        args.Length == 2 ? args[1] : throw new CliUsageException($"{args[0]} takes one file");

    private static string Value(string[] arguments, int index) =>
        index + 1 < arguments.Length
            ? arguments[index + 1]
            : throw new CliUsageException($"{arguments[index]} needs a value");

    // The bundle, the output file, the target and the packs already shipped, as far as the arguments give them.
    private static (string? Bundle, string? Out, CookTarget? Target, List<string> Bases) BuildArguments(
        string[] arguments)
    {
        string? bundle = null;
        string? outPath = null;
        CookTarget? target = null;
        var bases = new List<string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--target":
                    target = CookTargets.Find(Value(arguments, i++))
                        ?? throw new CliUsageException($"that is not a target. The targets are {TargetNames}");
                    break;
                case "-o" or "--out":
                    outPath = Value(arguments, i++);
                    break;
                case "--base":
                    bases.Add(Value(arguments, i++));
                    break;
                default:
                    bundle = bundle is null ? arguments[i] : throw new CliUsageException("one bundle at a time");
                    break;
            }
        }

        return (bundle, outPath, target, bases);
    }

    private static async Task<int> BuildAsync(string[] arguments, TextWriter output)
    {
        var (bundle, outPath, target, bases) = BuildArguments(arguments);
        if (bundle is null || outPath is null || target is null)
        {
            throw new CliUsageException("build needs a bundle, --target and -o");
        }

        CheckTargetMatchesFolder(bundle, target);
        var summary = await WithPacksAsync(
            bases, packs => PackBundle.PackAsync(bundle, outPath, target, packs)).ConfigureAwait(false);
        var changes = bases.Count == 0
            ? ""
            : $", {summary.Added} new, {summary.Replaced} replaced, {summary.Removed} removed";
        Line(
            output,
            $"{summary.Assets} assets in {summary.Blobs} blobs, {summary.Bytes} bytes, "
            + $"{summary.SharedBytes} saved by sharing{changes}\n{summary.Hash}  {outPath}");
        return 0;
    }

    // A bundle folder is named for its target, so one that says otherwise is a mistake that would pack ASTC
    // textures as a macOS pack.
    private static void CheckTargetMatchesFolder(string bundle, CookTarget target)
    {
        var folder = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(bundle)));
        if (CookTargets.Find(folder) is { } named && named.Name != target.Name)
        {
            throw new CliUsageException($"\"{bundle}\" holds the {named.Name} bundle, and --target says {target.Name}");
        }
    }

    // Opens each of the paths, hands the packs to use and closes the files whatever happens.
    private static async Task<T> WithPacksAsync<T>(
        IEnumerable<string> paths, Func<IReadOnlyList<AssetPack>, Task<T>> use)
    {
        var sources = new List<FilePackBytes>();
        try
        {
            var packs = new List<AssetPack>();
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"There is no such file: {path}", path);
                }

                var source = new FilePackBytes(path);
                sources.Add(source);
                packs.Add(await AssetPack.OpenAsync(source).ConfigureAwait(false));
            }

            return await use(packs).ConfigureAwait(false);
        }
        finally
        {
            foreach (var source in sources)
            {
                await source.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static Task<int> ListAsync(string path, TextWriter output) =>
        WithPacksAsync([path], packs =>
        {
            var pack = packs[0];
            Line(output, $"target   {pack.Target}");
            Line(output, $"hash     {pack.Hash}");
            Line(output, $"base     {pack.Base?.ToString() ?? "none"}");
            Line(output, $"assets   {pack.Manifest.Entries.Count}");
            Line(output, $"removed  {pack.Removed.Count}");
            foreach (var (id, entry) in pack.Manifest.Entries.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            {
                Line(output, $"  {entry.Hash.Hex[..12]}  {entry.Bytes.ToString().PadLeft(10)}  {id}");
            }

            foreach (var id in pack.Removed)
            {
                Line(output, $"  removed  {id}");
            }

            return Task.FromResult(0);
        });

    private static Task<int> CheckAsync(string path, TextWriter output, TextWriter error) =>
        WithPacksAsync([path], async packs =>
        {
            var corrupt = await packs[0].FindCorruptAsync().ConfigureAwait(false);
            if (corrupt.Count == 0)
            {
                Line(output, $"{packs[0].Manifest.Entries.Count} assets, all intact");
                return 0;
            }

            foreach (var id in corrupt)
            {
                Line(error, $"  changed  {id}");
            }

            Line(error, $"pack: {corrupt.Count} assets do not match their hash.");
            return Failed;
        });

    private static async Task<int> DiffAsync(string[] arguments, TextWriter output, TextWriter error)
    {
        var paths = new List<string>();
        var bases = new List<string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == "--base")
            {
                bases.Add(Value(arguments, i++));
            }
            else
            {
                paths.Add(arguments[i]);
            }
        }

        if (paths.Count != 2)
        {
            throw new CliUsageException("diff takes an old pack and a new one");
        }

        // The packs a patch stands on are found among the bases and the two packs themselves, so a patch on
        // the old pack needs no flag.
        var diff = await WithPacksAsync(
            [.. paths, .. bases],
            packs => PackDiff.BetweenAsync(MountedPacks.WithBases(packs[0], packs), MountedPacks.WithBases(packs[1], packs)))
            .ConfigureAwait(false);
        PrintDiff(diff, output);
        if (!diff.IsBroken)
        {
            return 0;
        }

        Line(error, $"pack: missing names {diff.Dangling.Count}, unreadable documents {diff.Unreadable.Count}.");
        return Failed;
    }

    private static void PrintDiff(PackDiff diff, TextWriter output)
    {
        Section(output, "added", [.. diff.Added.Select(id => id.ToString())]);
        Section(output, "removed", [.. diff.Removed.Select(id => id.ToString())]);
        Section(output, "changed", [.. diff.Changed.Select(c => $"{c.Id}  {c.Before} to {c.After} bytes")]);
        Section(output, "dangling", [.. diff.Dangling.Select(d => $"{d.Document} names {d.Missing}")]);
        if (diff.Unreadable.Count > 0)
        {
            Section(output, "unreadable", [.. diff.Unreadable.Select(id => id.ToString())]);
        }
    }

    private static void Section(TextWriter output, string label, List<string> lines)
    {
        Line(output, $"{label.PadRight(11)}{lines.Count}");
        foreach (var line in lines)
        {
            Line(output, $"  {line}");
        }
    }
}
