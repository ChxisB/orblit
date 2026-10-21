using static Orblit.Asset.CliOutput;

namespace Orblit.Asset;

/// <summary>
/// A project's assets in, one target's bundle out. Each target is written to <c>&lt;out&gt;/&lt;target&gt;/</c>,
/// with a <c>manifest.json</c> naming what every file is.
/// </summary>
/// <remarks>
/// Exit codes: 0 when everything cooked, 1 when an asset failed, 2 when the arguments did not make sense. A build
/// reads the code. The text is for a person.
/// </remarks>
internal static class CookCli
{
    private const int Failed = 1;
    private const int BadUsage = 2;

    private static readonly string Usage = $"""
        Usage: orblit-cook --target <name> [options]

          --target <name>     What to cook for. Repeatable.
                              One of: {TargetNames}
          --assets <dir>      Where the project's assets are (default: assets)
          --out <dir>         Where bundles are written (default: build/assets)
          --cache <dir>       Where cooked bytes are kept between runs
          --cache-limit <n>   Megabytes the cache may hold, or 0 for no limit
                              (default: 2048)
          --jobs <n>          How many importers may run at once (default: 4)
          --quiet             Print only the summary and anything that went wrong
          --verbose           Print every asset, including cached ones
          --help

        Each target is written to <out>/<target>/, with a manifest.json naming what
        every file is. A build copies one of those folders and nothing else, which is
        how an iOS build ends up carrying ASTC and no BC.
        """;

    private static readonly Dictionary<string, Func<Options, Options>> Switches = new()
    {
        ["--help"] = options => options with { Help = true },
        ["-h"] = options => options with { Help = true },
        ["--quiet"] = options => options with { Quiet = true },
        ["--verbose"] = options => options with { Verbose = true },
    };

    private static readonly Dictionary<string, Func<Options, string, TextWriter, Options?>> Valued = new()
    {
        ["--target"] = WithTarget,
        ["--assets"] = (options, value, _) => options with { AssetsPath = value },
        ["--out"] = (options, value, _) => options with { OutPath = value },
        ["--cache"] = (options, value, _) => options with { CachePath = value },
        ["--cache-limit"] = WithCacheLimit,
        ["--jobs"] = WithJobs,
    };

    private static string TargetNames => string.Join(", ", CookTargets.Names);

    /// <summary>Runs the command and returns its exit code.</summary>
    public static async Task<int> RunAsync(string[] arguments, TextWriter output, TextWriter error)
    {
        if (Parse(arguments, error) is not { } options)
        {
            return BadUsage;
        }

        if (options.Help)
        {
            Line(output, Usage);
            return 0;
        }

        var failures = 0;
        foreach (var target in options.Targets)
        {
            var project = ProjectFor(options, target);
            CookReport report;
            try
            {
                report = await project.RunAsync(result => Report(result, options, output, error)).ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                // shortcut: .NET's own text, which names the parameter and is longer than Dart's. Upgrade when a
                // build has to match the Dart line.
                Line(error, $"cook: {e.Message}");
                return BadUsage;
            }

            failures += report.Count(CookStatus.Failed);
            if (!options.Quiet)
            {
                Line(output, $"{target.Name}: {report.Summary} -> {project.BundlePath}");
            }
        }

        // A failure is worth repeating at the end. On a project of any size the line that said what broke has
        // scrolled away by the time the cook finishes, and a build log is read from the bottom.
        if (failures > 0)
        {
            Line(
                error,
                $"\ncook: {failures} asset{(failures == 1 ? "" : "s")} failed. "
                + $"The bundle was written without {(failures == 1 ? "it" : "them")}.");
            return Failed;
        }

        return 0;
    }

    internal static CookProject ProjectFor(Options options, CookTarget target) => new(
        assetsPath: options.AssetsPath,
        outPath: options.OutPath,
        target: target,
        cachePath: options.CachePath,
        limitBytes: options.CacheLimitBytes,
        concurrency: options.Jobs);

    internal static void Report(CookResult result, Options options, TextWriter output, TextWriter error)
    {
        switch (result.Status)
        {
            case CookStatus.Failed:
                Line(error, $"  failed  {result.Id}: {result.Error?.Message}");
                break;
            case CookStatus.Cooked when !options.Quiet:
                Line(output, $"  cooked  {result.Id}");
                foreach (var note in result.Notes)
                {
                    Line(output, $"          {note}");
                }

                break;
            case CookStatus.Cached when options.Verbose:
                Line(output, $"  cached  {result.Id}");
                break;
            case CookStatus.Skipped when options.Verbose:
                Line(output, $"  skipped {result.Id}");
                break;
        }
    }

    /// <summary>Reads the arguments, or complains and returns null.</summary>
    internal static Options? Parse(string[] arguments, TextWriter error)
    {
        var options = new Options();
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            Options? next;
            if (Switches.TryGetValue(argument, out var apply))
            {
                next = apply(options);
            }
            else if (Valued.TryGetValue(argument, out var read))
            {
                next = i + 1 < arguments.Length
                    ? read(options, arguments[++i], error)
                    : Refuse(error, $"{argument} needs a value");
            }
            else
            {
                Line(error, $"cook: unknown option \"{argument}\"");
                Line(error, Usage);
                return null;
            }

            if (next is null)
            {
                return null;
            }

            options = next;
        }

        return Complete(options, error);
    }

    private static Options? Complete(Options options, TextWriter error)
    {
        if (!options.Help && options.Targets.Count == 0)
        {
            return Refuse(error, $"nothing to cook for. Pass --target, one of {TargetNames}.");
        }

        return options.Quiet && options.Verbose
            ? Refuse(error, "--quiet and --verbose ask for opposite things.")
            : options;
    }

    private static Options? WithTarget(Options options, string value, TextWriter error) =>
        CookTargets.Find(value) is { } target
            ? options with { Targets = [.. options.Targets, target] }
            : Refuse(error, $"\"{value}\" is not a target. The targets are {TargetNames}.");

    private static Options? WithCacheLimit(Options options, string value, TextWriter error) =>
        DartText.ParseInt(value) is { } megabytes and >= 0
            ? options with { CacheLimitMegabytes = megabytes }
            : Refuse(error, $"--cache-limit is megabytes, so \"{value}\" is not one. Pass 0 for no limit.");

    private static Options? WithJobs(Options options, string value, TextWriter error) =>
        DartText.ParseInt(value) is { } count and >= 1
            ? options with { Jobs = (int)Math.Min(count, int.MaxValue) }
            : Refuse(error, $"--jobs is how many at once, so it is at least 1, not \"{value}\".");

    private static Options? Refuse(TextWriter error, string message)
    {
        Line(error, $"cook: {message}");
        return null;
    }

    internal sealed record Options
    {
        public IReadOnlyList<CookTarget> Targets { get; init; } = [];

        public string AssetsPath { get; init; } = "assets";

        public string OutPath { get; init; } = Path.Combine("build", "assets");

        public string? CachePath { get; init; }

        public long CacheLimitMegabytes { get; init; } = 2048;

        public int Jobs { get; init; } = 4;

        public bool Quiet { get; init; }

        public bool Verbose { get; init; }

        public bool Help { get; init; }

        // Zero means no limit, which the cache spells as null. It refuses a limit of zero bytes, since a cache that
        // may hold nothing is not one.
        public long? CacheLimitBytes => CacheLimitMegabytes == 0 ? null : CacheLimitMegabytes * 1024 * 1024;
    }
}
