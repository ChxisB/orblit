using Orblit.Editor.App;

namespace Orblit.Editor.App.Tests;

public sealed class BreakpointFileTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_breakpoints_{Guid.NewGuid():N}");

    public BreakpointFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void A_project_with_no_file_has_no_text()
    {
        Assert.Null(BreakpointFile.Read(_folder));
    }

    [Fact]
    public void Text_that_was_written_is_read_back()
    {
        BreakpointFile.Write(_folder, "{\"res://a.ograph#main\":[2]}");

        Assert.Equal("{\"res://a.ograph#main\":[2]}", BreakpointFile.Read(_folder));
    }

    [Fact]
    public void The_file_is_in_the_editor_data_of_the_project_and_not_among_its_scripts()
    {
        BreakpointFile.Write(_folder, "{}");

        Assert.True(File.Exists(Path.Combine(_folder, ".orblit", "editor", "graph_breakpoints.json")));
        Assert.Empty(ProjectScripts.FindGraphs(_folder));
    }

    [Fact]
    public void A_second_write_replaces_the_first()
    {
        BreakpointFile.Write(_folder, "{\"res://a.ograph#main\":[2,5]}");
        BreakpointFile.Write(_folder, "{}");

        Assert.Equal("{}", BreakpointFile.Read(_folder));
    }
}
