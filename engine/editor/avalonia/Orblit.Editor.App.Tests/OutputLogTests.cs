using System.Text;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class OutputLogTests
{
    [Fact]
    public void A_poll_reads_the_lines_a_file_gained_and_never_the_same_line_twice()
    {
        using var log = new OutputLog();
        var path = log.NewFile();

        File.AppendAllText(path, "one\ntwo\n");
        log.Poll();
        log.Poll();
        Assert.Equal(["one", "two"], log.Lines);

        File.AppendAllText(path, "three\n");
        log.Poll();
        Assert.Equal(["one", "two", "three"], log.Lines);
    }

    [Fact]
    public void A_file_that_restarts_shorter_is_read_from_its_start()
    {
        using var log = new OutputLog();
        var path = log.NewFile();
        File.AppendAllText(path, "first run line\nsecond run line\n");
        log.Poll();

        File.WriteAllText(path, "again\n");
        log.Poll();

        Assert.Equal(["first run line", "second run line", "again"], log.Lines);
    }

    [Fact]
    public void A_line_with_no_end_yet_waits_for_the_rest_of_it()
    {
        using var log = new OutputLog();
        var path = log.NewFile();

        File.AppendAllText(path, "half a li");
        log.Poll();
        Assert.Empty(log.Lines);

        File.AppendAllText(path, "ne\nnext");
        log.Poll();
        Assert.Equal(["half a line"], log.Lines);

        File.AppendAllText(path, "\n");
        log.Poll();
        Assert.Equal(["half a line", "next"], log.Lines);
    }

    [Fact]
    public void A_character_split_between_two_polls_is_read_whole()
    {
        using var log = new OutputLog();
        var path = log.NewFile();
        var bytes = Encoding.UTF8.GetBytes("café ☃\n");
        Assert.Equal(10, bytes.Length);

        using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            file.Write(bytes, 0, 4);
            file.Flush();
            log.Poll();
            file.Write(bytes, 4, 4);
            file.Flush();
            log.Poll();
            file.Write(bytes, 8, 2);
        }

        log.Poll();
        Assert.Equal(["café ☃"], log.Lines);
    }

    [Fact]
    public void A_carriage_return_before_the_newline_is_dropped()
    {
        using var log = new OutputLog();
        File.AppendAllText(log.NewFile(), "windows\r\nunix\n\r\n");

        log.Poll();

        Assert.Equal(["windows", "unix", ""], log.Lines);
    }

    [Fact]
    public void A_file_that_does_not_exist_yet_reads_nothing_and_is_read_once_it_does()
    {
        using var log = new OutputLog();
        var path = log.NewFile();
        Assert.False(File.Exists(path));

        log.Poll();
        Assert.Empty(log.Lines);

        File.WriteAllText(path, "late\n");
        log.Poll();
        Assert.Equal(["late"], log.Lines);
    }

    [Fact]
    public void Clear_empties_the_lines_and_the_old_ones_do_not_come_back()
    {
        using var log = new OutputLog();
        var path = log.NewFile();
        File.AppendAllText(path, "old\n");
        log.Poll();

        log.Clear();
        Assert.Empty(log.Lines);
        log.Poll();
        Assert.Empty(log.Lines);

        File.AppendAllText(path, "fresh\n");
        log.Poll();
        Assert.Equal(["fresh"], log.Lines);
    }

    [Fact]
    public void The_lines_report_each_change_so_a_list_can_follow_them()
    {
        using var log = new OutputLog();
        var changes = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)log.Lines).CollectionChanged += (_, _) => changes++;

        File.AppendAllText(log.NewFile(), "a\nb\n");
        log.Poll();
        log.Clear();

        Assert.Equal(3, changes);
    }

    [Fact]
    public void Only_the_newest_lines_are_kept()
    {
        using var log = new OutputLog();
        var count = OutputLog.MaxLines + 10;
        File.WriteAllLines(log.NewFile(), Enumerable.Range(0, count).Select(i => $"line {i}"));

        log.Poll();

        Assert.Equal(OutputLog.MaxLines, log.Lines.Count);
        Assert.Equal($"line {count - OutputLog.MaxLines}", log.Lines[0]);
        Assert.Equal($"line {count - 1}", log.Lines[^1]);
    }

    [Fact]
    public void A_poll_reads_a_bounded_slice_and_the_next_poll_reads_on()
    {
        using var log = new OutputLog();
        var line = new string('x', 99);
        var lines = OutputLog.MaxRead / 100 + 500;
        File.WriteAllLines(log.NewFile(), Enumerable.Repeat(line, lines));

        log.Poll();
        var first = log.Lines.Count;
        log.Poll();

        Assert.InRange(first, 1, lines - 1);
        Assert.Equal(Math.Min(lines, OutputLog.MaxLines), log.Lines.Count);
    }

    [Fact]
    public void Every_file_is_followed_and_has_a_name_of_its_own()
    {
        using var log = new OutputLog();
        var first = log.NewFile();
        var second = log.NewFile();
        Assert.NotEqual(first, second);
        Assert.Equal(Path.GetDirectoryName(first), Path.GetDirectoryName(second));

        File.AppendAllText(first, "from the editor\n");
        File.AppendAllText(second, "from the game\n");
        log.Poll();

        Assert.Equal(["from the editor", "from the game"], log.Lines);
    }

    [Fact]
    public void The_folder_exists_for_the_engine_to_write_in_and_goes_when_the_log_is_disposed()
    {
        var log = new OutputLog();
        var folder = Path.GetDirectoryName(log.NewFile())!;
        Assert.True(Directory.Exists(folder));
        File.WriteAllText(Path.Combine(folder, "0.log"), "x\n");

        log.Dispose();

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void A_folder_that_is_already_gone_does_not_stop_a_poll_or_a_dispose()
    {
        var log = new OutputLog();
        var folder = Path.GetDirectoryName(log.NewFile())!;
        Directory.Delete(folder);

        log.Poll();
        log.Dispose();

        Assert.Empty(log.Lines);
    }
}
