using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App.Tests;

public sealed class LogEntryTests
{
    [Fact]
    public void Lines_read_into_levels_without_their_prefix_with_the_place_from_the_at_line_under_them()
    {
        var entries = LogEntry.Read(
        [
            "Loaded the scene.",
            "WARNING: Slow frame",
            "   at: _process (res://player/walk.cs:30)",
            "SCRIPT ERROR: Null instance",
            "   at: _ready (res://enemies/slime.cs:4)",
            "   at: _ready (res://enemies/other.cs:9)",
            "SHADER ERROR: Bad uniform",
            "   at: res://sky.shader:7",
            "UNKNOWN ERROR: Lost",
            "ERROR: Plain",
            "   at: load (no place)",
        ]);

        Assert.Equal(
        [
            new LogEntry(LogLevel.Info, "Loaded the scene.", ""),
            new LogEntry(LogLevel.Warning, "Slow frame", "walk.cs:30"),
            new LogEntry(LogLevel.Error, "Null instance", "slime.cs:4"),
            new LogEntry(LogLevel.Info, "   at: _ready (res://enemies/other.cs:9)", ""),
            new LogEntry(LogLevel.Error, "Bad uniform", "sky.shader:7"),
            new LogEntry(LogLevel.Error, "Lost", ""),
            new LogEntry(LogLevel.Error, "Plain", "no place"),
        ], entries);
    }

    [Fact]
    public void An_at_line_under_a_plain_message_or_first_in_the_log_stays_a_message()
    {
        var entries = LogEntry.Read(["   at: early (res://a.cs:1)", "hello", "   at: late (res://b.cs:2)"]);

        Assert.Equal([LogLevel.Info, LogLevel.Info, LogLevel.Info], entries.Select(entry => entry.Level));
        Assert.All(entries, entry => Assert.Equal("", entry.Source));
    }
}
