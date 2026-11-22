namespace Orblit.Editor.App.Shell;

/// <summary>How loud a message of the log is. The order is the order of the Output chips.</summary>
internal enum LogLevel
{
    Error,
    Warning,
    Info,
}

/// <summary>
/// One message of the engine's log: its level, its text without the level's prefix, the file and line it came
/// from, and when the editor read it. The engine writes the place on an "at:" line under a warning or an error.
/// </summary>
internal sealed record LogEntry(LogLevel Level, string Text, string Source, DateTimeOffset Time = default)
{
    const string At = "at: ";

    // The prefixes the engine's logger writes. A list of cases, so it is data.
    static readonly (string Prefix, LogLevel Level)[] s_prefixes =
    [
        ("WARNING: ", LogLevel.Warning),
        ("ERROR: ", LogLevel.Error),
        ("SCRIPT ERROR: ", LogLevel.Error),
        ("SHADER ERROR: ", LogLevel.Error),
        ("UNKNOWN ERROR: ", LogLevel.Error),
    ];

    /// <summary>
    /// Reads lines into messages, each with the time at the same place in <paramref name="times"/> when given. An
    /// "at:" line gives its place to the warning or the error over it. A blank line is no message.
    /// </summary>
    public static List<LogEntry> Read(IReadOnlyList<string> lines, IReadOnlyList<DateTimeOffset>? times = null)
    {
        var entries = new List<LogEntry>();
        for (var at = 0; at < lines.Count; at++)
        {
            var line = lines[at];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.TrimStart().StartsWith(At, StringComparison.Ordinal) && entries.Count > 0 && entries[^1] is { Level: not LogLevel.Info, Source: "" } last)
            {
                entries[^1] = last with { Source = SourceOf(line) };
            }
            else
            {
                entries.Add(EntryOf(line) with { Time = times is null ? default : times[at] });
            }
        }

        return entries;
    }

    static LogEntry EntryOf(string line)
    {
        foreach (var (prefix, level) in s_prefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return new LogEntry(level, line[prefix.Length..], "");
            }
        }

        return new LogEntry(LogLevel.Info, line, "");
    }

    // "   at: _ready (res://enemies/slime.cs:4)" gives slime.cs:4, the name a reader looks for in the file list.
    static string SourceOf(string line)
    {
        var open = line.LastIndexOf('(');
        var close = line.LastIndexOf(')');
        var place = open >= 0 && close > open ? line[(open + 1)..close] : line.TrimStart()[At.Length..];
        return place[(place.LastIndexOf('/') + 1)..];
    }
}
