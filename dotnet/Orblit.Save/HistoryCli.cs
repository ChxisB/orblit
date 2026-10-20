namespace Orblit.Save;

/// <summary>The command line over <see cref="SaveHistory"/> and <see cref="SaveTools"/>.</summary>
internal static class HistoryCli
{
    private const string Usage = """
        orblit-save-history <saves-directory> list [key]
        orblit-save-history <saves-directory> restore <key> <time> --server-stopped
        orblit-save-history <saves-directory> restore-all <time> [--apply] --server-stopped

        A <time> is ISO 8601, as list prints it. restore-all lists what it would
        change and writes only with --apply. The command cannot see who is playing,
        so a write needs --server-stopped to say you stopped the server.
        """;

    private const string Apply = "--apply";
    private const string Stopped = "--server-stopped";
    private const int Failed = 1;
    private const int BadUsage = 64;

    /// <summary>Runs the command and returns its exit code.</summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, Func<DateTimeOffset> clock)
    {
        var flags = args.Where(IsFlag).ToHashSet(StringComparer.Ordinal);
        var words = args.Where(arg => !IsFlag(arg)).ToList();
        if (!IsUsable(flags, words))
        {
            return BadUsageWith(error);
        }

        var store = HistoryStore.Files(words[0]);
        // The command cannot see who is online. --server-stopped says it is safe.
        var tools = new SaveTools(store, _ => false, clock);
        var rest = words.Skip(2).ToList();
        try
        {
            return words[1] switch
            {
                "list" when rest.Count <= 1 => await ListAsync(store, rest, output).ConfigureAwait(false),
                "restore" when rest.Count == 2 => await RestoreAsync(tools, rest, output).ConfigureAwait(false),
                "restore-all" when rest.Count == 1 => await RestoreAllAsync(tools, rest[0], flags.Contains(Apply), output).ConfigureAwait(false),
                _ => BadUsageWith(error),
            };
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        {
            Line(error, e.Message);
            return Failed;
        }
    }

    private static bool IsFlag(string arg) => arg.StartsWith("--", StringComparison.Ordinal);

    private static bool IsUsable(HashSet<string> flags, List<string> words)
    {
        if (words.Count < 2 || !flags.IsSubsetOf([Apply, Stopped]))
        {
            return false;
        }

        var command = words[1];
        var writes = command == "restore" || (command == "restore-all" && flags.Contains(Apply));
        return !writes || flags.Contains(Stopped);
    }

    private static int BadUsageWith(TextWriter error)
    {
        Line(error, Usage);
        return BadUsage;
    }

    private static async Task<int> ListAsync(HistoryStore store, List<string> rest, TextWriter output)
    {
        if (rest.Count == 0)
        {
            foreach (var key in await store.History.KeysAsync().ConfigureAwait(false))
            {
                Line(output, key);
            }

            return 0;
        }

        foreach (var v in await store.History.ListAsync(rest[0]).ConfigureAwait(false))
        {
            Line(output, $"{Times.Iso(v.At)}  {v.Reason.Label}  {v.Hash[..12]}");
        }

        return 0;
    }

    private static async Task<int> RestoreAsync(SaveTools tools, List<string> rest, TextWriter output)
    {
        await tools.RestoreAsync(rest[0], Times.Parse(rest[1])).ConfigureAwait(false);
        Line(output, $"Restored {rest[0]}.");
        return 0;
    }

    private static async Task<int> RestoreAllAsync(SaveTools tools, string time, bool apply, TextWriter output)
    {
        var plan = await tools.RestorePlanAsync(Times.Parse(time)).ConfigureAwait(false);
        foreach (var e in plan.Changes)
        {
            Line(output, $"{e.Key}  {Times.Iso(e.At)}  {e.Reason.Label}");
        }

        if (!apply)
        {
            Line(output, $"{plan.Changes.Count} to restore. Add --apply to write.");
            return 0;
        }

        await tools.ApplyRestoreAsync(plan).ConfigureAwait(false);
        Line(output, $"Restored {plan.Changes.Count}.");
        return 0;
    }

    // The output is read by scripts, so a line ends in \n on every platform.
    private static void Line(TextWriter writer, string text) => writer.Write(text + "\n");
}
